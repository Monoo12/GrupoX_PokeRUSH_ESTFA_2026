using System.Collections.Generic;

// Motor de turnos del combate. Logica PURA: recibe las dos acciones del turno,
// muta los modelos y devuelve la lista de eventos para que la capa de
// presentacion (BattleController) los reproduzca a su ritmo.
//
// Pipeline de un turno:
//   1. Cambios de Poken (prioridad maxima)
//   2. Movimientos en orden de prioridad -> velocidad efectiva -> azar
//      (chequeos de estado antes de actuar; efectos del movimiento despues)
//   3. Fin de turno: daño residual (quemadura/veneno) + habilidades
//   4. Debilitados: XP, reemplazos o fin del combate
public class TurnEngine
{
    private const float StruggleRecoilFraction = 0.25f;

    private readonly BattleModel _model;
    private readonly IBattleRng _rng;

    // Como elige el rival su reemplazo cuando su Poken cae. Lo setea la IA
    // (los lideres eligen por matchup); por defecto manda el primero con vida.
    public System.Func<BattleModel, int> EnemyReplacementSelector;

    // Intentos de huida acumulados (mejoran la chance, como en la saga)
    private int _escapeAttempts;

    public TurnEngine(BattleModel model, IBattleRng rng)
    {
        _model = model;
        _rng = rng;
    }

    // Eventos de apertura del combate: habilidades de entrada de ambos lideres.
    public List<BattleEvent> BeginBattle()
    {
        var events = new List<BattleEvent>();
        AbilityEffects.OnSwitchIn(_model.PlayerState, _model.EnemyState, events);
        AbilityEffects.OnSwitchIn(_model.EnemyState, _model.PlayerState, events);
        return events;
    }

    public List<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction enemyAction)
    {
        var events = new List<BattleEvent>();

        // 0) Huir (solo de salvajes): se resuelve antes que todo, como en la saga.
        //    Si falla, el jugador pierde su accion y el rival ataca igual.
        if (playerAction.Kind == BattleActionKind.Flee)
        {
            if (TryEscape(events)) return events;
        }

        // 1) Cambios primero (si ambos cambian, primero el jugador)
        if (playerAction.Kind == BattleActionKind.SwitchPoken)
            DoSwitch(isPlayer: true, playerAction.SwitchIndex, events);
        if (enemyAction.Kind == BattleActionKind.SwitchPoken)
            DoSwitch(isPlayer: false, enemyAction.SwitchIndex, events);

        // 2) Movimientos
        bool playerUsesMove = playerAction.Kind == BattleActionKind.UseMove;
        bool enemyUsesMove = enemyAction.Kind == BattleActionKind.UseMove;

        if (playerUsesMove && enemyUsesMove)
        {
            bool playerFirst = PlayerActsFirst(playerAction, enemyAction);
            ExecuteMoveAction(playerFirst, playerFirst ? playerAction : enemyAction, events);

            bool secondIsPlayer = !playerFirst;
            if (!_model.IsBattleOver)
                ExecuteMoveAction(secondIsPlayer, secondIsPlayer ? playerAction : enemyAction, events);
        }
        else if (playerUsesMove)
        {
            ExecuteMoveAction(true, playerAction, events);
        }
        else if (enemyUsesMove)
        {
            ExecuteMoveAction(false, enemyAction, events);
        }

        // 3) Fin de turno: residuales y habilidades, en orden de velocidad
        if (!_model.IsBattleOver)
        {
            BattlePokenState fast = _model.PlayerState.EffectiveSpeed >= _model.EnemyState.EffectiveSpeed
                ? _model.PlayerState : _model.EnemyState;
            BattlePokenState slow = fast.IsPlayerSide ? _model.EnemyState : _model.PlayerState;

            StatusEffects.OnTurnEnd(fast, events);
            AbilityEffects.OnTurnEnd(fast, events);
            StatusEffects.OnTurnEnd(slow, events);
            AbilityEffects.OnTurnEnd(slow, events);

            // Clima (daño residual + contador), terreno y pantallas
            FieldEffects.OnTurnEnd(_model, events);

            // El retroceso solo dura el turno en que se produjo
            _model.PlayerState.Flinched = false;
            _model.EnemyState.Flinched = false;
        }

        // 4) Debilitados: XP, reemplazos o fin
        HandleFaints(events);

        if (!_model.IsBattleOver)
            _model.AdvanceTurn();

        return events;
    }

    // Formula clasica de escape: F = velocidad*128/velocidadRival + 30*intentos.
    // Escapa si un numero al azar de 0-255 es menor que F.
    private bool TryEscape(List<BattleEvent> events)
    {
        if (!_model.Enemy.IsWild)
        {
            // Salvaguarda: el controller no deberia dejar llegar esto aca
            BattleEvent.Add(events, "¡No puedes huir de un combate contra un entrenador!");
            return false;
        }

        _escapeAttempts++;
        int playerSpeed = _model.PlayerState.EffectiveSpeed;
        int enemySpeed = System.Math.Max(1, _model.EnemyState.EffectiveSpeed);
        int threshold = playerSpeed * 128 / enemySpeed + 30 * _escapeAttempts;

        if (threshold > 255 || _rng.Range(0, 256) < threshold)
        {
            BattleEvent.Add(events, "¡Escapaste sin problemas!");
            _model.FinishEscaped();
            events.Add(BattleEvent.Of(BattleEventKind.BattleEnded, false));
            return true;
        }

        BattleEvent.Add(events, "¡No puedes escapar!");
        return false;
    }

    // ---------- orden de turno ----------

    private bool PlayerActsFirst(BattleAction playerAction, BattleAction enemyAction)
    {
        int playerPriority = MovePriority(_model.PlayerState, playerAction);
        int enemyPriority = MovePriority(_model.EnemyState, enemyAction);
        if (playerPriority != enemyPriority) return playerPriority > enemyPriority;

        int playerSpeed = _model.PlayerState.EffectiveSpeed;
        int enemySpeed = _model.EnemyState.EffectiveSpeed;
        if (playerSpeed != enemySpeed) return playerSpeed > enemySpeed;
        return _rng.Value() < 0.5f; // empate: al azar
    }

    private static int MovePriority(BattlePokenState actor, BattleAction action)
    {
        if (action.IsStruggle) return 0;
        if (action.MoveIndex < 0 || action.MoveIndex >= actor.Poken.Moves.Count) return 0;
        return actor.Poken.Moves[action.MoveIndex].Definition.priority;
    }

    // ---------- cambio de Poken ----------

    private void DoSwitch(bool isPlayer, int index, List<BattleEvent> events)
    {
        List<PokenModel> team = isPlayer ? _model.Player.Team : _model.Enemy.Team;
        int currentIndex = isPlayer ? _model.ActivePlayerIndex : _model.ActiveEnemyIndex;
        if (index < 0 || index >= team.Count || index == currentIndex || team[index].IsFainted)
            return; // el llamador valido mal: no hacemos nada

        string outgoing = (isPlayer ? _model.ActivePlayerPoken : _model.ActiveEnemyPoken).Definition.pokenName;

        if (isPlayer)
        {
            BattleEvent.Add(events, $"¡Vuelve, {outgoing}!");
            _model.SwitchPlayerPoken(index);
            events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, true,
                $"¡Adelante, {_model.ActivePlayerPoken.Definition.pokenName}!"));
            AbilityEffects.OnSwitchIn(_model.PlayerState, _model.EnemyState, events);
            FieldEffects.OnSwitchIn(_model, isPlayerSide: true, events, _rng);
        }
        else
        {
            BattleEvent.Add(events, $"El rival retiró a {outgoing}.");
            _model.SwitchEnemyPoken(index);
            events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, false,
                $"¡El rival envió a {_model.ActiveEnemyPoken.Definition.pokenName}!"));
            AbilityEffects.OnSwitchIn(_model.EnemyState, _model.PlayerState, events);
            FieldEffects.OnSwitchIn(_model, isPlayerSide: false, events, _rng);
        }
    }

    // ---------- ejecucion de movimientos ----------

    private void ExecuteMoveAction(bool attackerIsPlayer, BattleAction action, List<BattleEvent> events)
    {
        BattlePokenState attacker = attackerIsPlayer ? _model.PlayerState : _model.EnemyState;
        BattlePokenState defender = attackerIsPlayer ? _model.EnemyState : _model.PlayerState;

        // Sin atacante o sin objetivo (debilitado a mitad de turno): no hay accion
        if (attacker.Poken.IsFainted || defender.Poken.IsFainted) return;

        // Retroceso, sueño, congelado, paralisis, confusion...
        if (!StatusEffects.CanAct(attacker, events, _rng))
        {
            AnnounceFaintIfNeeded(attacker, events); // el autogolpe de confusion puede debilitar
            return;
        }

        // Resolucion del movimiento (o Forcejeo si no hay PP)
        bool isStruggle = action.IsStruggle;
        MoveModel moveModel = null;
        if (!isStruggle)
        {
            if (action.MoveIndex < 0 || action.MoveIndex >= attacker.Poken.Moves.Count) return;
            moveModel = attacker.Poken.Moves[action.MoveIndex];
            if (!moveModel.HasPP) isStruggle = true; // salvaguarda
        }

        if (isStruggle)
        {
            ExecuteStruggle(attacker, defender, events);
            return;
        }

        MoveDefinition move = moveModel.Definition;
        string attackerName = attacker.Poken.Definition.pokenName;
        string defenderName = defender.Poken.Definition.pokenName;

        moveModel.ConsumePP();
        var moveUsedEvent = BattleEvent.Of(BattleEventKind.MoveUsed, attackerIsPlayer,
            $"¡{attackerName} usó {move.moveName}!");
        moveUsedEvent.Move = move; // para que la presentacion elija la animacion
        events.Add(moveUsedEvent);

        // Salvaguarda: los NotImplemented no deberian estar en ningun learnset
        if (move.implementation == MoveImplementation.NotImplemented)
        {
            BattleEvent.Add(events, "¡Pero falló!");
            return;
        }

        // Precision (con stages de precision/evasion)
        if (!AccuracyHits(attacker, defender, move))
        {
            BattleEvent.Add(events, $"¡El ataque de {attackerName} falló!");
            return;
        }

        bool isPureStatus = move.attackType == AttackType.Status || move.power <= 0;
        if (isPureStatus)
        {
            // Inmunidad por tipo para estados con tipo (ej: Onda Trueno vs Ground)
            if (move.effect != StatusEffect.None
                && DamageCalculator.DualEffectiveness(move.type, defender.Poken.Definition) <= 0f)
            {
                BattleEvent.Add(events, $"No afecta a {defenderName}...");
                return;
            }

            bool anyEffect = MoveEffectExecutor.ApplyEffects(
                attacker, defender, move, events, _rng, isPureStatusMove: true, _model);
            if (!anyEffect)
                BattleEvent.Add(events, "¡Pero falló!");
            return;
        }

        // --- movimiento de daño ---

        // Habilidades que anulan/absorben (Levitación, Absorber Electricidad...)
        if (AbilityEffects.CheckAbsorbOrImmunity(defender, move.type, events)) return;

        float effectiveness = DamageCalculator.DualEffectiveness(move.type, defender.Poken.Definition);
        if (effectiveness <= 0f)
        {
            BattleEvent.Add(events, $"No afecta a {defenderName}...");
            return;
        }

        // Multi-golpe (2-5 con probabilidades oficiales, u otro rango del asset)
        int plannedHits = 1;
        if (move.minHits > 0 && move.maxHits >= move.minHits)
            plannedHits = RollHitCount(move.minHits, move.maxHits);

        int totalDamage = 0;
        int landedHits = 0;
        bool anyCrit = false;
        bool sturdyEndured = false;

        for (int i = 0; i < plannedHits && !defender.Poken.IsFainted; i++)
        {
            DamageResult result = DamageCalculator.Calculate(attacker, defender, move, _rng, _model);
            int damage = AbilityEffects.ModifyIncomingDamage(defender, result.Damage, out bool endured);
            sturdyEndured |= endured;
            anyCrit |= result.IsCritical;

            defender.Poken.TakeDamage(damage);
            totalDamage += damage;
            landedHits++;
            events.Add(BattleEvent.Of(BattleEventKind.HpChanged, defender.IsPlayerSide));
        }

        if (anyCrit) BattleEvent.Add(events, "¡Golpe crítico!");
        string effMsg = EffectivenessMessage(effectiveness);
        if (effMsg != null) BattleEvent.Add(events, effMsg);
        if (plannedHits > 1) BattleEvent.Add(events, $"¡Golpeó {landedHits} veces!");
        if (sturdyEndured)
            BattleEvent.Add(events, $"¡{defenderName} aguantó el golpe gracias a Robustez!");

        // Un golpe de fuego descongela al defensor
        if (!defender.Poken.IsFainted
            && defender.Poken.CurrentStatus == StatusEffect.Frozen
            && move.type == PokenType.Fire)
        {
            defender.Poken.CureStatus();
            events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, defender.IsPlayerSide,
                $"¡{defenderName} se descongeló!"));
        }

        // Drenaje y retroceso del movimiento
        if (move.drainPercent > 0 && totalDamage > 0 && !attacker.Poken.IsFainted
            && attacker.Poken.CurrentHP < attacker.Poken.MaxHP)
        {
            int healed = System.Math.Max(1, totalDamage * move.drainPercent / 100);
            attacker.Poken.Heal(healed);
            events.Add(BattleEvent.Of(BattleEventKind.HpChanged, attacker.IsPlayerSide,
                $"¡{attackerName} absorbió energía de {defenderName}!"));
        }
        if (move.recoilPercent > 0 && totalDamage > 0 && !attacker.Poken.IsFainted)
        {
            int recoil = System.Math.Max(1, totalDamage * move.recoilPercent / 100);
            attacker.Poken.TakeDamage(recoil);
            events.Add(BattleEvent.Of(BattleEventKind.HpChanged, attacker.IsPlayerSide,
                $"¡{attackerName} se hirió por el retroceso!"));
        }

        // Efectos secundarios: estado, stats, retroceso del rival
        MoveEffectExecutor.ApplyEffects(attacker, defender, move, events, _rng, isPureStatusMove: false, _model);

        // Castigos por contacto (Impacto Estático, Piel Tosca...)
        if (move.makesContact)
            AbilityEffects.OnContactReceived(attacker, defender, events, _rng);

        AnnounceFaintIfNeeded(defender, events);
        AnnounceFaintIfNeeded(attacker, events);
    }

    private void ExecuteStruggle(BattlePokenState attacker, BattlePokenState defender, List<BattleEvent> events)
    {
        string attackerName = attacker.Poken.Definition.pokenName;
        events.Add(BattleEvent.Of(BattleEventKind.MoveUsed, attacker.IsPlayerSide,
            $"¡{attackerName} no tiene PP! ¡Usó Forcejeo!"));

        DamageResult result = DamageCalculator.CalculateStruggle(attacker, defender, _rng, _model);
        int damage = AbilityEffects.ModifyIncomingDamage(defender, result.Damage, out bool endured);
        defender.Poken.TakeDamage(damage);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, defender.IsPlayerSide));

        if (result.IsCritical) BattleEvent.Add(events, "¡Golpe crítico!");
        if (endured)
            BattleEvent.Add(events, $"¡{defender.Poken.Definition.pokenName} aguantó el golpe gracias a Robustez!");

        // Retroceso fijo: 25% del MaxHP propio
        int recoil = System.Math.Max(1, (int)(attacker.Poken.MaxHP * StruggleRecoilFraction));
        attacker.Poken.TakeDamage(recoil);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, attacker.IsPlayerSide,
            $"¡{attackerName} se hirió por el retroceso!"));

        // Forcejeo hace contacto
        AbilityEffects.OnContactReceived(attacker, defender, events, _rng);

        AnnounceFaintIfNeeded(defender, events);
        AnnounceFaintIfNeeded(attacker, events);
    }

    private bool AccuracyHits(BattlePokenState attacker, BattlePokenState defender, MoveDefinition move)
    {
        // accuracy <= 0 se mantiene como "nunca falla" (convencion de los assets de prueba)
        if (move.alwaysHits || move.accuracy <= 0) return true;
        float chance = move.accuracy * BattlePokenState.HitChanceMultiplier(attacker, defender);
        return _rng.Value() * 100f < chance;
    }

    private int RollHitCount(int minHits, int maxHits)
    {
        // Caso clasico 2-5: probabilidades oficiales 35/35/15/15
        if (minHits == 2 && maxHits == 5)
        {
            float roll = _rng.Value();
            if (roll < 0.35f) return 2;
            if (roll < 0.70f) return 3;
            if (roll < 0.85f) return 4;
            return 5;
        }
        return _rng.Range(minHits, maxHits + 1);
    }

    private static string EffectivenessMessage(float eff)
    {
        if (eff <= 0f) return "No tuvo efecto...";
        if (eff < 1f) return "No es muy efectivo...";
        if (eff > 1f) return "¡Es súper efectivo!";
        return null;
    }

    private static void AnnounceFaintIfNeeded(BattlePokenState state, List<BattleEvent> events)
    {
        if (!state.Poken.IsFainted || state.FaintAnnounced) return;
        state.FaintAnnounced = true;
        events.Add(BattleEvent.Of(BattleEventKind.Fainted, state.IsPlayerSide,
            $"¡{state.Poken.Definition.pokenName} se debilitó!"));
    }

    // ---------- debilitados, XP y reemplazos ----------

    private void HandleFaints(List<BattleEvent> events)
    {
        // Debilitados por daño residual que nadie anuncio todavia
        AnnounceFaintIfNeeded(_model.EnemyState, events);
        AnnounceFaintIfNeeded(_model.PlayerState, events);

        // Rival debilitado: XP para los participantes y reemplazo (o victoria)
        if (_model.EnemyState.Poken.IsFainted)
        {
            AwardExperience(events);

            int next = EnemyReplacementSelector != null
                ? EnemyReplacementSelector(_model)
                : FirstAliveIndex(_model.Enemy.Team);

            if (next >= 0 && next < _model.Enemy.Team.Count && !_model.Enemy.Team[next].IsFainted)
            {
                _model.SwitchEnemyPoken(next);
                events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, false,
                    $"¡El rival envió a {_model.ActiveEnemyPoken.Definition.pokenName}!"));
                AbilityEffects.OnSwitchIn(_model.EnemyState, _model.PlayerState, events);
                FieldEffects.OnSwitchIn(_model, isPlayerSide: false, events, _rng);
                AnnounceFaintIfNeeded(_model.EnemyState, events); // por si las trampas lo debilitan
            }
            else
            {
                _model.Finish(playerVictory: true);
                events.Add(BattleEvent.Of(BattleEventKind.BattleEnded, true));
                return;
            }
        }

        // Jugador debilitado: por ahora auto-cambia al siguiente con vida (la
        // eleccion manual llega con la UI de cambio), o pierde.
        if (_model.PlayerState.Poken.IsFainted)
        {
            int next = FirstAliveIndex(_model.Player.Team);
            if (next >= 0)
            {
                _model.SwitchPlayerPoken(next);
                events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, true,
                    $"¡Adelante, {_model.ActivePlayerPoken.Definition.pokenName}!"));
                AbilityEffects.OnSwitchIn(_model.PlayerState, _model.EnemyState, events);
                FieldEffects.OnSwitchIn(_model, isPlayerSide: true, events, _rng);
                AnnounceFaintIfNeeded(_model.PlayerState, events); // por si las trampas lo debilitan
            }
            else
            {
                _model.Finish(playerVictory: false);
                events.Add(BattleEvent.Of(BattleEventKind.BattleEnded, false));
            }
        }
    }

    // Reparte la XP del Poken rival caido entre los participantes vivos.
    private void AwardExperience(List<BattleEvent> events)
    {
        PokenModel fainted = _model.EnemyState.Poken;
        List<PokenModel> eligible = RewardCalculator.EligibleParticipants(_model.PlayerParticipants);
        if (eligible.Count == 0) return;

        int total = RewardCalculator.XpForDefeating(fainted);
        int share = System.Math.Max(1, total / eligible.Count);

        foreach (PokenModel poken in eligible)
        {
            string name = poken.Definition.pokenName;
            List<int> levelsGained = poken.AddXP(share);
            BattleEvent.Add(events, $"{name} ganó {share} puntos de experiencia.");

            foreach (int level in levelsGained)
            {
                events.Add(BattleEvent.Of(BattleEventKind.HpChanged,
                    poken == _model.ActivePlayerPoken, $"¡{name} subió al nivel {level}!"));

                // Movimientos nuevos de este nivel
                foreach (MoveDefinition move in poken.MovesLearnedAt(level))
                {
                    if (poken.KnowsMove(move)) continue;
                    BattleEvent.Add(events, poken.TryLearnMove(move)
                        ? $"¡{name} aprendió {move.moveName}!"
                        : $"{name} quiere aprender {move.moveName}, pero ya conoce 4 movimientos.");
                    // TODO: flujo de reemplazo de movimientos (UI definitiva)
                }
            }
        }
    }

    private static int FirstAliveIndex(List<PokenModel> team)
    {
        for (int i = 0; i < team.Count; i++)
            if (!team[i].IsFainted) return i;
        return -1;
    }
}
