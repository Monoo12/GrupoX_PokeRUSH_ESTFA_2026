using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Arma una escena JUGABLE para probar el combate en Play mode:
//   PokeRush > Armar demo de triggers de combate
//
// Sale Assets/Scenes/DemoCombate.unity, y con apretar Play ya se puede caminar
// y meterse en las zonas. Es el flujo real completo, sin BattleTester:
//
//   caminar -> BattleZoneTrigger -> BattleController -> combate
//           -> BattleEnded -> MapBattleBridge devuelve el mapa
//
// QUE SE PUEDE PROBAR ACA (y no en CombatePrueba)
//   - que el mapa vuelva entero despues de pelear
//   - que el trigger no se re-dispare al volver (el bug clasico del bucle)
//   - que el prefab se vea bien con una camara de mapa distinta a la del combate
//
// ZONAS
//   verde  -> entrenador (2 Pokens): para el cambio de Poken al caer el activo
//   azul   -> salvaje: combate corto
//   rojo   -> boss de prueba (3 Pokens): fase de liberacion y fin de combate
namespace PokeRush.EditorTools
{
    public static class BattleZoneDemoBuilder
    {
        private const string ScenePath = "Assets/Scenes/DemoCombate.unity";
        private const string PrefabPath = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
        private const string TestingFolder = "Assets/Resources/Definitions/Testing";

        [MenuItem("PokeRush/Armar demo de triggers de combate")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // El prefab es la pieza central: si no esta, se genera al toque.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.Log("[Demo] No existe el prefab de combate: lo genero primero.");
                BattlePrefabBuilder.Build();
            }

            var trainer = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                $"{TestingFolder}/EntrenadorDePrueba.asset");
            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(
                $"{TestingFolder}/BossDePrueba.asset");
            var wild = AssetDatabase.LoadAssetAtPath<WildEncounterDefinition>(
                $"{TestingFolder}/SalvajeDePrueba.asset");

            if (trainer == null || boss == null || wild == null)
            {
                EditorUtility.DisplayDialog("Faltan los assets de prueba",
                    "No estan los definitions de Testing.\n" +
                    "Corre primero 'PokeRush > Armar combate de prueba'.", "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- infraestructura suelta ---
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            // Mismo equipo de arranque que las escenas del juego, por referencia
            var equipo = InitialTeamMigrator.EquipoPorDefecto();
            if (equipo != null) InitialTeamMigrator.Asignar(gm, equipo);

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            // --- el mapa (todo lo que se esconde durante la pelea) ---
            var mapaRaiz = new GameObject("MapaRaiz");

            var camGo = new GameObject("CamaraMapa");
            camGo.transform.SetParent(mapaRaiz.transform, false);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;      // vista de exploracion: NO es la del combate
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.20f);
            camGo.AddComponent<AudioListener>();

            Cuadrado("Piso", mapaRaiz.transform, Vector3.zero,
                new Vector2(24f, 14f), new Color(0.22f, 0.35f, 0.24f), -20);

            PlayerController jugador = BuildJugador(mapaRaiz.transform);

            var follow = camGo.AddComponent<CameraFollow>();
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("target").objectReferenceValue = jugador.transform;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            // --- el combate (el prefab, apagado hasta que haga falta) ---
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var combateRaiz = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            combateRaiz.name = "SistemaDeCombate";
            var systemRoot = combateRaiz.GetComponent<BattleSystemRoot>();
            BattleController controller = systemRoot.Controller;
            combateRaiz.SetActive(false);

            // --- zonas de combate ---
            BuildZona("ZonaEntrenador", mapaRaiz.transform, new Vector3(-5f, 2.5f, 0f),
                new Color(0.30f, 0.75f, 0.35f, 0.45f), controller, trainer, null, true);
            BuildZona("ZonaSalvaje", mapaRaiz.transform, new Vector3(5f, 2.5f, 0f),
                new Color(0.30f, 0.55f, 0.90f, 0.45f), controller, null, wild, false);
            BuildZona("ZonaBoss", mapaRaiz.transform, new Vector3(0f, -3f, 0f),
                new Color(0.90f, 0.30f, 0.30f, 0.45f), controller, boss, null, true);

            // --- puente mapa <-> combate ---
            var puenteGo = new GameObject("PuenteCombate");
            var puente = puenteGo.AddComponent<MapBattleBridge>();
            var puenteSo = new SerializedObject(puente);
            puenteSo.FindProperty("mapaRaiz").objectReferenceValue = mapaRaiz;
            puenteSo.FindProperty("combateRaiz").objectReferenceValue = combateRaiz;
            puenteSo.FindProperty("battleController").objectReferenceValue = controller;
            puenteSo.FindProperty("playerController").objectReferenceValue = jugador;
            puenteSo.ApplyModifiedPropertiesWithoutUndo();

            // --- arranque + objetos para probar la mochila ---
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<SceneBootstrapper>();
            var bootSo = new SerializedObject(boot);
            bootSo.FindProperty("playerController").objectReferenceValue = jugador;
            bootSo.FindProperty("crearPartidaSiFalta").boolValue = true;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            var seeder = bootGo.AddComponent<DemoInventorySeeder>();
            WireObjetos(seeder);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Demo] Escena lista: {ScenePath}\n" +
                      "Apreta Play y camina con WASD/flechas hacia una zona:\n" +
                      "  verde = entrenador (2 Pokens)  |  azul = salvaje  |  rojo = boss (3 Pokens)\n" +
                      "En combate: click en los botones, o 1-4 movimientos y F1-F6 cambio.");
        }

        // Jugador minimo: se mueve por tiles y tiene con que disparar triggers.
        // El Rigidbody2D es OBLIGATORIO aunque no se use fisica: sin el, dos
        // colliders que solo se mueven por transform nunca generan OnTriggerEnter2D.
        private static PlayerController BuildJugador(Transform parent)
        {
            GameObject go = Cuadrado("Jugador", parent, Vector3.zero,
                new Vector2(0.7f, 0.7f), new Color(0.95f, 0.85f, 0.30f), 5);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.6f;

            var jugador = go.AddComponent<PlayerController>();
            var input = go.AddComponent<PlayerInputController>();

            var inputSo = new SerializedObject(input);
            inputSo.FindProperty("playerController").objectReferenceValue = jugador;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            var jugadorSo = new SerializedObject(jugador);
            jugadorSo.FindProperty("inputController").objectReferenceValue = input;
            jugadorSo.ApplyModifiedPropertiesWithoutUndo();

            return jugador;
        }

        private static void BuildZona(string nombre, Transform parent, Vector3 position,
            Color color, BattleController controller,
            NPCDefinition combate, WildEncounterDefinition salvaje, bool unaSolaVez)
        {
            var tamaño = new Vector2(3f, 2f);
            GameObject go = Cuadrado(nombre, parent, position, tamaño, color, -5);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = tamaño;   // el padre tiene escala 1: son unidades de mundo

            var zona = go.AddComponent<BattleZoneTrigger>();
            var so = new SerializedObject(zona);
            so.FindProperty("combate").objectReferenceValue = combate;
            so.FindProperty("salvaje").objectReferenceValue = salvaje;
            so.FindProperty("battleController").objectReferenceValue = controller;
            so.FindProperty("unaSolaVez").boolValue = unaSolaVez;
            so.FindProperty("probabilidad").intValue = 100;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireObjetos(DemoInventorySeeder seeder)
        {
            var pocion = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
                "Assets/Resources/Definitions/Items/Pocion.asset");
            var superPocion = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
                "Assets/Resources/Definitions/Items/SuperPocion.asset");

            var so = new SerializedObject(seeder);
            SerializedProperty lista = so.FindProperty("objetos");
            lista.arraySize = 2;
            Poner(lista.GetArrayElementAtIndex(0), pocion, 5);
            Poner(lista.GetArrayElementAtIndex(1), superPocion, 3);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (pocion == null || superPocion == null)
                Debug.LogWarning("[Demo] Falta alguna pocion en Resources/Definitions/Items: " +
                                 "la mochila puede abrir vacia.");
        }

        private static void Poner(SerializedProperty entrada, Object objeto, int cantidad)
        {
            entrada.FindPropertyRelative("objeto").objectReferenceValue = objeto;
            entrada.FindPropertyRelative("cantidad").intValue = cantidad;
        }

        // Cuadrado plano con el sprite integrado de Unity: alcanza para ver
        // donde esta cada cosa sin depender de ningun arte del proyecto.
        //
        // El sprite va en un HIJO y no en el mismo objeto a proposito. Asi el
        // padre queda con escala 1 y los Collider2D se miden en unidades de
        // mundo, sin heredar el escalado del visual: un collider que hereda
        // escala es de los errores mas dificiles de ver en el Inspector.
        private static GameObject Cuadrado(string nombre, Transform parent,
            Vector3 position, Vector2 tamaño, Color color, int sortingOrder)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);

            var renderer = visualGo.AddComponent<SpriteRenderer>();
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            // Escala en modo Simple (nada de Sliced: el sprite integrado no es
            // Full Rect y Unity tira un warning por cada uno)
            Vector2 nativo = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            visualGo.transform.localScale = new Vector3(
                nativo.x > 0f ? tamaño.x / nativo.x : 1f,
                nativo.y > 0f ? tamaño.y / nativo.y : 1f,
                1f);

            return go;
        }
    }
}
