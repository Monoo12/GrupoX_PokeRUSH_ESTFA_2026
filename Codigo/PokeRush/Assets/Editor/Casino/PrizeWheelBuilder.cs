using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Arma la pantalla de la ruleta de premios y la registra en el MenuSystem.
//
// Igual que CasinoPrefabBuilder: NO reconstruye el sistema de menus, le agrega
// la pantalla que le falta y lo vuelve a guardar, asi lo que el equipo retoco a
// mano no se pisa.
//
// El arte sale de Assets/Graphics/Ruleta, que es la version limpia de
// "Nuevo Intento ruleta" (ver PrizeWheelArtImporter para el por que).
//
// Las medidas de abajo son pixeles de la resolucion de referencia del Canvas
// (512x384) y salen de medir el propio dibujo, no de tantear:
//   - el aro del marco mide 176x229 y su circulo esta corrido (+1.1, +10.1)
//     respecto del centro de la imagen;
//   - el disco de gajos mide 146x146 y entra justo adentro del aro;
//   - los iconos van a 44 px del centro, que es el medio de cada gajo.
public static class PrizeWheelBuilder
{
    private const string ArtFolder = "Assets/Graphics/Ruleta";
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    // Geometria de la rueda, medida sobre el arte.
    private const float MarcoW = 176f, MarcoH = 229f;
    private const float CentroX = 1.1f, CentroY = 10.1f;   // circulo dentro del marco
    private const float DiscoLado = 146f;
    private const float RadioIconos = 44f;
    private const float RadioAlPuntero = 76f;
    private const float MarcoY = 40f;                      // el marco dentro de la pantalla

    [MenuItem("PokeRush/Casino/Construir la ruleta de premios")]
    public static void BuildAll()
    {
        PrizeWheelArtImporter.ImportarTodo();
        MenuUiKit.EnsureFolder(PrefabFolder);

        GameObject pantalla = BuildScreen();
        RegistrarEnMenuSystem(pantalla);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Ruleta] Pantalla generada en {PrefabFolder}/PrizeWheelScreen.prefab " +
                  "y registrada en el MenuSystem.");
    }

    private static Sprite Art(string nombre) => MenuUiKit.Sprite($"{ArtFolder}/{nombre}.png");

    // ------------------------------------------------------------- pantalla

    private static GameObject BuildScreen()
    {
        GameObject root = new GameObject("PrizeWheelScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<PrizeWheelController>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.PrizeWheel);

        // Marco de 512x384 centrado y SIN fondo: la ruleta se apoya sobre el
        // mapa, no sobre un panel. Este nodo existe solo para que las
        // coordenadas de adentro sean pixeles del arte, no porcentajes de una
        // pantalla que cambia de ancho.
        GameObject marco = MenuUiKit.Node("Marco", root.transform);
        MenuUiKit.Box(marco, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));

        // ---- contenedor de la rueda ----
        GameObject contenedor = MenuUiKit.Node("WheelContainer", marco.transform);
        MenuUiKit.Box(contenedor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, MarcoY), new Vector2(MarcoW, MarcoH));

        // El aro y la base van ATRAS del disco: el disco entra adentro del aro.
        GameObject aro = MenuUiKit.Node("Aro", contenedor.transform);
        MenuUiKit.Full(aro);
        MenuUiKit.AddImage(aro, Art("ruleta_marco"), Color.white);

        // ---- lo unico que rota ----
        GameObject rueda = MenuUiKit.Node("Wheel", contenedor.transform);
        var ruedaRt = MenuUiKit.Box(rueda, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(CentroX, CentroY), new Vector2(DiscoLado, DiscoLado));
        MenuUiKit.AddImage(rueda, Art("ruleta_disco"), Color.white);

        Sprite[] iconos =
        {
            Art("icon_pokeball"), Art("icon_greatball"), Art("icon_ultraball"),
            Art("icon_pocion"), Art("icon_superpocion"), Art("icon_hiperpocion"),
            Art("icon_rare_candy"), Art("icon_misterio")
        };

        var slots = new Image[iconos.Length];
        for (int i = 0; i < iconos.Length; i++)
        {
            // El gajo 0 esta arriba (bajo el puntero) y de ahi van en sentido
            // horario, que es el mismo orden con el que esta pintado el disco.
            float grados = 90f - i * (360f / iconos.Length);
            var pos = new Vector2(
                RadioIconos * Mathf.Cos(grados * Mathf.Deg2Rad),
                RadioIconos * Mathf.Sin(grados * Mathf.Deg2Rad));

            GameObject go = MenuUiKit.Node($"Icono{i}", rueda.transform);
            MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(30f, 30f));
            Image img = MenuUiKit.AddImage(go, iconos[i], Color.white);
            img.preserveAspect = true;
            if (iconos[i] != null) img.SetNativeSize();
            slots[i] = img;
        }

        // ---- efectos: cuelgan del centro pero NO rotan ----
        GameObject efectos = MenuUiKit.Node("Efectos", contenedor.transform);
        MenuUiKit.Box(efectos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(CentroX, CentroY), Vector2.zero);

        GameObject fxPremio = MenuUiKit.Node("FxPremio", efectos.transform);
        MenuUiKit.Box(fxPremio, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(86f, 83f));
        Image fxPremioImg = MenuUiKit.AddImage(fxPremio, Art("fx_premio"), Color.white);
        fxPremioImg.preserveAspect = true;
        fxPremio.SetActive(false);

        GameObject fxChispa = MenuUiKit.Node("FxChispa", efectos.transform);
        MenuUiKit.Box(fxChispa, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, RadioIconos), new Vector2(46f, 34f));
        Image fxChispaImg = MenuUiKit.AddImage(fxChispa, Art("fx_destello_1"), Color.white);
        fxChispaImg.preserveAspect = true;
        fxChispa.SetActive(false);

        // ---- puntero: fijo, arriba, con el pivote en el enganche ----
        // Va con pivot (0.5, 1) para que el tic lo haga pivotear desde donde
        // esta amurado y se mueva la punta, no todo el triangulo de costado.
        GameObject puntero = MenuUiKit.Node("Pointer", contenedor.transform);
        var punteroRt = MenuUiKit.Box(puntero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(CentroX, CentroY + RadioAlPuntero + 36f), new Vector2(29f, 36f));
        Image punteroImg = MenuUiKit.AddImage(puntero, Art("puntero"), Color.white);
        punteroImg.preserveAspect = true;

        // ---- botones ----
        Button girar = Boton(marco.transform, "BotonGirar", "btn_girar",
            new Vector2(-58f, -96f), new Vector2(109f, 32f), out Image girarImg);
        Button volver = Boton(marco.transform, "BotonVolver", "btn_volver",
            new Vector2(60f, -96f), new Vector2(104f, 28f), out Image volverImg);

        UnityEventTools.AddVoidPersistentListener(girar.onClick,
            new UnityAction(screen.OnSpinButton));
        UnityEventTools.AddVoidPersistentListener(volver.onClick,
            new UnityAction(screen.OnBackButton));

        // ---- cableado ----
        MenuUiKit.SetField(screen, "wheel", ruedaRt);
        MenuUiKit.SetArrayField(screen, "prizeIcons", slots);
        MenuUiKit.SetField(screen, "pointer", punteroRt);
        MenuUiKit.SetField(screen, "pointerImage", punteroImg);
        MenuUiKit.SetArrayField(screen, "pointerFrames", new Object[]
        {
            Art("puntero"), Art("puntero_tic_1"), Art("puntero_tic_2"), Art("puntero_tic_3")
        });
        MenuUiKit.SetField(screen, "spinButton", girar);
        MenuUiKit.SetField(screen, "spinButtonImage", girarImg);
        MenuUiKit.SetField(screen, "spinEnabledSprite", Art("btn_girar"));
        MenuUiKit.SetField(screen, "spinDisabledSprite", Art("btn_girar_off"));
        MenuUiKit.SetField(screen, "backButton", volver);
        MenuUiKit.SetField(screen, "backButtonImage", volverImg);
        MenuUiKit.SetField(screen, "prizeFx", fxPremioImg);
        MenuUiKit.SetField(screen, "sparkFx", fxChispaImg);
        MenuUiKit.SetArrayField(screen, "sparkFrames", new Object[]
        {
            Art("fx_destello_1"), Art("fx_destello_2"), Art("fx_destello_3")
        });

        CargarPremiosPorDefecto(screen, iconos);

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/PrizeWheelScreen.prefab");
    }

    private static Button Boton(Transform padre, string nombre, string sprite,
                                Vector2 pos, Vector2 size, out Image imagen)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        imagen = MenuUiKit.AddImage(go, Art(sprite), Color.white, raycast: true);

        var boton = go.AddComponent<Button>();
        boton.targetGraphic = imagen;
        // Sin transicion de UGUI: el estado lo dibuja la pantalla cambiando el
        // sprite (btn_girar / btn_girar_off), que es el arte que hizo el equipo.
        boton.transition = Selectable.Transition.None;
        return boton;
    }

    // ---------------------------------------------------------------- premios

    // Los ocho por defecto, en el orden en que estan pintados los gajos. Los
    // pesos suman 100 para que se lean como porcentajes, pero el sorteo los
    // normaliza igual: se pueden tocar sin cuidar la suma.
    //
    // 'item' es el objeto que va a la mochila. Solo lo tienen las tres pociones,
    // que son los unicos HealItemDefinition que existen hoy en el proyecto: las
    // balls y el caramelo salen como cartel hasta que alguien cree su asset y lo
    // arrastre al campo Item del Inspector.
    private static void CargarPremiosPorDefecto(PrizeWheelController screen, Sprite[] iconos)
    {
        const string Items = "Assets/Resources/Definitions/Items/";
        HealItemDefinition Objeto(string archivo)
            => AssetDatabase.LoadAssetAtPath<HealItemDefinition>(Items + archivo + ".asset");

        var premios = new (string nombre, string plural, string art, int cant, float peso, string item)[]
        {
            ("Poké Ball",     "Poké Balls",      "una", 3, 30f, null),
            ("Gran Ball",     "Gran Balls",      "una", 1, 5f,  null),
            ("Ultra Ball",    "Ultra Balls",     "una", 1, 10f, null),
            ("Poción",        "Pociones",        "una", 2, 20f, "Pocion"),
            ("Superpoción",   "Superpociones",   "una", 1, 15f, "SuperPocion"),
            ("Hiperpoción",   "Hiperpociones",   "una", 1, 7f,  "HiperPocion"),
            ("Caramelo Raro", "Caramelos Raros", "un",  1, 8f,  null),
            ("premio misterioso", "premios misteriosos", "un", 1, 5f, null)
        };

        var so = new SerializedObject(screen);
        SerializedProperty lista = so.FindProperty("prizes");
        lista.arraySize = premios.Length;

        for (int i = 0; i < premios.Length; i++)
        {
            SerializedProperty p = lista.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("prizeName").stringValue = premios[i].nombre;
            p.FindPropertyRelative("prizePlural").stringValue = premios[i].plural;
            p.FindPropertyRelative("article").stringValue = premios[i].art;
            p.FindPropertyRelative("amount").intValue = premios[i].cant;
            p.FindPropertyRelative("probability").floatValue = premios[i].peso;
            p.FindPropertyRelative("icon").objectReferenceValue = iconos[i];
            p.FindPropertyRelative("item").objectReferenceValue =
                premios[i].item != null ? Objeto(premios[i].item) : null;
            p.FindPropertyRelative("customMessage").stringValue = "";
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------ MenuSystem

    private static void RegistrarEnMenuSystem(GameObject pantalla)
    {
        var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
        if (screen == null) return;

        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Ruleta] No se encontro {MenuSystemPath}. " +
                           "Genera primero el sistema de menus con PokeRush/UI.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[Ruleta] El prefab MenuSystem no tiene MenuManager.");
                return;
            }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            var yaEstan = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++)
                yaEstan.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);

            if (yaEstan.Contains(screen)) return;

            lista.InsertArrayElementAtIndex(lista.arraySize);
            lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
            Debug.Log("[Ruleta] MenuSystem: agregada la pantalla PrizeWheel.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
