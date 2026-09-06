using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Genera los prefabs y assets del casino y los engancha al sistema de menus que
// ya existe.
//
// A diferencia de MenuPrefabBuilder, este NO reconstruye el MenuSystem: lo abre,
// le AGREGA lo que le falte y lo vuelve a guardar. Asi el equipo puede seguir
// retocando a mano las pantallas del juego base sin que correr esto se las pise.
//
// La pantalla de slots se arma sobre el arte de tragamonedas que ya trae el pack
// (Graphics/UI/Slot Machine): la maquina, la sombra de los rodillos y la tira de
// simbolos Pokemon. El fondo mide 512x384, que es exactamente la resolucion de
// referencia del Canvas, asi que las posiciones de abajo son pixeles del dibujo.
public static class CasinoPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string DefinitionFolder = "Assets/Resources/Definitions/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    // Medidas leidas del propio dibujo (bg.png).
    private const float VentanaX0 = 64f;    // borde izquierdo del rodillo 1
    private const float VentanaY0 = 110f;   // borde superior de los rodillos
    private const float PasoRodillo = 80f;  // de un rodillo al siguiente
    private const float AnchoCelda = 64f;
    private const float AltoCelda = 48f;

    [MenuItem("PokeRush/Casino/Construir pantallas del casino")]
    public static void BuildAll()
    {
        MenuUiKit.EnsureFolder(PrefabFolder);
        MenuUiKit.EnsureFolder(DefinitionFolder);

        CasinoSlotArtImporter.ImportarTodo();

        SlotMachineDefinition maquina = BuildMaquinaBasica();
        GameObject slots = BuildSlotsScreen();

        RegistrarEnMenuSystem(new List<GameObject> { slots });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Casino] Pantalla de slots en {PrefabFolder} y maquina " +
                  $"'{maquina.name}' en {DefinitionFolder}. Registrada en MenuSystem. " +
                  "La ruleta de premios del piso 2 la arma PrizeWheelBuilder.");
    }

    // ------------------------------------------------------------- definicion

    // La maquina del piso 1, con los simbolos del Game Corner que trae el pack.
    //
    // La tabla de pagos respeta la que esta DIBUJADA abajo del gabinete (777 =
    // 300, 777 azul = 90, caritas = 15, bichos = 8, cereza = 2), y los pesos
    // estan elegidos para que la maquina devuelva ~86% de lo apostado: la casa
    // gana a la larga pero una racha existe. Son tres lineas, las tres filas,
    // que es lo que marcan los indicadores 1/2/3 del dibujo.
    //
    // Al tocar estos numeros, correr PokeRush/Casino/Correr smoke tests del
    // casino: reporta la devolucion resultante y falla si la maquina regala.
    private static SlotMachineDefinition BuildMaquinaBasica()
    {
        string path = DefinitionFolder + "/MaquinaBasica.asset";

        var def = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<SlotMachineDefinition>();

        Sprite[] iconos = CasinoSlotArtImporter.CargarSimbolos();
        Sprite Icono(int i) => iconos != null && i < iconos.Length ? iconos[i] : null;

        def.maquinaNombre = "TRAGAPERRAS";
        def.descripcion = "Maquina del piso 1 del casino.";
        def.rodillos = 3;
        def.filas = 3;
        def.apuestaMinima = 10;
        def.apuestaMaxima = 100;
        def.pasoDeApuesta = 10;
        def.duracionDelGiro = 0.9f;
        def.retrasoEntreRodillos = 0.3f;

        def.simbolos = new[]
        {
            Simbolo("Cereza",    Icono(0), 60, 2),
            Simbolo("Magnemite", Icono(1), 48, 8),
            Simbolo("Gastly",    Icono(2), 36, 8),
            Simbolo("Psyduck",   Icono(4), 26, 15),
            Simbolo("Pikachu",   Icono(3), 18, 15),
            Simbolo("Siete azul", Icono(6), 8, 90),
            Simbolo("Siete rojo", Icono(5), 3, 300),
            Comodin("Replay",    Icono(7), 1, 500)
        };

        // Las tres filas, como los indicadores 1/2/3 del gabinete.
        def.lineas = new[]
        {
            Linea("Linea 1 (arriba)", 0, 0, 0),
            Linea("Linea 2 (medio)",  1, 1, 1),
            Linea("Linea 3 (abajo)",  2, 2, 2)
        };

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static SlotSymbol Simbolo(string nombre, Sprite icono, int peso, int pagoPorTres)
        => new SlotSymbol
        {
            nombre = nombre,
            icono = icono,
            letra = nombre.Substring(0, 1),
            peso = peso,
            pagos = new[] { 0, pagoPorTres }
        };

    private static SlotSymbol Comodin(string nombre, Sprite icono, int peso, int pagoPorTres)
    {
        SlotSymbol s = Simbolo(nombre, icono, peso, pagoPorTres);
        s.comodin = true;
        return s;
    }

    private static SlotPayline Linea(string nombre, params int[] filas)
        => new SlotPayline { nombre = nombre, filas = filas };

    // ---------------------------------------------------------------- pantalla

    private static GameObject BuildSlotsScreen()
    {
        GameObject root = new GameObject("SlotsScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<SlotsScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Slots);

        // Oscurecedor: la maquina mide 512x384 y en pantallas mas anchas quedan
        // franjas a los costados. Sin esto se ve el mapa del casino al lado de la
        // maquina y no se entiende que hay un menu abierto.
        GameObject velo = MenuUiKit.Node("Oscurecedor", root.transform);
        MenuUiKit.Full(velo);
        MenuUiKit.AddImage(velo, null, new Color(0f, 0f, 0f, 0.72f));

        // El gabinete entero. Mide 512x384 igual que el Canvas: entra clavado.
        GameObject fondo = MenuUiKit.Node("Maquina", root.transform);
        MenuUiKit.Box(fondo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));
        MenuUiKit.AddImage(fondo, CasinoSlotArtImporter.CargarSprite(CasinoSlotArtImporter.Maquina),
            Color.white);

        // Las casillas, encima de las ventanas de los rodillos. Se crean rodillo
        // por rodillo y de arriba hacia abajo: ese ES el orden que espera
        // SlotsScreen.
        var celdas = new List<Object>();
        for (int r = 0; r < 3; r++)
            for (int f = 0; f < 3; f++)
                celdas.Add(BuildCelda($"Celda_R{r}F{f}", fondo.transform,
                    new Vector2(VentanaX0 + PasoRodillo * r, VentanaY0 + AltoCelda * f)));
        MenuUiKit.SetArrayField(screen, "celdas", celdas.ToArray());

        // Sombra curva del tambor, encima de los simbolos.
        Sprite sombra = CasinoSlotArtImporter.CargarSprite(CasinoSlotArtImporter.Sombra);
        for (int r = 0; r < 3; r++)
        {
            GameObject go = MenuUiKit.Node($"Sombra_R{r}", fondo.transform);
            Arriba(go, new Vector2(VentanaX0 + PasoRodillo * r, VentanaY0),
                new Vector2(AnchoCelda, AltoCelda * 3f));
            Image img = MenuUiKit.AddImage(go, sombra, Color.white);
            img.raycastTarget = false;
        }

        // Los dos visores del gabinete ya dicen CREDITO y PREMIO en el dibujo:
        // aca solo van los numeros, calzados adentro de cada ventanita.
        Text credito = Numero("Credito", fondo.transform, new Vector2(360f, 61f));
        Text premio = Numero("Premio", fondo.transform, new Vector2(437f, 61f));

        // El panel negro de la derecha es el sitio para hablarle al jugador.
        Text mensaje = MenuUiKit.Label("Mensaje", fondo.transform, Vector2.zero,
            new Vector2(140f, 150f), "", 12, TextAnchor.UpperLeft, MenuUiKit.Ink);
        Arriba(mensaje.gameObject, new Vector2(364f, 104f), new Vector2(140f, 150f));
        mensaje.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text apuesta = MenuUiKit.Label("Apuesta", fondo.transform, Vector2.zero,
            new Vector2(140f, 22f), "APUESTA 10", 16, TextAnchor.MiddleLeft, MenuUiKit.Gold);
        Arriba(apuesta.gameObject, new Vector2(364f, 214f), new Vector2(140f, 22f));

        // La ayuda entra en el panel negro con aire de sobra: apretada contra el
        // borde de abajo, con las lineas pisandose, es como se veia mal.
        Text ayuda = MenuUiKit.Label("Ayuda", fondo.transform, Vector2.zero,
            new Vector2(140f, 66f), "IZQ/DER  apuesta\nX  girar\nZ  salir",
            14, TextAnchor.UpperLeft, MenuUiKit.Border);
        ayuda.lineSpacing = 1.25f;
        Arriba(ayuda.gameObject, new Vector2(364f, 240f), new Vector2(140f, 66f));

        // El titulo no va: el gabinete ya tiene el suyo dibujado.
        MenuUiKit.SetField(screen, "tituloText", null);
        MenuUiKit.SetField(screen, "saldoText", credito);
        MenuUiKit.SetField(screen, "premioText", premio);
        MenuUiKit.SetField(screen, "apuestaText", apuesta);
        MenuUiKit.SetField(screen, "mensajeText", mensaje);

        root.SetActive(false);
        return MenuUiKit.SavePrefab(root, PrefabFolder + "/SlotsScreen.prefab");
    }

    private static SlotCellWidget BuildCelda(string nombre, Transform padre, Vector2 posArriba)
    {
        GameObject celda = MenuUiKit.Node(nombre, padre);
        Arriba(celda, posArriba, new Vector2(AnchoCelda, AltoCelda));

        // Fondo del tambor: el blanco hueso del dibujo, para que los simbolos
        // con transparencia no dejen ver el gabinete por atras.
        Image fondo = MenuUiKit.AddImage(celda, null, new Color(0.94f, 0.94f, 0.97f, 1f));

        GameObject iconoGo = MenuUiKit.Node("Icono", celda.transform);
        MenuUiKit.Box(iconoGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(AnchoCelda, AltoCelda));
        Image icono = MenuUiKit.AddImage(iconoGo, null, Color.white);
        icono.enabled = false;
        icono.preserveAspect = true;

        // Respaldo para cuando un simbolo no tenga sprite cargado.
        Text letra = MenuUiKit.Label("Letra", celda.transform, Vector2.zero,
            new Vector2(AnchoCelda, AltoCelda), "", 20, TextAnchor.MiddleCenter,
            MenuUiKit.InkDark);
        letra.enabled = false;

        GameObject resaltado = MenuUiKit.Node("Resaltado", celda.transform);
        MenuUiKit.Box(resaltado, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(AnchoCelda, AltoCelda));
        Image marco = MenuUiKit.AddImage(resaltado, null, new Color(1f, 0.9f, 0.2f, 0.4f));
        marco.raycastTarget = false;
        resaltado.SetActive(false);

        var widget = celda.AddComponent<SlotCellWidget>();
        MenuUiKit.SetField(widget, "fondo", fondo);
        MenuUiKit.SetField(widget, "icono", icono);
        MenuUiKit.SetField(widget, "letra", letra);
        MenuUiKit.SetField(widget, "resaltado", resaltado);
        return widget;
    }

    // Numero de visor: alineado a la derecha, como los contadores del gabinete.
    //
    // Va sobre una tapa negra porque el dibujo trae los digitos '88888' apagados
    // pintados adentro de la ventanita: sin taparlos, el numero de verdad se lee
    // encima de ellos y no se entiende nada.
    private static Text Numero(string nombre, Transform padre, Vector2 posArriba)
    {
        GameObject tapa = MenuUiKit.Node(nombre + "Fondo", padre);
        Arriba(tapa, posArriba, new Vector2(70f, 22f));
        MenuUiKit.AddImage(tapa, null, Color.black);

        Text t = MenuUiKit.Label(nombre, tapa.transform, Vector2.zero, new Vector2(64f, 20f),
            "0", 16, TextAnchor.MiddleRight, MenuUiKit.Gold);
        MenuUiKit.Box(t.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-2f, 0f), new Vector2(64f, 20f));
        return t;
    }

    // Coloca por pixel del dibujo, contando desde arriba a la izquierda.
    private static void Arriba(GameObject go, Vector2 posArriba, Vector2 size)
    {
        MenuUiKit.Box(go, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(posArriba.x, -posArriba.y), size);
    }

    // ------------------------------------------------------------ MenuSystem

    // Agrega al MenuSystem las pantallas del casino que le falten, sin tocar las
    // que ya estaban, y le deja el arreglo de nitidez de la fuente.
    private static void RegistrarEnMenuSystem(List<GameObject> pantallas)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Casino] No se encontro {MenuSystemPath}. " +
                           "Genera primero el sistema de menus con PokeRush/UI.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[Casino] El prefab MenuSystem no tiene MenuManager.");
                return;
            }

            bool cambio = false;

            if (contenido.GetComponent<PixelFontCrisp>() == null)
            {
                contenido.AddComponent<PixelFontCrisp>();
                cambio = true;
                Debug.Log("[Casino] MenuSystem: agregado PixelFontCrisp (texto de pixel nitido).");
            }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            var yaEstan = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++)
                yaEstan.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (GameObject pantalla in pantallas)
            {
                var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
                if (screen == null || yaEstan.Contains(screen)) continue;

                lista.InsertArrayElementAtIndex(lista.arraySize);
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
                cambio = true;
            }

            if (!cambio) return;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
