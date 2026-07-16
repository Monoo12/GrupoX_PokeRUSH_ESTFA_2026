using UnityEngine;

// Define un encuentro con un Poken salvaje: especie, rango de nivel y perfil de IA.
// A diferencia de EnemyDefinition/BossDefinition, no es un NPC: no tiene equipo,
// dialogo ni recompensa de dinero.
[CreateAssetMenu(fileName = "NewWildEncounter", menuName = "PokeRush/WildEncounterDefinition")]
public class WildEncounterDefinition : ScriptableObject
{
    [Header("Especie")]
    public PokenDefinition species;

    [Header("Nivel")]
    public int minLevel = 2;
    public int maxLevel = 5;

    [Header("IA")]
    [Tooltip("Normalmente el perfil Wild. Si queda vacio, BattleAIFactory usa el comportamiento Wild por defecto.")]
    public AIProfileDefinition aiProfile;
}
