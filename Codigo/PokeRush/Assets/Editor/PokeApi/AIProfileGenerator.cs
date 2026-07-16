using UnityEditor;
using UnityEngine;

// Genera los 5 assets de AIProfileDefinition en Resources/Definitions/AIProfiles/
// con los pesos de diseño. Re-ejecutable: actualiza los existentes.
namespace PokeRush.EditorTools.PokeApi
{
    public static class AIProfileGenerator
    {
        private const string Folder = "Assets/Resources/Definitions/AIProfiles";

        [MenuItem("PokeRush/Generar perfiles de IA")]
        public static void Generate()
        {
            EnsureFolder();

            // name, tipo, randomness, avoidsIneffective, W_dmg, W_ko, W_status, W_setup, W_heal,
            // canSwitch, switchThreshold, minHpSetup
            CreateOrUpdate("Wild", BattleAIType.Wild, 0.5f, false, 1f, 0f, 0f, 0f, 0f, false, 0.5f, 0.7f);
            CreateOrUpdate("Trainer", BattleAIType.Trainer, 0.2f, true, 1f, 0.6f, 0.35f, 0f, 0f, false, 0.5f, 0.7f);
            CreateOrUpdate("GymLeader", BattleAIType.GymLeader, 0.1f, true, 1f, 0.8f, 0.45f, 0.5f, 0.4f, true, 0.5f, 0.7f);
            CreateOrUpdate("Elite", BattleAIType.GymLeader, 0.05f, true, 1f, 0.9f, 0.5f, 0.6f, 0.5f, true, 0.35f, 0.6f);
            CreateOrUpdate("FinalBoss", BattleAIType.FinalBoss, 0.02f, true, 1f, 1f, 0.5f, 0.6f, 0.5f, true, 0.25f, 0.5f);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PokeRush] Perfiles de IA generados/actualizados en {Folder}.");
        }

        private static void CreateOrUpdate(
            string name, BattleAIType type, float randomness, bool avoidsIneffective,
            float damageWeight, float koWeight, float statusWeight, float setupWeight, float healWeight,
            bool canSwitch, float switchGainThreshold, float minHpPctForSetup)
        {
            string path = $"{Folder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(path);
            bool created = profile == null;
            if (created) profile = ScriptableObject.CreateInstance<AIProfileDefinition>();

            profile.aiType = type;
            profile.randomness = randomness;
            profile.avoidsIneffective = avoidsIneffective;
            profile.damageWeight = damageWeight;
            profile.koWeight = koWeight;
            profile.statusWeight = statusWeight;
            profile.setupWeight = setupWeight;
            profile.healWeight = healWeight;
            profile.canSwitch = canSwitch;
            profile.switchGainThreshold = switchGainThreshold;
            profile.minHpPctForSetup = minHpPctForSetup;

            if (created) AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolder()
        {
            string[] parts = Folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
