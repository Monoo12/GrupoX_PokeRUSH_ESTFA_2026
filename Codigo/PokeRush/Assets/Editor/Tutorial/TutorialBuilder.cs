using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// El tutorial de la habitacion: el esqueleto y lo que hace falta para pasarlo a
// la habitacion decorada.
//
//   PokeRush > Tutorial > Armar el esqueleto (escena vacia)
//       Crea HabitacionTutorial_Esqueleto: un cuarto de prueba (baldosas lisas
//       con el nombre de cada mueble) con el tutorial entero andando.
//   PokeRush > Tutorial > Preparar la escena abierta (despues de pegar)
//       En la habitacion decorada, con el objeto 'Tutorial' ya pegado: agrega lo
//       que el juego necesita (jugador, camara, combate, dialogo, GameManager),
//       repara las colisiones y verifica que se llegue caminando a todo.
//   PokeRush > Tutorial > Conectar la escena abierta al recorrido
//       La habitacion decorada va a Build Settings y la creacion de personaje
//       pasa a llevar ahi. Se tiene que llamar como dice
//       RecorridoDeLaHistoria.EscenaDelTutorial ('habitacion').
//
// Lo que se copia es UN objeto: 'Tutorial'. Adentro estan 'Despertar' (donde
// aparece el jugador), 'Partes' (lo que brilla) y 'Puerta'. Todo se busca solo
// al arrancar, asi que pegarlo en otra escena no rompe ninguna referencia.
//
// El esqueleto se REGENERA entero cada vez: no se trabaja sobre el, se copia.
// Los textos y el rival son assets (TutorialAssets) y esos no se pisan nunca.
public static class TutorialBuilder
{
    public const string EscenaEsqueleto = "Assets/Scenes/HabitacionTutorial_Esqueleto.unity";
    public const string Raiz = "Tutorial";

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string CombatePrefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string DialogoPrefab = "Assets/Prefabs/UI/DialogoCanvas.prefab";
    private const string ArteDelEsqueleto = "Assets/Sprites/Tutorial/Esqueleto";
    private const string SpriteHalo = "Assets/Sprites/UI/Battle/ballBurst_particle.png";
    private const string SpriteEstrella = "Assets/Sprites/UI/Battle/ballBurst_star.png";

    // El equipo de la partida DEMO (entrar directo por Play): como una partida
    // recien creada, un solo inicial a Nv 20. En el juego de verdad manda el
    // GameManager que viene de la creacion de personaje.
    private const string InicialDeLaDemo = "Assets/Resources/Definitions/Pokens/0448_Lucario.asset";
    private const int NivelDeLaDemo = 20;

    private const int CapaDefault = 0;
    private const int CapaObstacles = 3;

    // ---------------------------------------------------------------- donde

    // El cuarto de prueba, en celdas: piso de x -5..5 e y -4..4, rodeado de
    // pared. La puerta es la celda de la pared de abajo que queda libre.
    private const int MinX = -5, MaxX = 5, MinY = -4, MaxY = 4;
    private static readonly Vector2Int CeldaPuerta = new Vector2Int(0, -5);

    // El jugador despierta al lado de la cama (arriba a la izquierda).
    private static readonly Vector2Int CeldaDespertar = new Vector2Int(-4, 3);
    private static readonly Vector2Int CeldaCama = new Vector2Int(-5, 3);
    // Rebeca, la compañera, parada en el medio del cuarto: se la ve apenas se despierta.
    private static readonly Vector2Int CeldaCompanera = new Vector2Int(3, 0);

    // Los muebles que son partes: celda de abajo a la izquierda y tamanio. El
    // orden de la tabla es el de la jerarquia, y con el se nombran en "te
    // falta ...": la mochila, tu equipo, el PC, el diario y la tele.
    private struct Mueble
    {
        public string Objeto, Baldosa, Nombre;
        public Vector2Int Celda, Tamanio;
        public TutorialParte.Tipo Tipo;
        public StoryFlag Flag;
    }

    private static readonly Mueble[] Muebles =
    {
        new Mueble { Objeto = "Mochila", Baldosa = "mueble_mochila", Nombre = "la mochila", Celda = new Vector2Int(-2, 4),
                     Tamanio = Vector2Int.one, Tipo = TutorialParte.Tipo.Mochila, Flag = StoryFlag.Tutorial_Mochila },
        // La Pokeball del estante: el equipo.
        new Mueble { Objeto = "Equipo", Baldosa = "mueble_equipo", Nombre = "tu equipo", Celda = new Vector2Int(0, 4),
                     Tamanio = Vector2Int.one, Tipo = TutorialParte.Tipo.Equipo, Flag = StoryFlag.Tutorial_Equipo },
        // El escritorio con la PC: dos celdas.
        new Mueble { Objeto = "PC", Baldosa = "mueble_pc", Nombre = "el PC", Celda = new Vector2Int(2, 4),
                     Tamanio = new Vector2Int(2, 1), Tipo = TutorialParte.Tipo.Pc, Flag = StoryFlag.Tutorial_Pc },
        // El diario en la mesita de luz, al lado de la cama.
        new Mueble { Objeto = "Diario", Baldosa = "mueble_diario", Nombre = "el diario", Celda = new Vector2Int(-5, 2),
                     Tamanio = Vector2Int.one, Tipo = TutorialParte.Tipo.Guardar, Flag = StoryFlag.Tutorial_Guardar },
        new Mueble { Objeto = "Tele", Baldosa = "mueble_tele", Nombre = "la tele", Celda = new Vector2Int(5, 4),
                     Tamanio = Vector2Int.one, Tipo = TutorialParte.Tipo.Opciones, Flag = StoryFlag.Tutorial_Opciones },
    };

    // ------------------------------------------------------------- menus

    [MenuItem("PokeRush/Tutorial/Armar el esqueleto (escena vacía)")]
    public static void ArmarEsqueleto()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Tutorial] Salí del Play Mode antes de correr esto.");
            return;
        }

        Scene abierta = SceneManager.GetSceneByPath(EscenaEsqueleto);
        if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
        {
            Debug.LogError("[Tutorial] El esqueleto está abierto con cambios sin guardar. Guardalo o descartalo " +
                           "y volvé a correr esto (se regenera entero: lo que cambies ahí se pierde).");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Los assets se cargan DESPUES de abrir la escena nueva: abrir en modo
        // Single descarga lo que no usa ninguna escena abierta, y una referencia
        // leida antes llegaria nula (ver builder-assets-de-otra-escena).
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject mapaRaiz = AsegurarRaiz(scene, "MapaRaiz");
        ArmarCuartoDePrueba(mapaRaiz);
        TutorialDeLaHabitacion tutorial = ArmarTutorial(mapaRaiz);

        if (ArmarRig(scene, tutorial) == null) return;
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, out int alcanzables);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Tutorial] {p}");
            Debug.LogError("[Tutorial] El esqueleto NO se guardó. Revisá la tabla de posiciones de TutorialBuilder.");
            return;
        }

        EditorSceneManager.SaveScene(scene, EscenaEsqueleto);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Tutorial] TERMINADO. Esqueleto en {EscenaEsqueleto} ({alcanzables} celdas alcanzables). " +
                  "Lo que se copia a la habitación decorada es el objeto MapaRaiz/Tutorial.");
    }

    [MenuItem("PokeRush/Tutorial/Preparar la escena abierta (después de pegar)")]
    public static void PrepararEscenaAbierta()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Tutorial] Salí del Play Mode antes de correr esto.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[Tutorial] La escena abierta no tiene archivo: guardala primero.");
            return;
        }

        TutorialDeLaHabitacion tutorial = Buscar<TutorialDeLaHabitacion>(scene);
        if (tutorial == null)
        {
            Debug.LogError("[Tutorial] En esta escena no hay tutorial. Copiá el objeto MapaRaiz/Tutorial de " +
                           $"{EscenaEsqueleto} y pegalo acá antes de correr esto.");
            return;
        }

        if (ArmarRig(scene, tutorial) == null) return;
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, out int alcanzables);
        EditorSceneManager.MarkSceneDirty(scene);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Tutorial] {p}");
            Debug.LogError("[Tutorial] La escena quedó armada pero NO se guardó: acomodá lo de arriba " +
                           "(mové la parte sobre su mueble, del lado del cuarto) y volvé a correr esto.");
            return;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Tutorial] TERMINADO. '{scene.name}' quedó jugable ({alcanzables} celdas alcanzables desde " +
                  "donde despierta). Para que el juego pase por acá: PokeRush > Tutorial > Conectar la escena abierta al recorrido.");
    }

    [MenuItem("PokeRush/Tutorial/Conectar la escena abierta al recorrido")]
    public static void ConectarEscenaAbierta()
    {
        if (EditorApplication.isPlaying) return;

        Scene scene = SceneManager.GetActiveScene();
        if (Buscar<TutorialDeLaHabitacion>(scene) == null)
        {
            Debug.LogError("[Tutorial] La escena abierta no tiene tutorial: abrí la habitación decorada.");
            return;
        }
        if (scene.path == EscenaEsqueleto)
        {
            Debug.LogError("[Tutorial] El esqueleto no va al recorrido: es la escena de prueba. " +
                           "Conectá la habitación decorada.");
            return;
        }
        if (scene.name != RecorridoDeLaHistoria.EscenaDelTutorial)
        {
            Debug.LogError($"[Tutorial] La escena se llama '{scene.name}' y el recorrido espera " +
                           $"'{RecorridoDeLaHistoria.EscenaDelTutorial}'. Renombrala (o cambiá " +
                           "RecorridoDeLaHistoria.EscenaDelTutorial) y volvé a correr esto.");
            return;
        }

        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        // Nombre y ruta antes de seguir: ConectarRecorrido abre otras escenas y
        // el struct 'scene' queda apuntando a una escena cerrada.
        string path = scene.path;
        string nombre = scene.name;

        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EditorBuildSettingsScene existente = lista.FirstOrDefault(s => s.path == path);
        if (existente == null) lista.Add(new EditorBuildSettingsScene(path, true));
        else existente.enabled = true;
        EditorBuildSettings.scenes = lista.ToArray();

        // Con la habitacion en Build Settings, el destino de la creacion de
        // personaje pasa a ser ella (RecorridoDeLaHistoria.EscenaDespuesDeCrearPersonaje).
        QuarchoCuevaBuilder.ConectarRecorrido();
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log($"[Tutorial] Recorrido: creación de personaje -> {nombre} -> {RecorridoDeLaHistoria.EscenaDelLobby}.");
    }

    // ------------------------------------------------------------- el cuarto de prueba

    // Baldosas lisas con el nombre del mueble: se ve que es un esqueleto y en
    // Play se entiende que es cada cosa. En la habitacion decorada no se usa.
    private static void ArmarCuartoDePrueba(GameObject mapaRaiz)
    {
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(mapaRaiz.transform, false);
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        Tilemap suelo = Capa(gridGo, "Suelo", -10);
        Capa(gridGo, "Decorado", 0);
        Tilemap colisiones = Capa(gridGo, "Colisiones", 10);
        Capa(gridGo, "Techos", 20);

        Tile piso = Baldosa("piso", false);
        Tile pared = Baldosa("pared", true);
        Tile salida = Baldosa("salida", false);

        for (int x = MinX - 1; x <= MaxX + 1; x++)
        for (int y = MinY - 1; y <= MaxY + 1; y++)
        {
            var celda = new Vector3Int(x, y, 0);
            bool adentro = x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
            if (adentro) suelo.SetTile(celda, piso);
            else if (x == CeldaPuerta.x && y == CeldaPuerta.y) suelo.SetTile(celda, salida);
            else colisiones.SetTile(celda, pared);
        }

        // La cama no es una parte: es de donde se despierta.
        Tile cama = Baldosa("mueble_cama", true);
        colisiones.SetTile(new Vector3Int(CeldaCama.x, CeldaCama.y, 0), cama);
        colisiones.SetTile(new Vector3Int(CeldaCama.x, CeldaCama.y + 1, 0), cama);

        foreach (Mueble m in Muebles)
        {
            Tile t = Baldosa(m.Baldosa, true);
            for (int dx = 0; dx < m.Tamanio.x; dx++)
            for (int dy = 0; dy < m.Tamanio.y; dy++)
                colisiones.SetTile(new Vector3Int(m.Celda.x + dx, m.Celda.y + dy, 0), t);
        }
    }

    private static Tilemap Capa(GameObject grid, string nombre, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = orden;
        return tilemap;
    }

    // Un Tile con su sprite a 32 px por celda. Solo las del esqueleto: con
    // colliderType propio, porque no las comparte nadie (las de las paletas del
    // equipo si se comparten, y por eso ahi va el override por celda).
    private static Tile Baldosa(string nombre, bool solida)
    {
        string png = $"{ArteDelEsqueleto}/{nombre}.png";
        string carpeta = $"{ArteDelEsqueleto}/Tiles";
        string path = $"{carpeta}/{nombre}.asset";

        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        bool nueva = tile == null;
        if (nueva) tile = ScriptableObject.CreateInstance<Tile>();

        tile.sprite = SpriteDeBaldosa(png);
        tile.colliderType = solida ? Tile.ColliderType.Grid : Tile.ColliderType.None;

        if (nueva)
        {
            MenuUiKit.EnsureFolder(carpeta);
            AssetDatabase.CreateAsset(tile, path);
        }
        else EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Sprite SpriteDeBaldosa(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) { Debug.LogError($"[Tutorial] No existe {path}."); return null; }

        bool bien = importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    !importer.mipmapEnabled &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 32f);
        if (!bien)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ------------------------------------------------------------- el tutorial

    private static TutorialDeLaHabitacion ArmarTutorial(GameObject mapaRaiz)
    {
        var raiz = new GameObject(Raiz);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        var despertar = new GameObject("Despertar");
        despertar.transform.SetParent(raiz.transform, false);
        despertar.transform.position = Centro(CeldaDespertar, Vector2Int.one);

        var partes = new GameObject("Partes");
        partes.transform.SetParent(raiz.transform, false);

        HealItemDefinition pocion = TutorialAssets.Pocion();
        foreach (Mueble m in Muebles)
        {
            TutorialParte parte = Parte(partes, m, ExplicacionDe(m.Tipo));
            if (m.Tipo == TutorialParte.Tipo.Mochila && pocion != null)
                Entrega(parte, pocion, 2);
        }

        PonerCompanera(partes);
        ArmarPuerta(raiz);

        var tutorial = raiz.AddComponent<TutorialDeLaHabitacion>();
        var so = new SerializedObject(tutorial);
        so.FindProperty("puntoDeDespertar").objectReferenceValue = despertar.transform;
        so.FindProperty("despertar").objectReferenceValue = TutorialAssets.Despertar();
        so.FindProperty("cierre").objectReferenceValue = TutorialAssets.Cierre();
        so.ApplyModifiedPropertiesWithoutUndo();
        return tutorial;
    }

    private static DialogueDefinition ExplicacionDe(TutorialParte.Tipo tipo)
    {
        switch (tipo)
        {
            case TutorialParte.Tipo.Mochila: return TutorialAssets.Mochila();
            case TutorialParte.Tipo.Equipo: return TutorialAssets.Equipo();
            case TutorialParte.Tipo.Pc: return TutorialAssets.Pc();
            case TutorialParte.Tipo.Guardar: return TutorialAssets.Guardar();
            case TutorialParte.Tipo.Opciones: return TutorialAssets.Opciones();
            default: return null;
        }
    }

    // Una parte sobre un mueble: el collider tapa el mueble entero (frena y es
    // lo que se aprieta con X desde cualquier lado) y el brillo lo marca.
    private static TutorialParte Parte(GameObject padre, Mueble m, DialogueDefinition explicacion)
    {
        var go = new GameObject(m.Objeto);
        go.transform.SetParent(padre.transform, false);
        go.transform.position = Centro(m.Celda, m.Tamanio);
        // Obstacles: es la capa que mira el interactableMask del jugador.
        go.layer = CapaObstacles;
        go.AddComponent<BoxCollider2D>().size = new Vector2(m.Tamanio.x - 0.1f, m.Tamanio.y - 0.1f);

        var parte = go.AddComponent<TutorialParte>();
        var so = new SerializedObject(parte);
        so.FindProperty("nombre").stringValue = m.Nombre;
        so.FindProperty("flag").enumValueIndex = (int)m.Flag;
        so.FindProperty("tipo").enumValueIndex = (int)m.Tipo;
        so.FindProperty("explicacion").objectReferenceValue = explicacion;
        so.ApplyModifiedPropertiesWithoutUndo();

        Brillo(go, new Vector2(0f, 0.1f), 1.15f, 0.4f, 12);
        return parte;
    }

    private static void Entrega(TutorialParte parte, HealItemDefinition objeto, int cantidad)
    {
        var so = new SerializedObject(parte);
        SerializedProperty lista = so.FindProperty("entrega");
        lista.arraySize = 1;
        SerializedProperty e = lista.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("objeto").objectReferenceValue = objeto;
        e.FindPropertyRelative("cantidad").intValue = cantidad;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // La pelea: una persona parada en el cuarto (sprite y nombre provisorios).
    // Espera a las demas partes: se llega sabiendo usar la mochila.
    private static void PonerCompanera(GameObject partes)
    {
        Sprite retrato = CasinoNpcBuilder.SpriteRetrato(TutorialAssets.RetratoDelRival);
        GameObject go = CasinoNpcBuilder.Personaje(partes, "Rebeca", Centro(CeldaCompanera, Vector2Int.one),
                                                   TutorialAssets.HojaDelRival, CasinoNpcBuilder.Izquierda);

        var parte = go.AddComponent<TutorialParte>();
        var so = new SerializedObject(parte);
        so.FindProperty("nombre").stringValue = "la pelea";
        so.FindProperty("flag").enumValueIndex = (int)StoryFlag.Tutorial_Pelea;
        so.FindProperty("tipo").enumValueIndex = (int)TutorialParte.Tipo.Pelea;
        so.FindProperty("esperaALasDemas").boolValue = true;
        so.FindProperty("explicacion").objectReferenceValue = TutorialAssets.Pelea(retrato);
        so.FindProperty("rival").objectReferenceValue = TutorialAssets.Rival(retrato);
        so.FindProperty("mensajeEsperando").stringValue =
            "Primero acomodá tus cosas: te falta {0}. Después peleamos.";
        so.FindProperty("mensajeYaHecha").stringValue = "Ya está: ya sabés pelear. La puerta te espera.";
        so.ApplyModifiedPropertiesWithoutUndo();

        // Arriba de la cabeza, como un aviso: a la altura de los pies el cuerpo
        // taparia el brillo.
        Brillo(go, new Vector2(0f, 2.2f), 0.8f, 0.5f, 16);
    }

    private static void ArmarPuerta(GameObject raiz)
    {
        // Zona para PISAR: trigger en Default. En Obstacles frena al jugador.
        var puerta = new GameObject("Puerta");
        puerta.transform.SetParent(raiz.transform, false);
        puerta.transform.position = Centro(CeldaPuerta, Vector2Int.one);
        puerta.layer = CapaDefault;
        var zona = puerta.AddComponent<BoxCollider2D>();
        zona.size = new Vector2(0.8f, 0.8f);
        zona.isTrigger = true;
        var componente = puerta.AddComponent<PuertaDelTutorial>();

        // El camino a la Quarcho Cueva (Cruz, el viejo Ameghino, la cerradura
        // NFC): lo cuenta la puerta sobre negro antes de cargar la cueva.
        var so = new SerializedObject(componente);
        so.FindProperty("camino").objectReferenceValue = HistoriaAssets.Dialogo("Historia/Camino/Camino_Inicio");
        so.ApplyModifiedPropertiesWithoutUndo();

        // Del otro lado no hay mapa: un tope para no salir caminando al vacio
        // despues de decir "todavia no". Si la puerta queda en otra pared, se
        // mueve el tope con ella (va hijo, del lado de afuera).
        var tope = new GameObject("Tope");
        tope.transform.SetParent(puerta.transform, false);
        tope.transform.localPosition = new Vector3(0f, -1f, 0f);
        tope.layer = CapaObstacles;
        tope.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);

        PonerFlecha(puerta);
    }

    // ---------- la flecha de la salida ----------

    // La hoja animada de Essentials: ocho cuadros de 28x40 que ya suben y bajan.
    // Venia importada como UN sprite de 224x28 (mal cortada): FlechaHaciaAbajo
    // la corta bien la primera vez.
    private const string HojaDeLaFlecha = "Assets/Graphics/UI/down_arrow.png";
    private const int CuadrosDeLaFlecha = 8;
    private static readonly Vector2Int CuadroDeLaFlecha = new Vector2Int(28, 40);

    // Una flecha sobre la puerta, apuntando hacia afuera
    // (playtest del 4-oct: no se encontraba la salida). Aparece al terminar el
    // tutorial, que es cuando la puerta abre. Va hija de la puerta: si la
    // puerta se mueve, la flecha va con ella.
    private static void PonerFlecha(GameObject puerta)
    {
        Transform vieja = puerta.transform.Find("Flecha");
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var flecha = new GameObject("Flecha");
        flecha.transform.SetParent(puerta.transform, false);
        flecha.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        Sprite[] cuadros = FlechaHaciaAbajo();
        var sr = flecha.AddComponent<SpriteRenderer>();
        sr.sprite = cuadros.Length > 0 ? cuadros[0] : null;
        sr.sortingOrder = 16;   // sobre los personajes (15): es una indicacion

        var so = new SerializedObject(flecha.AddComponent<FlechaIndicadora>());
        SerializedProperty lista = so.FindProperty("cuadros");
        lista.arraySize = cuadros.Length;
        for (int i = 0; i < cuadros.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = cuadros[i];
        so.FindProperty("cuadrosPorSegundo").floatValue = 8f;
        so.FindProperty("direccion").vector2Value = Vector2.down;
        // Los cuadros ya rebotan: el recorrido extra es apenas un empujon.
        so.FindProperty("recorrido").floatValue = 0.1f;
        so.FindProperty("apareceCon").enumValueIndex = (int)StoryFlag.Tutorial_Completado;
        so.FindProperty("seVaCon").enumValueIndex = (int)StoryFlag.None;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (cuadros.Length != CuadrosDeLaFlecha)
            Debug.LogWarning($"[Tutorial] La flecha salio con {cuadros.Length} cuadros (esperaba {CuadrosDeLaFlecha}): revisa {HojaDeLaFlecha}.");
    }

    public static Sprite[] FlechaHaciaAbajo()
    {
        var cortados = AssetDatabase.LoadAllAssetsAtPath(HojaDeLaFlecha).OfType<Sprite>()
            .OrderBy(sp => sp.rect.x).ToArray();
        if (cortados.Length == CuadrosDeLaFlecha) return cortados;

        var importer = AssetImporter.GetAtPath(HojaDeLaFlecha) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[Tutorial] No encontre {HojaDeLaFlecha}.");
            return new Sprite[0];
        }

        // Pixel art del mismo tamanio que los tiles (32 px por celda).
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 32;
        importer.SaveAndReimport();

        // Unity 6 ignora 'spritesheet': los cortes van por el data provider.
        var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = new List<SpriteRect>();
        var pares = new List<SpriteNameFileIdPair>();
        for (int i = 0; i < CuadrosDeLaFlecha; i++)
        {
            var r = new SpriteRect
            {
                name = $"down_arrow_{i}",
                spriteID = GUID.Generate(),
                rect = new Rect(i * CuadroDeLaFlecha.x, 0f, CuadroDeLaFlecha.x, CuadroDeLaFlecha.y),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
            rects.Add(r);
            pares.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        }
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(pares);
        provider.Apply();
        AssetDatabase.ImportAsset(HojaDeLaFlecha, ImportAssetOptions.ForceUpdate);

        return AssetDatabase.LoadAllAssetsAtPath(HojaDeLaFlecha).OfType<Sprite>().OrderBy(sp => sp.rect.x).ToArray();
    }

    // Lo mismo sobre la habitacion ya armada, sin rehacer nada mas.
    [MenuItem("PokeRush/Tutorial/Poner la flecha de la salida (escena abierta)")]
    public static void PonerFlechaEnLaEscenaAbierta()
    {
        if (EditorApplication.isPlaying) return;
        Scene scene = SceneManager.GetActiveScene();
        PuertaDelTutorial puerta = Buscar<PuertaDelTutorial>(scene);
        if (puerta == null)
        {
            Debug.LogError("[Tutorial] La escena abierta no tiene PuertaDelTutorial.");
            return;
        }
        PonerFlecha(puerta.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Tutorial] Flecha de la salida puesta en '{scene.name}' sobre {puerta.transform.position}.");
    }

    // El brillo sin flag propio: lo toma de la TutorialParte, asi cambiar el
    // flag de la parte alcanza.
    private static void Brillo(GameObject go, Vector2 desplazamiento, float halo, float estrella, int orden)
    {
        var brillo = go.AddComponent<BrilloInteractuable>();
        var so = new SerializedObject(brillo);
        so.FindProperty("flagQueLoApaga").enumValueIndex = (int)StoryFlag.None;
        so.FindProperty("spriteHalo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteHalo);
        so.FindProperty("spriteEstrella").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteEstrella);
        so.FindProperty("desplazamiento").vector2Value = desplazamiento;
        so.FindProperty("tamanioHalo").floatValue = halo;
        so.FindProperty("tamanioEstrella").floatValue = estrella;
        so.FindProperty("sortingOrder").intValue = orden;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Centro de un bloque de celdas (con el Grid de siempre, medios enteros).
    private static Vector3 Centro(Vector2Int celda, Vector2Int tamanio)
        => new Vector3(celda.x + tamanio.x * 0.5f, celda.y + tamanio.y * 0.5f, 0f);

    // ------------------------------------------------------------- el rig

    // Lo mismo que la cueva y los pisos del casino: MapaRaiz con el mapa, el
    // jugador, la camara y el tutorial adentro (es lo que MapBattleBridge
    // esconde durante la pelea), y afuera el GameManager, el sistema, el dialogo
    // y el combate. Idempotente: completa lo que falta y no duplica nada.
    private static GameObject ArmarRig(Scene scene, TutorialDeLaHabitacion tutorial)
    {
        GameObject mapaRaiz = AsegurarRaiz(scene, "MapaRaiz");
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            if (raiz == mapaRaiz) continue;
            if (raiz.GetComponent<Grid>() != null || raiz.GetComponent<Camera>() != null)
                raiz.transform.SetParent(mapaRaiz.transform, true);
        }

        // Pegado en cualquier otro lado, el tutorial tambien va adentro.
        if (!tutorial.transform.IsChildOf(mapaRaiz.transform))
            tutorial.transform.SetParent(mapaRaiz.transform, true);

        if (mapaRaiz.GetComponentInChildren<Grid>(true) == null)
        {
            Debug.LogError("[Tutorial] La escena no tiene Grid: ¿es la habitación?");
            return null;
        }

        PrepararColisiones(mapaRaiz);
        GameObject player = AsegurarPlayer(scene, mapaRaiz, tutorial);
        PrepararCamara(mapaRaiz, player);
        AsegurarGameManager(scene);
        AsegurarEventSystem(scene);
        GameObject combate = AsegurarCombate(scene);
        AsegurarSistema(scene, mapaRaiz, combate, player);
        AsegurarDialogo(scene);
        return mapaRaiz;
    }

    private static void PrepararColisiones(GameObject mapaRaiz)
    {
        foreach (Tilemap tilemap in mapaRaiz.GetComponentsInChildren<Tilemap>(true))
        {
            if (tilemap.name != "Colisiones") continue;

            GameObject go = tilemap.gameObject;
            go.layer = CapaObstacles;
            foreach (Vector3Int p in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(p) && tilemap.GetColliderType(p) != Tile.ColliderType.Grid)
                    tilemap.SetColliderType(p, Tile.ColliderType.Grid);

            var col = Asegurar<TilemapCollider2D>(go);
            var rb = Asegurar<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Static;
            var comp = Asegurar<CompositeCollider2D>(go);
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            Asegurar<ColisionesTilemapRuntimeFix>(go);

            var renderer = go.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    private static GameObject AsegurarPlayer(Scene scene, GameObject mapaRaiz, TutorialDeLaHabitacion tutorial)
    {
        PlayerController existente = Buscar<PlayerController>(scene);
        GameObject player;
        if (existente == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapaRaiz.transform);
            player.name = "Player";
        }
        else
        {
            player = existente.gameObject;
            if (!player.transform.IsChildOf(mapaRaiz.transform)) player.transform.SetParent(mapaRaiz.transform, true);
        }

        // En el centro de la celda de 'Despertar' (el tutorial lo vuelve a
        // poner ahi al arrancar, pero asi la escena ya se ve como va a empezar).
        Transform despertar = tutorial.PuntoDeDespertar;
        if (despertar != null)
        {
            Vector3 p = despertar.position;
            player.transform.position = new Vector3(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f, 0f);
        }

        // Sin esto X no hace nada: el prefab viene con la mascara vacia.
        var so = new SerializedObject(player.GetComponent<PlayerController>());
        so.FindProperty("interactableMask").intValue |= 1 << CapaObstacles;
        so.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static void PrepararCamara(GameObject mapaRaiz, GameObject player)
    {
        Camera cam = mapaRaiz.GetComponentsInChildren<Camera>(true).FirstOrDefault();
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.transform.SetParent(mapaRaiz.transform);
            cam = go.AddComponent<Camera>();
            // 7,5 celdas de media altura, como el resto de los mapas (CameraFollow
            // cuenta con eso para que no aparezcan rayas entre tiles).
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        }

        cam.gameObject.tag = "MainCamera";
        cam.orthographic = true;
        Asegurar<AudioListener>(cam.gameObject);

        var follow = Asegurar<CameraFollow>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
    }

    private static void AsegurarGameManager(Scene scene)
    {
        GameObject go = AsegurarRaiz(scene, "GameManager");
        var gm = Asegurar<GameManager>(go);
        var so = new SerializedObject(gm);

        // Solo si esta vacio: no se pisa lo que el equipo haya elegido.
        SerializedProperty equipo = so.FindProperty("equipoInicial");
        bool vacio = true;
        for (int i = 0; i < equipo.arraySize; i++)
            if (equipo.GetArrayElementAtIndex(i).objectReferenceValue != null) vacio = false;

        var inicial = AssetDatabase.LoadAssetAtPath<PokenDefinition>(InicialDeLaDemo);
        if (vacio && inicial != null)
        {
            equipo.arraySize = 1;
            equipo.GetArrayElementAtIndex(0).objectReferenceValue = inicial;
            SerializedProperty niveles = so.FindProperty("nivelesIniciales");
            niveles.arraySize = 1;
            niveles.GetArrayElementAtIndex(0).intValue = NivelDeLaDemo;
        }

        Transform audio = go.transform.Find("Audio");
        if (audio == null)
        {
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(go.transform);
            audio = audioGo.transform;
            var musica = audioGo.AddComponent<AudioSource>();
            musica.playOnAwake = false;
            musica.loop = true;
            audioGo.AddComponent<AudioSource>().playOnAwake = false;
        }
        AudioSource[] fuentes = audio.GetComponents<AudioSource>();
        so.FindProperty("musicSource").objectReferenceValue = fuentes[0];
        so.FindProperty("sfxSource").objectReferenceValue = fuentes.Length > 1 ? fuentes[1] : fuentes[0];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarEventSystem(Scene scene)
    {
        if (Buscar<EventSystem>(scene) != null) return;
        GameObject go = AsegurarRaiz(scene, "EventSystem");
        Asegurar<EventSystem>(go);
        Asegurar<InputSystemUIInputModule>(go);
    }

    private static GameObject AsegurarCombate(Scene scene)
    {
        BattleController existente = Buscar<BattleController>(scene);
        GameObject combate = existente != null ? existente.gameObject : null;
        if (combate == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatePrefab);
            if (prefab == null) { Debug.LogError($"[Tutorial] Falta {CombatePrefab}."); return null; }
            combate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            combate.name = "SistemaDeCombate";
        }

        // Va DESACTIVADO: lo prende MapBattleBridge cuando arranca la pelea.
        combate.SetActive(false);
        return combate;
    }

    private static void AsegurarSistema(Scene scene, GameObject mapaRaiz, GameObject combate, GameObject player)
    {
        GameObject sistema = AsegurarRaiz(scene, "Sistema");
        var bridge = Asegurar<MapBattleBridge>(sistema);
        var boot = Asegurar<SceneBootstrapper>(sistema);
        Asegurar<GuardadoRapido>(sistema);

        var playerController = player.GetComponent<PlayerController>();
        var soBridge = new SerializedObject(bridge);
        soBridge.FindProperty("mapaRaiz").objectReferenceValue = mapaRaiz;
        soBridge.FindProperty("combateRaiz").objectReferenceValue = combate;
        soBridge.FindProperty("battleController").objectReferenceValue =
            combate != null ? combate.GetComponent<BattleController>() : null;
        soBridge.FindProperty("playerController").objectReferenceValue = playerController;
        soBridge.ApplyModifiedPropertiesWithoutUndo();

        var soBoot = new SerializedObject(boot);
        soBoot.FindProperty("playerController").objectReferenceValue = playerController;
        soBoot.FindProperty("crearPartidaSiFalta").boolValue = true;
        SerializedProperty nombre = soBoot.FindProperty("nombrePartidaDePrueba");
        if (string.IsNullOrEmpty(nombre.stringValue)) nombre.stringValue = "DEMO";
        soBoot.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarDialogo(Scene scene)
    {
        if (Buscar<DialogueController>(scene) != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogoPrefab);
        if (prefab == null) { Debug.LogError($"[Tutorial] Falta {DialogoPrefab}."); return; }
        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        canvas.name = "DialogoCanvas";
    }

    // ------------------------------------------------------------- verificar

    // Devuelve la lista de problemas (vacia = todo bien). Mira el mapa en
    // celdas: el piso es el Suelo, las paredes y muebles son Colisiones, y
    // frenan tambien los colliders solidos en Obstacles (las partes, la
    // compañera, el tope de la puerta y lo que haya puesto el equipo).
    public static List<string> Verificar(Scene scene, out int alcanzables)
    {
        var problemas = new List<string>();
        alcanzables = 0;

        TutorialDeLaHabitacion tutorial = Buscar<TutorialDeLaHabitacion>(scene);
        if (tutorial == null) { problemas.Add("no hay TutorialDeLaHabitacion en la escena."); return problemas; }
        if (tutorial.PuntoDeDespertar == null) { problemas.Add("el tutorial no tiene 'puntoDeDespertar'."); return problemas; }

        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap suelo = tilemaps.FirstOrDefault(t => t.name == "Suelo");
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        if (suelo == null || colisiones == null)
        {
            problemas.Add("faltan los tilemaps 'Suelo' o 'Colisiones' (los nombres de siempre, ver " +
                          "Como_Armar_Un_Mapa_Nuevo.md): sin ellos no se puede revisar que se llegue a todo.");
            return problemas;
        }

        var partes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TutorialParte>(true)).ToList();
        var puertas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PuertaDelTutorial>(true)).ToList();
        if (partes.Count == 0) problemas.Add("el tutorial no tiene ninguna parte.");
        if (puertas.Count == 0) problemas.Add("el tutorial no tiene puerta.");

        var solidos = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
                        c.gameObject.layer == CapaObstacles &&
                        !(c is TilemapCollider2D) && !(c is CompositeCollider2D) &&
                        c.GetComponentInParent<PlayerController>() == null)
            .ToList();

        bool Bloqueada(Vector3Int c)
        {
            if (colisiones.HasTile(c)) return true;
            Vector3 centro = suelo.GetCellCenterWorld(c);
            return solidos.Any(s => s.bounds.Contains(new Vector3(centro.x, centro.y, s.bounds.center.z)));
        }

        Vector3Int spawn = suelo.WorldToCell(tutorial.PuntoDeDespertar.position);
        if (!suelo.HasTile(spawn) || Bloqueada(spawn)) problemas.Add($"donde despierta ({spawn}) no es piso libre.");

        // Flood fill desde donde despierta, acotado: las escenas hechas desde el
        // template traen un bloque fantasma de tiles muy lejos. De paso junta
        // los HUECOS: celdas sin piso ni pared pegadas a una que se pisa. El
        // jugador no mira si hay piso, solo si hay pared, asi que por un hueco
        // sale caminando al vacio.
        var alcanzadas = new HashSet<Vector3Int> { spawn };
        var huecos = new SortedSet<string>();
        var cola = new Queue<Vector3Int>();
        cola.Enqueue(spawn);
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (cola.Count > 0)
        {
            Vector3Int c = cola.Dequeue();
            foreach (Vector3Int d in dirs)
            {
                Vector3Int n = c + d;
                if (alcanzadas.Contains(n) || Mathf.Abs(n.x - spawn.x) > 40 || Mathf.Abs(n.y - spawn.y) > 40) continue;
                if (Bloqueada(n)) continue;
                if (!suelo.HasTile(n)) { huecos.Add($"({n.x}, {n.y})"); continue; }
                alcanzadas.Add(n);
                cola.Enqueue(n);
            }
        }
        alcanzables = alcanzadas.Count;

        if (huecos.Count > 0)
            problemas.Add($"se puede salir caminando del mapa por {string.Join(" ", huecos)}: ahi no hay piso " +
                          "ni pared. Taparlas en 'Colisiones' con la pared invisible (ANDRUSIANI/Tiles/ParedInvisible).");

        foreach (TutorialParte parte in partes)
            if (!AlcanzableAlLado(parte.GetComponent<Collider2D>(), suelo, alcanzadas))
                problemas.Add($"no se llega a '{parte.name}' ({parte.Nombre}): ninguna celda de al lado se alcanza caminando.");

        foreach (PuertaDelTutorial puerta in puertas)
        {
            Collider2D zona = puerta.GetComponent<Collider2D>();
            if (zona != null && !zona.isTrigger)
            {
                // Dibujada como pared: se usa con X desde al lado.
                if (puerta.gameObject.layer != CapaObstacles)
                    problemas.Add("la puerta es solida (se usa con X) y tiene que estar en la capa Obstacles.");
                if (!AlcanzableAlLado(zona, suelo, alcanzadas))
                    problemas.Add("no se llega a pararse al lado de la puerta.");
                continue;
            }

            if (puerta.gameObject.layer == CapaObstacles)
                problemas.Add("la puerta es una zona para pisar y esta en Obstacles: frena al jugador. Va en Default.");
            Vector3Int celda = suelo.WorldToCell(puerta.transform.position);
            if (!alcanzadas.Contains(celda)) problemas.Add($"no se llega caminando a la puerta ({celda}).");
        }

        Debug.Log($"[Tutorial] Verificación: {alcanzables} celdas alcanzables desde {spawn}.");
        return problemas;
    }

    private static bool AlcanzableAlLado(Collider2D col, Tilemap suelo, HashSet<Vector3Int> alcanzadas)
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        Vector3Int min = suelo.WorldToCell(b.min + new Vector3(0.05f, 0.05f, 0f));
        Vector3Int max = suelo.WorldToCell(b.max - new Vector3(0.05f, 0.05f, 0f));
        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
        {
            var c = new Vector3Int(x, y, 0);
            if (alcanzadas.Contains(c + Vector3Int.up) || alcanzadas.Contains(c + Vector3Int.down) ||
                alcanzadas.Contains(c + Vector3Int.left) || alcanzadas.Contains(c + Vector3Int.right))
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------- utilidades

    // Abrir una escena en el editor pierde los colliderType = Grid por celda, y
    // guardarla asi los pierde en disco (ver Como_Armar_Un_Mapa_Nuevo.md).
    private static void ReponerColisiones(Scene scene)
    {
        foreach (Tilemap t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (t.name != "Colisiones") continue;
            foreach (Vector3Int p in t.cellBounds.allPositionsWithin)
                if (t.HasTile(p) && t.GetColliderType(p) != Tile.ColliderType.Grid)
                    t.SetColliderType(p, Tile.ColliderType.Grid);
        }
    }

    private static GameObject AsegurarRaiz(Scene scene, string nombre)
    {
        GameObject existente = scene.GetRootGameObjects().FirstOrDefault(r => r.name == nombre);
        if (existente != null) return existente;
        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    public static T Buscar<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

    private static T Asegurar<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
