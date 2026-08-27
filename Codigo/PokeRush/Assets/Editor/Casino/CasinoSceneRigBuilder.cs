using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Deja jugables las escenas del casino que hizo el equipo de nivel.
//
// Las escenas vienen con el arte y nada mas: Grid con Suelo/Decorado/Colisiones/
// Techos y una camara. Les falta todo lo que el juego necesita para funcionar
// (jugador, GameManager, bootstrap, combate, dialogo, puente mapa-combate).
// Esto se lo agrega SIN tocar los tilemaps: no se pinta, no se mueve y no se
// borra nada de lo que hicieron.
//
// Es idempotente: correrlo de nuevo no duplica nada, actualiza lo que ya esta.
// El molde es NestorGOD2, la escena de referencia del proyecto.
public static class CasinoSceneRigBuilder
{
    private static readonly string[] Escenas =
    {
        "Assets/Scenes/Casino Nivel 1.unity",
        "Assets/Scenes/Casino Nivel 2.unity",
        "Assets/Scenes/Casino nivel 3.unity",
        "Assets/Scenes/Casino piso FINAL.unity"
    };

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string CombatePrefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string DialogoPrefab = "Assets/Prefabs/UI/DialogoCanvas.prefab";
    private const string EscenaModelo = "Assets/Scenes/NestorGOD2.unity";

    private const int CapaObstacles = 3;

    [MenuItem("PokeRush/Casino/Armar el rig de las escenas del casino")]
    public static void ArmarTodas()
    {
        if (!GuardarSiHaceFalta()) return;
        if (!AsegurarPrefabDeDialogo()) return;

        foreach (string escena in Escenas)
        {
            if (!System.IO.File.Exists(escena))
            {
                Debug.LogWarning($"[CasinoRig] No existe {escena}, se saltea.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);
            Armar(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CasinoRig] Listo: {scene.name}");
        }

        AgregarABuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[CasinoRig] TERMINADO. Las cuatro escenas del casino quedaron jugables.");
    }

    private static bool GuardarSiHaceFalta()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[CasinoRig] Sali del Play Mode antes de correr esto.");
            return false;
        }
        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    // ---------------------------------------------------------------- dialogo

    // El canvas de dialogo no tiene prefab en el proyecto: la documentacion dice
    // "copialo de NestorGOD2". Lo convertimos en prefab una sola vez, asi las
    // cuatro escenas (y los mapas que vengan) lo comparten de verdad.
    private static bool AsegurarPrefabDeDialogo()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DialogoPrefab) != null) return true;

        Scene modelo = EditorSceneManager.OpenScene(EscenaModelo, OpenSceneMode.Single);
        GameObject canvas = BuscarRaiz(modelo, "DialogoCanvas");
        if (canvas == null)
        {
            Debug.LogError($"[CasinoRig] No encontre 'DialogoCanvas' en {EscenaModelo}.");
            return false;
        }

        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        PrefabUtility.SaveAsPrefabAsset(canvas, DialogoPrefab);
        Debug.Log($"[CasinoRig] Canvas de dialogo guardado como prefab en {DialogoPrefab}.");
        return true;
    }

    // ------------------------------------------------------------------ rig

    private static void Armar(Scene scene)
    {
        GameObject mapaRaiz = AsegurarRaiz(scene, "MapaRaiz");

        // El arte del equipo (Grid) y la camara pasan a vivir adentro de
        // MapaRaiz: es lo que el puente apaga durante el combate. Se reparenta,
        // no se toca su contenido.
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            if (raiz == mapaRaiz) continue;
            if (raiz.GetComponent<Grid>() != null || raiz.GetComponent<Camera>() != null)
                raiz.transform.SetParent(mapaRaiz.transform, true);
        }

        PrepararColisiones(mapaRaiz);

        GameObject player = AsegurarPlayer(scene, mapaRaiz);
        PrepararCamara(mapaRaiz, player);

        AsegurarComponente<GameManager>(AsegurarRaiz(scene, "GameManager"));
        AsegurarEventSystem(scene);

        GameObject combate = AsegurarCombate(scene);
        AsegurarSistema(scene, mapaRaiz, combate, player);
        AsegurarDialogo(scene);
    }

    // ---------- colisiones ----------

    // Pintar en la capa 'Colisiones' no alcanza: los tiles del pack traen la
    // physics shape vacia, asi que el collider sale vacio salvo que se les fije
    // colliderType = Grid celda por celda. Es el mismo arreglo que documenta
    // Como_Armar_Un_Mapa_Nuevo.md, aplicado aca para no depender de que alguien
    // se acuerde de correrlo a mano.
    private static void PrepararColisiones(GameObject mapaRaiz)
    {
        foreach (Tilemap tilemap in mapaRaiz.GetComponentsInChildren<Tilemap>(true))
        {
            if (tilemap.name != "Colisiones") continue;

            GameObject go = tilemap.gameObject;
            go.layer = CapaObstacles;

            int arreglados = 0;
            foreach (Vector3Int p in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(p)) continue;
                if (tilemap.GetColliderType(p) == Tile.ColliderType.Grid) continue;
                tilemap.SetColliderType(p, Tile.ColliderType.Grid);
                arreglados++;
            }

            var col = AsegurarComponente<TilemapCollider2D>(go);
            var rb = AsegurarComponente<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Static;
            var comp = AsegurarComponente<CompositeCollider2D>(go);
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // OJO: la capa 'Colisiones' de estas escenas NO son paredes
            // invisibles. El equipo pinto ahi los muebles del casino (las
            // tragaperras, las mesas), asi que su TilemapRenderer tiene que
            // quedar PRENDIDO. Apagarlo borra medio casino de la pantalla.
            var renderer = go.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = true;

            Debug.Log($"[CasinoRig] Colisiones: {arreglados} celdas pasadas a ColliderType.Grid, " +
                      $"shapeCount={comp.shapeCount}.");
        }
    }

    // ---------- jugador ----------

    private static GameObject AsegurarPlayer(Scene scene, GameObject mapaRaiz)
    {
        GameObject player = Buscar(scene, "Player");
        if (player == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab == null)
            {
                Debug.LogError($"[CasinoRig] Falta {PlayerPrefab}.");
                return null;
            }
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapaRaiz.transform);
            player.name = "Player";
        }
        else
        {
            player.transform.SetParent(mapaRaiz.transform, true);
        }

        Vector3 spawn = BuscarSpawn(mapaRaiz);
        player.transform.position = spawn;

        // Sin esto, apretar X no hace nada: PlayerController busca IInteractable
        // por capa y el prefab viene con la mascara vacia.
        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            SerializedProperty mask = so.FindProperty("interactableMask");
            if (mask != null) mask.intValue |= 1 << CapaObstacles;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"[CasinoRig] Player en {spawn}.");
        return player;
    }

    // Busca una celda pisable para el spawn: con suelo, sin colision y sin
    // decorado encima. Se elige la mas al sur (la entrada del piso) y, entre
    // esas, la mas centrada. Se calcula en vez de hardcodearse para que siga
    // funcionando si el equipo mueve el mapa.
    private static Vector3 BuscarSpawn(GameObject mapaRaiz)
    {
        Tilemap suelo = null, colisiones = null, decorado = null;
        foreach (Tilemap t in mapaRaiz.GetComponentsInChildren<Tilemap>(true))
        {
            if (t.name == "Suelo") suelo = t;
            else if (t.name == "Colisiones") colisiones = t;
            else if (t.name == "Decorado") decorado = t;
        }

        if (suelo == null) return Vector3.zero;

        var libres = new List<Vector3Int>();
        foreach (Vector3Int p in suelo.cellBounds.allPositionsWithin)
        {
            if (!suelo.HasTile(p)) continue;
            if (colisiones != null && colisiones.HasTile(p)) continue;
            if (decorado != null && decorado.HasTile(p)) continue;
            libres.Add(p);
        }

        if (libres.Count == 0) return Vector3.zero;

        int minY = int.MaxValue;
        foreach (Vector3Int p in libres) if (p.y < minY) minY = p.y;

        // Entre las celdas de la fila mas al sur, la del medio.
        var fila = new List<Vector3Int>();
        foreach (Vector3Int p in libres) if (p.y <= minY + 1) fila.Add(p);
        fila.Sort((a, b) => a.x.CompareTo(b.x));
        Vector3Int elegida = fila[fila.Count / 2];

        return suelo.GetCellCenterWorld(elegida);
    }

    // ---------- camara ----------

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
        AsegurarComponente<AudioListener>(cam.gameObject);

        var follow = AsegurarComponente<CameraFollow>(cam.gameObject);
        if (player != null)
        {
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (player != null)
            cam.transform.position = new Vector3(player.transform.position.x,
                                                 player.transform.position.y, -10f);
    }

    // ---------- combate, sistema y dialogo ----------

    private static GameObject AsegurarCombate(Scene scene)
    {
        GameObject combate = Buscar(scene, "SistemaDeCombate");
        if (combate == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatePrefab);
            if (prefab == null)
            {
                Debug.LogError($"[CasinoRig] Falta {CombatePrefab}.");
                return null;
            }
            combate = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            combate.name = "SistemaDeCombate";
        }

        // Va DESACTIVADO: se prende solo cuando arranca una pelea.
        combate.SetActive(false);
        return combate;
    }

    private static void AsegurarSistema(Scene scene, GameObject mapaRaiz,
                                        GameObject combate, GameObject player)
    {
        GameObject sistema = AsegurarRaiz(scene, "Sistema");

        var bridge = AsegurarComponente<MapBattleBridge>(sistema);
        var boot = AsegurarComponente<SceneBootstrapper>(sistema);
        AsegurarComponente<GuardadoRapido>(sistema);

        var battleController = combate != null ? combate.GetComponent<BattleController>() : null;
        var playerController = player != null ? player.GetComponent<PlayerController>() : null;

        var soBridge = new SerializedObject(bridge);
        soBridge.FindProperty("mapaRaiz").objectReferenceValue = mapaRaiz;
        soBridge.FindProperty("combateRaiz").objectReferenceValue = combate;
        soBridge.FindProperty("battleController").objectReferenceValue = battleController;
        soBridge.FindProperty("playerController").objectReferenceValue = playerController;
        soBridge.ApplyModifiedPropertiesWithoutUndo();

        var soBoot = new SerializedObject(boot);
        soBoot.FindProperty("playerController").objectReferenceValue = playerController;
        soBoot.FindProperty("crearPartidaSiFalta").boolValue = true;
        SerializedProperty nombre = soBoot.FindProperty("nombrePartidaDePrueba");
        if (nombre != null && string.IsNullOrEmpty(nombre.stringValue)) nombre.stringValue = "DEMO";
        soBoot.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarDialogo(Scene scene)
    {
        if (Buscar(scene, "DialogoCanvas") != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogoPrefab);
        if (prefab == null) { Debug.LogError($"[CasinoRig] Falta {DialogoPrefab}."); return; }

        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        canvas.name = "DialogoCanvas";
    }

    private static void AsegurarEventSystem(Scene scene)
    {
        if (Buscar(scene, "EventSystem") != null) return;

        GameObject go = AsegurarRaiz(scene, "EventSystem");
        AsegurarComponente<EventSystem>(go);
        AsegurarComponente<InputSystemUIInputModule>(go);
    }

    // ---------- build settings ----------

    private static void AgregarABuildSettings()
    {
        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool cambio = false;

        foreach (string escena in Escenas)
        {
            if (!System.IO.File.Exists(escena)) continue;
            if (lista.Exists(s => s.path == escena)) continue;

            lista.Add(new EditorBuildSettingsScene(escena, true));
            cambio = true;
        }

        if (!cambio) return;
        EditorBuildSettings.scenes = lista.ToArray();
        Debug.Log("[CasinoRig] Escenas del casino agregadas a Build Settings.");
    }

    // ---------- utilidades ----------

    private static GameObject AsegurarRaiz(Scene scene, string nombre)
    {
        GameObject existente = BuscarRaiz(scene, nombre);
        if (existente != null) return existente;

        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static GameObject BuscarRaiz(Scene scene, string nombre)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
            if (raiz.name == nombre) return raiz;
        return null;
    }

    private static GameObject Buscar(Scene scene, string nombre)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        }
        return null;
    }

    private static T AsegurarComponente<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
