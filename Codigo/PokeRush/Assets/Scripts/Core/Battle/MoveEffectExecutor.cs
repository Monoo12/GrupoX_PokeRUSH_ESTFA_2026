using System.Collections.Generic;

// Aplica los efectos de un movimiento mas alla del daño directo: curacion,
// cambios de stats, estados alterados y retroceso. Estatica y pura.
public static class MoveEffectExecutor
{
    // Aplica los efectos del movimiento. Devuelve true si algo surtio efecto
    // (para que los movimientos de estado puros digan "¡Pero falló!" si no).
    // 'field' habilita clima/terrenos/pantallas/trampas (null en tests basicos).
    public static bool ApplyEffects(
        BattlePokenState attacker, BattlePokenState defender, MoveDefinition move,
        List<BattleEvent> events, IBattleRng rng, bool isPureStatusMove,
        BattleModel field = null)
    {
        // Los DamageOnly ignoran su efecto secundario por diseño
        if (move.implementation != MoveImplementation.Full) return false;

        bool anyEffect = false;

        // --- clima / terreno / pantallas / trampas ---
        if (field != null
            && (move.weatherEffect != WeatherKind.None || move.fieldEffect != FieldEffectKind.None))
        {
            if (FieldEffects.ApplyMoveFieldEffect(field, attacker.IsPlayerSide, move, events))
                anyEffect = true;
        }

        // --- curacion del usuario (ej: Recuperacion) ---
        if (move.healPercent > 0 && !attacker.Poken.IsFainted)
        {
            if (attacker.Poken.CurrentHP < attacker.Poken.MaxHP)
            {
                int amount = attacker.Poken.MaxHP * move.healPercent / 100;
                if (amount < 1) amount = 1;
                attacker.Poken.Heal(amount);
                events.Add(BattleEvent.Of(BattleEventKind.HpChanged, attacker.IsPlayerSide,
                    $"¡{attacker.Poken.Definition.pokenName} recuperó HP!"));
                anyEffect = true;
            }
            else if (isPureStatusMove)
            {
                BattleEvent.Add(events, $"El HP de {attacker.Poken.Definition.pokenName} ya está al máximo.");
            }
        }

        // --- cambios de stats ---
        if (move.statChanges != null && move.statChanges.Length > 0)
        {
            // statChangeChance 0 = siempre (movimientos de estado puros)
            bool applies = move.statChangeChance <= 0 || rng.Value() * 100f < move.statChangeChance;
            if (applies)
            {
                foreach (StatChangeEntry entry in move.statChanges)
                {
                    BattlePokenState target = entry.targetSelf ? attacker : defender;
                    if (target.Poken.IsFainted) continue;
                    if (ApplyStatChange(target, entry.stat, entry.stages, events))
                        anyEffect = true;
                }
            }
        }

        // --- estado alterado sobre el rival ---
        if (move.effect != StatusEffect.None && !defender.Poken.IsFainted)
        {
            bool applies = move.effectChance <= 0 || rng.Value() * 100f < move.effectChance;
            if (applies && StatusEffects.TryApplyStatus(defender, move.effect, events, rng,
                    showFailMessage: isPureStatusMove, field))
                anyEffect = true;
        }

        // --- retroceso (pierde su accion si todavia no actuo este turno) ---
        if (move.flinchChance > 0 && !defender.Poken.IsFainted)
        {
            if (rng.Value() * 100f < move.flinchChance)
            {
                defender.Flinched = true;
                anyEffect = true;
            }
        }

        return anyEffect;
    }

    // Aplica un cambio de stage con su mensaje. Devuelve true si cambio algo.
    public static bool ApplyStatChange(
        BattlePokenState target, Stat stat, int stages, List<BattleEvent> events)
    {
        int changed = target.ModifyStage(stat, stages);
        string name = target.Poken.Definition.pokenName;
        string statName = StatDisplayName(stat);

        if (changed == 0)
        {
            BattleEvent.Add(events, stages > 0
                ? $"¡El {statName} de {name} no puede subir más!"
                : $"¡El {statName} de {name} no puede bajar más!");
            return false;
        }

        string verb;
        if (changed >= 2) verb = "subió mucho";
        else if (changed == 1) verb = "subió";
        else if (changed == -1) verb = "bajó";
        else verb = "bajó mucho";

        events.Add(BattleEvent.Of(BattleEventKind.StatStageChanged, target.IsPlayerSide,
            $"¡El {statName} de {name} {verb}!"));
        return true;
    }

    public static string StatDisplayName(Stat stat)
    {
        switch (stat)
        {
            case Stat.Attack: return "Ataque";
            case Stat.Defense: return "Defensa";
            case Stat.SpecialAttack: return "Ataque Especial";
            case Stat.SpecialDefense: return "Defensa Especial";
            case Stat.Speed: return "Velocidad";
            case Stat.Accuracy: return "Precisión";
            case Stat.Evasion: return "Evasión";
            default: return stat.ToString();
        }
    }
}
