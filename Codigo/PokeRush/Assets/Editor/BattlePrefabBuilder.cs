using UnityEditor;
using UnityEngine;

// Genera el PREFAB de combate reutilizable:
//   PokeRush > Armar prefab de combate
//
// Sale un solo asset, Assets/Prefabs/Combate/SistemaDeCombate.prefab, con el
// HUD, los PokenView, el fondo, los efectos y el BattleController ya cableados
// entre si. Se arrastra a cualquier escena y el combate anda; no hay que
// reconstruir nada a mano.
//
// Usa el MISMO BuildCombatRig que la escena de prueba a proposito: si el
// prefab se armara por su cuenta, cualquier cambio del HUD habria que hacerlo
// dos veces y las dos versiones se irian separando.
//
// FLUJO COMPLETO EN EL JUEGO
//   Trigger (BattleZoneTrigger / InteractableTrigger)
//     -> BattleController.StartBattle
//     -> combate
//     -> BattleEnded  -> EventController aplica flags, items, dinero y guardado
//                     -> MapBattleBridge devuelve el mapa
//
// COMO SE MONTA EN UNA ESCENA DE MAPA
//   1. Arrastrar el prefab y DESACTIVARLO.
//   2. MapBattleBridge: 'combateRaiz' = el prefab, 'mapaRaiz' = el mundo,
//      'battleController' = el BattleController del prefab.
//   3. Poner los BattleZoneTrigger donde se quiera pelear. Se buscan el
//      controller solos.
namespace PokeRush.EditorTools
{
    public static class BattlePrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Combate";
        private const string PrefabPath = PrefabFolder + "/SistemaDeCombate.prefab";

        [MenuItem("PokeRush/Armar prefab de combate")]
        public static void Build()
        {
            // Los graficos del HUD se recortan de Diseño/: sin eso el prefab
            // saldria con todos los sprites en null.
            if (!BattleUiAssetImporter.ImportAll())
            {
                Debug.LogError("[PrefabCombate] No se encontro Diseño/Graphics/UI/Battle en el repo. " +
                               "El prefab no se genera para no dejarlo a medias.");
                return;
            }

            EnsureFolder(PrefabFolder);

            // Se arma en la escena abierta, se guarda como prefab y se limpia:
            // no queda basura suelta pase lo que pase.
            BattleSystemRoot rig = BattleTestSceneBuilder.BuildCombatRig();
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(rig.gameObject, PrefabPath, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"[PrefabCombate] No se pudo guardar el prefab en {PrefabPath}.");
                    return;
                }

                AssetDatabase.SaveAssets();
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
                Debug.Log($"[PrefabCombate] Listo: {PrefabPath}\n" +
                          "Arrastralo a la escena, desactivalo y apuntale el MapBattleBridge. " +
                          "Los combates se disparan con BattleZoneTrigger o InteractableTrigger.");
            }
            finally
            {
                Object.DestroyImmediate(rig.gameObject);
            }
        }

        // Campos que TIENEN que estar cableados para que el combate sea jugable.
        // Un null aca no rompe la compilacion ni tira excepcion: simplemente el
        // menu no abre y parece que la logica esta rota. Por eso se verifica.
        private static readonly string[] HudCriticos =
        {
            "messageText", "commandPanel", "fightPanel", "teamPanel", "bagPanel",
            "liberationPanel", "predictionView",
            "fightButton", "pokemonButton", "bagButton", "fleeButton",
            "teamBackButton", "bagBackButton",
            "playerHpFill", "enemyHpFill", "playerHpText"
        };

        private static readonly string[] HudArraysCriticos =
        {
            "moveButtons", "teamButtons", "teamNameTexts", "teamHpTexts",
            "bagButtons", "bagNameTexts", "bagQuantityTexts"
        };

        private static readonly string[] ControllerCriticos =
        {
            "battleHudView", "playerPokenView", "enemyPokenView",
            "backgroundRenderer", "battleFxView", "battleCamera"
        };

        // Arma el prefab y despues verifica que quedo entero. Es el metodo que
        // conviene correr en batchmode: deja el asset generado Y avisa si algo
        // quedo sin cablear.
        public static void BuildAndValidate()
        {
            Build();
            Validate();
        }

        [MenuItem("PokeRush/Verificar cableado del combate")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PrefabCombate] No existe {PrefabPath}. " +
                               "Corre primero 'PokeRush > Armar prefab de combate'.");
                return;
            }

            int fallos = 0;

            var controller = prefab.GetComponentInChildren<BattleController>(true);
            fallos += Revisar(controller, "BattleController", ControllerCriticos, null);

            var hud = prefab.GetComponentInChildren<BattleHudView>(true);
            fallos += Revisar(hud, "BattleHudView", HudCriticos, HudArraysCriticos);

            var prediccion = prefab.GetComponentInChildren<BattlePredictionView>(true);
            fallos += Revisar(prediccion, "BattlePredictionView",
                new[] { "titleText", "detailText" }, null);

            var root = prefab.GetComponent<BattleSystemRoot>();
            fallos += Revisar(root, "BattleSystemRoot",
                new[] { "battleController", "battleCamera", "audioListener" }, null);

            var fx = prefab.GetComponentInChildren<BattleFxView>(true);
            fallos += Revisar(fx, "BattleFxView", new[] { "battleCamera" }, null);

            // El layout del combate depende de esto: si alguien lo toca a mano,
            // los Pokens dejan de coincidir con el fondo y con el HUD.
            var cam = prefab.GetComponentInChildren<Camera>(true);
            if (cam != null && (!cam.orthographic || !Mathf.Approximately(cam.orthographicSize, 1.92f)))
            {
                Debug.LogError($"[PrefabCombate] La camara de combate tiene que ser ortografica " +
                               $"con size 1.92 (ahora: orto={cam.orthographic}, size={cam.orthographicSize}).");
                fallos++;
            }

            if (fallos == 0)
                Debug.Log("[PrefabCombate] Cableado OK: el prefab de combate esta completo.");
            else
                Debug.LogError($"[PrefabCombate] {fallos} referencias sin cablear en el prefab.");
        }

        // Devuelve cuantas referencias criticas quedaron en null.
        private static int Revisar(Object componente, string nombre,
            string[] campos, string[] arrays)
        {
            if (componente == null)
            {
                Debug.LogError($"[PrefabCombate] Falta el componente {nombre} en el prefab.");
                return 1;
            }

            int fallos = 0;
            var so = new SerializedObject(componente);

            foreach (string campo in campos)
            {
                SerializedProperty prop = so.FindProperty(campo);
                if (prop == null)
                {
                    Debug.LogError($"[PrefabCombate] {nombre}.{campo} no existe (¿se renombro?).");
                    fallos++;
                }
                else if (prop.objectReferenceValue == null)
                {
                    Debug.LogError($"[PrefabCombate] {nombre}.{campo} quedo sin cablear.");
                    fallos++;
                }
            }

            if (arrays == null) return fallos;

            foreach (string campo in arrays)
            {
                SerializedProperty prop = so.FindProperty(campo);
                if (prop == null || !prop.isArray || prop.arraySize == 0)
                {
                    Debug.LogError($"[PrefabCombate] {nombre}.{campo} esta vacio.");
                    fallos++;
                    continue;
                }

                for (int i = 0; i < prop.arraySize; i++)
                {
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                    Debug.LogError($"[PrefabCombate] {nombre}.{campo}[{i}] quedo sin cablear.");
                    fallos++;
                }
            }

            return fallos;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            string[] parts = assetPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
