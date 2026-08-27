using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Genera los prefabs y assets del casino y los engancha al sistema de menus que
// ya existe.
//
// A diferencia de MenuPrefabBuilder, este NO reconstruye el MenuSystem: lo abre,
// le AGREGA las pantallas del casino que le falten y lo vuelve a guardar. Asi el
// equipo puede seguir retocando a mano las pantallas del juego base sin que
// correr esto se las pise.
//
// Lo que sale son prefabs y assets comunes: mover cosas, cambiar sprites y
// tocar pagos se hace desde Unity como con cualquier otro objeto.
public static class CasinoPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string DefinitionFolder = "Assets/Resources/Definitions/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    [MenuItem("PokeRush/Casino/Construir pantallas del casino")]
    public static void BuildAll()
    {
        MenuUiKit.EnsureFolder(PrefabFolder);
        MenuUiKit.EnsureFolder(DefinitionFolder);

        SlotMachineDefinition maquina = BuildMaquinaBasica();
        GameObject slots = BuildSlotsScreen();

        RegistrarEnMenuSystem(new List<GameObject> { slots });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Casino] Pantalla de slots en {PrefabFolder}, maquina de ejemplo en " +
                  $"{DefinitionFolder}/{maquina.name}.asset y registrada en MenuSystem.");
    }

    // ------------------------------------------------------------- definicion

    // Maquina de ejemplo para poder jugar de entrada. Los pesos y los pagos son
    // un punto de partida balanceado con CasinoSmokeTests: cambiarlos desde el
    // Inspector es la forma prevista de ajustar la economia del piso 1.
    private static SlotMachineDefinition BuildMaquinaBasica()
    {
        string path = DefinitionFolder + "/MaquinaBasica.asset";

        var def = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<SlotMachineDefinition>();

        def.maquinaNombre = "TRAGAPERRAS";
        def.descripcion = "Maquina basica del piso 1.";
        def.rodillos = 3;
        def.filas = 3;
        def.apuestaMinima = 10;
        def.apuestaMaxima = 100;
        def.pasoDeApuesta = 10;
        def.duracionDelGiro = 0.9f;
        def.retrasoEntreRodillos = 0.3f;

        // Los pagos estan calculados para que la maquina devuelva ~90% de lo
        // apostado: la casa gana a la larga, pero una racha buena existe. Ojo al
        // tocarlos: las CINCO lineas se pagan con una sola apuesta, asi que
        // duplicar un pago duplica su peso en la devolucion. Despues de cambiar
        // estos numeros, correr PokeRush/Casino/Correr smoke tests del casino,
        // que reporta la devolucion resultante.
        def.simbolos = new[]
        {
            Simbolo("Baya",     "B", new Color(0.85f, 0.30f, 0.35f), 34, new[] { 0, 1 }),
            Simbolo("Pokeball", "P", new Color(0.95f, 0.62f, 0.20f), 26, new[] { 0, 2 }),
            Simbolo("Rayo",     "R", new Color(0.98f, 0.85f, 0.25f), 18, new[] { 0, 4 }),
            Simbolo("Estrella", "E", new Color(0.40f, 0.75f, 0.95f), 11, new[] { 0, 8 }),
            Simbolo("Siete",    "7", new Color(0.60f, 0.45f, 0.90f), 6,  new[] { 0, 15 }),
            Comodin("Comodin",  "W", new Color(0.35f, 0.85f, 0.55f), 3,  new[] { 0, 30 })
        };

        // Las tres filas y las dos diagonales. Se pagan todas en cada tirada.
        def.lineas = new[]
        {
            Linea("Fila de arriba", 0, 0, 0),
            Linea("Fila del medio", 1, 1, 1),
            Linea("Fila de abajo",  2, 2, 2),
            Linea("Diagonal baja",  0, 1, 2),
            Linea("Diagonal alta",  2, 1, 0)
        };

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static SlotSymbol Simbolo(string nombre, string letra, Color color, int peso, int[] pagos)
        => new SlotSymbol { nombre = nombre, letra = letra, color = color, peso = peso, pagos = pagos };

    private static SlotSymbol Comodin(string nombre, string letra, Color color, int peso, int[] pagos)
    {
        SlotSymbol s = Simbolo(nombre, letra, color, peso, pagos);
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

        // Panel de la maquina.
        GameObject panel = MenuUiKit.Node("Panel", root.transform);
        MenuUiKit.Box(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(320f, 300f));
        MenuUiKit.AddImage(panel, null, MenuUiKit.Panel);
        Borde(panel);

        Text titulo = MenuUiKit.Label("Titulo", panel.transform, new Vector2(0f, -12f),
            new Vector2(300f, 22f), "TRAGAPERRAS", 14, TextAnchor.UpperCenter, MenuUiKit.Gold);
        Anclar(titulo.rectTransform, new Vector2(0.5f, 1f));

        Text saldo = MenuUiKit.Label("Saldo", panel.transform, new Vector2(-12f, -12f),
            new Vector2(110f, 20f), "$0", 12, TextAnchor.UpperRight, MenuUiKit.Ink);
        Anclar(saldo.rectTransform, new Vector2(1f, 1f));

        // Grilla 3x3. Las casillas se crean rodillo por rodillo y de arriba
        // hacia abajo: ese ES el orden que espera SlotsScreen.
        GameObject grilla = MenuUiKit.Node("Grilla", panel.transform);
        MenuUiKit.Box(grilla, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 18f), new Vector2(210f, 210f));
        MenuUiKit.AddImage(grilla, null, new Color(0.08f, 0.09f, 0.13f, 1f));

        const float lado = 62f;
        const float paso = 68f;
        var celdas = new List<Object>();
        for (int r = 0; r < 3; r++)
        {
            for (int f = 0; f < 3; f++)
            {
                Vector2 pos = new Vector2((r - 1) * paso, (1 - f) * paso);
                celdas.Add(BuildCelda($"Celda_R{r}F{f}", grilla.transform, pos, lado));
            }
        }
        MenuUiKit.SetArrayField(screen, "celdas", celdas.ToArray());

        Text apuesta = MenuUiKit.Label("Apuesta", panel.transform, new Vector2(0f, 62f),
            new Vector2(220f, 20f), "APUESTA  10", 13, TextAnchor.MiddleCenter, MenuUiKit.Accent);
        Anclar(apuesta.rectTransform, new Vector2(0.5f, 0f));

        Text mensaje = MenuUiKit.Label("Mensaje", panel.transform, new Vector2(0f, 40f),
            new Vector2(290f, 20f), "", 12, TextAnchor.MiddleCenter, MenuUiKit.Ink);
        Anclar(mensaje.rectTransform, new Vector2(0.5f, 0f));

        Text ayuda = MenuUiKit.Label("Ayuda", panel.transform, new Vector2(0f, 14f),
            new Vector2(290f, 18f), "IZQ/DER apuesta    X girar    Z salir",
            10, TextAnchor.MiddleCenter, MenuUiKit.Border);
        Anclar(ayuda.rectTransform, new Vector2(0.5f, 0f));

        MenuUiKit.SetField(screen, "tituloText", titulo);
        MenuUiKit.SetField(screen, "saldoText", saldo);
        MenuUiKit.SetField(screen, "apuestaText", apuesta);
        MenuUiKit.SetField(screen, "mensajeText", mensaje);

        root.SetActive(false);
        return MenuUiKit.SavePrefab(root, PrefabFolder + "/SlotsScreen.prefab");
    }

    private static SlotCellWidget BuildCelda(string nombre, Transform padre, Vector2 pos, float lado)
    {
        GameObject celda = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(celda, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, new Vector2(lado, lado));
        Image fondo = MenuUiKit.AddImage(celda, null, new Color(0.30f, 0.34f, 0.44f, 1f));

        GameObject iconoGo = MenuUiKit.Node("Icono", celda.transform);
        MenuUiKit.Box(iconoGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(lado - 10f, lado - 10f));
        Image icono = MenuUiKit.AddImage(iconoGo, null, Color.white);
        icono.enabled = false;
        icono.preserveAspect = true;

        Text letra = MenuUiKit.Label("Letra", celda.transform, Vector2.zero,
            new Vector2(lado, lado), "?", 22, TextAnchor.MiddleCenter, MenuUiKit.Ink);
        MenuUiKit.Contorno(letra);

        // Marco que se prende cuando la casilla entra en una linea que pago.
        GameObject resaltado = MenuUiKit.Node("Resaltado", celda.transform);
        MenuUiKit.Box(resaltado, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(lado, lado));
        Image marco = MenuUiKit.AddImage(resaltado, null, new Color(1f, 0.85f, 0.24f, 0.35f));
        marco.raycastTarget = false;
        resaltado.SetActive(false);

        var widget = celda.AddComponent<SlotCellWidget>();
        MenuUiKit.SetField(widget, "fondo", fondo);
        MenuUiKit.SetField(widget, "icono", icono);
        MenuUiKit.SetField(widget, "letra", letra);
        MenuUiKit.SetField(widget, "resaltado", resaltado);
        return widget;
    }

    private static void Anclar(RectTransform rt, Vector2 ancla)
    {
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = ancla;
    }

    private static void Borde(GameObject go)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = MenuUiKit.Border;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    // ------------------------------------------------------------ MenuSystem

    // Agrega las pantallas del casino a la lista del MenuManager sin tocar las
    // que ya estaban. Si ya figuran, no hace nada.
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

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            var yaEstan = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++)
                yaEstan.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);

            int agregadas = 0;
            foreach (GameObject pantalla in pantallas)
            {
                var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
                if (screen == null || yaEstan.Contains(screen)) continue;

                lista.InsertArrayElementAtIndex(lista.arraySize);
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
                agregadas++;
            }

            if (agregadas == 0) return;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
            Debug.Log($"[Casino] {agregadas} pantalla(s) agregada(s) al MenuSystem.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
