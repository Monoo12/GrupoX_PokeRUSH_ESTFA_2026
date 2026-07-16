// Resultado de calcular el daño de un movimiento contra un Poken.
public struct DamageResult
{
    public int Damage;
    public float Effectiveness; // 0, 0.25, 0.5, 1, 2, 4...
    public bool IsCritical;
}

// Calculo de daño estilo Pokemon. Logica pura: solo numeros, no toca la escena.
// Trabaja sobre BattlePokenState para aplicar stat stages, quemadura y habilidades.
public static class DamageCalculator
{
    private const float CritMultiplier = 1.5f;
    private const float StabMultiplier = 1.5f;
    // Roll promedio de daño (0.85..1.0): lo usa la IA para estimar sin azar
    public const float AverageRoll = 0.925f;

    public const int StrugglePower = 50;

    // Probabilidad de critico segun el ratio del movimiento (0 = normal)
    public static float CritChance(int critRateStage)
    {
        if (critRateStage <= 0) return 1f / 16f;
        if (critRateStage == 1) return 1f / 8f;
        return 1f / 2f;
    }

    // Efectividad combinada contra los (uno o dos) tipos del defensor
    public static float DualEffectiveness(PokenType moveType, PokenDefinition defender)
    {
        float eff = TypeChart.Effectiveness(moveType, defender.type);
        if (defender.type2 != PokenType.None && defender.type2 != defender.type)
            eff *= TypeChart.Effectiveness(moveType, defender.type2);
        return eff;
    }

    public static bool HasStab(PokenDefinition attacker, PokenType moveType)
        => moveType != PokenType.None &&
           (moveType == attacker.type ||
            (attacker.type2 != PokenType.None && moveType == attacker.type2));

    // Daño real de un movimiento (con azar: critico + roll 0.85-1.0)
    public static DamageResult Calculate(
        BattlePokenState attacker, BattlePokenState defender, MoveDefinition move, IBattleRng rng)
    {
        if (move.attackType == AttackType.Status || move.power <= 0)
            return new DamageResult { Damage = 0, Effectiveness = 1f, IsCritical = false };

        bool crit = rng.Value() < CritChance(move.critRateStage);
        float roll = rng.Range(0.85f, 1f);
        float abilityMult = AbilityEffects.AttackerDamageMultiplier(attacker, move.type, move.attackType);

        return Compute(attacker, defender, move.power, move.type, move.attackType,
            HasStab(attacker.Poken.Definition, move.type), abilityMult, crit, roll);
    }

    // Forcejeo: potencia 50, sin tipo (efectividad 1 contra todo), fisico.
    public static DamageResult CalculateStruggle(
        BattlePokenState attacker, BattlePokenState defender, IBattleRng rng)
    {
        bool crit = rng.Value() < CritChance(0);
        float roll = rng.Range(0.85f, 1f);
        return Compute(attacker, defender, StrugglePower, PokenType.None, AttackType.Physical,
            stab: false, abilityMult: 1f, crit, roll);
    }

    // Estimacion determinista para la IA: sin critico, roll promedio.
    public static DamageResult CalculateExpected(
        BattlePokenState attacker, BattlePokenState defender, MoveDefinition move)
    {
        if (move.attackType == AttackType.Status || move.power <= 0)
            return new DamageResult { Damage = 0, Effectiveness = 1f, IsCritical = false };

        float abilityMult = AbilityEffects.AttackerDamageMultiplier(attacker, move.type, move.attackType);
        return Compute(attacker, defender, move.power, move.type, move.attackType,
            HasStab(attacker.Poken.Definition, move.type), abilityMult,
            isCritical: false, randomRoll: AverageRoll);
    }

    private static DamageResult Compute(
        BattlePokenState attacker, BattlePokenState defender,
        int power, PokenType moveType, AttackType attackType,
        bool stab, float abilityMult, bool isCritical, float randomRoll)
    {
        // Stats con stages. El critico ignora los stages que perjudican al
        // atacante (ataque bajado, defensa rival subida), como en la saga.
        bool physical = attackType == AttackType.Physical;
        int atkBase = physical ? attacker.Poken.Attack : attacker.Poken.SpecialAttack;
        int defBase = physical ? defender.Poken.Defense : defender.Poken.SpecialDefense;

        int atkStage = attacker.GetStage(physical ? Stat.Attack : Stat.SpecialAttack);
        if (isCritical && atkStage < 0) atkStage = 0;
        int defStage = defender.GetStage(physical ? Stat.Defense : Stat.SpecialDefense);
        if (isCritical && defStage > 0) defStage = 0;

        float atkStat = atkBase * StatStageTable.Multiplier(atkStage);
        float defStat = defBase * StatStageTable.Multiplier(defStage);
        if (defStat < 1f) defStat = 1f;

        // Quemadura: ataque fisico a la mitad, salvo que tenga Agallas (Guts)
        float burnMult = physical
                         && attacker.Poken.CurrentStatus == StatusEffect.Burned
                         && attacker.Ability != AbilityId.Guts
            ? 0.5f : 1f;

        // Daño base (formula principal de la saga).
        float baseDamage = ((2f * attacker.Poken.Level / 5f + 2f) * power * atkStat / defStat) / 50f + 2f;

        float eff = DualEffectiveness(moveType, defender.Poken.Definition);
        float stabMult = stab ? StabMultiplier : 1f;
        float critMult = isCritical ? CritMultiplier : 1f;

        float total = baseDamage * stabMult * eff * critMult * randomRoll * burnMult * abilityMult;

        return new DamageResult
        {
            Effectiveness = eff,
            IsCritical = isCritical,
            // Si no es inmune (eff > 0), siempre hace al menos 1 de daño.
            Damage = eff <= 0f ? 0 : System.Math.Max(1, (int)total)
        };
    }
}
