using System.Collections.Generic;
using System.Linq;
using PokeRush.EditorTools.PokeApi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arma TODO lo necesario para probar el combate con un solo click:
//   PokeRush > Armar combate de prueba
// 1. Genera los perfiles de IA y la tabla de XP si faltan.
// 2. Importa y recorta los graficos de UI de batalla y los fondos (Diseño/).
// 3. Crea assets de prueba: un entrenador, un boss y un encuentro salvaje.
// 4. Construye la escena Assets/Scenes/CombatePrueba.unity con el HUD clasico
//    (databoxes, LUCHAR/POKEMON/MOCHILA/HUIR, panel PP/TIPO) todo cableado.
// En Play: click en los botones, o teclado (1-4 movimientos, F1-F6 cambio,
// B reiniciar, W salvaje).
namespace PokeRush.EditorTools
{
    public static class BattleTestSceneBuilder
    {
        private const string TestingFolder = "Assets/Resources/Definitions/Testing";
        private const string ScenePath = "Assets/Scenes/CombatePrueba.unity";

        // El canvas usa la resolucion nativa de los graficos (512x384): las
        // coordenadas de este builder son pixeles del mockup del diseñador.
        private static readonly Vector2 CanvasSize = new Vector2(512, 384);

        private static Font _font;
        // La de pixel art del juego (la misma de los menus y del mockup del HUD);
        // si no esta, la de Unity.
        private static Font UiFont => _font != null ? _font
            : (_font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/power clear.ttf")
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        [MenuItem("PokeRush/Armar combate de prueba")]
        public static void Build()
        {
            // Por si hay cambios sin guardar en la escena abierta
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // La escena nueva se crea ANTES de cargar cualquier asset: al cerrar
            // la escena anterior Unity descarga los assets en memoria y las
            // referencias cargadas antes se serializarian como null.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- 1. Dependencias: perfiles de IA, tabla de XP y graficos de UI ---
            AIProfileGenerator.Generate();
            if (AssetDatabase.LoadAssetAtPath<ExperienceTableDefinition>(
                    "Assets/Resources/Definitions/ExperienceTable.asset") == null)
                ExperienceTableGenerator.Generate();
            if (!BattleUiAssetImporter.ImportAll())
            {
                EditorUtility.DisplayDialog("Faltan graficos",
                    "No se encontro Diseño/Graphics/UI/Battle en el repo.", "OK");
                return;
            }

            var profileTrainer = LoadProfile("Trainer");
            var profileGymLeader = LoadProfile("GymLeader");
            var profileWild = LoadProfile("Wild");

            // --- 2. Especies para los equipos (de las importadas de PokeAPI) ---
            List<PokenDefinition> allSpecies = LoadAllSpecies();
            if (allSpecies.Count == 0)
            {
                EditorUtility.DisplayDialog("Falta la Pokedex",
                    "No hay PokenDefinition en Resources/Definitions/Pokens.\n" +
                    "Corre primero PokeRush > Importador PokeAPI (boton 'Todo').", "OK");
                return;
            }

            PokenDefinition Species(int dex, string fallbackInfo)
            {
                PokenDefinition found = allSpecies.FirstOrDefault(s => s.dexNumber == dex);
                if (found == null)
                {
                    found = allSpecies[0];
                    Debug.LogWarning($"[PokeRush] No se encontro la especie dex {dex} ({fallbackInfo}); uso {found.pokenName}.");
                }
                return found;
            }

            // --- 3. Assets de prueba (con fondo de batalla propio) ---
            EnsureFolder(TestingFolder);

            var trainer = LoadOrCreate<EnemyDefinition>($"{TestingFolder}/EntrenadorDePrueba.asset");
            trainer.pokenTeam = new[]
            {
                new EnemyTeamEntry { species = Species(4, "Charmander"), level = 9 },
                new EnemyTeamEntry { species = Species(7, "Squirtle"), level = 10 }
            };
            trainer.aiProfile = profileTrainer;
            trainer.moneyReward = 200;
            trainer.battleBackground = BattleUiAssetImporter.LoadBackground("Bosque_bg.png");
            trainer.battleFrontSprite = BattleUiAssetImporter.LoadTrainer("ASH.png");
            EditorUtility.SetDirty(trainer);

            var boss = LoadOrCreate<BossDefinition>($"{TestingFolder}/BossDePrueba.asset");
            boss.pokenTeam = new[]
            {
                new EnemyTeamEntry { species = Species(2, "Ivysaur"), level = 15 },
                new EnemyTeamEntry { species = Species(5, "Charmeleon"), level = 15 },
                new EnemyTeamEntry { species = Species(8, "Wartortle"), level = 16 }
            };
            boss.aiProfile = profileGymLeader;
            boss.moneyReward = 1000;
            boss.battleBackground = BattleUiAssetImporter.LoadBackground("Gimnasio1_bg.png");
            boss.battleFrontSprite = BattleUiAssetImporter.LoadTrainer("AGATHA1.png");
            EditorUtility.SetDirty(boss);

            var wild = LoadOrCreate<WildEncounterDefinition>($"{TestingFolder}/SalvajeDePrueba.asset");
            wild.species = Species(151, "Mew");
            wild.minLevel = 3;
            wild.maxLevel = 6;
            wild.aiProfile = profileWild;
            wild.battleBackground = BattleUiAssetImporter.LoadBackground("Pradera_bg.png");
            EditorUtility.SetDirty(wild);

            AssetDatabase.SaveAssets();

            // --- 4. Contenido de la escena ---
            // Sin BuildCamera(): el rig ya trae la suya, con el tamaño correcto
            BuildEventSystem();

            // Todo el combate junto bajo una raiz: es la misma que empaqueta
            // BattlePrefabBuilder, asi la escena de prueba y el prefab del juego
            // no pueden quedar distintos.
            BattleSystemRoot root = BuildCombatRig();
            BattleController controller = root.Controller;

            // BattleTester: equipo del jugador (iniciales nivel 12) vs el entrenador
            var testerGo = new GameObject("BattleTester");
            var tester = testerGo.AddComponent<BattleTester>();
            var testerSo = new SerializedObject(tester);
            testerSo.FindProperty("battleController").objectReferenceValue = controller;
            SetArray(testerSo, "playerSpecies",
                Species(1, "Bulbasaur"), Species(4, "Charmander"), Species(7, "Squirtle"));
            SetIntArray(testerSo, "playerLevels", 12, 12, 12);
            testerSo.FindProperty("enemyDefinition").objectReferenceValue = trainer;
            testerSo.FindProperty("wildEncounter").objectReferenceValue = wild;
            testerSo.FindProperty("startOnPlay").boolValue = true;

            // Nestor y sus dos variantes, para probar los subsistemas con la
            // tecla N sin tener que caminar la torre.
            testerSo.FindProperty("nestorConMelmetal").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BossDefinition>(
                    "Assets/Resources/Definitions/Torre1/Nestor.asset");
            testerSo.FindProperty("nestorSinMelmetal").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BossDefinition>(
                    "Assets/Resources/Definitions/Torre1/Nestor_SinMelmetal.asset");

            // Un objeto de curacion cualquiera: sin esto el boton CURAR de la
            // fase de liberacion no tiene con que curar.
            testerSo.FindProperty("testHealItem").objectReferenceValue = PrimerObjetoDeCuracion();
            testerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            // Verificacion: si alguna referencia quedo null, avisar en vez de
            // dejar una escena rota en silencio.
            testerSo.Update();
            if (testerSo.FindProperty("enemyDefinition").objectReferenceValue == null
                || testerSo.FindProperty("playerSpecies").GetArrayElementAtIndex(0).objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("Algo salió mal",
                    "La escena se creó pero quedaron referencias vacías en el BattleTester. " +
                    "Volvé a correr PokeRush > Armar combate de prueba.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Combate de prueba listo",
                "Escena creada: Assets/Scenes/CombatePrueba.unity (ya esta abierta).\n\n" +
                "TECLAS\n" +
                "  B  combate contra el entrenador de prueba\n" +
                "  W  combate salvaje\n" +
                "  N  combate contra NESTOR con los subsistemas del Inspector\n" +
                "  1-4 movimientos   F1-F6 cambio de Poken\n\n" +
                "Para probar la torre 1: selecciona el objeto BattleTester y marca\n" +
                "que subsistemas estan cortados (camaras, nodos del firewall,\n" +
                "rastreo, energia). Apreta N y pelea. Son 16 combinaciones y no\n" +
                "hace falta caminar ninguna torre.\n\n" +
                "Con los CUATRO cortados sale Unown en vez de Melmetal.",
                "¡A jugar!");

            Debug.Log("[PokeRush] Combate de prueba armado con el HUD clasico.");
        }

        // ---------- helpers de assets ----------

        private static AIProfileDefinition LoadProfile(string name)
            => AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(
                $"Assets/Resources/Definitions/AIProfiles/{name}.asset");

        private static List<PokenDefinition> LoadAllSpecies()
            => AssetDatabase.FindAssets("t:PokenDefinition",
                    new[] { "Assets/Resources/Definitions/Pokens" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PokenDefinition>)
                .Where(p => p != null)
                .ToList();

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetArray(SerializedObject so, string property, params Object[] values)
        {
            SerializedProperty prop = so.FindProperty(property);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetIntArray(SerializedObject so, string property, params int[] values)
        {
            SerializedProperty prop = so.FindProperty(property);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        // ---------- mundo: camara, fondo y battlers ----------

        // Un objeto de curacion cualquiera para el equipo de prueba. Se prefiere
        // la Pocion comun; si no esta, el primero que aparezca.
        private static HealItemDefinition PrimerObjetoDeCuracion()
        {
            var pocion = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
                "Assets/Resources/Definitions/Items/Pocion.asset");
            if (pocion != null) return pocion;

            string[] guids = AssetDatabase.FindAssets("t:HealItemDefinition");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[PokeRush] No hay ningun HealItemDefinition: el boton CURAR " +
                                 "de la fase de liberacion no va a tener con que curar.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---------- el sistema de combate completo, bajo UNA raiz ----------

        // Arma el combate entero (fondo, PokenViews, HUD, FX y BattleController)
        // colgando de un solo GameObject. Lo usan la escena de prueba y el
        // prefab del juego: un unico armado, una sola cosa que mantener.
        //
        // NO incluye camara ni EventSystem a proposito: esos son de la escena.
        // BattleSystemRoot crea el EventSystem en runtime si falta, asi que el
        // prefab igual se puede soltar en cualquier mapa.
        internal static BattleSystemRoot BuildCombatRig()
        {
            var rootGo = new GameObject("SistemaDeCombate");
            Transform root = rootGo.transform;

            // Camara PROPIA. Va adentro del prefab y no en la escena porque el
            // layout del combate esta hecho para orthographicSize 1.92: con la
            // camara de un mapa (7.5 en MapaNuevo) los Pokens y el fondo salen
            // ~4x mas chicos, mientras el HUD —que es screen-space y no depende
            // de la camara— queda igual. Eso es lo que se ve desproporcionado.
            Camera battleCamera = BuildBattleCamera(root);

            SpriteRenderer background = BuildBackground(root);
            // Los sprites tienen pivot en los pies y PPU = alto del frame (miden
            // 1 unidad): la Y es el piso de cada plataforma y la escala es directa
            PokenView playerView = BuildPokenView("PokenJugador", new Vector3(-1.28f, -1.55f, 0f), 1.7f, root);
            PokenView enemyView = BuildPokenView("PokenRival", new Vector3(0.95f, 0.30f, 0f), 1.1f, root);
            BattleHudView hud = BuildHud(root);
            BattleFxView fx = BuildBattleFx(root);

            // BattleController cableado al HUD, las vistas, el fondo y los FX
            var controller = rootGo.AddComponent<BattleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("battleHudView").objectReferenceValue = hud;
            so.FindProperty("playerPokenView").objectReferenceValue = playerView;
            so.FindProperty("enemyPokenView").objectReferenceValue = enemyView;
            so.FindProperty("backgroundRenderer").objectReferenceValue = background;
            so.FindProperty("battleFxView").objectReferenceValue = fx;
            so.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            so.FindProperty("messageDelay").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // El temblor de pantalla tiene que mover la camara del combate, no
            // la del mapa
            var fxSo = new SerializedObject(fx);
            fxSo.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            var systemRoot = rootGo.AddComponent<BattleSystemRoot>();
            var rootSo = new SerializedObject(systemRoot);
            rootSo.FindProperty("battleController").objectReferenceValue = controller;
            rootSo.FindProperty("battleCamera").objectReferenceValue = battleCamera;
            rootSo.FindProperty("audioListener").objectReferenceValue =
                battleCamera.GetComponent<AudioListener>();
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            return systemRoot;
        }

        // Camara del combate. Va tageada MainCamera para que todo lo que use
        // Camera.main (FX heredados, futuros efectos) apunte a ella mientras la
        // pelea esta activa; como el prefab arranca DESACTIVADO, fuera del
        // combate la unica MainCamera sigue siendo la del mapa.
        private static Camera BuildBattleCamera(Transform parent)
        {
            var go = new GameObject("CamaraCombate");
            go.transform.SetParent(parent, false);
            go.tag = "MainCamera";
            go.transform.localPosition = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            // Media altura del fondo (384 px / 2 / 100 ppu): el fondo llena la vista
            cam.orthographicSize = 1.92f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.depth = 10; // por encima de la camara de exploracion

            // BattleSystemRoot lo apaga si la escena ya tiene otro activo
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            // Media altura del fondo (384 px / 2 / 100 ppu): el fondo llena la vista
            cam.orthographicSize = 1.92f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        internal static SpriteRenderer BuildBackground(Transform parent)
        {
            var go = new GameObject("FondoBatalla");
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BattleUiAssetImporter.LoadBackground("Pradera_bg.png");
            renderer.sortingOrder = -10; // detras de los Pokens
            return renderer;
        }

        internal static PokenView BuildPokenView(string name, Vector3 position, float scale, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<PokenView>();
            var so = new SerializedObject(view);
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // el proyecto usa el Input System nuevo
        }

        // Objetos de las secuencias visuales: entrenador rival, ball volando,
        // destello y el fundido de pantalla (en su propio canvas, encima del HUD)
        internal static BattleFxView BuildBattleFx(Transform parent)
        {
            var fxGo = new GameObject("BattleFx");
            fxGo.transform.SetParent(parent, false);
            var fx = fxGo.AddComponent<BattleFxView>();

            // Todo lo de los FX cuelga de fxGo (que esta en el origen, asi que
            // local == mundo). Antes quedaban sueltos en la raiz de la escena y
            // no se podian empaquetar en un prefab.
            Transform fxRoot = fxGo.transform;

            var trainerGo = new GameObject("EntrenadorRival");
            var trainerRenderer = trainerGo.AddComponent<SpriteRenderer>();
            trainerRenderer.sortingOrder = 5;
            trainerGo.transform.localScale = Vector3.one * 1.6f;
            trainerGo.SetActive(false);

            var ballGo = new GameObject("BallVolando");
            var ballRenderer = ballGo.AddComponent<SpriteRenderer>();
            ballRenderer.sortingOrder = 10;
            ballGo.SetActive(false);

            var burstGo = new GameObject("DestelloBall");
            var burstRenderer = burstGo.AddComponent<SpriteRenderer>();
            burstRenderer.sprite = BattleUiAssetImporter.LoadSimple("ballBurst_ray.png");
            burstRenderer.sortingOrder = 9;
            burstGo.SetActive(false);

            var projectileGo = new GameObject("Proyectil");
            var projectileRenderer = projectileGo.AddComponent<SpriteRenderer>();
            projectileRenderer.sprite = BattleUiAssetImporter.LoadSimple("ballBurst_particle.png");
            projectileRenderer.sortingOrder = 8;
            projectileGo.SetActive(false);

            var impactGo = new GameObject("ImpactoGolpe");
            var impactRenderer = impactGo.AddComponent<SpriteRenderer>();
            impactRenderer.sprite = BattleUiAssetImporter.LoadSimple("ballBurst_star.png");
            impactRenderer.sortingOrder = 8;
            impactGo.SetActive(false);

            // Canvas de ambiente: tintes de clima/terreno sobre el mundo pero
            // DEBAJO del HUD (sorting -5 < HUD 0 < fundido 100)
            var ambientCanvasGo = new GameObject("CanvasAmbiente");
            var ambientCanvas = ambientCanvasGo.AddComponent<Canvas>();
            ambientCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ambientCanvas.sortingOrder = -5;
            Image weatherOverlay = CreateOverlayImage(ambientCanvasGo.transform, "OverlayClima");
            Image terrainOverlay = CreateOverlayImage(ambientCanvasGo.transform, "OverlayTerreno");

            var fadeCanvasGo = new GameObject("CanvasFx");
            var fadeCanvas = fadeCanvasGo.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 100; // encima del canvas del HUD
            var fadeGo = CreateRect("Fundido", fadeCanvasGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fadeImage = fadeGo.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
            fadeGo.SetActive(false);

            foreach (GameObject hijo in new[]
                     { trainerGo, ballGo, burstGo, projectileGo, impactGo,
                       ambientCanvasGo, fadeCanvasGo })
                hijo.transform.SetParent(fxRoot, false);

            var so = new SerializedObject(fx);
            so.FindProperty("trainerRenderer").objectReferenceValue = trainerRenderer;
            so.FindProperty("ballRenderer").objectReferenceValue = ballRenderer;
            so.FindProperty("burstRenderer").objectReferenceValue = burstRenderer;
            so.FindProperty("projectileRenderer").objectReferenceValue = projectileRenderer;
            so.FindProperty("impactRenderer").objectReferenceValue = impactRenderer;
            so.FindProperty("fadeOverlay").objectReferenceValue = fadeImage;
            so.FindProperty("weatherOverlay").objectReferenceValue = weatherOverlay;
            so.FindProperty("terrainOverlay").objectReferenceValue = terrainOverlay;
            SetArray(so, "ballSpinFrames",
                BattleUiAssetImporter.LoadAllSlices("ball_POKEBALL.png").Cast<Object>().ToArray());
            so.FindProperty("ballOpenSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Battle/ball_POKEBALL_open.png");
            so.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        // ---------- HUD ----------

        internal static BattleHudView BuildHud(Transform parent)
        {
            // Canvas a resolucion nativa de los graficos (512x384)
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CanvasSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // encaja la altura (384): los pixeles coinciden
            canvasGo.AddComponent<GraphicRaycaster>();

            var hudGo = CreateRect("BattleHud", canvasGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var hud = hudGo.AddComponent<BattleHudView>();

            // --- barra de mensajes (abajo, a lo ancho) ---
            var messageBar = CreateRect("BarraMensajes", hudGo.transform,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                Vector2.zero, new Vector2(0, 96));
            var barImage = messageBar.AddComponent<Image>();
            barImage.sprite = BattleUiAssetImporter.LoadSimple("overlay_message.png");
            barImage.color = new Color(0.12f, 0.12f, 0.15f, 0.92f);

            Text messageText = CreateText("TextoMensaje", messageBar.transform,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(12, 6), new Vector2(238, 84), 15, TextAnchor.MiddleLeft);

            // --- databox del RIVAL (arriba a la izquierda) ---
            var enemyBox = CreateRect("DataboxRival", hudGo.transform,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, -24), new Vector2(260, 70));
            AddNativeImage(enemyBox, BattleUiAssetImporter.LoadSimple("databox_normal_foe.png"));

            Text enemyName = CreateBoxText("NombreRival", enemyBox.transform, new Vector2(14, -8), new Vector2(150, 20), 14, TextAnchor.MiddleLeft);
            Text enemyLevel = CreateBoxText("NivelRival", enemyBox.transform, new Vector2(120, -8), new Vector2(90, 20), 12, TextAnchor.MiddleRight);
            Image enemyHpFill = CreateFill(enemyBox.transform, "HpRival", new Vector2(118, -40), new Vector2(96, 6));
            Image enemyStatus = CreateIcon(enemyBox.transform, "EstadoRival", new Vector2(40, -36), new Vector2(44, 16));

            // --- barra del ESCUDO DEL NUCLEO (debajo del databox rival) ---
            // Mismo lenguaje visual que los databox: marco oscuro, relleno
            // Filled horizontal y rotulo corto. Solo se prende contra el boss.
            var shieldBox = CreateRect("EscudoNucleo", hudGo.transform,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(14, -96), new Vector2(232, 18));
            var shieldBg = shieldBox.AddComponent<Image>();
            shieldBg.color = new Color(0.07f, 0.09f, 0.13f, 0.92f);
            shieldBg.raycastTarget = false;

            Text shieldText = CreateText("TextoEscudo", shieldBox.transform,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
                new Vector2(6, 0), new Vector2(78, 0), 10, TextAnchor.MiddleLeft);
            shieldText.color = new Color(0.62f, 0.88f, 0.98f);

            var shieldTrack = CreateRect("Riel", shieldBox.transform,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(40, 0), new Vector2(-92, 8));
            var trackImg = shieldTrack.AddComponent<Image>();
            trackImg.color = new Color(0.18f, 0.20f, 0.26f, 1f);
            trackImg.raycastTarget = false;

            var shieldFillGo = CreateRect("Relleno", shieldTrack.transform,
                Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var shieldFill = shieldFillGo.AddComponent<Image>();
            shieldFill.color = new Color(0.35f, 0.80f, 0.95f);
            shieldFill.type = Image.Type.Filled;
            shieldFill.fillMethod = Image.FillMethod.Horizontal;
            shieldFill.raycastTarget = false;

            shieldBox.SetActive(false); // lo prende el HUD si la pelea tiene escudo

            // --- databox del JUGADOR (abajo a la derecha, sobre la barra) ---
            var playerBox = CreateRect("DataboxJugador", hudGo.transform,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(0, 104), new Vector2(260, 84));
            AddNativeImage(playerBox, BattleUiAssetImporter.LoadSimple("databox_normal.png"));

            Text playerName = CreateBoxText("NombreJugador", playerBox.transform, new Vector2(24, -6), new Vector2(140, 20), 14, TextAnchor.MiddleLeft);
            Text playerHpText = CreateBoxText("HpTexto", playerBox.transform, new Vector2(24, -24), new Vector2(120, 18), 13, TextAnchor.MiddleLeft);
            Image playerHpFill = CreateFill(playerBox.transform, "HpJugador", new Vector2(136, -40), new Vector2(96, 6));
            Image playerExpFill = CreateFill(playerBox.transform, "ExpJugador", new Vector2(38, -72), new Vector2(192, 4));
            playerExpFill.color = new Color(0.25f, 0.78f, 0.97f); // azul EXP (fijo)
            Text playerLevel = CreateBoxText("NivelJugador", playerBox.transform, new Vector2(2, -64), new Vector2(48, 16), 10, TextAnchor.MiddleLeft);
            Image playerStatus = CreateIcon(playerBox.transform, "EstadoJugador", new Vector2(58, -36), new Vector2(44, 16));

            // --- menu de COMANDOS (botones con texto horneado en el sprite) ---
            var commandPanel = CreateRect("MenuComandos", hudGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Button fightBtn = CreateSheetButton(commandPanel.transform, "BotonLuchar",
                BattleUiAssetImporter.CommandRowFight, new Vector2(-140, 50));
            Button pokemonBtn = CreateSheetButton(commandPanel.transform, "BotonPokemon",
                BattleUiAssetImporter.CommandRowPokemon, new Vector2(-6, 50));
            Button bagBtn = CreateSheetButton(commandPanel.transform, "BotonMochila",
                BattleUiAssetImporter.CommandRowBag, new Vector2(-140, 3));
            Button fleeBtn = CreateSheetButton(commandPanel.transform, "BotonHuir",
                BattleUiAssetImporter.CommandRowFlee, new Vector2(-6, 3));

            // --- menu de LUCHA (4 movimientos + panel PP/TIPO + ATRÁS) ---
            var fightPanel = CreateRect("MenuLucha", hudGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var moveButtons = new Button[4];
            var moveImages = new Image[4];
            var moveTexts = new Text[4];
            Vector2[] movePositions = { new Vector2(4, 50), new Vector2(198, 50), new Vector2(4, 3), new Vector2(198, 3) };
            for (int i = 0; i < 4; i++)
            {
                var btnGo = CreateRect($"Movimiento{i + 1}", fightPanel.transform,
                    new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                    movePositions[i], new Vector2(192, 46));
                moveImages[i] = btnGo.AddComponent<Image>();
                moveButtons[i] = btnGo.AddComponent<Button>();
                moveButtons[i].targetGraphic = moveImages[i];
                moveTexts[i] = CreateText("Nombre", btnGo.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(18, 0), new Vector2(-24, 0), 14, TextAnchor.MiddleLeft);
            }

            var infoPanel = CreateRect("PanelInfo", fightPanel.transform,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-4, 3), new Vector2(112, 56));
            var infoImage = infoPanel.AddComponent<Image>();
            infoImage.color = new Color(0.08f, 0.08f, 0.11f, 0.92f);
            Text ppText = CreateBoxText("TextoPP", infoPanel.transform, new Vector2(6, -4), new Vector2(100, 22), 13, TextAnchor.MiddleCenter);
            Image typeIcon = CreateIcon(infoPanel.transform, "IconoTipo", new Vector2(20, -28), new Vector2(72, 24));

            Button fightBack = CreateSheetButton(fightPanel.transform, "BotonAtras",
                BattleUiAssetImporter.CommandRowBack, new Vector2(-4, 62));
            ((RectTransform)fightBack.transform).sizeDelta = new Vector2(112, 34);

            // --- menu de EQUIPO (overlay con la lista de Pokens) ---
            var teamPanel = CreateRect("MenuEquipo", hudGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var teamOverlay = teamPanel.AddComponent<Image>();
            teamOverlay.color = new Color(0f, 0f, 0f, 0.65f);

            Sprite slotSprite = BattleUiAssetImporter.LoadSlice("cursor_fight.png", "move", 0, 0);
            Sprite slotSelected = BattleUiAssetImporter.LoadSlice("cursor_fight.png", "move", 0, 1);
            var teamButtons = new Button[6];
            var teamNames = new Text[6];
            var teamHps = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                var slotGo = CreateRect($"Poken{i + 1}", teamPanel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 130 - i * 46), new Vector2(300, 42));
                var slotImage = slotGo.AddComponent<Image>();
                slotImage.sprite = slotSprite;
                teamButtons[i] = slotGo.AddComponent<Button>();
                teamButtons[i].targetGraphic = slotImage;
                var state = teamButtons[i].spriteState;
                state.highlightedSprite = slotSelected;
                state.pressedSprite = slotSelected;
                teamButtons[i].spriteState = state;
                teamButtons[i].transition = Selectable.Transition.SpriteSwap;

                teamNames[i] = CreateText("Nombre", slotGo.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(16, 0), new Vector2(-140, 0), 14, TextAnchor.MiddleLeft);
                teamHps[i] = CreateText("HP", slotGo.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(160, 0), new Vector2(-16, 0), 12, TextAnchor.MiddleRight);
            }

            Button teamBack = CreateSheetButton(teamPanel.transform, "BotonAtras",
                BattleUiAssetImporter.CommandRowBack, new Vector2(65, -168));
            var teamBackRect = (RectTransform)teamBack.transform;
            teamBackRect.anchorMin = teamBackRect.anchorMax = teamBackRect.pivot = new Vector2(0.5f, 0.5f);
            teamBackRect.anchoredPosition = new Vector2(0, -160);

            // --- menu de MOCHILA (overlay con la lista de objetos) ---
            var bagPanel = CreateRect("MenuMochila", hudGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bagOverlay = bagPanel.AddComponent<Image>();
            bagOverlay.color = new Color(0f, 0f, 0f, 0.65f);

            var bagButtons = new Button[6];
            var bagNames = new Text[6];
            var bagQuantities = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                var slotGo = CreateRect($"Objeto{i + 1}", bagPanel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, 130 - i * 46), new Vector2(300, 42));
                var slotImage = slotGo.AddComponent<Image>();
                slotImage.sprite = slotSprite;
                bagButtons[i] = slotGo.AddComponent<Button>();
                bagButtons[i].targetGraphic = slotImage;
                var bagState = bagButtons[i].spriteState;
                bagState.highlightedSprite = slotSelected;
                bagState.pressedSprite = slotSelected;
                bagButtons[i].spriteState = bagState;
                bagButtons[i].transition = Selectable.Transition.SpriteSwap;

                bagNames[i] = CreateText("Nombre", slotGo.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(16, 0), new Vector2(-90, 0), 14, TextAnchor.MiddleLeft);
                bagQuantities[i] = CreateText("Cantidad", slotGo.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(210, 0), new Vector2(-16, 0), 13, TextAnchor.MiddleRight);
            }

            Text bagEmpty = CreateText("MochilaVacia", bagPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 60), new Vector2(320, 24), 14, TextAnchor.MiddleCenter);
            bagEmpty.gameObject.SetActive(false);

            Button bagBack = CreateSheetButton(bagPanel.transform, "BotonAtras",
                BattleUiAssetImporter.CommandRowBack, Vector2.zero);
            var bagBackRect = (RectTransform)bagBack.transform;
            bagBackRect.anchorMin = bagBackRect.anchorMax = bagBackRect.pivot = new Vector2(0.5f, 0.5f);
            bagBackRect.anchoredPosition = new Vector2(0, -160);

            // --- panel de la PREDICCION de Nestor (arriba, centrado) ---
            // Componente propio: recibe datos y decide como se ve. Todo esto se
            // puede rehacer a mano desde Unity sin tocar la logica.
            BattlePredictionView predictionView = BuildPredictionView(hudGo.transform);

            // --- menu de la FASE DE LIBERACION ---
            // Panel propio y no reuso del de comandos: esos botones tienen el
            // texto horneado en el sprite (CreateSheetButton), asi que "HUIR"
            // no puede decir "PROTEGER" sin sprites nuevos.
            var liberationPanel = CreateRect("MenuLiberacion", hudGo.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Text liberationTurns = CreateText("TurnosLiberacion", liberationPanel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -34), new Vector2(420, 22), 15, TextAnchor.MiddleCenter);
            liberationTurns.color = new Color(1f, 0.55f, 0.35f);

            Text liberationHint = CreateText("AvisoDelChip", liberationPanel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -56), new Vector2(460, 20), 12, TextAnchor.MiddleCenter);

            Button libHeal = CreateLabelButton(liberationPanel.transform, "BotonCurar",
                "CURAR", new Vector2(-140, 50));
            Button libProtect = CreateLabelButton(liberationPanel.transform, "BotonProteger",
                "PROTEGER", new Vector2(-6, 50));
            Button libSwitch = CreateLabelButton(liberationPanel.transform, "BotonCambiar",
                "CAMBIAR", new Vector2(-140, 3));
            // Atacar queda habilitado: es un error castigado, no una opcion
            // prohibida. La leccion es dejar de atacar, y hay que poder errarle.
            Button libAttack = CreateLabelButton(liberationPanel.transform, "BotonAtacar",
                "ATACAR", new Vector2(-6, 3));

            // Los menus arrancan ocultos (los muestra BattleHudView)
            commandPanel.SetActive(false);
            fightPanel.SetActive(false);
            teamPanel.SetActive(false);
            bagPanel.SetActive(false);
            liberationPanel.SetActive(false);

            // --- cablear todos los campos privados del BattleHudView ---
            var so = new SerializedObject(hud);
            so.FindProperty("messageBar").objectReferenceValue = messageBar;
            so.FindProperty("messageText").objectReferenceValue = messageText;

            so.FindProperty("playerBox").objectReferenceValue = playerBox;
            so.FindProperty("playerNameText").objectReferenceValue = playerName;
            so.FindProperty("playerLevelText").objectReferenceValue = playerLevel;
            so.FindProperty("playerHpText").objectReferenceValue = playerHpText;
            so.FindProperty("playerHpFill").objectReferenceValue = playerHpFill;
            so.FindProperty("playerExpFill").objectReferenceValue = playerExpFill;
            so.FindProperty("playerStatusIcon").objectReferenceValue = playerStatus;

            so.FindProperty("enemyBox").objectReferenceValue = enemyBox;
            so.FindProperty("enemyNameText").objectReferenceValue = enemyName;
            so.FindProperty("enemyLevelText").objectReferenceValue = enemyLevel;
            so.FindProperty("enemyHpFill").objectReferenceValue = enemyHpFill;
            so.FindProperty("enemyStatusIcon").objectReferenceValue = enemyStatus;

            so.FindProperty("predictionView").objectReferenceValue = predictionView;

            so.FindProperty("shieldBox").objectReferenceValue = shieldBox;
            so.FindProperty("shieldFill").objectReferenceValue = shieldFill;
            so.FindProperty("shieldText").objectReferenceValue = shieldText;

            so.FindProperty("liberationPanel").objectReferenceValue = liberationPanel;
            so.FindProperty("liberationHealButton").objectReferenceValue = libHeal;
            so.FindProperty("liberationProtectButton").objectReferenceValue = libProtect;
            so.FindProperty("liberationSwitchButton").objectReferenceValue = libSwitch;
            so.FindProperty("liberationAttackButton").objectReferenceValue = libAttack;
            so.FindProperty("liberationTurnsText").objectReferenceValue = liberationTurns;
            so.FindProperty("liberationHintText").objectReferenceValue = liberationHint;

            so.FindProperty("commandPanel").objectReferenceValue = commandPanel;
            so.FindProperty("fightButton").objectReferenceValue = fightBtn;
            so.FindProperty("pokemonButton").objectReferenceValue = pokemonBtn;
            so.FindProperty("bagButton").objectReferenceValue = bagBtn;
            so.FindProperty("fleeButton").objectReferenceValue = fleeBtn;

            so.FindProperty("fightPanel").objectReferenceValue = fightPanel;
            SetArray(so, "moveButtons", moveButtons.Cast<Object>().ToArray());
            SetArray(so, "moveButtonImages", moveImages.Cast<Object>().ToArray());
            SetArray(so, "moveNameTexts", moveTexts.Cast<Object>().ToArray());
            so.FindProperty("ppText").objectReferenceValue = ppText;
            so.FindProperty("moveTypeIcon").objectReferenceValue = typeIcon;
            so.FindProperty("fightBackButton").objectReferenceValue = fightBack;

            so.FindProperty("teamPanel").objectReferenceValue = teamPanel;
            SetArray(so, "teamButtons", teamButtons.Cast<Object>().ToArray());
            SetArray(so, "teamNameTexts", teamNames.Cast<Object>().ToArray());
            SetArray(so, "teamHpTexts", teamHps.Cast<Object>().ToArray());
            so.FindProperty("teamBackButton").objectReferenceValue = teamBack;

            so.FindProperty("bagPanel").objectReferenceValue = bagPanel;
            SetArray(so, "bagButtons", bagButtons.Cast<Object>().ToArray());
            SetArray(so, "bagNameTexts", bagNames.Cast<Object>().ToArray());
            SetArray(so, "bagQuantityTexts", bagQuantities.Cast<Object>().ToArray());
            so.FindProperty("bagEmptyText").objectReferenceValue = bagEmpty;
            so.FindProperty("bagBackButton").objectReferenceValue = bagBack;

            // Tablas de sprites por tipo y por estado
            WireTypeSprites(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // Panel de la lectura de Nestor: marco, titulo, detalle y barra de
        // progreso hacia la proxima lectura. Es una jerarquia comun y corriente
        // con un BattlePredictionView arriba: se puede reemplazar entera a mano
        // (otros sprites, otro layout, animaciones) sin tocar el combate,
        // porque el componente solo recibe un PredictionSnapshot.
        private static BattlePredictionView BuildPredictionView(Transform parent)
        {
            var panel = CreateRect("PanelPrediccion", parent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -4), new Vector2(380, 58));

            var marco = panel.AddComponent<Image>();
            marco.color = new Color(0.07f, 0.07f, 0.11f, 0.88f);
            marco.raycastTarget = false;

            var group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Text titulo = CreateText("Titulo", panel.transform,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                // X en 0: con anclas estiradas, un offset en X corre la caja
                // entera y el texto queda descentrado aunque diga MiddleCenter.
                new Vector2(0, -5), new Vector2(-24, 22), 17, TextAnchor.MiddleCenter);
            titulo.color = new Color(1f, 0.85f, 0.35f);
            titulo.fontStyle = FontStyle.Bold;

            Text detalle = CreateText("Detalle", panel.transform,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -28), new Vector2(-24, 20), 14, TextAnchor.MiddleCenter);
            detalle.color = new Color(0.88f, 0.88f, 0.94f);

            // Barra de progreso: fondo + relleno horizontal
            var barraFondo = CreateRect("BarraFondo", panel.transform,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, 4), new Vector2(-16, 4));
            var fondoImage = barraFondo.AddComponent<Image>();
            fondoImage.color = new Color(0.2f, 0.2f, 0.26f, 0.9f);
            fondoImage.raycastTarget = false;

            var relleno = CreateRect("BarraRelleno", barraFondo.transform,
                Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var progressFill = relleno.AddComponent<Image>();
            progressFill.color = new Color(1f, 0.85f, 0.35f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.raycastTarget = false;

            var view = panel.AddComponent<BattlePredictionView>();
            var so = new SerializedObject(view);
            so.FindProperty("root").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = titulo;
            so.FindProperty("detailText").objectReferenceValue = detalle;
            so.FindProperty("progressFill").objectReferenceValue = progressFill;
            so.FindProperty("pulseGroup").objectReferenceValue = group;
            SetArray(so, "tintTargets", new Object[] { titulo, progressFill });
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            return view;
        }

        // Llena las tablas sprite-por-enum del BattleHudView desde las hojas recortadas
        private static void WireTypeSprites(SerializedObject so)
        {
            var typeValues = (PokenType[])System.Enum.GetValues(typeof(PokenType));
            var moveNormal = new Object[typeValues.Length];
            var moveSelected = new Object[typeValues.Length];
            var typeIcons = new Object[typeValues.Length];
            foreach (PokenType type in typeValues)
            {
                int row = BattleUiAssetImporter.TypeRow.TryGetValue(type, out int r) ? r : 0;
                moveNormal[(int)type] = BattleUiAssetImporter.LoadSlice("cursor_fight.png", "move", row, 0);
                moveSelected[(int)type] = BattleUiAssetImporter.LoadSlice("cursor_fight.png", "move", row, 1);
                typeIcons[(int)type] = BattleUiAssetImporter.LoadSlice("icon_types.png", "type", row, 0);
            }
            SetArray(so, "moveSpritesByType", moveNormal);
            SetArray(so, "moveSelectedSpritesByType", moveSelected);
            SetArray(so, "typeIconsByType", typeIcons);

            var statusValues = (StatusEffect[])System.Enum.GetValues(typeof(StatusEffect));
            var statusIcons = new Object[statusValues.Length];
            foreach (StatusEffect status in statusValues)
            {
                if (BattleUiAssetImporter.StatusRow.TryGetValue(status, out int row))
                    statusIcons[(int)status] = BattleUiAssetImporter.LoadSlice("icon_statuses.png", "status", row, 0);
            }
            SetArray(so, "statusIconsByEffect", statusIcons);
        }

        // Image fullscreen inactiva para tintes persistentes (clima/terreno)
        private static Image CreateOverlayImage(Transform parent, string name)
        {
            var go = CreateRect(name, parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            go.SetActive(false);
            return image;
        }

        // ---------- helpers de UI ----------

        private static GameObject CreateRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go;
        }

        private static void AddNativeImage(GameObject go, Sprite sprite)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
        }

        // Texto blanco con sombra, anclado al borde superior-izquierdo de su caja
        private static Text CreateBoxText(string name, Transform parent,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
            => CreateText(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                position, size, fontSize, alignment);

        private static Text CreateText(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
            // Con anclas estiradas, position/size funcionan como offsets
            if (anchorMin != anchorMax)
            {
                var rect = (RectTransform)go.transform;
                rect.offsetMin = position;
                rect.offsetMax = size;
            }
            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        // Barra de relleno (HP/EXP): imagen "Filled" horizontal sin sprite
        private static Image CreateFill(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = CreateRect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, size);
            var image = go.AddComponent<Image>();
            // OJO: un Image tipo Filled SIN sprite ignora fillAmount (dibuja el
            // rectangulo completo siempre). El sprite blanco built-in es obligatorio.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateIcon(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = CreateRect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, size);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false; // lo enciende la vista cuando hay icono
            return image;
        }

        // Boton con el texto en un Text hijo, no horneado en el sprite.
        // Hace falta para la fase de liberacion: sus acciones (CURAR, PROTEGER,
        // CAMBIAR, ATACAR) no existen en la hoja cursor_command, que solo tiene
        // los comandos clasicos. Cuando Diseño haga los sprites, esto se
        // reemplaza por CreateSheetButton y listo.
        private static Button CreateLabelButton(Transform parent, string name,
            string label, Vector2 position)
        {
            var go = CreateRect(name, parent, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                position, new Vector2(130, 46));
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.16f, 0.92f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            Text texto = CreateText("Etiqueta", go.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, 14, TextAnchor.MiddleCenter);
            texto.text = label;
            texto.color = Color.white;

            return button;
        }

        // Boton de la hoja cursor_command (texto horneado + estado iluminado).
        // Anclado abajo a la derecha (la zona de comandos del mockup).
        private static Button CreateSheetButton(Transform parent, string name, int commandRow, Vector2 position)
        {
            Sprite normal = BattleUiAssetImporter.LoadSlice("cursor_command.png", "command", commandRow, 0);
            Sprite selected = BattleUiAssetImporter.LoadSlice("cursor_command.png", "command", commandRow, 1);

            var go = CreateRect(name, parent, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                position, new Vector2(130, 46));
            var image = go.AddComponent<Image>();
            image.sprite = normal;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var state = button.spriteState;
            state.highlightedSprite = selected;
            state.pressedSprite = selected;
            state.selectedSprite = selected;
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
            return button;
        }
    }
}
