using System.Collections.Generic;

// Logica de estados alterados: aplicacion, chequeo antes de actuar y daño
// residual de fin de turno. Estatica y pura: emite eventos, no toca la escena.
public static class StatusEffects
{
    public const float ParalysisSkipChance = 0.25f;
    public const float ThawChance = 0.20f;
    public const float ConfusionSelfHitChance = 1f / 3f;
    public const int ConfusionSelfHitPower = 40;
    // Fracciones del MaxHP por turno
    public const int BurnDamageDivisor = 16;
    public const int PoisonDamageDivisor = 8;

    // Intenta aplicar un estado. Devuelve false si no pudo (ya tiene estado,
    // es inmune por tipo, etc.). showFailMessage: true para movimientos de
    // estado puros (Onda Trueno fallida), false para efectos secundarios.
    public static bool TryApplyStatus(
        BattlePokenState target, StatusEffect status,
        List<BattleEvent> events, IBattleRng rng, bool showFailMessage,
        BattleModel field = null)
    {
        string name = target.Poken.Definition.pokenName;

        // Terrenos que protegen a los Pokens en el suelo
        if (field != null && FieldEffects.IsGrounded(target))
        {
            if (field.CurrentTerrain == FieldTerrain.Misty)
            {
                if (showFailMessage) BattleEvent.Add(events, $"¡La niebla protege a {name}!");
                return false;
            }
            if (field.CurrentTerrain == FieldTerrain.Electric && status == StatusEffect.Asleep)
            {
                if (showFailMessage) BattleEvent.Add(events, $"¡El Campo Eléctrico impide que {name} se duerma!");
                return false;
            }
        }

        // La confusion es volatil: convive con los estados persistentes
        if (status == StatusEffect.Confused)
        {
            if (target.IsConfused)
            {
                if (showFailMessage) BattleEvent.Add(events, $"¡{name} ya está confuso!");
                return false;
            }
            target.IsConfused = true;
            target.ConfusionTurnsLeft = rng.Range(1, 5); // 1-4 turnos
            events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, IsPlayerSide(target), $"¡{name} está confuso!"));
            return true;
        }

        if (target.Poken.CurrentStatus != StatusEffect.None)
        {
            if (showFailMessage) BattleEvent.Add(events, $"¡{name} ya tiene un problema de estado!");
            return false;
        }

        if (IsImmuneByType(status, target))
        {
            if (showFailMessage) BattleEvent.Add(events, $"No afecta a {name}...");
            return false;
        }

        target.Poken.SetStatus(status);
        if (status == StatusEffect.Asleep)
            target.SleepTurnsLeft = rng.Range(1, 4); // 1-3 turnos

        events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, IsPlayerSide(target), StatusAppliedMessage(name, status)));
        return true;
    }

    // Inmunidades por tipo de la saga, contra los tipos ACTUALES del que esta
    // en el campo: con el tipo cambiado por la ruleta del jefe del casino, un
    // Poken que paso a ser de Fuego no se puede quemar.
    public static bool IsImmuneByType(StatusEffect status, BattlePokenState target)
        => target != null && IsImmuneByType(status, target.Tipo1, target.Tipo2);

    public static bool IsImmuneByType(StatusEffect status, PokenDefinition def)
        => IsImmuneByType(status, def.type, def.type2);

    private static bool IsImmuneByType(StatusEffect status, PokenType tipo1, PokenType tipo2)
    {
        bool HasType(PokenType t) => tipo1 == t || tipo2 == t;
        switch (status)
        {
            case StatusEffect.Burned: return HasType(PokenType.Fire);
            case StatusEffect.Paralyzed: return HasType(PokenType.Electric);
            case StatusEffect.Poisoned: return HasType(PokenType.Poison) || HasType(PokenType.Steel);
            case StatusEffect.Frozen: return HasType(PokenType.Ice);
            default: return false;
        }
    }

    // Chequeos previos a la accion: retroceso, sueño, congelado, paralisis,
    // confusion. Devuelve false si el Poken pierde su accion este turno.
    public static bool CanAct(BattlePokenState actor, List<BattleEvent> events, IBattleRng rng)
    {
        string name = actor.Poken.Definition.pokenName;

        if (actor.Flinched)
        {
            actor.Flinched = false;
            BattleEvent.Add(events, $"¡{name} retrocedió y no pudo moverse!");
            return false;
        }

        switch (actor.Poken.CurrentStatus)
        {
            case StatusEffect.Asleep:
                actor.SleepTurnsLeft--;
                if (actor.SleepTurnsLeft <= 0)
                {
                    actor.Poken.CureStatus();
                    events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, IsPlayerSide(actor), $"¡{name} se despertó!"));
                }
                else
                {
                    BattleEvent.Add(events, $"{name} está profundamente dormido...");
                    return false;
                }
                break;

            case StatusEffect.Frozen:
                if (rng.Value() < ThawChance)
                {
                    actor.Poken.CureStatus();
                    events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, IsPlayerSide(actor), $"¡{name} se descongeló!"));
                }
                else
                {
                    BattleEvent.Add(events, $"{name} está congelado...");
                    return false;
                }
                break;

            case StatusEffect.Paralyzed:
                if (rng.Value() < ParalysisSkipChance)
                {
                    BattleEvent.Add(events, $"¡{name} está paralizado! No se puede mover.");
                    return false;
                }
                break;
        }

        if (actor.IsConfused)
        {
            actor.ConfusionTurnsLeft--;
            if (actor.ConfusionTurnsLeft <= 0)
            {
                actor.IsConfused = false;
                BattleEvent.Add(events, $"¡{name} ya no está confuso!");
            }
            else
            {
                BattleEvent.Add(events, $"{name} está confuso...");
                if (rng.Value() < ConfusionSelfHitChance)
                {
                    int damage = ConfusionSelfDamage(actor);
                    actor.Poken.TakeDamage(damage);
                    events.Add(BattleEvent.Of(BattleEventKind.HpChanged, IsPlayerSide(actor),
                        "¡Está tan confuso que se hirió a sí mismo!"));
                    return false;
                }
            }
        }

        return true;
    }

    // Autogolpe de confusion: fisico sin tipo, potencia 40, contra su propia
    // defensa (con stages), sin STAB ni critico.
    public static int ConfusionSelfDamage(BattlePokenState actor)
    {
        float atk = actor.EffectiveAttack;
        float def = actor.EffectiveDefense;
        if (def < 1f) def = 1f;
        float baseDamage = ((2f * actor.Poken.Level / 5f + 2f) * ConfusionSelfHitPower * atk / def) / 50f + 2f;
        int damage = (int)baseDamage;
        return damage < 1 ? 1 : damage;
    }

    // Daño residual al final del turno (quemadura, veneno).
    public static void OnTurnEnd(BattlePokenState state, List<BattleEvent> events)
    {
        if (state.Poken.IsFainted) return;
        string name = state.Poken.Definition.pokenName;

        switch (state.Poken.CurrentStatus)
        {
            case StatusEffect.Burned:
                ApplyResidual(state, state.Poken.MaxHP / BurnDamageDivisor, $"¡{name} sufre por su quemadura!", events);
                break;
            case StatusEffect.Poisoned:
                ApplyResidual(state, state.Poken.MaxHP / PoisonDamageDivisor, $"¡{name} sufre por el veneno!", events);
                break;
        }
    }

    private static void ApplyResidual(BattlePokenState state, int damage, string message, List<BattleEvent> events)
    {
        if (damage < 1) damage = 1;
        state.Poken.TakeDamage(damage);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, IsPlayerSide(state), message));
    }

    private static string StatusAppliedMessage(string name, StatusEffect status)
    {
        switch (status)
        {
            case StatusEffect.Burned: return $"¡{name} se quemó!";
            case StatusEffect.Frozen: return $"¡{name} se congeló!";
            case StatusEffect.Paralyzed: return $"¡{name} está paralizado! Quizás no pueda moverse.";
            case StatusEffect.Poisoned: return $"¡{name} fue envenenado!";
            case StatusEffect.Asleep: return $"¡{name} se durmió!";
            default: return $"{name} sufre {status}.";
        }
    }

    private static bool IsPlayerSide(BattlePokenState state) => state.IsPlayerSide;
}
