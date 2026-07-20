using System.Collections.Generic;

// Climas, terrenos, pantallas y trampas de entrada. Estatico y puro: muta el
// BattleModel y emite eventos; el TurnEngine lo invoca en los momentos justos.
public static class FieldEffects
{
    public const int WeatherDuration = 5;
    public const int TerrainDuration = 5;
    public const int ScreenDuration = 5;
    public const int WeatherResidualDivisor = 16;
    public const int GrassyHealDivisor = 16;

    // "En el suelo": le afectan terrenos y trampas (los voladores/levitadores no)
    public static bool IsGrounded(BattlePokenState state)
    {
        PokenDefinition def = state.Poken.Definition;
        if (def.type == PokenType.Flying || def.type2 == PokenType.Flying) return false;
        return state.Ability != AbilityId.Levitate;
    }

    // ---------- aplicar el efecto de un movimiento ----------

    // Instala clima/terreno/pantalla/trampa. Devuelve true si surtio efecto
    // (false = ya estaba activo / al maximo, para el "¡Pero fallo!").
    public static bool ApplyMoveFieldEffect(
        BattleModel model, bool attackerIsPlayer, MoveDefinition move, List<BattleEvent> events)
    {
        bool applied = false;

        if (move.weatherEffect != WeatherKind.None && model.CurrentWeather != move.weatherEffect)
        {
            model.SetWeather(move.weatherEffect, WeatherDuration);
            events.Add(BattleEvent.Of(BattleEventKind.WeatherChanged, attackerIsPlayer,
                WeatherStartMessage(move.weatherEffect)));
            applied = true;
        }

        switch (move.fieldEffect)
        {
            case FieldEffectKind.None:
                break;

            // --- pantallas: protegen el lado del USUARIO ---
            case FieldEffectKind.Reflect:
            {
                SideState side = model.SideOf(attackerIsPlayer);
                if (!side.HasReflect)
                {
                    side.ReflectTurns = ScreenDuration;
                    BattleEvent.Add(events, attackerIsPlayer
                        ? "¡Reflejo protege a tu equipo de los ataques físicos!"
                        : "¡Reflejo protege al equipo rival de los ataques físicos!");
                    applied = true;
                }
                break;
            }
            case FieldEffectKind.LightScreen:
            {
                SideState side = model.SideOf(attackerIsPlayer);
                if (!side.HasLightScreen)
                {
                    side.LightScreenTurns = ScreenDuration;
                    BattleEvent.Add(events, attackerIsPlayer
                        ? "¡Pantalla de Luz protege a tu equipo de los ataques especiales!"
                        : "¡Pantalla de Luz protege al equipo rival de los ataques especiales!");
                    applied = true;
                }
                break;
            }
            case FieldEffectKind.AuroraVeil:
            {
                SideState side = model.SideOf(attackerIsPlayer);
                // Como en la saga: solo funciona mientras graniza
                if (model.CurrentWeather == WeatherKind.Hail && !side.HasAuroraVeil)
                {
                    side.AuroraVeilTurns = ScreenDuration;
                    BattleEvent.Add(events, "¡El Velo Aurora reduce el daño recibido!");
                    applied = true;
                }
                break;
            }

            // --- trampas: se colocan en el lado RIVAL ---
            case FieldEffectKind.StealthRock:
            {
                SideState foeSide = model.SideOf(!attackerIsPlayer);
                if (!foeSide.StealthRock)
                {
                    foeSide.StealthRock = true;
                    BattleEvent.Add(events, "¡Piedras puntiagudas flotan alrededor del equipo rival!");
                    applied = true;
                }
                break;
            }
            case FieldEffectKind.Spikes:
            {
                SideState foeSide = model.SideOf(!attackerIsPlayer);
                if (foeSide.SpikesLayers < 3)
                {
                    foeSide.SpikesLayers++;
                    BattleEvent.Add(events, "¡Púas esparcidas a los pies del equipo rival!");
                    applied = true;
                }
                break;
            }
            case FieldEffectKind.ToxicSpikes:
            {
                SideState foeSide = model.SideOf(!attackerIsPlayer);
                if (foeSide.ToxicSpikesLayers < 2)
                {
                    foeSide.ToxicSpikesLayers++;
                    BattleEvent.Add(events, "¡Púas venenosas esparcidas a los pies del equipo rival!");
                    applied = true;
                }
                break;
            }
            case FieldEffectKind.StickyWeb:
            {
                SideState foeSide = model.SideOf(!attackerIsPlayer);
                if (!foeSide.StickyWeb)
                {
                    foeSide.StickyWeb = true;
                    BattleEvent.Add(events, "¡Una red viscosa se extiende a los pies del equipo rival!");
                    applied = true;
                }
                break;
            }

            // --- terrenos ---
            case FieldEffectKind.ElectricTerrain:
                applied |= TrySetTerrain(model, attackerIsPlayer, FieldTerrain.Electric,
                    "¡Una corriente eléctrica recorre el campo!", events);
                break;
            case FieldEffectKind.PsychicTerrain:
                applied |= TrySetTerrain(model, attackerIsPlayer, FieldTerrain.Psychic,
                    "¡El campo se volvió extraño!", events);
                break;
            case FieldEffectKind.GrassyTerrain:
                applied |= TrySetTerrain(model, attackerIsPlayer, FieldTerrain.Grassy,
                    "¡La hierba cubrió el campo!", events);
                break;
            case FieldEffectKind.MistyTerrain:
                applied |= TrySetTerrain(model, attackerIsPlayer, FieldTerrain.Misty,
                    "¡La niebla envolvió el campo!", events);
                break;
        }

        return applied;
    }

    private static bool TrySetTerrain(BattleModel model, bool attackerIsPlayer,
        FieldTerrain terrain, string message, List<BattleEvent> events)
    {
        if (model.CurrentTerrain == terrain) return false;
        model.SetTerrain(terrain, TerrainDuration);
        events.Add(BattleEvent.Of(BattleEventKind.TerrainChanged, attackerIsPlayer, message));
        return true;
    }

    // Para la IA: ¿este movimiento de campo no aportaria nada ahora?
    public static bool IsRedundant(BattleModel model, bool attackerIsPlayer, MoveDefinition move)
    {
        if (move.weatherEffect != WeatherKind.None)
            return model.CurrentWeather == move.weatherEffect;

        SideState ownSide = model.SideOf(attackerIsPlayer);
        SideState foeSide = model.SideOf(!attackerIsPlayer);
        switch (move.fieldEffect)
        {
            case FieldEffectKind.Reflect: return ownSide.HasReflect;
            case FieldEffectKind.LightScreen: return ownSide.HasLightScreen;
            case FieldEffectKind.AuroraVeil:
                return ownSide.HasAuroraVeil || model.CurrentWeather != WeatherKind.Hail;
            case FieldEffectKind.StealthRock: return foeSide.StealthRock;
            case FieldEffectKind.Spikes: return foeSide.SpikesLayers >= 3;
            case FieldEffectKind.ToxicSpikes: return foeSide.ToxicSpikesLayers >= 2;
            case FieldEffectKind.StickyWeb: return foeSide.StickyWeb;
            case FieldEffectKind.ElectricTerrain: return model.CurrentTerrain == FieldTerrain.Electric;
            case FieldEffectKind.PsychicTerrain: return model.CurrentTerrain == FieldTerrain.Psychic;
            case FieldEffectKind.GrassyTerrain: return model.CurrentTerrain == FieldTerrain.Grassy;
            case FieldEffectKind.MistyTerrain: return model.CurrentTerrain == FieldTerrain.Misty;
            default: return false;
        }
    }

    // ---------- trampas al entrar un Poken ----------

    public static void OnSwitchIn(BattleModel model, bool isPlayerSide,
        List<BattleEvent> events, IBattleRng rng)
    {
        SideState side = model.SideOf(isPlayerSide);
        BattlePokenState entrant = isPlayerSide ? model.PlayerState : model.EnemyState;
        PokenModel poken = entrant.Poken;
        string name = poken.Definition.pokenName;

        // Trampa Rocas: daño segun la efectividad de Roca contra el que entra
        if (side.StealthRock && !poken.IsFainted)
        {
            float eff = DamageCalculator.DualEffectiveness(PokenType.Rock, poken.Definition);
            int damage = (int)(poken.MaxHP * eff / 8f);
            if (damage < 1) damage = 1;
            poken.TakeDamage(damage);
            events.Add(BattleEvent.Of(BattleEventKind.HpChanged, isPlayerSide,
                $"¡Las piedras puntiagudas hirieron a {name}!"));
        }

        bool grounded = IsGrounded(entrant);

        // Puas: 1/8, 1/6, 1/4 segun capas (solo si toca el suelo)
        if (side.SpikesLayers > 0 && grounded && !poken.IsFainted)
        {
            int divisor = side.SpikesLayers == 1 ? 8 : (side.SpikesLayers == 2 ? 6 : 4);
            int damage = poken.MaxHP / divisor;
            if (damage < 1) damage = 1;
            poken.TakeDamage(damage);
            events.Add(BattleEvent.Of(BattleEventKind.HpChanged, isPlayerSide,
                $"¡{name} se lastimó con las púas!"));
        }

        // Puas Toxicas: envenenan; los tipo Veneno en el suelo las absorben
        if (side.ToxicSpikesLayers > 0 && grounded && !poken.IsFainted)
        {
            bool isPoisonType = poken.Definition.type == PokenType.Poison
                                || poken.Definition.type2 == PokenType.Poison;
            if (isPoisonType)
            {
                side.ToxicSpikesLayers = 0;
                BattleEvent.Add(events, $"¡{name} absorbió las púas venenosas!");
            }
            else
            {
                StatusEffects.TryApplyStatus(entrant, StatusEffect.Poisoned, events, rng,
                    showFailMessage: false, model);
            }
        }

        // Red Viscosa: -1 Velocidad al entrar
        if (side.StickyWeb && grounded && !poken.IsFainted)
        {
            BattleEvent.Add(events, $"¡{name} quedó atrapado en la red viscosa!");
            MoveEffectExecutor.ApplyStatChange(entrant, Stat.Speed, -1, events);
        }
    }

    // ---------- fin de turno ----------

    // Daño residual del clima, curacion de hierba y descuento de contadores.
    public static void OnTurnEnd(BattleModel model, List<BattleEvent> events)
    {
        // Daño residual del clima (a los no inmunes)
        if (model.CurrentWeather == WeatherKind.Sandstorm || model.CurrentWeather == WeatherKind.Hail)
        {
            BattleEvent.Add(events, model.CurrentWeather == WeatherKind.Sandstorm
                ? "¡La tormenta de arena arrecia!"
                : "¡El granizo sigue cayendo!");
            ApplyWeatherResidual(model.PlayerState, model.CurrentWeather, events);
            ApplyWeatherResidual(model.EnemyState, model.CurrentWeather, events);
        }

        // Campo de Hierba: cura a los que tocan el suelo
        if (model.CurrentTerrain == FieldTerrain.Grassy)
        {
            GrassyHeal(model.PlayerState, events);
            GrassyHeal(model.EnemyState, events);
        }

        // Contadores: clima
        if (model.CurrentWeather != WeatherKind.None)
        {
            model.TickWeather();
            if (model.WeatherTurnsLeft <= 0)
            {
                WeatherKind ended = model.CurrentWeather;
                model.SetWeather(WeatherKind.None, 0);
                events.Add(BattleEvent.Of(BattleEventKind.WeatherChanged, true,
                    WeatherEndMessage(ended)));
            }
        }

        // Contadores: terreno
        if (model.CurrentTerrain != FieldTerrain.None)
        {
            model.TickTerrain();
            if (model.TerrainTurnsLeft <= 0)
            {
                model.SetTerrain(FieldTerrain.None, 0);
                events.Add(BattleEvent.Of(BattleEventKind.TerrainChanged, true,
                    "El campo volvió a la normalidad."));
            }
        }

        // Contadores: pantallas de ambos lados
        TickScreens(model.PlayerSide, isPlayer: true, events);
        TickScreens(model.EnemySide, isPlayer: false, events);
    }

    private static void ApplyWeatherResidual(BattlePokenState state, WeatherKind weather, List<BattleEvent> events)
    {
        if (state.Poken.IsFainted) return;
        PokenDefinition def = state.Poken.Definition;

        bool immune;
        if (weather == WeatherKind.Sandstorm)
            immune = HasAnyType(def, PokenType.Rock, PokenType.Ground, PokenType.Steel);
        else
            immune = HasAnyType(def, PokenType.Ice);
        if (immune) return;

        int damage = state.Poken.MaxHP / WeatherResidualDivisor;
        if (damage < 1) damage = 1;
        state.Poken.TakeDamage(damage);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, state.IsPlayerSide,
            $"¡{def.pokenName} sufre por el clima!"));
    }

    private static void GrassyHeal(BattlePokenState state, List<BattleEvent> events)
    {
        if (state.Poken.IsFainted || !IsGrounded(state)) return;
        if (state.Poken.CurrentHP >= state.Poken.MaxHP) return;
        int heal = state.Poken.MaxHP / GrassyHealDivisor;
        if (heal < 1) heal = 1;
        state.Poken.Heal(heal);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, state.IsPlayerSide,
            $"¡El Campo de Hierba curó a {state.Poken.Definition.pokenName}!"));
    }

    private static void TickScreens(SideState side, bool isPlayer, List<BattleEvent> events)
    {
        string team = isPlayer ? "tu equipo" : "el equipo rival";
        if (side.ReflectTurns > 0 && --side.ReflectTurns == 0)
            BattleEvent.Add(events, $"Se disipó el Reflejo de {team}.");
        if (side.LightScreenTurns > 0 && --side.LightScreenTurns == 0)
            BattleEvent.Add(events, $"Se disipó la Pantalla de Luz de {team}.");
        if (side.AuroraVeilTurns > 0 && --side.AuroraVeilTurns == 0)
            BattleEvent.Add(events, $"Se disipó el Velo Aurora de {team}.");
    }

    private static bool HasAnyType(PokenDefinition def, params PokenType[] types)
    {
        foreach (PokenType t in types)
            if (def.type == t || def.type2 == t) return true;
        return false;
    }

    // ---------- mensajes ----------

    public static string WeatherStartMessage(WeatherKind weather)
    {
        switch (weather)
        {
            case WeatherKind.Rain: return "¡Empezó a llover!";
            case WeatherKind.Sun: return "¡El sol brilla con fuerza!";
            case WeatherKind.Sandstorm: return "¡Se desató una tormenta de arena!";
            case WeatherKind.Hail: return "¡Empezó a granizar!";
            default: return "";
        }
    }

    public static string WeatherEndMessage(WeatherKind weather)
    {
        switch (weather)
        {
            case WeatherKind.Rain: return "La lluvia cesó.";
            case WeatherKind.Sun: return "El sol volvió a la normalidad.";
            case WeatherKind.Sandstorm: return "La tormenta de arena amainó.";
            case WeatherKind.Hail: return "Dejó de granizar.";
            default: return "";
        }
    }
}
