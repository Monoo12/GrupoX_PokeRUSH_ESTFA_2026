using UnityEditor;
using UnityEngine;

// Genera Resources/Definitions/ExperienceTable.asset con la curva medium-fast
// de la saga: XP total para alcanzar el nivel n = n^3.
namespace PokeRush.EditorTools.PokeApi
{
    public static class ExperienceTableGenerator
    {
        private const string AssetPath = "Assets/Resources/Definitions/ExperienceTable.asset";
        private const int MaxLevel = 100;

        [MenuItem("PokeRush/Generar tabla de experiencia")]
        public static void Generate()
        {
            var table = AssetDatabase.LoadAssetAtPath<ExperienceTableDefinition>(AssetPath);
            bool created = table == null;
            if (created)
                table = ScriptableObject.CreateInstance<ExperienceTableDefinition>();

            table.maxLevel = MaxLevel;
            // xpThresholds[nivel - 1] = XP para pasar de 'nivel' a 'nivel + 1'
            // Curva medium-fast: total(n) = n^3  =>  umbral(n) = (n+1)^3 - n^3
            table.xpThresholds = new int[MaxLevel];
            for (int level = 1; level <= MaxLevel; level++)
            {
                long next = (long)(level + 1) * (level + 1) * (level + 1);
                long current = (long)level * level * level;
                table.xpThresholds[level - 1] = (int)(next - current);
            }

            if (created)
                AssetDatabase.CreateAsset(table, AssetPath);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PokeRush] Tabla de experiencia {(created ? "creada" : "actualizada")} en {AssetPath} " +
                      $"(maxLevel {MaxLevel}, curva medium-fast).");
        }
    }
}
