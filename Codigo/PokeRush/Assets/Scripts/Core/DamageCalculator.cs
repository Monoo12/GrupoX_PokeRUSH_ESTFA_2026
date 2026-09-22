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

    // Las dos mismas cuentas pero contra los tipos ACTUALES del Poken en el
    // campo, que pueden no ser los de su especie: la ruleta del jefe del casino
    // le cambia el tipo al que esta peleando (ver BattlePokenState.TipoCambiado).
    //
    // Las versiones de arriba, que toman la PokenDefinition, siguen existiendo
    // para quien mira un Poken que NO esta en el campo (la IA evaluando a quien
    // mandar): ahi no hay estado volatil que consultar.
    public static float DualEffectiveness(PokenType moveType, BattlePokenState defender)
    {
        float eff = TypeChart.Effectiveness(moveType, defender.Tipo1);
        if (defender.Tipo2 != PokenType.None && defender.Tipo2 != defender.Tipo1)
            eff *= TypeChart.Effectiveness(moveType, defender.Tipo2);
        return eff;
    }

    public static bool HasStab(BattlePokenState attacker, PokenType moveType)
        => attacker.EsDeTipo(moveType);

    // Daño real de un movimiento (con azar: critico + roll 0.85-1.0).
    // 'field' (opcional) aplica clima, terreno y pantallas del defensor.
    public static DamageResult Calculate(
        BattlePokenState attacker, BattlePokenState defender, MoveDefinition move,
        IBattleRng rng, BattleModel field = null)
    {
        if (move.attackType == AttackType.Status || move.power <= 0)
            return new DamageResult { Damage = 0, Effectiveness = 1f, IsCritical = false };

        bool crit = rng.Value() < CritChance(move.critRateStage);
        float roll = rng.Range(0.85f, 1f);
        float abilityMult = AbilityEffects.AttackerDamageMultiplier(attacker, move.type, move.attackType);

        return Compute(attacker, defender, move.power, move.type, move.attackType,
            HasStab(attacker, move.type), abilityMult, crit, roll, field);
    }

    // Forcejeo: potencia 50, sin tipo (efectividad 1 contra todo), fisico.
    public static DamageResult CalculateStruggle(
        BattlePokenState attacker, BattlePokenState defender, IBattleRng rng, BattleModel field = null)
    {
        bool crit = rng.Value() < CritChance(0);
        float roll = rng.Range(0.85f, 1f);
        return Compute(attacker, defender, StrugglePower, PokenType.None, AttackType.Physical,
            stab: false, abilityMult: 1f, crit, roll, field);
    }

    // Estimacion determinista para la IA: sin critico, roll promedio.
    public static DamageResult CalculateExpected(
        BattlePokenState attacker, BattlePokenState defender, MoveDefinition move,
        BattleModel field = null)
    {
        if (move.attackType == AttackType.Status || move.power <= 0)
            return new DamageResult { Damage = 0, Effectiveness = 1f, IsCritical = false };

        float abilityMult = AbilityEffects.AttackerDamageMultiplier(attacker, move.type, move.attackType);
        return Compute(attacker, defender, move.power, move.type, move.attackType,
            HasStab(attacker.Poken.Definition, move.type), abilityMult,
            isCritical: false, randomRoll: AverageRoll, field);
    }

    // Multiplicador de clima/terreno/pantallas sobre el daño
    private static float FieldMultiplier(
        BattleModel field, BattlePokenState attacker, BattlePokenState defender,
        PokenType moveType, AttackType attackType, bool isCritical)
    {
        if (field == null) return 1f;
        float mult = 1f;

        // Clima
        switch (field.CurrentWeather)
        {
            case WeatherKind.Rain:
                if (moveType == PokenType.Water) mult *= 1.5f;
                else if (moveType == PokenType.Fire) mult *= 0.5f;
                break;
            case WeatherKind.Sun:
                if (moveType == PokenType.Fire) mult *= 1.5f;
                else if (moveType == PokenType.Water) mult *= 0.5f;
                break;
        }

        // Terreno: potencia si el ATACANTE toca el suelo
        if (FieldEffects.IsGrounded(attacker))
        {
            if (field.CurrentTerrain == FieldTerrain.Electric && moveType == PokenType.Electric) mult *= 1.3f;
            else if (field.CurrentTerrain == FieldTerrain.Psychic && moveType == PokenType.Psychic) mult *= 1.3f;
            else if (field.CurrentTerrain == FieldTerrain.Grassy && moveType == PokenType.Grass) mult *= 1.3f;
        }
        // Campo Nebuloso: protege a los que tocan el suelo del daño Dragon
        if (field.CurrentTerrain == FieldTerrain.Misty && moveType == PokenType.Dragon
            && FieldEffects.IsGrounded(defender))
            mult *= 0.5f;

        // Pantallas del lado defensor (los criticos las ignoran, como en la saga)
        if (!isCritical)
        {
            SideState defenderSide = field.SideOf(defender.IsPlayerSide);
            bool physical = attackType == AttackType.Physical;
            if (defenderSide.HasAuroraVeil
                || (physical && defenderSide.HasReflect)
                || (!physical && defenderSide.HasLightScreen))
                mult *= 0.5f;
        }

        return mult;
    }

    private static DamageResult Compute(
        BattlePokenState attacker, BattlePokenState defender,
        int power, PokenType moveType, AttackType attackType,
        bool stab, float abilityMult, bool isCritical, float randomRoll,
        BattleModel field = null)
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

        // Tormenta de arena: Defensa Especial x1.5 para los tipo Roca
        if (field != null && field.CurrentWeather == WeatherKind.Sandstorm && !physical
            && defender.EsDeTipo(PokenType.Rock))
            defStat *= 1.5f;

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

        float fieldMult = FieldMultiplier(field, attacker, defender, moveType, attackType, isCritical);
        float total = baseDamage * stabMult * eff * critMult * randomRoll * burnMult * abilityMult * fieldMult;

        return new DamageResult
        {
            Effectiveness = eff,
            IsCritical = isCritical,
            // Si no es inmune (eff > 0), siempre hace al menos 1 de daño.
            Damage = eff <= 0f ? 0 : System.Math.Max(1, (int)total)
        };
    }
}
