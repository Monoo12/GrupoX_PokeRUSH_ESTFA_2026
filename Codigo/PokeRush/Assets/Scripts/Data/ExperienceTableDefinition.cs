using UnityEngine;

[CreateAssetMenu(fileName = "ExperienceTable", menuName = "PokeRush/ExperienceTableDefinition")]
public class ExperienceTableDefinition : ScriptableObject
{
    [Header("Configuracion")]
    public int maxLevel = 100;

    public int[] xpThresholds;

    // XP necesaria para subir del nivel dado al siguiente. Retorna -1 si ya es nivel maximo o fuera de rango.
    public int GetXPForLevel(int level)
    {
        int index = level - 1;
        if (index < 0 || index >= xpThresholds.Length) return -1;
        return xpThresholds[index];
    }
}