using UnityEngine;

// Crea la IA que corresponde al perfil del enemigo. Si el asset no tiene
// perfil asignado, genera uno por defecto (Wild para salvajes, Trainer para
// el resto) para que el combate nunca se quede sin IA.
public static class BattleAIFactory
{
    public static IBattleAI Create(AIProfileDefinition profile)
    {
        BattleAIType type = profile != null ? profile.aiType : BattleAIType.Trainer;
        switch (type)
        {
            case BattleAIType.Wild: return new WildPokemonAI();
            case BattleAIType.GymLeader: return new GymLeaderAI();
            case BattleAIType.FinalBoss: return new FinalBossAI();
            default: return new TrainerAI();
        }
    }

    // Perfil en memoria con los valores por defecto del tipo pedido.
    // Se usa cuando el EnemyDefinition/WildEncounter no tiene perfil asignado.
    public static AIProfileDefinition CreateDefaultProfile(BattleAIType type)
    {
        var profile = ScriptableObject.CreateInstance<AIProfileDefinition>();
        profile.name = $"Default_{type}";
        profile.aiType = type;

        switch (type)
        {
            case BattleAIType.Wild:
                profile.randomness = 0.5f;
                profile.avoidsIneffective = false;
                profile.damageWeight = 1f;
                profile.koWeight = 0f;
                profile.statusWeight = 0f;
                profile.setupWeight = 0f;
                profile.healWeight = 0f;
                profile.canSwitch = false;
                break;

            case BattleAIType.GymLeader:
                profile.randomness = 0.1f;
                profile.avoidsIneffective = true;
                profile.damageWeight = 1f;
                profile.koWeight = 0.8f;
                profile.statusWeight = 0.45f;
                profile.setupWeight = 0.5f;
                profile.healWeight = 0.4f;
                profile.canSwitch = true;
                profile.switchGainThreshold = 0.5f;
                profile.minHpPctForSetup = 0.7f;
                break;

            case BattleAIType.FinalBoss:
                profile.randomness = 0.02f;
                profile.avoidsIneffective = true;
                profile.damageWeight = 1f;
                profile.koWeight = 1f;
                profile.statusWeight = 0.5f;
                profile.setupWeight = 0.6f;
                profile.healWeight = 0.5f;
                profile.canSwitch = true;
                profile.switchGainThreshold = 0.25f;
                profile.minHpPctForSetup = 0.5f;
                break;

            default: // Trainer
                profile.randomness = 0.2f;
                profile.avoidsIneffective = true;
                profile.damageWeight = 1f;
                profile.koWeight = 0.6f;
                profile.statusWeight = 0.35f;
                profile.setupWeight = 0f;
                profile.healWeight = 0f;
                profile.canSwitch = false;
                break;
        }

        return profile;
    }
}
