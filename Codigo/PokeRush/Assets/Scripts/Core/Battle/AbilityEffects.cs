using System.Collections.Generic;

// Efectos de las habilidades basicas. Un hook por momento del combate, cada uno
// con un switch central: para agregar una habilidad se suma el caso aca y el
// valor en AbilityId. Estatica y pura.
public static class AbilityEffects
{
    private const float AbsorbHealFraction = 0.25f;
    private const float ContactStatusChance = 0.30f;
    private const int RoughSkinDamageDivisor = 8;
    private const float PinchBoostThreshold = 1f / 3f;
    private const float PinchBoostMultiplier = 1.5f;

    // Al entrar en combate (inicio o cambio). foe puede ser null si todavia no salio.
    public static void OnSwitchIn(BattlePokenState self, BattlePokenState foe, List<BattleEvent> events)
    {
        if (foe == null) return;
        string selfName = self.Poken.Definition.pokenName;
        string foeName = foe.Poken.Definition.pokenName;

        switch (self.Ability)
        {
            case AbilityId.Intimidate:
                int changed = foe.ModifyStage(Stat.Attack, -1);
                events.Add(BattleEvent.Of(BattleEventKind.StatStageChanged, foe.IsPlayerSide,
                    changed != 0
                        ? $"¡La Intimidación de {selfName} bajó el Ataque de {foeName}!"
                        : $"La Intimidación de {selfName} no pudo bajar más el Ataque de {foeName}."));
                break;
        }
    }

    // Inmunidades/absorciones por habilidad ANTES de calcular el daño.
    // Devuelve true si el movimiento queda anulado (no hace daño ni efectos).
    public static bool CheckAbsorbOrImmunity(
        BattlePokenState defender, PokenType moveType, List<BattleEvent> events)
    {
        string name = defender.Poken.Definition.pokenName;

        switch (defender.Ability)
        {
            case AbilityId.Levitate when moveType == PokenType.Ground:
                BattleEvent.Add(events, $"¡{name} levita! No le afecta...");
                return true;

            case AbilityId.VoltAbsorb when moveType == PokenType.Electric:
                AbsorbHeal(defender, $"¡{name} absorbió la electricidad y recuperó HP!", events);
                return true;

            case AbilityId.WaterAbsorb when moveType == PokenType.Water:
                AbsorbHeal(defender, $"¡{name} absorbió el agua y recuperó HP!", events);
                return true;

            case AbilityId.FlashFire when moveType == PokenType.Fire:
                defender.FlashFireBoosted = true;
                BattleEvent.Add(events, $"¡Absorber Fuego potenció los movimientos de fuego de {name}!");
                return true;
        }

        return false;
    }

    private static void AbsorbHeal(BattlePokenState state, string message, List<BattleEvent> events)
    {
        int amount = (int)(state.Poken.MaxHP * AbsorbHealFraction);
        if (amount < 1) amount = 1;
        state.Poken.Heal(amount);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, state.IsPlayerSide, message));
    }

    // Multiplicador de daño del atacante por su habilidad.
    public static float AttackerDamageMultiplier(
        BattlePokenState attacker, PokenType moveType, AttackType attackType)
    {
        float mult = 1f;
        bool inPinch = attacker.Poken.CurrentHP <= attacker.Poken.MaxHP * PinchBoostThreshold;

        switch (attacker.Ability)
        {
            // Agallas: fisico x1.5 con cualquier estado (la reduccion de quemadura
            // se anula aparte, en DamageCalculator)
            case AbilityId.Guts when attackType == AttackType.Physical
                                     && attacker.Poken.CurrentStatus != StatusEffect.None:
                mult *= 1.5f;
                break;

            case AbilityId.Blaze when inPinch && moveType == PokenType.Fire:
            case AbilityId.Torrent when inPinch && moveType == PokenType.Water:
            case AbilityId.Overgrow when inPinch && moveType == PokenType.Grass:
            case AbilityId.Swarm when inPinch && moveType == PokenType.Bug:
                mult *= PinchBoostMultiplier;
                break;
        }

        // Flash Fire activado: movimientos de fuego x1.5 (independiente del pinch)
        if (attacker.FlashFireBoosted && moveType == PokenType.Fire)
            mult *= 1.5f;

        return mult;
    }

    // Robustez: con HP lleno, aguanta cualquier golpe letal con 1 HP.
    // Devuelve el daño ajustado y avisa si se activo via 'endured'.
    public static int ModifyIncomingDamage(BattlePokenState defender, int damage, out bool endured)
    {
        endured = false;
        if (defender.Ability == AbilityId.Sturdy
            && defender.Poken.CurrentHP == defender.Poken.MaxHP
            && damage >= defender.Poken.CurrentHP)
        {
            endured = true;
            return defender.Poken.CurrentHP - 1;
        }
        return damage;
    }

    // Tras recibir un golpe de contacto: castigos al atacante.
    public static void OnContactReceived(
        BattlePokenState attacker, BattlePokenState defender,
        List<BattleEvent> events, IBattleRng rng)
    {
        if (attacker.Poken.IsFainted) return;
        string defenderName = defender.Poken.Definition.pokenName;
        string attackerName = attacker.Poken.Definition.pokenName;

        switch (defender.Ability)
        {
            case AbilityId.Static:
                if (rng.Value() < ContactStatusChance)
                    StatusEffects.TryApplyStatus(attacker, StatusEffect.Paralyzed, events, rng, showFailMessage: false);
                break;

            case AbilityId.PoisonPoint:
                if (rng.Value() < ContactStatusChance)
                    StatusEffects.TryApplyStatus(attacker, StatusEffect.Poisoned, events, rng, showFailMessage: false);
                break;

            case AbilityId.FlameBody:
                if (rng.Value() < ContactStatusChance)
                    StatusEffects.TryApplyStatus(attacker, StatusEffect.Burned, events, rng, showFailMessage: false);
                break;

            case AbilityId.RoughSkin:
                int damage = attacker.Poken.MaxHP / RoughSkinDamageDivisor;
                if (damage < 1) damage = 1;
                attacker.Poken.TakeDamage(damage);
                events.Add(BattleEvent.Of(BattleEventKind.HpChanged, attacker.IsPlayerSide,
                    $"¡La Piel Tosca de {defenderName} hirió a {attackerName}!"));
                break;
        }
    }

    // Final del turno (gancho tambien pensado para clima en el futuro).
    public static void OnTurnEnd(BattlePokenState self, List<BattleEvent> events)
    {
        if (self.Poken.IsFainted) return;

        switch (self.Ability)
        {
            case AbilityId.SpeedBoost:
                if (self.ModifyStage(Stat.Speed, 1) != 0)
                    events.Add(BattleEvent.Of(BattleEventKind.StatStageChanged, self.IsPlayerSide,
                        $"¡El Impulso subió la Velocidad de {self.Poken.Definition.pokenName}!"));
                break;
        }
    }
}
