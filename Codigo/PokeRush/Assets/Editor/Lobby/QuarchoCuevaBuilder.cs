using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Deja jugable la Quarcho Cueva, el lobby entre niveles.
//
//   PokeRush > Quarcho Cueva > Armar el lobby
//   PokeRush > Quarcho Cueva > Conectar el recorrido
//
// El MAPA lo hizo el equipo y no se toca: no se pinta, no se mueve y no se borra
// ningun tile. Arriba del mapa se agrega lo mismo que a los pisos del casino
// (jugador, camara, GameManager, bootstrap, dialogo) y un contenedor 'Lobby'
// con los personajes:
//
//   Tixi      recibe al jugador, cuenta el lore, vende y lo manda a cada nivel.
//   Nestor    aparece con Casino_Completado (en el guion se queda en su Data
//             Center hasta recordar): tutor de MTs.
//   Martin    aparece con Casino_Completado: sus tres ruletas y los juegos del
//             casino, sentado atras de SU mesa (la del ultimo piso).
//   Tacconi   aparece con Casino_Completado: tres chances de ganarle un Mew.
//   PC        el escritorio de la computadora.
//   Salida    la puerta del este, al siguiente nivel de la historia.
//
// Idempotente: borra y rehace SOLO el contenedor 'Lobby'. Antes de guardar
// verifica que cada cosa este sobre piso libre y que se llegue caminando desde
// el spawn a hablarle; si algo falla, no guarda.
//
// Las posiciones estan en la tabla de abajo, en celdas (el centro de una celda
// cae en medios enteros). Cada una tiene su por que: mover a alguien es cambiar
// un numero y volver a correr el menu.
public static class QuarchoCuevaBuilder
{
    public const string Escena = "Assets/Scenes/Quarcho-Cueva.unity";
    private const string EscenaModelo = "Assets/Scenes/NestorGOD2.unity";
    private const string EscenaCreacion = "Assets/Scenes/CreacionPersonaje.unity";
    private const string EscenaSaves = "Assets/Scenes/MenuSaves.unity";

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string DialogoPrefab = "Assets/Prefabs/UI/DialogoCanvas.prefab";
    private const string Retratos = "Assets/Graphics/Trainers/";
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Casino = "Assets/Resources/Definitions/Casino/";

    public const string Contenedor = "Lobby";
    private const int CapaDefault = 0;
    private const int CapaObstacles = 3;
    private const int OrdenPersonajes = 15;

    // ---------------------------------------------------------------- donde

    // El jugador aparece adentro, al lado de la puerta del este: es por donde
    // se llega de cada nivel.
    public static readonly Vector2 Spawn = new Vector2(5.5f, 0.5f);
    // La puerta: la unica celda de piso de la pared del este.
    public static readonly Vector2 Salida = new Vector2(6.5f, 0.5f);
    // Del otro lado de la puerta no hay mapa: un tope invisible para no salir
    // caminando al vacio despues de decir "todavia no".
    private static readonly Vector2 Tope = new Vector2(7.5f, 0.5f);

    // Tixi en el hueco entre el escritorio y la biblioteca (celdas x 0..1 de la
    // fila 2), pegado a la biblioteca: es lo primero que se ve al entrar, y
    // parado ahi atiende como en un mostrador.
    public static readonly Vector2 Tixi = new Vector2(1.5f, 2.5f);
    // El PC es la computadora que ya esta dibujada en el escritorio (x -3). El
    // hotspot cubre el escritorio entero (x -3..-1) para agarrarlo desde abajo.
    public static readonly Vector2 Pc = new Vector2(-1.5f, 2.5f);
    private static readonly Vector2 TamanioPc = new Vector2(2.9f, 0.9f);
    // Nestor entre los racks de la sala de servidores: el de ciberseguridad.
    public static readonly Vector2 Nestor = new Vector2(-11.5f, 2.5f);

    // La mesa de Martin: la de cartas del ultimo piso del casino (3x2), en las
    // celdas (-4..-2, -4..-3): el rincon de juegos, contra la pared de abajo y
    // pegada a la maquina arcade y su silla (x -6 y -5). Antes estaba en el
    // medio del cuarto, sin nada alrededor (2-oct-2026, lo pidio Augusto).
    // Martin sentado atras, del lado recto (el del croupier); se le habla desde
    // arriba o desde el costado derecho.
    private static readonly Vector3Int MesaEnLaCueva = new Vector3Int(-4, -4, 0);
    private static readonly Vector2Int TamanioDeLaMesa = new Vector2Int(3, 2);
    // El busto va METIDO en la mesa, como en el piso final: el borde que se
    // ve de la mesa esta en y ~ -2,53 (la fila de arriba del tile es
    // transparente); con la base en -2,4 quedaba separado (playtest 5-oct).
    private static readonly Vector2 ApoyoDeMartin = new Vector2(-2.5f, -2.85f);
    // Martin ocupa la fila de atras de la mesa (y -2).
    private static readonly Vector2 SillaDeMartin = new Vector2(-2.5f, -1.5f);
    // Tacconi parado al costado derecho de la mesa de Martin: juega sus apuestas ahi.
    public static readonly Vector2 Tacconi = new Vector2(-0.5f, -3.5f);

    // La enfermera (playtest del 4-oct): a la izquierda del escritorio del PC,
    // contra la pared de arriba, sin tapar el paso hacia la sala de Nestor.
    // Cura gratis y habla como la del Data Center (NpcQueCura).
    public static readonly Vector2 Enfermera = new Vector2(-5.5f, 1.5f);
    private const string HojaDeLaEnfermera = "enfermera";
    private const string DialogoDeLaEnfermera = "Lobby/Dialogos/Lobby_Enfermera";

    // Lo que Tixi le dice al PJ cuando se va de la cueva despues de la vuelta
    // del casino: le da el transmisor. Antes iba pegado al final de esa charla
    // (playtest del 4-oct: demasiado texto seguido). Lo dicen la puerta del
    // este y el viaje con Tixi, lo que se use primero.
    private const string DialogoDelTransmisor = "Lobby/Dialogos/Lobby_Tixi_VueltaDelCasino_Transmisor";

    // Mew: el premio de Tacconi en la cueva. Nv 52, lo mas alto que puede
    // conseguir el jugador, dos arriba del Mewtwo de Julieta (rebalanceo del
    // 6-oct-2026; era 30).
    private const string EspecieMew = "0151_Mew";
    private const int NivelMew = 52;

    // El arte que paso el equipo (Graphics/Tixi), pasado a pixel art: hoja 4x4
    // de 64x82 y retrato de 160x160. Mide 76 px de alto y no 58: se igualo por
    // cabeza, como Tacconi (ver Casino_Personajes_Y_Ruletas.md).
    private const string HojaDeTixi = "tixi";
    private const string RetratoDeTixi = "TIXI";
    private const string SpriteDeNestor = "Assets/Sprites/Nestor/NESTOR_frente.png";
    private const string RetratoDeNestor = "Assets/Sprites/Nestor/NESTOR_retrato_feliz.png";
    private const float AltoDeNestor = 2.6f;
    private const string SpriteDeMartin = "Assets/Sprites/Casino/martin_sentado.png";
    private const string RetratoDeMartin = "MARTIN";
    private const string SpriteDeTacconi = "Assets/Sprites/Casino/tacconi_frente.png";
    private const string RetratoDeTacconi = "TACCONI";

    // ------------------------------------------------------------- el menu

    [MenuItem("PokeRush/Quarcho Cueva/Armar el lobby")]
    public static void Armar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Lobby] Sali del Play Mode antes de correr esto.");
            return;
        }

        Scene abierta = SceneManager.GetSceneByPath(Escena);
        if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
        {
            Debug.LogError("[Lobby] La Quarcho Cueva esta abierta con cambios sin guardar. " +
                           "Guardala o descartala y volve a correr esto: no se toco nada.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // El equipo de respaldo se lee de NestorGOD2 antes de abrir la cueva
        // (como ruta: ver LeerEquipoInicialDelModelo).
        var equipo = LeerEquipoInicialDelModelo(out int[] niveles);

        Scene scene = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        GameObject mapaRaiz = ArmarRig(scene, equipo, niveles);
        if (mapaRaiz == null) return;

        GameObject lobby = ArmarLobby(scene, mapaRaiz);

        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, lobby);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Lobby] {p}");
            Debug.LogError("[Lobby] La cueva NO se guardo. Corregi la tabla de posiciones de QuarchoCuevaBuilder.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AgregarABuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Lobby] TERMINADO. La Quarcho Cueva quedo armada y en Build Settings.");
    }

    // El "Continuar" de la pantalla de guardados lleva a la cueva: es el lobby,
    // y SaveService no guarda la escena. La creacion de personaje, a la
    // habitacion tutorial si ya esta en Build Settings; si no, tambien a la cueva.
    [MenuItem("PokeRush/Quarcho Cueva/Conectar el recorrido")]
    public static void ConectarRecorrido()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string trasCrear = RecorridoDeLaHistoria.EscenaDespuesDeCrearPersonaje;
        CambiarDestino<CreacionPersonajeView>(EscenaCreacion, "escenaDestino", trasCrear);
        CambiarDestino<SaveSlotMenuView>(EscenaSaves, "sceneJuego", RecorridoDeLaHistoria.EscenaDelLobby);
        AgregarABuildSettings();
        Debug.Log($"[Lobby] Recorrido conectado: la creacion de personaje va a {trasCrear} y " +
                  $"'Continuar' a {RecorridoDeLaHistoria.EscenaDelLobby}.");
    }

    private static void CambiarDestino<T>(string path, string campo, string destino) where T : Component
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        T vista = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        if (vista == null) { Debug.LogError($"[Lobby] No hay {typeof(T).Name} en {path}."); return; }

        var so = new SerializedObject(vista);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop.stringValue == destino) return;

        Debug.Log($"[Lobby] {scene.name}: {campo} '{prop.stringValue}' -> '{destino}'.");
        prop.stringValue = destino;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------- la mesa

    // Pone en la cueva los seis tiles de la mesa de cartas del ultimo piso del
    // casino, cargados del tileset por nombre (los mismos que dibuja
    // CasinoPisoFinalBuilder).
    //
    // Hasta el 4-oct se copiaban del tilemap del piso final, abriendolo aditivo.
    // Desde que alla la mesa es un dibujo agrandado, en esas celdas hay el tile
    // invisible que frena: copiarlas dejaba la mesa de la cueva sin dibujo.
    private static int CopiarLaMesaDelCasino(Tilemap destino)
    {
        int copiados = 0;
        for (int dy = 0; dy < TamanioDeLaMesa.y; dy++)
        for (int dx = 0; dx < TamanioDeLaMesa.x; dx++)
        {
            TileBase tile = CasinoPisoFinalBuilder.TileDeLaMesa(dy, dx);
            if (tile == null) continue;

            Vector3Int celda = MesaEnLaCueva + new Vector3Int(dx, dy, 0);
            destino.SetTile(celda, tile);
            destino.SetTransformMatrix(celda, Matrix4x4.identity);
            copiados++;
        }

        if (copiados != TamanioDeLaMesa.x * TamanioDeLaMesa.y)
            Debug.LogWarning($"[Lobby] La mesa de cartas dio {copiados} tiles en vez de " +
                             $"{TamanioDeLaMesa.x * TamanioDeLaMesa.y}: revisa CasinoPisoFinalBuilder.TilesDeLaMesa.");
        return copiados;
    }

    // ------------------------------------------------------------- el rig

    // Lo mismo que CasinoSceneRigBuilder hace con los pisos del casino, menos el
    // combate (en la cueva no se pelea).
    private static GameObject ArmarRig(Scene scene, string[] equipo, int[] niveles)
    {
        GameObject mapaRaiz = AsegurarRaiz(scene, "MapaRaiz");
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            if (raiz == mapaRaiz) continue;
            if (raiz.GetComponent<Grid>() != null || raiz.GetComponent<Camera>() != null)
                raiz.transform.SetParent(mapaRaiz.transform, true);
        }

        if (mapaRaiz.GetComponentInChildren<Grid>(true) == null)
        {
            Debug.LogError("[Lobby] La escena no tiene Grid: no es el mapa de la cueva.");
            return null;
        }

        PrepararColisiones(mapaRaiz);
        GameObject player = AsegurarPlayer(scene, mapaRaiz);
        PrepararCamara(mapaRaiz, player);
        AsegurarGameManager(scene, equipo, niveles);
        AsegurarEventSystem(scene);
        AsegurarSistema(scene, player);
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

            // Los muebles de la cueva estan pintados en esta capa: se tiene que ver.
            var renderer = go.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    private static GameObject AsegurarPlayer(Scene scene, GameObject mapaRaiz)
    {
        GameObject player = Buscar(scene, "Player");
        if (player == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapaRaiz.transform);
            player.name = "Player";
        }
        else player.transform.SetParent(mapaRaiz.transform, true);

        player.transform.position = new Vector3(Spawn.x, Spawn.y, 0f);

        // Sin esto X no hace nada: el prefab viene con la mascara vacia.
        var pc = player.GetComponent<PlayerController>();
        var so = new SerializedObject(pc);
        so.FindProperty("interactableMask").intValue |= 1 << CapaObstacles;
        so.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static void PrepararCamara(GameObject mapaRaiz, GameObject player)
    {
        Camera cam = mapaRaiz.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.transform.SetParent(mapaRaiz.transform);
            cam = go.AddComponent<Camera>();
        }

        cam.gameObject.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 7.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        Asegurar<AudioListener>(cam.gameObject);

        var follow = Asegurar<CameraFollow>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
    }

    // El equipo de respaldo para entrar a la cueva con Play directo (partida
    // DEMO). En el juego de verdad manda el GameManager que viene del menu.
    //
    // Se guardan RUTAS y no las referencias: al abrir la cueva en modo Single,
    // Unity descarga los assets que no usa ninguna escena abierta y la
    // referencia leida de NestorGOD2 llega nula (paso: la cueva quedo con el
    // equipo en None y la partida DEMO arrancaba con cero Pokemon).
    private static string[] LeerEquipoInicialDelModelo(out int[] niveles)
    {
        niveles = new int[0];
        Scene modelo = EditorSceneManager.OpenScene(EscenaModelo, OpenSceneMode.Additive);
        try
        {
            var gm = modelo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameManager>(true)).FirstOrDefault();
            if (gm == null) return new string[0];

            var so = new SerializedObject(gm);
            SerializedProperty equipo = so.FindProperty("equipoInicial");
            SerializedProperty nivelesProp = so.FindProperty("nivelesIniciales");
            var especies = new string[equipo.arraySize];
            for (int i = 0; i < especies.Length; i++)
                especies[i] = AssetDatabase.GetAssetPath(equipo.GetArrayElementAtIndex(i).objectReferenceValue);
            niveles = new int[nivelesProp.arraySize];
            for (int i = 0; i < niveles.Length; i++) niveles[i] = nivelesProp.GetArrayElementAtIndex(i).intValue;
            return especies;
        }
        finally
        {
            EditorSceneManager.CloseScene(modelo, true);
        }
    }

    private static void AsegurarGameManager(Scene scene, string[] rutas, int[] niveles)
    {
        GameObject go = AsegurarRaiz(scene, "GameManager");
        var gm = Asegurar<GameManager>(go);
        var so = new SerializedObject(gm);

        // Solo se completa si esta vacio (o quedo con huecos en None), para no
        // pisar lo que el equipo haya elegido a mano para la cueva.
        SerializedProperty equipoProp = so.FindProperty("equipoInicial");
        bool vacio = true;
        for (int i = 0; i < equipoProp.arraySize; i++)
            if (equipoProp.GetArrayElementAtIndex(i).objectReferenceValue != null) vacio = false;

        Object[] equipo = (rutas ?? new string[0])
            .Select(r => (Object)AssetDatabase.LoadAssetAtPath<PokenDefinition>(r))
            .Where(e => e != null).ToArray();
        if (vacio && equipo.Length > 0)
        {
            equipoProp.arraySize = equipo.Length;
            for (int i = 0; i < equipo.Length; i++) equipoProp.GetArrayElementAtIndex(i).objectReferenceValue = equipo[i];
            SerializedProperty nivelesProp = so.FindProperty("nivelesIniciales");
            nivelesProp.arraySize = niveles.Length;
            for (int i = 0; i < niveles.Length; i++) nivelesProp.GetArrayElementAtIndex(i).intValue = niveles[i];
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
        so.FindProperty("sfxSource").objectReferenceValue = fuentes[1];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarEventSystem(Scene scene)
    {
        if (Buscar(scene, "EventSystem") != null) return;
        GameObject go = AsegurarRaiz(scene, "EventSystem");
        Asegurar<EventSystem>(go);
        Asegurar<InputSystemUIInputModule>(go);
    }

    private static void AsegurarSistema(Scene scene, GameObject player)
    {
        GameObject sistema = AsegurarRaiz(scene, "Sistema");
        var boot = Asegurar<SceneBootstrapper>(sistema);
        Asegurar<GuardadoRapido>(sistema);

        var so = new SerializedObject(boot);
        so.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
        so.FindProperty("crearPartidaSiFalta").boolValue = true;
        SerializedProperty nombre = so.FindProperty("nombrePartidaDePrueba");
        if (string.IsNullOrEmpty(nombre.stringValue)) nombre.stringValue = "DEMO";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarDialogo(Scene scene)
    {
        if (Buscar(scene, "DialogoCanvas") != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogoPrefab);
        if (prefab == null) { Debug.LogError($"[Lobby] Falta {DialogoPrefab}."); return; }
        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        canvas.name = "DialogoCanvas";
    }

    // ------------------------------------------------------------- el lobby

    private static GameObject ArmarLobby(Scene scene, GameObject mapaRaiz)
    {
        // Idempotente: fuera lo que habia y se arma de cero. La mesa vive en el
        // Grid (un Tilemap tiene que ser hijo de un Grid), asi que se busca aparte.
        Transform viejo = mapaRaiz.transform.Find(Contenedor);
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        Grid grid = mapaRaiz.GetComponentInChildren<Grid>(true);
        Transform mesaVieja = grid.transform.Find("MesaDeMartin");
        if (mesaVieja != null) Object.DestroyImmediate(mesaVieja.gameObject);

        var lobby = new GameObject(Contenedor);
        lobby.transform.SetParent(mapaRaiz.transform, false);

        Sprite retratoTixi = CasinoNpcBuilder.SpriteRetrato($"{Retratos}{RetratoDeTixi}.png");
        // El de Nestor ya viene bien importado (lo usa el combate): no se toca.
        Sprite retratoNestor = AssetDatabase.LoadAssetAtPath<Sprite>(RetratoDeNestor);
        Sprite retratoMartin = CasinoNpcBuilder.SpriteRetrato($"{Retratos}{RetratoDeMartin}.png");
        Sprite retratoTacconi = CasinoNpcBuilder.SpriteRetrato($"{Retratos}{RetratoDeTacconi}.png");

        PonerTixi(lobby, retratoTixi);
        PonerPc(lobby);
        PonerEnfermera(lobby);
        PonerSalida(lobby);
        GameObject nestor = PonerNestor(lobby, retratoNestor);
        GameObject mesaGo = PonerMesa(grid);
        GameObject martin = PonerMartin(lobby, retratoMartin);
        GameObject tacconi = PonerTacconi(lobby, retratoTacconi);

        // Los que se suman con la historia arrancan APAGADOS (asi no se ven un
        // frame antes de que el flag los prenda) y los prende un BarreraPorFlag
        // al reves desde un objeto que siempre esta activo.
        var apariciones = new GameObject("Apariciones");
        apariciones.transform.SetParent(lobby.transform, false);
        // Nestor llega recien despues del casino: en el guion se queda en su
        // Data Center recuperando la memoria ("Todavía no... Cuando recuerde
        // todo, te voy a buscar"). Ver Historia_En_Los_Niveles.md.
        Aparicion(apariciones, StoryFlag.Casino_Completado, nestor);
        Aparicion(apariciones, StoryFlag.Casino_Completado, martin, mesaGo, tacconi);

        return lobby;
    }

    private static void Aparicion(GameObject donde, StoryFlag flag, params GameObject[] objetos)
    {
        var barrera = donde.AddComponent<BarreraPorFlag>();
        var so = new SerializedObject(barrera);
        so.FindProperty("flagQueLaApaga").enumValueIndex = (int)flag;
        so.FindProperty("alReves").boolValue = true;
        SerializedProperty lista = so.FindProperty("objetos");
        lista.arraySize = objetos.Length;
        for (int i = 0; i < objetos.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = objetos[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (GameObject o in objetos) o.SetActive(false);
    }

    private static void PonerTixi(GameObject lobby, Sprite retrato)
    {
        GameObject tixi = CasinoNpcBuilder.Personaje(lobby, "Tixi", Tixi, HojaDeTixi, CasinoNpcBuilder.Abajo);

        // Camina hasta el jugador cuando habla sola y vuelve a su lugar.
        var camina = tixi.AddComponent<NpcQueCamina>();
        var soCamina = new SerializedObject(camina);
        Sprite[] cuadros = CasinoNpcBuilder.Cuadros(HojaDeTixi);
        SerializedProperty lista = soCamina.FindProperty("cuadros");
        lista.arraySize = cuadros.Length;
        for (int i = 0; i < cuadros.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = cuadros[i];
        soCamina.FindProperty("visual").objectReferenceValue = tixi.transform.Find("Visual").GetComponent<SpriteRenderer>();
        soCamina.FindProperty("obstaculos").intValue = 1 << CapaObstacles;
        soCamina.ApplyModifiedPropertiesWithoutUndo();

        var npc = tixi.AddComponent<QuarchoTixiNpc>();

        var so = new SerializedObject(npc);
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("lore").objectReferenceValue = QuarchoCuevaAssets.LoreDeTixi(retrato);
        so.FindProperty("tienda").objectReferenceValue = QuarchoCuevaAssets.TiendaDeTixi(retrato);
        // La historia: lo que charla y lo que cuenta sola al volver de cada nivel.
        so.FindProperty("charla").objectReferenceValue = HistoriaAssets.Dialogo("Lobby/Dialogos/Lobby_Tixi_Charla");
        so.FindProperty("consejo").stringValue =
            "Lo que sigue es {0}. Cuando quieras, salí por la puerta del este o pedime que te lleve.";
        HistoriaAssets.PonerLista(so.FindProperty("alVolver"),
            "Lobby/Dialogos/Lobby_Tixi_VueltaDelDataCenter",
            "Lobby/Dialogos/Lobby_Tixi_VueltaDelCasino");
        so.FindProperty("antesDeViajar").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDelTransmisor);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerPc(GameObject lobby)
    {
        var pc = new GameObject("PC");
        pc.transform.SetParent(lobby.transform, false);
        pc.transform.position = new Vector3(Pc.x, Pc.y, 0f);
        pc.layer = CapaObstacles;
        pc.AddComponent<BoxCollider2D>().size = TamanioPc;
        pc.AddComponent<PcInteractable>();
    }

    private static void PonerSalida(GameObject lobby)
    {
        // Zona para PISAR: trigger en Default. En Obstacles frena al jugador.
        var salida = new GameObject("Salida");
        salida.transform.SetParent(lobby.transform, false);
        salida.transform.position = new Vector3(Salida.x, Salida.y, 0f);
        salida.layer = CapaDefault;
        var zona = salida.AddComponent<BoxCollider2D>();
        zona.size = new Vector2(0.8f, 0.8f);
        zona.isTrigger = true;
        var puerta = new SerializedObject(salida.AddComponent<SalidaDelLobby>());
        puerta.FindProperty("antesDeSalir").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDelTransmisor);
        puerta.ApplyModifiedPropertiesWithoutUndo();

        var tope = new GameObject("TopeDeLaPuerta");
        tope.transform.SetParent(lobby.transform, false);
        tope.transform.position = new Vector3(Tope.x, Tope.y, 0f);
        tope.layer = CapaObstacles;
        tope.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
    }

    // Nestor se arma a mano y no con Prefabs/NPCs/NPC_Nestor: ese prefab trae el
    // InteractableTrigger de la PELEA de la torre, y dos IInteractable en el
    // mismo objeto no se sabe cual contesta.
    private static GameObject PonerNestor(GameObject lobby, Sprite retrato)
    {
        var go = new GameObject("Nestor");
        go.transform.SetParent(lobby.transform, false);
        go.transform.position = new Vector3(Nestor.x, Nestor.y, 0f);
        go.layer = CapaObstacles;
        go.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);

        // Como el prefab: el sprite tiene el pivote en los pies y mide 1; se
        // agranda a 2,6 y se apoya un cuarto de celda abajo del centro.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        visual.transform.localScale = Vector3.one * AltoDeNestor;
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDeNestor);
        sr.sortingOrder = OrdenPersonajes;

        var npc = go.AddComponent<QuarchoNestorNpc>();
        var so = new SerializedObject(npc);
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("charla").objectReferenceValue = QuarchoCuevaAssets.CharlaDeNestor(retrato);
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // La mesa como Tilemap propio (hijo del Grid), con los MISMOS tiles del
    // piso final. En modo Individual y en el orden de los personajes: asi cada
    // tile se ordena por su altura con el eje (0,1,0) del Renderer2D, y queda
    // delante de Martin (que esta atras) y detras de quien juega (que esta abajo).
    private static GameObject PonerMesa(Grid grid)
    {
        var go = new GameObject("MesaDeMartin");
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortingOrder = OrdenPersonajes;

        CopiarLaMesaDelCasino(tilemap);
        return go;
    }

    private static GameObject PonerMartin(GameObject lobby, Sprite retrato)
    {
        // El objeto ES la mesa: su collider tapa las seis celdas (frena y es lo
        // que se aprieta con X desde cualquier lado). Martin, sentado, es un hijo.
        var centroDeLaMesa = new Vector2(MesaEnLaCueva.x + TamanioDeLaMesa.x * 0.5f,
                                         MesaEnLaCueva.y + TamanioDeLaMesa.y * 0.5f);
        var go = new GameObject("Martin");
        go.transform.SetParent(lobby.transform, false);
        go.transform.position = new Vector3(centroDeLaMesa.x, centroDeLaMesa.y, 0f);
        go.layer = CapaObstacles;
        go.AddComponent<BoxCollider2D>().size = new Vector2(TamanioDeLaMesa.x - 0.1f, TamanioDeLaMesa.y - 0.1f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.position = new Vector3(ApoyoDeMartin.x, ApoyoDeMartin.y, 0f);
        visual.transform.localScale = Vector3.one * CasinoNpcBuilder.EscalaDelVisual;
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CasinoNpcBuilder.SpriteDePersonaje(SpriteDeMartin);
        sr.sortingOrder = OrdenPersonajes;
        // Ordena por el pie del busto (donde apoya en la mesa), no por el centro.
        sr.spriteSortPoint = SpriteSortPoint.Pivot;

        // La silla: la fila de atras de la mesa, donde esta sentado.
        var silla = new GameObject("Silla");
        silla.transform.SetParent(go.transform, false);
        silla.transform.position = new Vector3(SillaDeMartin.x, SillaDeMartin.y, 0f);
        silla.layer = CapaObstacles;
        silla.AddComponent<BoxCollider2D>().size = new Vector2(TamanioDeLaMesa.x - 0.1f, 0.9f);

        // Los juegos del casino: los MISMOS componentes que en los pisos, con el
        // collider apagado (nadie los pisa ni los aprieta: los abre Martin).
        var juegos = new GameObject("Juegos");
        juegos.transform.SetParent(go.transform, false);
        var tragaperras = Juego<SlotMachineInteractable>(juegos, "Tragaperras");
        var ruleta = Juego<RouletteTableInteractable>(juegos, "Ruleta");
        var blackjack = Juego<BlackjackTableInteractable>(juegos, "Blackjack");
        ConfigurarJuegos(tragaperras, ruleta, blackjack);

        var npc = go.AddComponent<QuarchoMartinNpc>();
        var so = new SerializedObject(npc);
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("charla").objectReferenceValue = QuarchoCuevaAssets.CharlaDeMartin(retrato);

        SerializedProperty ruletas = so.FindProperty("ruletas");
        var datos = new (string nombre, CasinoPrizeTable tabla, int precio)[]
        {
            ("Pokémon", QuarchoCuevaAssets.RuletaDePokemon(), QuarchoCuevaAssets.PrecioRuletaPokemon),
            ("objetos", QuarchoCuevaAssets.RuletaDeObjetos(), QuarchoCuevaAssets.PrecioRuletaObjetos),
            ("mejoras", QuarchoCuevaAssets.RuletaDeMejoras(), QuarchoCuevaAssets.PrecioRuletaMejoras)
        };
        ruletas.arraySize = datos.Length;
        for (int i = 0; i < datos.Length; i++)
        {
            SerializedProperty r = ruletas.GetArrayElementAtIndex(i);
            r.FindPropertyRelative("nombre").stringValue = datos[i].nombre;
            r.FindPropertyRelative("tabla").objectReferenceValue = datos[i].tabla;
            r.FindPropertyRelative("precio").intValue = datos[i].precio;
        }

        SerializedProperty lista = so.FindProperty("juegos");
        var conNombre = new (string nombre, MonoBehaviour juego)[]
        {
            ("Tragaperras", tragaperras), ("Ruleta", ruleta), ("Blackjack", blackjack)
        };
        lista.arraySize = conNombre.Length;
        for (int i = 0; i < conNombre.Length; i++)
        {
            SerializedProperty j = lista.GetArrayElementAtIndex(i);
            j.FindPropertyRelative("nombre").stringValue = conNombre[i].nombre;
            j.FindPropertyRelative("interactuable").objectReferenceValue = conNombre[i].juego;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    private static T Juego<T>(GameObject padre, string nombre) where T : MonoBehaviour
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        // El collider lo pide el componente; apagado no frena ni se aprieta.
        var col = go.AddComponent<BoxCollider2D>();
        col.enabled = false;
        return go.AddComponent<T>();
    }

    // Plain: sin los flags de objetivo de los pisos (aca no destraban nada).
    private static void ConfigurarJuegos(SlotMachineInteractable tragaperras, RouletteTableInteractable ruleta,
                                         BlackjackTableInteractable blackjack)
    {
        var maquina = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>($"{Casino}MaquinaBasica.asset");
        var soT = new SerializedObject(tragaperras);
        soT.FindProperty("maquina").objectReferenceValue = maquina;
        soT.FindProperty("titulo").stringValue = "La tragaperras de Martín";
        soT.ApplyModifiedPropertiesWithoutUndo();
        if (maquina == null) Debug.LogError("[Lobby] No existe Casino/MaquinaBasica.asset: la tragaperras de Martin no anda.");

        var soR = new SerializedObject(ruleta);
        soR.FindProperty("titulo").stringValue = "La ruleta de Martín";
        soR.FindProperty("ruletaPorColor").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>($"{Casino}RuletaDeLaMesa.asset");
        soR.ApplyModifiedPropertiesWithoutUndo();

        var soB = new SerializedObject(blackjack);
        soB.FindProperty("titulo").stringValue = "La mesa de Martín";
        soB.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject PonerTacconi(GameObject lobby, Sprite retrato)
    {
        GameObject go = CasinoNpcBuilder.PersonajeSuelto(lobby, "Tacconi", Tacconi, SpriteDeTacconi);
        var npc = go.AddComponent<CasinoTacconiNpc>();

        var mew = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{EspecieMew}.asset");
        if (mew == null) Debug.LogError($"[Lobby] No existe la especie {EspecieMew}.");

        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = "TACCONI";
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("flagGanado").enumValueIndex = (int)StoryFlag.Lobby_TacconiMewGanado;
        so.FindProperty("especiePremio").objectReferenceValue = mew;
        so.FindProperty("nivelPremio").intValue = NivelMew;
        so.FindProperty("premioShiny").boolValue = false;
        so.FindProperty("pregunta").stringValue =
            "Tres chances, una sola vez: apostás un Pokémon a una mano de blackjack. Si me ganás, mi {0} es tuyo. ¿Jugamos?";
        so.FindProperty("mensajePerdiste").stringValue = "Tacconi siempre gana. Tu {0} se queda conmigo.";
        so.FindProperty("mensajeEmpate").stringValue = "Empate. Esa no cuenta: la chance sigue en pie. ¿Otra?";
        so.FindProperty("mensajeYaGanado").stringValue = "Ya te llevaste mi {0}. Un trato es un trato.";
        so.FindProperty("presentacion").objectReferenceValue = HistoriaAssets.Dialogo("Lobby/Dialogos/Lobby_Tacconi_Presentacion");

        SerializedProperty chances = so.FindProperty("chances");
        StoryFlag[] flags = { StoryFlag.Lobby_TacconiChance1, StoryFlag.Lobby_TacconiChance2, StoryFlag.Lobby_TacconiChance3 };
        chances.arraySize = flags.Length;
        for (int i = 0; i < flags.Length; i++) chances.GetArrayElementAtIndex(i).enumValueIndex = (int)flags[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // ------------------------------------------------------------- verificar

    // Devuelve la lista de problemas (vacia = todo bien). Mira el mapa en
    // celdas: piso del Suelo, paredes de Colisiones y los colliders solidos del
    // lobby (con TODOS los personajes prendidos: si se llega con todos a la
    // vista, se llega con menos).
    // La enfermera, con sus cuatro dialogos (saludo, curado, sano, no gracias).
    public static GameObject PonerEnfermera(GameObject lobby)
    {
        GameObject go = CasinoNpcBuilder.Personaje(lobby, "Enfermera", Enfermera, HojaDeLaEnfermera, CasinoNpcBuilder.Abajo);
        var so = new SerializedObject(go.AddComponent<NpcQueCura>());
        so.FindProperty("saludo").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDeLaEnfermera);
        so.FindProperty("alCurar").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDeLaEnfermera + "_Curado");
        so.FindProperty("yaEstaSano").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDeLaEnfermera + "_Sano");
        so.FindProperty("alRechazar").objectReferenceValue = HistoriaAssets.Dialogo(DialogoDeLaEnfermera + "_NoGracias");
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // Lo del playtest del 4-oct sobre la cueva YA armada, sin rehacer el lobby
    // entero: la enfermera y el transmisor al salir. Idempotente.
    [MenuItem("PokeRush/Quarcho Cueva/Poner la enfermera y el transmisor al salir")]
    public static void PonerEnfermeraYTransmisor()
    {
        if (EditorApplication.isPlaying) return;
        Scene abierta = SceneManager.GetSceneByPath(Escena);
        if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
        {
            Debug.LogError("[Lobby] La Quarcho Cueva esta abierta con cambios sin guardar: no se toco nada.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        Transform lobby = mapaRaiz != null ? mapaRaiz.transform.Find(Contenedor) : null;
        if (lobby == null)
        {
            Debug.LogError("[Lobby] La cueva no tiene MapaRaiz/Lobby: corre 'Armar el lobby'.");
            return;
        }

        Transform vieja = lobby.Find("Enfermera");
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
        PonerEnfermera(lobby.gameObject);

        DialogueDefinition transmisor = HistoriaAssets.Dialogo(DialogoDelTransmisor);
        foreach (SalidaDelLobby salida in lobby.GetComponentsInChildren<SalidaDelLobby>(true))
        {
            var so = new SerializedObject(salida);
            so.FindProperty("antesDeSalir").objectReferenceValue = transmisor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (QuarchoTixiNpc tixi in lobby.GetComponentsInChildren<QuarchoTixiNpc>(true))
        {
            var so = new SerializedObject(tixi);
            so.FindProperty("antesDeViajar").objectReferenceValue = transmisor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        ReponerColisiones(scene);
        Physics2D.SyncTransforms();
        List<string> problemas = Verificar(scene, lobby.gameObject);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Lobby] {p}");
            Debug.LogError("[Lobby] La cueva NO se guardo.");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lobby] Enfermera y transmisor al salir: listo y guardado.");
    }

    // ---------------------------------------------------------------- el chip

    private const string CombatePrefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string PerfilesDeIa = "Assets/Resources/Definitions/AIProfiles/";
    // Los entrenadores virtuales: frentes del pack que ya usa el casino (estan
    // en git), sin los de la historia (Julieta, Martin, Tacconi, Tixi...).
    private static readonly string[] FrentesDelChip =
    {
        "CABALLERO", "CHICA", "CIENTIFICA", "CIENTIFICO", "GUAY", "JOVENRICA", "JUGON", "KARATEKA",
        "MALABARISTA", "MATON", "MAXIMO", "MEDIUM", "MODELO", "MOTORISTA", "POKEMANIACO",
        "VETERANA", "VETERANO"
    };

    // El Entrenamiento intenso del chip (playtest del 5-oct) sobre la cueva YA
    // armada: el sistema de combate (la cueva no tenia peleas), el puente
    // mapa-combate, el entrenamiento y la opcion en el menu de Tixi. Idempotente.
    [MenuItem("PokeRush/Quarcho Cueva/Poner el entrenamiento intenso del chip")]
    public static void PonerEntrenamientoDelChip()
    {
        if (EditorApplication.isPlaying) return;
        Scene abierta = SceneManager.GetSceneByPath(Escena);
        if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
        {
            Debug.LogError("[Lobby] La Quarcho Cueva esta abierta con cambios sin guardar: no se toco nada.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        GameObject[] raices = scene.GetRootGameObjects();
        GameObject mapaRaiz = raices.FirstOrDefault(r => r.name == "MapaRaiz");
        GameObject sistema = raices.FirstOrDefault(r => r.name == "Sistema");
        Transform lobby = mapaRaiz != null ? mapaRaiz.transform.Find(Contenedor) : null;
        if (lobby == null || sistema == null)
        {
            Debug.LogError("[Lobby] La cueva no tiene MapaRaiz/Lobby o Sistema: corre 'Armar el lobby'.");
            return;
        }

        // El combate: el prefab de siempre, apagado hasta que arranca una pelea.
        GameObject combate = raices.FirstOrDefault(r => r.name == "SistemaDeCombate");
        if (combate == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatePrefab);
            if (prefab == null) { Debug.LogError($"[Lobby] Falta {CombatePrefab}."); return; }
            combate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            combate.name = "SistemaDeCombate";
        }
        combate.SetActive(false);
        var battleController = combate.GetComponent<BattleController>();

        // El puente: esconde el mapa durante la pelea y lo devuelve al terminar.
        var bridge = sistema.GetComponent<MapBattleBridge>();
        if (bridge == null) bridge = sistema.AddComponent<MapBattleBridge>();
        var soBridge = new SerializedObject(bridge);
        soBridge.FindProperty("mapaRaiz").objectReferenceValue = mapaRaiz;
        soBridge.FindProperty("combateRaiz").objectReferenceValue = combate;
        soBridge.FindProperty("battleController").objectReferenceValue = battleController;
        soBridge.FindProperty("playerController").objectReferenceValue = mapaRaiz.GetComponentInChildren<PlayerController>(true);
        soBridge.ApplyModifiedPropertiesWithoutUndo();

        // El entrenamiento, FUERA de MapaRaiz (ver EntrenamientoDelChip).
        GameObject chipGo = raices.FirstOrDefault(r => r.name == "EntrenamientoDelChip");
        if (chipGo == null)
        {
            chipGo = new GameObject("EntrenamientoDelChip");
            SceneManager.MoveGameObjectToScene(chipGo, scene);
        }
        var chip = chipGo.GetComponent<EntrenamientoDelChip>();
        if (chip == null) chip = chipGo.AddComponent<EntrenamientoDelChip>();

        var soChip = new SerializedObject(chip);
        soChip.FindProperty("battleController").objectReferenceValue = battleController;
        soChip.FindProperty("dialogueController").objectReferenceValue =
            raices.SelectMany(r => r.GetComponentsInChildren<DialogueController>(true)).FirstOrDefault();
        soChip.FindProperty("retrato").objectReferenceValue = CasinoNpcBuilder.SpriteRetrato($"{Retratos}{RetratoDeTixi}.png");
        var frentes = FrentesDelChip
            .Select(f => CasinoNpcBuilder.SpriteRetrato($"{Retratos}{f}.png"))
            .Where(f => f != null).ToArray();
        SerializedProperty lista = soChip.FindProperty("entrenadores");
        lista.arraySize = frentes.Length;
        for (int i = 0; i < frentes.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = frentes[i];
        soChip.FindProperty("iaDeLosPrimeros").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(PerfilesDeIa + "Trainer.asset");
        soChip.FindProperty("iaDelMedio").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(PerfilesDeIa + "GymLeader.asset");
        soChip.FindProperty("iaDeLosUltimos").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(PerfilesDeIa + "Elite.asset");
        soChip.ApplyModifiedPropertiesWithoutUndo();

        foreach (QuarchoTixiNpc tixi in lobby.GetComponentsInChildren<QuarchoTixiNpc>(true))
        {
            var so = new SerializedObject(tixi);
            so.FindProperty("entrenamiento").objectReferenceValue = chip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        ReponerColisiones(scene);
        Physics2D.SyncTransforms();
        List<string> problemas = Verificar(scene, lobby.gameObject);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Lobby] {p}");
            Debug.LogError("[Lobby] La cueva NO se guardo.");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Lobby] Entrenamiento intenso del chip: combate, puente y Tixi listos ({frentes.Length} entrenadores). Guardado.");
    }

    private static List<string> Verificar(Scene scene, GameObject lobby)
    {
        var problemas = new List<string>();
        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap suelo = tilemaps.FirstOrDefault(t => t.name == "Suelo");
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        if (suelo == null || colisiones == null) { problemas.Add("faltan los tilemaps Suelo o Colisiones."); return problemas; }

        // Prender todo lo que arranca apagado mientras se mide.
        var apagados = lobby.GetComponentsInChildren<Transform>(true)
            .Select(t => t.gameObject).Where(g => !g.activeSelf).ToList();
        foreach (GameObject g in apagados) g.SetActive(true);
        Physics2D.SyncTransforms();

        try
        {
            var solidos = lobby.GetComponentsInChildren<Collider2D>(true)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == CapaObstacles).ToList();

            bool Bloqueada(Vector3Int c)
            {
                if (colisiones.HasTile(c)) return true;
                Vector3 centro = suelo.GetCellCenterWorld(c);
                return solidos.Any(s => s.bounds.Contains(new Vector3(centro.x, centro.y, s.bounds.center.z)));
            }

            Vector3Int spawn = suelo.WorldToCell(Spawn);
            if (!suelo.HasTile(spawn) || Bloqueada(spawn)) problemas.Add($"el spawn {spawn} no es piso libre.");

            // Flood fill desde el spawn, acotado al mapa (hay un bloque fantasma
            // de tiles sin sprite muy lejos: ver Como_Armar_Un_Mapa_Nuevo).
            var alcanzables = new HashSet<Vector3Int> { spawn };
            var cola = new Queue<Vector3Int>();
            cola.Enqueue(spawn);
            var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
            while (cola.Count > 0)
            {
                Vector3Int c = cola.Dequeue();
                foreach (Vector3Int d in dirs)
                {
                    Vector3Int n = c + d;
                    if (alcanzables.Contains(n) || Mathf.Abs(n.x - spawn.x) > 40 || Mathf.Abs(n.y - spawn.y) > 40) continue;
                    if (!suelo.HasTile(n) || Bloqueada(n)) continue;
                    alcanzables.Add(n);
                    cola.Enqueue(n);
                }
            }

            // Cada interactuable: alguna celda pegada a su collider se alcanza.
            foreach (var (nombre, pos) in new (string, Vector2)[]
                     { ("Tixi", Tixi), ("PC", Pc), ("Nestor", Nestor), ("Tacconi", Tacconi) })
            {
                Collider2D col = lobby.GetComponentsInChildren<Collider2D>(true)
                    .FirstOrDefault(c => c.name == nombre || c.transform.name == nombre);
                if (!AlcanzableAlLado(col, suelo, alcanzables)) problemas.Add($"no se llega a hablarle a {nombre}.");
            }

            Collider2D mesa = lobby.transform.Find("Martin")?.GetComponent<Collider2D>();
            if (!AlcanzableAlLado(mesa, suelo, alcanzables)) problemas.Add("no se llega a la mesa de Martin.");

            Vector3Int puerta = suelo.WorldToCell(Salida);
            if (!alcanzables.Contains(puerta)) problemas.Add($"no se llega caminando a la salida {puerta}.");

            // El paso entre las dos salas (la unica celda que las une).
            if (!alcanzables.Contains(suelo.WorldToCell(new Vector2(-7.5f, 0.5f))))
                problemas.Add("la sala de servidores quedo cortada del resto.");

            // Nadie parado sobre una pared.
            foreach (var (nombre, pos) in new (string, Vector2)[]
                     { ("Tixi", Tixi), ("Nestor", Nestor), ("Tacconi", Tacconi), ("Spawn", Spawn), ("Salida", Salida) })
            {
                Vector3Int c = suelo.WorldToCell(pos);
                if (!suelo.HasTile(c) || colisiones.HasTile(c)) problemas.Add($"{nombre} quedo sobre {c}, que no es piso libre.");
            }

            Debug.Log($"[Lobby] Verificacion: {alcanzables.Count} celdas alcanzables desde el spawn {spawn}.");
        }
        finally
        {
            foreach (GameObject g in apagados) g.SetActive(false);
        }
        return problemas;
    }

    private static bool AlcanzableAlLado(Collider2D col, Tilemap suelo, HashSet<Vector3Int> alcanzables)
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        Vector3Int min = suelo.WorldToCell(b.min + new Vector3(0.05f, 0.05f, 0f));
        Vector3Int max = suelo.WorldToCell(b.max - new Vector3(0.05f, 0.05f, 0f));
        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
        {
            var c = new Vector3Int(x, y, 0);
            if (alcanzables.Contains(c + Vector3Int.up) || alcanzables.Contains(c + Vector3Int.down) ||
                alcanzables.Contains(c + Vector3Int.left) || alcanzables.Contains(c + Vector3Int.right))
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------- utilidades

    // Abrir una escena en el editor pierde los colliderType = Grid por celda;
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

    private static void AgregarABuildSettings()
    {
        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (lista.Exists(s => s.path == Escena)) return;
        lista.Add(new EditorBuildSettingsScene(Escena, true));
        EditorBuildSettings.scenes = lista.ToArray();
        Debug.Log("[Lobby] Quarcho-Cueva agregada a Build Settings.");
    }

    private static GameObject AsegurarRaiz(Scene scene, string nombre)
    {
        GameObject existente = scene.GetRootGameObjects().FirstOrDefault(r => r.name == nombre);
        if (existente != null) return existente;
        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static GameObject Buscar(Scene scene, string nombre)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        return null;
    }

    private static T Asegurar<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
