using System.Collections.Generic;
using System.Linq;
using PokeRush.EditorTools.PokeApi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arma TODO lo necesario para probar el combate con un solo click:
//   PokeRush > Armar combate de prueba
// 1. Genera los perfiles de IA y la tabla de XP si faltan.
// 2. Crea assets de prueba: un entrenador, un boss y un encuentro salvaje
//    (en Assets/Resources/Definitions/Testing/).
// 3. Construye la escena Assets/Scenes/CombatePrueba.unity con el
//    BattleController, el BattleTester y un HUD minimo ya cableados.
// Despues: abrir la escena, dar Play y usar 1-4 (movimientos), F1-F6
// (cambio de Poken), B (reiniciar contra el rival), W (combate salvaje).
namespace PokeRush.EditorTools
{
    public static class BattleTestSceneBuilder
    {
        private const string TestingFolder = "Assets/Resources/Definitions/Testing";
        private const string ScenePath = "Assets/Scenes/CombatePrueba.unity";

        [MenuItem("PokeRush/Armar combate de prueba")]
        public static void Build()
        {
            // Por si hay cambios sin guardar en la escena abierta
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // La escena nueva se crea ANTES de cargar cualquier asset: al cerrar
            // la escena anterior Unity descarga los assets en memoria y las
            // referencias cargadas antes se serializarian como null.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- 1. Dependencias: perfiles de IA y tabla de XP ---
            AIProfileGenerator.Generate();
            if (AssetDatabase.LoadAssetAtPath<ExperienceTableDefinition>(
                    "Assets/Resources/Definitions/ExperienceTable.asset") == null)
                ExperienceTableGenerator.Generate();

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

            // --- 3. Assets de prueba ---
            EnsureFolder(TestingFolder);

            // Entrenador comun: 2 Pokens, IA Trainer
            var trainer = LoadOrCreate<EnemyDefinition>($"{TestingFolder}/EntrenadorDePrueba.asset");
            trainer.pokenTeam = new[]
            {
                new EnemyTeamEntry { species = Species(4, "Charmander"), level = 9 },
                new EnemyTeamEntry { species = Species(7, "Squirtle"), level = 10 }
            };
            trainer.aiProfile = profileTrainer;
            trainer.moneyReward = 200;
            EditorUtility.SetDirty(trainer);

            // Boss: 3 Pokens mas fuertes, IA GymLeader (cambia, remata, hace setup)
            var boss = LoadOrCreate<BossDefinition>($"{TestingFolder}/BossDePrueba.asset");
            boss.pokenTeam = new[]
            {
                new EnemyTeamEntry { species = Species(2, "Ivysaur"), level = 15 },
                new EnemyTeamEntry { species = Species(5, "Charmeleon"), level = 15 },
                new EnemyTeamEntry { species = Species(8, "Wartortle"), level = 16 }
            };
            boss.aiProfile = profileGymLeader;
            boss.moneyReward = 1000;
            EditorUtility.SetDirty(boss);

            // Encuentro salvaje: Pikachu 3-6, IA Wild
            var wild = LoadOrCreate<WildEncounterDefinition>($"{TestingFolder}/SalvajeDePrueba.asset");
            wild.species = Species(25, "Pikachu");
            wild.minLevel = 3;
            wild.maxLevel = 6;
            wild.aiProfile = profileWild;
            EditorUtility.SetDirty(wild);

            AssetDatabase.SaveAssets();

            // --- 4. Contenido de la escena de prueba ---
            BuildCamera();
            PokenView playerView = BuildPokenView("PokenJugador", new Vector3(-4.5f, -1.5f, 0f));
            PokenView enemyView = BuildPokenView("PokenRival", new Vector3(4.5f, 1.5f, 0f));
            BattleHudView hud = BuildHud();

            // BattleController cableado al HUD y las vistas
            var controllerGo = new GameObject("BattleSystem");
            var controller = controllerGo.AddComponent<BattleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("battleHudView").objectReferenceValue = hud;
            so.FindProperty("playerPokenView").objectReferenceValue = playerView;
            so.FindProperty("enemyPokenView").objectReferenceValue = enemyView;
            so.FindProperty("messageDelay").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();

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
                "Dale Play y usa:\n" +
                "  1-4  = movimientos\n" +
                "  F1-F6 = cambiar de Poken (consume el turno)\n" +
                "  B    = reiniciar combate contra el rival\n" +
                "  W    = combate salvaje (Pikachu 3-6)\n\n" +
                "Para probar el BOSS (IA GymLeader): selecciona el objeto BattleTester\n" +
                "y arrastra Testing/BossDePrueba al campo 'Enemy Definition'.\n\n" +
                "Los mensajes salen en el HUD; sin sprites asignados los Pokens\n" +
                "no se ven, pero el combate funciona igual.", "¡A jugar!");

            Debug.Log("[PokeRush] Combate de prueba armado: escena, HUD, entrenador, boss y salvaje listos.");
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

        // ---------- helpers de escena ----------

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.13f, 0.17f);
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        private static PokenView BuildPokenView(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 2f;
            var renderer = go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<PokenView>();
            var so = new SerializedObject(view);
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static BattleHudView BuildHud()
        {
            // Canvas raiz
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Contenedor del HUD (BattleHudView hace SetActive sobre este objeto)
            var hudGo = new GameObject("BattleHud");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRect = hudGo.AddComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;
            var hud = hudGo.AddComponent<BattleHudView>();

            // Rival: nombre + barra arriba a la izquierda
            Text enemyName = CreateText(hudGo.transform, "NombreRival", new Vector2(0, 1),
                new Vector2(30, -30), new Vector2(360, 40), 28, TextAnchor.MiddleLeft);
            HealthBarView enemyBar = CreateHealthBar(hudGo.transform, "BarraRival",
                new Vector2(0, 1), new Vector2(30, -80));

            // Jugador: nombre + barra abajo a la derecha
            Text playerName = CreateText(hudGo.transform, "NombreJugador", new Vector2(1, 0),
                new Vector2(-390, 150), new Vector2(360, 40), 28, TextAnchor.MiddleLeft);
            HealthBarView playerBar = CreateHealthBar(hudGo.transform, "BarraJugador",
                new Vector2(1, 0), new Vector2(-390, 100));

            // Turno arriba a la derecha
            Text turnText = CreateText(hudGo.transform, "Turno", new Vector2(1, 1),
                new Vector2(-330, -30), new Vector2(300, 40), 26, TextAnchor.MiddleRight);

            // Panel de mensajes abajo, a lo ancho
            var msgPanel = new GameObject("PanelMensajes");
            msgPanel.transform.SetParent(hudGo.transform, false);
            var msgRect = msgPanel.AddComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0, 0);
            msgRect.anchorMax = new Vector2(1, 0);
            msgRect.pivot = new Vector2(0.5f, 0);
            msgRect.anchoredPosition = new Vector2(0, 10);
            msgRect.sizeDelta = new Vector2(-40, 80);
            var msgBg = msgPanel.AddComponent<Image>();
            msgBg.color = new Color(0f, 0f, 0f, 0.6f);

            Text messageText = CreateText(msgPanel.transform, "TextoMensaje", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1800, 70), 30, TextAnchor.MiddleCenter);

            // Cablear los campos privados del BattleHudView
            var so = new SerializedObject(hud);
            so.FindProperty("playerNameText").objectReferenceValue = playerName;
            so.FindProperty("playerHealthBar").objectReferenceValue = playerBar;
            so.FindProperty("enemyNameText").objectReferenceValue = enemyName;
            so.FindProperty("enemyHealthBar").objectReferenceValue = enemyBar;
            so.FindProperty("turnText").objectReferenceValue = turnText;
            so.FindProperty("messageText").objectReferenceValue = messageText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static HealthBarView CreateHealthBar(Transform parent, string name,
            Vector2 anchor, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(360, 30);

            // Fondo oscuro
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // Relleno verde (Image tipo Filled horizontal)
            var fillGo = new GameObject("Relleno");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3, 3);
            fillRect.offsetMax = new Vector2(-3, -3);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(0.3f, 0.85f, 0.3f);

            // Texto "HP/MaxHP" encima
            Text hpText = CreateText(go.transform, "TextoHP", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(340, 28), 20, TextAnchor.MiddleCenter);

            var bar = go.AddComponent<HealthBarView>();
            var so = new SerializedObject(bar);
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("hpText").objectReferenceValue = hpText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }
    }
}
