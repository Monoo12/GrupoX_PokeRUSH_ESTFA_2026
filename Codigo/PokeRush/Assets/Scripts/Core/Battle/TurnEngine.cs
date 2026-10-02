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

    // Cuanto respira el combate despues de una curacion con objeto, para que se
    // vea el efecto antes del ataque rival.
    private const float ItemPauseSeconds = 2f;

    private readonly BattleModel _model;
    private readonly IBattleRng _rng;

    // Como elige el rival su reemplazo cuando su Poken cae. Lo setea la IA
    // (los lideres eligen por matchup); por defecto manda el primero con vida.
    public System.Func<BattleModel, int> EnemyReplacementSelector;

    // Jefes que no pueden caer: en vez de debilitarse quedan en 1 PS. Lo setea
    // BattleController cuando el boss tiene fase de liberacion.
    //
    // Un solo punto de insercion (arriba de HandleFaints) cubre TODAS las
    // fuentes de dano: golpe directo, forcejeo, quemadura, veneno, clima y
    // retroceso. Chequearlo en cada una seria seis lugares que mantener.
    public System.Func<BattleModel, bool> EnemyCannotFaint;

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

        // Regla fundamental: un Poken debilitado no recibe turno. Si el combate
        // ya termino, o falta elegir reemplazo, o alguno de los dos activos esta
        // caido, este turno NO existe. En vez de dejar actuar a un muerto se
        // resuelve el debilitamiento pendiente y se devuelve el control: es lo
        // que evita el bucle "sigo peleando contra el que ya cayo".
        if (_model.IsBattleOver || _model.AwaitingPlayerReplacement) return events;
        if (_model.PlayerState.Poken.IsFainted || _model.EnemyState.Poken.IsFainted)
        {
            HandleFaints(events);
            return events;
        }

        // Cargar y recargar: el que esta cargando un movimiento (Rayo Solar,
        // Vuelo...) o tiene que descansar (Hiperrayo...) no elige este turno.
        // Se fuerza aca y no solo en el BattleController para que ni la IA ni
        // los tests puedan saltearlo.
        playerAction = AplicarAccionForzada(_model.PlayerState, playerAction);
        enemyAction = AplicarAccionForzada(_model.EnemyState, enemyAction);

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

        // 1b) Objetos y proteccion: van con los cambios y sin chequeo de
        //     velocidad, o sea con prioridad efectiva maxima, como en la saga.
        if (playerAction.Kind == BattleActionKind.UseItem)
        {
            int marca = events.Count;
            DoUseItem(playerAction.Item, playerAction.ItemTargetIndex, events);
            Sellar(events, marca);
        }
        if (playerAction.Kind == BattleActionKind.Protect)
            DoProtect(_model.PlayerState, events);
        if (enemyAction.Kind == BattleActionKind.Protect)
            DoProtect(_model.EnemyState, events);

        // NOTA sobre BattleActionKind.Wait: no aparece en ningun paso de este
        // pipeline y es a proposito. El lado que la trae no cambia, no usa
        // objeto, no aguanta y no ataca — pierde el turno y nada mas. La pone
        // NestorPredictionSystem sobre la accion que la IA rival ya habia
        // elegido, ANTES de llamar aca; el motor no tiene que saber por que.

        // 2) Movimientos
        // El descanso de Hiperrayo va en el orden de los movimientos, como en
        // la saga ("X necesita descansar" sale cuando le tocaria actuar).
        bool playerUsesMove = playerAction.Kind == BattleActionKind.UseMove
                              || playerAction.Kind == BattleActionKind.Recharge;
        bool enemyUsesMove = enemyAction.Kind == BattleActionKind.UseMove
                             || enemyAction.Kind == BattleActionKind.Recharge;

        if (playerUsesMove && enemyUsesMove)
        {
            bool playerFirst = PlayerActsFirst(playerAction, enemyAction);
            int marca1 = events.Count;
            ExecuteMoveAction(playerFirst, playerFirst ? playerAction : enemyAction, events);
            Sellar(events, marca1);

            bool secondIsPlayer = !playerFirst;
            if (!_model.IsBattleOver)
            {
                int marca2 = events.Count;
                ExecuteMoveAction(secondIsPlayer, secondIsPlayer ? playerAction : enemyAction, events);
                Sellar(events, marca2);
            }
        }
        else if (playerUsesMove)
        {
            int marca = events.Count;
            ExecuteMoveAction(true, playerAction, events);
            Sellar(events, marca);
        }
        else if (enemyUsesMove)
        {
            int marca = events.Count;
            ExecuteMoveAction(false, enemyAction, events);
            Sellar(events, marca);
        }

        // 3) Fin de turno: residuales y habilidades, en orden de velocidad
        if (!_model.IsBattleOver)
        {
            BattlePokenState fast = _model.PlayerState.EffectiveSpeed >= _model.EnemyState.EffectiveSpeed
                ? _model.PlayerState : _model.EnemyState;
            BattlePokenState slow = fast.IsPlayerSide ? _model.EnemyState : _model.PlayerState;

            int marcaResiduales = events.Count;
            StatusEffects.OnTurnEnd(fast, events);
            AbilityEffects.OnTurnEnd(fast, events);
            StatusEffects.OnTurnEnd(slow, events);
            AbilityEffects.OnTurnEnd(slow, events);

            // Clima (daño residual + contador), terreno y pantallas
            FieldEffects.OnTurnEnd(_model, events);

            Sellar(events, marcaResiduales);

            // El retroceso solo dura el turno en que se produjo
            _model.PlayerState.Flinched = false;
            _model.EnemyState.Flinched = false;

            // La proteccion tampoco se acumula entre turnos
            _model.PlayerState.Protected = false;
            _model.EnemyState.Protected = false;
        }

        // 4) Debilitados: XP, reemplazos o fin
        int marcaFin = events.Count;
        HandleFaints(events);
        Sellar(events, marcaFin);

        if (!_model.IsBattleOver)
            _model.AdvanceTurn();

        return events;
    }

    // Sella los HpChanged emitidos desde 'desde' con los PS de ESTE instante.
    // Se llama despues de cada paso que muta PS, porque el turno se resuelve
    // entero antes de reproducirse y despues ya es tarde para saber por cuanto
    // paso la barra en el medio.
    private void Sellar(List<BattleEvent> events, int desde)
    {
        for (int i = desde; i < events.Count; i++)
        {
            BattleEvent ev = events[i];
            if (ev.Kind != BattleEventKind.HpChanged || ev.HpAfter >= 0) continue;
            ev.HpAfter = ev.TargetIsPlayer
                ? _model.ActivePlayerPoken.CurrentHP
                : _model.ActiveEnemyPoken.CurrentHP;
        }
    }

    // ---------- objetos, proteccion y escudo ----------

    // Usa un objeto de curacion sobre un Poken del equipo del jugador.
    //
    // Las reglas (que cura, a quien se le puede aplicar, cuando NO se gasta el
    // objeto) viven en ItemUsageService, compartidas con la mochila del mundo.
    // Aca solo queda lo que es propio del combate: resolver el objetivo y
    // emitir los eventos que anima el BattleController.
    private void DoUseItem(HealItemDefinition item, int targetIndex, List<BattleEvent> events)
    {
        if (item == null) return;

        // Sin indice (IA, modo consola) se cae al Poken activo, como siempre.
        PokenModel objetivo = _model.ActivePlayerPoken;
        bool esElActivo = true;
        if (targetIndex >= 0 && targetIndex < _model.Player.Team.Count)
        {
            objetivo = _model.Player.Team[targetIndex];
            esElActivo = targetIndex == _model.ActivePlayerIndex;
        }

        ItemUseResult resultado = ItemUsageService.Apply(item, objetivo, _model.Player.Inventory);

        if (!resultado.Applied)
        {
            BattleEvent.Add(events, resultado.Message);
            return;
        }

        // La barra de PS que se anima en el HUD es la del activo: curar a uno
        // del banquillo cambia el modelo pero no dispara HpChanged, o el HUD
        // animaria la barra equivocada.
        if (esElActivo && resultado.HealedHp > 0)
        {
            BattleEvent cura = BattleEvent.Of(BattleEventKind.HpChanged, true);
            cura.Sonido = SonidoDelEvento.Curacion;
            events.Add(cura);
        }

        // Pausa extra: la curacion tiene que quedar a la vista ANTES de que el
        // rival pegue. Sin esto la barra subia y bajaba casi junta y la pocion
        // parecia amortiguar el golpe en vez de curar.
        BattleEvent mensaje = BattleEvent.Msg(resultado.Message);
        mensaje.ExtraPause = ItemPauseSeconds;
        events.Add(mensaje);
    }

    private void DoProtect(BattlePokenState quien, List<BattleEvent> events)
    {
        quien.Protected = true;
        BattleEvent.Add(events, $"¡{quien.Poken.Definition.pokenName} se puso en guardia!");
    }

    // El escudo del nucleo come daño antes que los PS. Devuelve lo que queda
    // para el Poken. Un solo punto de insercion en cada camino de daño alcanza
    // porque va justo despues de ModifyIncomingDamage, igual que Robustez.
    private int AbsorberEscudo(BattlePokenState defender, int damage, List<BattleEvent> events)
    {
        SideState lado = defender.IsPlayerSide ? _model.PlayerSide : _model.EnemySide;
        if (lado == null || lado.ShieldHp <= 0 || damage <= 0) return damage;

        int absorbido = System.Math.Min(lado.ShieldHp, damage);
        lado.ShieldHp -= absorbido;

        if (lado.ShieldHp <= 0)
            BattleEvent.Add(events, "¡El escudo del núcleo se rompió!");
        else
            BattleEvent.Add(events, $"El escudo del núcleo absorbió el golpe ({lado.ShieldHp} restante).");

        return damage - absorbido;
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

    // ---------- carga y recarga ----------

    // Lo que un lado esta obligado a hacer este turno, o null si elige libre.
    // Lo usa el BattleController para no mostrar el menu (ni consultar a la
    // IA), y el motor para que nadie lo saltee.
    public static BattleAction AccionForzada(BattlePokenState estado)
    {
        if (estado == null) return null;
        if (estado.MustRecharge) return BattleAction.Recharge();
        if (estado.ChargingMoveIndex >= 0) return BattleAction.Move(estado.ChargingMoveIndex);
        return null;
    }

    private static BattleAction AplicarAccionForzada(BattlePokenState estado, BattleAction elegida)
    {
        // Wait lo pone la lectura de Nestor sobre la jugada del rival: se la
        // anula. Si esa jugada era soltar una carga o descansar, tambien se pierde.
        if (elegida != null && elegida.Kind == BattleActionKind.Wait)
        {
            estado.CancelarCarga();
            estado.MustRecharge = false;
            return elegida;
        }
        return AccionForzada(estado) ?? elegida;
    }

    // Primer turno de un movimiento de carga: mensaje, stats del turno de carga
    // (Cabezazo sube Defensa) y, si corresponde, queda fuera de alcance.
    private void EmpezarCarga(BattlePokenState atacante, int indice, MoveDefinition move, List<BattleEvent> events)
    {
        atacante.ChargingMoveIndex = indice;
        atacante.SemiInvulnerable = move.turnRule == MoveTurnRule.ChargeInvulnerable;
        BattleEvent.Add(events, MoveTurnRules.MensajeDeCarga(move, atacante.Poken.Definition.pokenName));

        if (move.chargeStatChanges == null) return;
        foreach (StatChangeEntry cambio in move.chargeStatChanges)
        {
            if (cambio == null || cambio.stages == 0) continue;
            MoveEffectExecutor.ApplyStatChange(atacante, cambio.stat, cambio.stages, events);
        }
    }

    // ---------- cambio de Poken ----------

    private void DoSwitch(bool isPlayer, int index, List<BattleEvent> events)
    {
        List<PokenModel> team = isPlayer ? _model.Player.Team : _model.Enemy.Team;
        int currentIndex = isPlayer ? _model.ActivePlayerIndex : _model.ActiveEnemyIndex;
        if (index == currentIndex || !BattleTeamRules.EsIndiceVivo(team, index))
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

        // Sin atacante o sin objetivo (debilitado a mitad de turno): no hay
        // accion. Un Poken caido no ataca ni puede ser atacado.
        if (!BattleTeamRules.PuedeActuar(attacker)) return;

        // Turno de descanso despues de Hiperrayo y compania. Se pierde aunque el
        // rival de antes ya no este (cayo con el golpe y entro otro).
        if (action.Kind == BattleActionKind.Recharge)
        {
            attacker.MustRecharge = false;
            BattleEvent.Add(events, $"¡{attacker.Poken.Definition.pokenName} necesita descansar este turno!");
            return;
        }

        if (!BattleTeamRules.PuedeActuar(defender)) return;

        // Retroceso, sueño, congelado, paralisis, confusion...
        if (!StatusEffects.CanAct(attacker, events, _rng))
        {
            // Si estaba cargando, la carga se pierde (y vuelve a estar al alcance).
            attacker.CancelarCarga();
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

        // Movimientos de dos turnos. El PP se gasta al empezar a cargar y no al
        // soltar el golpe, como en la saga.
        bool sueltaLaCarga = attacker.ChargingMoveIndex >= 0 && attacker.ChargingMoveIndex == action.MoveIndex;
        if (sueltaLaCarga)
        {
            attacker.CancelarCarga();
        }
        else
        {
            moveModel.ConsumePP();
            if (MoveTurnRules.Carga(move) && !MoveTurnRules.SaltaLaCarga(move, _model.CurrentWeather))
            {
                EmpezarCarga(attacker, action.MoveIndex, move, events);
                return;
            }
        }

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

        // Vuelo, Excavar, Buceo...: mientras el rival esta fuera de alcance, lo
        // que va dirigido a el falla (lo que el atacante se aplica a si mismo, no).
        if (defender.SemiInvulnerable && MoveTurnRules.ApuntaAlRival(move))
        {
            BattleEvent.Add(events, $"¡{defenderName} está fuera de alcance! ¡El ataque falló!");
            return;
        }

        // Precision (con stages de precision/evasion)
        if (!AccuracyHits(attacker, defender, move))
        {
            BattleEvent.Add(events, $"¡El ataque de {attackerName} falló!");
            return;
        }

        // Proteccion: va DESPUES de la precision para que el mensaje "uso X" y
        // el consumo de PP ya hayan salido, que es lo correcto.
        if (defender.Protected)
        {
            BattleEvent.Add(events, $"¡{defenderName} aguantó el golpe!");
            return;
        }

        bool isPureStatus = move.attackType == AttackType.Status || move.power <= 0;
        if (isPureStatus)
        {
            // Inmunidad por tipo para estados con tipo (ej: Onda Trueno vs Ground)
            if (move.effect != StatusEffect.None
                && DamageCalculator.DualEffectiveness(move.type, defender) <= 0f)
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

        float effectiveness = DamageCalculator.DualEffectiveness(move.type, defender);
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
            damage = AbsorberEscudo(defender, damage, events);
            sturdyEndured |= endured;
            anyCrit |= result.IsCritical;

            defender.Poken.TakeDamage(damage);
            totalDamage += damage;
            landedHits++;
            BattleEvent golpe = BattleEvent.Of(BattleEventKind.HpChanged, defender.IsPlayerSide);
            golpe.Sonido = effectiveness > 1f ? SonidoDelEvento.GolpeSuperEficaz
                         : effectiveness < 1f ? SonidoDelEvento.GolpePocoEficaz
                         : SonidoDelEvento.Golpe;
            events.Add(golpe);
        }

        // Hiperrayo y compania: si pego, el turno que viene descansa (aunque el
        // rival haya caido). Si fallo, lo aguantaron o no le hizo nada, no.
        if (move.turnRule == MoveTurnRule.Recharge && landedHits > 0)
            attacker.MustRecharge = true;

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

        if (defender.SemiInvulnerable)
        {
            BattleEvent.Add(events, $"¡{defender.Poken.Definition.pokenName} está fuera de alcance! ¡El ataque falló!");
            return;
        }

        DamageResult result = DamageCalculator.CalculateStruggle(attacker, defender, _rng, _model);
        int damage = AbilityEffects.ModifyIncomingDamage(defender, result.Damage, out bool endured);
        damage = AbsorberEscudo(defender, damage, events);
        defender.Poken.TakeDamage(damage);
        BattleEvent golpe = BattleEvent.Of(BattleEventKind.HpChanged, defender.IsPlayerSide);
        golpe.Sonido = SonidoDelEvento.Golpe;
        events.Add(golpe);

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

    private void AnnounceFaintIfNeeded(BattlePokenState state, List<BattleEvent> events)
    {
        if (!state.Poken.IsFainted || state.FaintAnnounced) return;

        // Un boss que HandleFaints va a rescatar en 1 PS no se debilito: no hay
        // que anunciarlo ni reproducirle la animacion de caida. Sin esto el
        // sprite se caia y quedaba escondido, y despues la fase de liberacion
        // arrancaba con el campo vacio del lado rival.
        if (!state.IsPlayerSide && EnemyCannotFaint != null && EnemyCannotFaint(_model)) return;
        state.FaintAnnounced = true;
        events.Add(BattleEvent.Of(BattleEventKind.Fainted, state.IsPlayerSide,
            $"¡{state.Poken.Definition.pokenName} se debilitó!"));
    }

    // ---------- debilitados, XP y reemplazos ----------

    private void HandleFaints(List<BattleEvent> events)
    {
        if (_model.IsBattleOver) return;

        // Jefe con fase de liberacion: no puede caer, se queda en 1 PS.
        // Va PRIMERO para que nada de lo de abajo lo vea debilitado.
        if (EnemyCannotFaint != null && _model.EnemyState.Poken.IsFainted
            && EnemyCannotFaint(_model))
        {
            _model.EnemyState.Poken.SetCurrentHP(1);
            // Volvio a estar vivo: si mas adelante cae de verdad hay que poder
            // anunciarlo otra vez.
            _model.EnemyState.FaintAnnounced = false;
        }

        // Debilitados por daño residual que nadie anuncio todavia
        AnnounceFaintIfNeeded(_model.EnemyState, events);
        AnnounceFaintIfNeeded(_model.PlayerState, events);

        // Rival debilitado: XP para los participantes y reemplazo (o victoria).
        // Es un bucle porque las trampas pueden debilitar al que entra, y ahi
        // hay que mandar al siguiente en vez de seguir peleando contra un caido.
        // Termina siempre: cada vuelta o cierra el combate o consume un vivo.
        while (_model.EnemyState.Poken.IsFainted)
        {
            AwardExperience(events);

            int next = ElegirReemplazoRival();
            if (next < 0)
            {
                // Equipo rival sin Pokens disponibles: el combate TERMINA aca.
                // Nunca se vuelve a pedir turno contra el que acaba de caer.
                _model.Finish(playerVictory: true);
                events.Add(BattleEvent.Of(BattleEventKind.BattleEnded, true));
                return;
            }

            _model.SwitchEnemyPoken(next);
            events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, false,
                $"¡El rival envió a {_model.ActiveEnemyPoken.Definition.pokenName}!"));
            AbilityEffects.OnSwitchIn(_model.EnemyState, _model.PlayerState, events);
            FieldEffects.OnSwitchIn(_model, isPlayerSide: false, events, _rng);
            AnnounceFaintIfNeeded(_model.EnemyState, events); // por si las trampas lo debilitan
        }

        // Jugador debilitado: el motor NO elige por el. Marca que falta
        // reemplazo y se detiene; el controller abre el selector de Poken y
        // despues llama a CompletePlayerReplacement.
        if (_model.PlayerState.Poken.IsFainted)
        {
            if (BattleTeamRules.HayBanco(_model.Player.Team, _model.ActivePlayerIndex))
            {
                _model.SetAwaitingPlayerReplacement(true);
                events.Add(BattleEvent.Of(BattleEventKind.PlayerMustSwitch, true));
            }
            else
            {
                _model.Finish(playerVictory: false);
                events.Add(BattleEvent.Of(BattleEventKind.BattleEnded, false));
            }
        }
    }

    // Reemplazo del rival: lo propone la IA, pero el motor VALIDA. Un indice
    // fuera de rango o de un Poken debilitado cae al primer vivo del banco; si
    // no queda ninguno devuelve -1 y el combate se termina.
    private int ElegirReemplazoRival()
    {
        List<PokenModel> team = _model.Enemy.Team;
        int active = _model.ActiveEnemyIndex;

        if (EnemyReplacementSelector != null)
        {
            int elegido = EnemyReplacementSelector(_model);
            if (elegido != active && BattleTeamRules.EsIndiceVivo(team, elegido)) return elegido;
        }
        return BattleTeamRules.PrimerVivoDelBanco(team, active);
    }

    // El jugador ya eligio con que Poken sigue despues de un debilitamiento.
    // Devuelve los eventos de la entrada (habilidades + trampas). Lista vacia si
    // el indice no sirve: el controller vuelve a preguntar sin haber cambiado
    // nada.
    public List<BattleEvent> CompletePlayerReplacement(int index)
    {
        var events = new List<BattleEvent>();
        if (!_model.AwaitingPlayerReplacement) return events;
        if (!BattleTeamRules.EsIndiceVivo(_model.Player.Team, index)) return events;

        _model.SetAwaitingPlayerReplacement(false);
        _model.SwitchPlayerPoken(index);

        events.Add(BattleEvent.Of(BattleEventKind.PokenSwitched, true,
            $"¡Adelante, {_model.ActivePlayerPoken.Definition.pokenName}!"));
        AbilityEffects.OnSwitchIn(_model.PlayerState, _model.EnemyState, events);
        FieldEffects.OnSwitchIn(_model, isPlayerSide: true, events, _rng);

        // Las trampas pueden debilitar al que entra: se resuelve en el acto y,
        // si hace falta, se vuelve a pedir reemplazo.
        AnnounceFaintIfNeeded(_model.PlayerState, events);
        if (_model.PlayerState.Poken.IsFainted) HandleFaints(events);

        return events;
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
                BattleEvent nivel = BattleEvent.Of(BattleEventKind.HpChanged,
                    poken == _model.ActivePlayerPoken, $"¡{name} subió al nivel {level}!");
                nivel.Sonido = SonidoDelEvento.SubirNivel;
                events.Add(nivel);

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
}
