using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleHudView battleHudView;
    [SerializeField] private PokenView playerPokenView;          // sprite de espalda (jugador)
    [SerializeField] private PokenView enemyPokenView;           // sprite de frente (rival)
    [SerializeField] private PlayerInputController playerInputController; // corta el movimiento en mundo

    [Header("Ritmo")]
    [Tooltip("Segundos que se muestra cada mensaje de combate.")]
    [SerializeField] private float messageDelay = 1f;

    private BattleModel _battleModel;
    private PlayerModel _playerModel;
    private EnemyModel _enemyModel;

    public void StartBattle(PlayerModel player, NPCDefinition enemyDefinition)
    {
        _playerModel = player;
        _enemyModel = new EnemyModel(enemyDefinition);
        _battleModel = new BattleModel(_playerModel, _enemyModel);

        // El HP persiste entre combates: el primer Poken puede estar debilitado.
        // Cada lado envia a su primer Poken con vida (regla de la saga).
        int playerLead = FirstAliveIndex(_playerModel.Team);
        int enemyLead = FirstAliveIndex(_enemyModel.Team);
        if (playerLead < 0 || enemyLead < 0)
        {
            Debug.LogWarning("BattleController: algun equipo esta vacio o sin Pokens con vida. No se inicia el combate.");
            _battleModel = null;
            return;
        }
        _battleModel.SwitchPlayerPoken(playerLead);
        _battleModel.SwitchEnemyPoken(enemyLead);

        if (playerInputController != null)
            playerInputController.SetBattleMode(true);

        if (battleHudView != null)
            battleHudView.Show();

        RefreshActiveSprites();
        battleHudView?.UpdateHud(_battleModel);
        Announce($"Un {_battleModel.ActiveEnemyPoken.Definition.pokenName} rival apareció!");

        _battleModel.SetPhase(BattlePhase.PlayerTurn);
    }

    // Punto de entrada para elegir movimiento por indice (teclas 1-4 ahora, UI en Fase 3).
    public void SelectMove(int moveIndex)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        List<MoveModel> moves = _battleModel.ActivePlayerPoken.Moves;
        if (moveIndex < 0 || moveIndex >= moves.Count) return;

        MoveModel chosen = moves[moveIndex];
        if (!chosen.HasPP)
        {
            Announce("No quedan PP para ese movimiento!");
            return;
        }

        ExecutePlayerAction(chosen);
    }

    public void ExecutePlayerAction(MoveModel move)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;
        StartCoroutine(ResolveTurn(move));
    }

    // TEMPORAL: entrada de prueba con teclado hasta la UI de batalla (Fase 3).
    private void Update()
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SelectMove(0);
        else if (kb.digit2Key.wasPressedThisFrame) SelectMove(1);
        else if (kb.digit3Key.wasPressedThisFrame) SelectMove(2);
        else if (kb.digit4Key.wasPressedThisFrame) SelectMove(3);
    }

    // Resuelve un turno completo: ordena por prioridad/velocidad y ejecuta ambas acciones.
    private IEnumerator ResolveTurn(MoveModel playerMove)
    {
        _battleModel.SetPhase(BattlePhase.CalculatingResult);

        MoveModel enemyMove = ChooseEnemyMove();
        bool playerFirst = PlayerActsFirst(playerMove, enemyMove);

        // Primera accion.
        yield return ExecuteMove(playerFirst, playerFirst ? playerMove : enemyMove);

        // Segunda accion: solo si el segundo atacante sigue en pie y nadie fue barrido.
        if (!OneSideWiped())
        {
            bool secondIsPlayer = !playerFirst;
            PokenModel secondAttacker = secondIsPlayer
                ? _battleModel.ActivePlayerPoken
                : _battleModel.ActiveEnemyPoken;

            if (!secondAttacker.IsFainted)
                yield return ExecuteMove(secondIsPlayer, secondIsPlayer ? playerMove : enemyMove);
        }

        // Reemplazos por debilitamiento o fin del combate.
        yield return HandleFaints();

        if (_battleModel.IsBattleOver)
        {
            yield return EndBattleRoutine();
            yield break;
        }

        _battleModel.AdvanceTurn();
        _battleModel.SetPhase(BattlePhase.PlayerTurn);
        battleHudView?.UpdateHud(_battleModel);
    }

    private IEnumerator ExecuteMove(bool attackerIsPlayer, MoveModel move)
    {
        PokenModel attacker = attackerIsPlayer ? _battleModel.ActivePlayerPoken : _battleModel.ActiveEnemyPoken;
        PokenModel defender = attackerIsPlayer ? _battleModel.ActiveEnemyPoken : _battleModel.ActivePlayerPoken;
        string attackerName = attacker.Definition.pokenName;

        // Sin movimiento utilizable: pierde el turno (Forcejeo llega mas adelante).
        if (move == null || !move.HasPP)
        {
            Announce($"{attackerName} no puede atacar!");
            yield return new WaitForSeconds(messageDelay);
            yield break;
        }

        move.ConsumePP();
        Announce($"{attackerName} usó {move.Definition.moveName}!");
        yield return new WaitForSeconds(messageDelay);

        // Precision: algunos movimientos (accuracy 0 o >=100) nunca fallan.
        if (!AccuracyHits(move.Definition))
        {
            Announce($"{attackerName} falló el ataque!");
            yield return new WaitForSeconds(messageDelay);
            yield break;
        }

        // Movimientos de estado: aplican efecto, no daño (efectos llegan en Fase 2).
        if (move.Definition.attackType == AttackType.Status || move.Definition.power <= 0)
        {
            Announce($"{move.Definition.moveName} todavía no tiene efecto.");
            yield return new WaitForSeconds(messageDelay);
            yield break;
        }

        DamageResult result = DamageCalculator.Calculate(attacker, defender, move.Definition);

        // Inmunidad por tipo (x0): no hace nada, ni siquiera critico.
        if (result.Effectiveness <= 0f)
        {
            Announce($"No afecta a {defender.Definition.pokenName}...");
            yield return new WaitForSeconds(messageDelay);
            yield break;
        }

        defender.TakeDamage(result.Damage);
        battleHudView?.UpdateHud(_battleModel);

        if (result.IsCritical)
        {
            Announce("¡Golpe crítico!");
            yield return new WaitForSeconds(messageDelay);
        }

        string effMsg = EffectivenessMessage(result.Effectiveness);
        if (effMsg != null)
        {
            Announce(effMsg);
            yield return new WaitForSeconds(messageDelay);
        }

        if (defender.IsFainted)
        {
            Announce($"{defender.Definition.pokenName} se debilitó!");
            yield return new WaitForSeconds(messageDelay);
        }
    }

    private IEnumerator HandleFaints()
    {
        // Rival debilitado: manda el siguiente con vida, o pierde el combate.
        if (_battleModel.ActiveEnemyPoken.IsFainted)
        {
            int next = FirstAliveIndex(_enemyModel.Team);
            if (next >= 0)
            {
                _battleModel.SwitchEnemyPoken(next);
                RefreshActiveSprites();
                battleHudView?.UpdateHud(_battleModel);
                Announce($"El rival envió a {_battleModel.ActiveEnemyPoken.Definition.pokenName}!");
                yield return new WaitForSeconds(messageDelay);
            }
            else
            {
                _battleModel.Finish(playerVictory: true);
            }
        }

        // Jugador debilitado: por ahora auto-cambia al siguiente con vida (la
        // eleccion manual llega con la UI de cambio, Fase 2/3), o pierde.
        if (_battleModel.ActivePlayerPoken.IsFainted)
        {
            int next = FirstAliveIndex(_playerModel.Team);
            if (next >= 0)
            {
                _battleModel.SwitchPlayerPoken(next);
                RefreshActiveSprites();
                battleHudView?.UpdateHud(_battleModel);
                Announce($"¡Adelante, {_battleModel.ActivePlayerPoken.Definition.pokenName}!");
                yield return new WaitForSeconds(messageDelay);
            }
            else
            {
                _battleModel.Finish(playerVictory: false);
            }
        }
    }

    private IEnumerator EndBattleRoutine()
    {
        Announce(_battleModel.IsPlayerVictory
            ? "¡Ganaste el combate!"
            : "Perdiste el combate...");
        yield return new WaitForSeconds(messageDelay);

        // Fase 4: repartir XP, dinero, items, flags y diálogo del boss.

        if (playerInputController != null)
            playerInputController.SetBattleMode(false);

        battleHudView?.Hide();
    }

    // IA simple (Fase 1): un movimiento al azar de los que tengan PP.
    private MoveModel ChooseEnemyMove()
    {
        List<MoveModel> usable = _battleModel.ActiveEnemyPoken.Moves.FindAll(m => m.HasPP);
        if (usable.Count == 0) return null;
        return usable[Random.Range(0, usable.Count)];
    }

    // Orden de turno: prioridad del movimiento primero; si empata, velocidad; si empata, azar.
    private bool PlayerActsFirst(MoveModel playerMove, MoveModel enemyMove)
    {
        int playerPriority = playerMove != null ? playerMove.Definition.priority : 0;
        int enemyPriority = enemyMove != null ? enemyMove.Definition.priority : 0;
        if (playerPriority != enemyPriority) return playerPriority > enemyPriority;

        int playerSpeed = _battleModel.ActivePlayerPoken.Speed;
        int enemySpeed = _battleModel.ActiveEnemyPoken.Speed;
        if (playerSpeed != enemySpeed) return playerSpeed > enemySpeed;
        return Random.value < 0.5f; // empate: al azar
    }

    private bool OneSideWiped()
        => !_playerModel.HasAlivePoken || !_enemyModel.HasAlivePoken;

    private bool AccuracyHits(MoveDefinition move)
    {
        if (move.accuracy <= 0 || move.accuracy >= 100) return true;
        return Random.value * 100f < move.accuracy;
    }

    private string EffectivenessMessage(float eff)
    {
        if (eff <= 0f) return "No tuvo efecto...";
        if (eff < 1f) return "No es muy efectivo...";
        if (eff > 1f) return "¡Es súper efectivo!";
        return null;
    }

    // Muestra el mensaje en el HUD si existe; si no, lo manda a la Console.
    // Asi se puede probar el combate sin armar la UI.
    private void Announce(string message)
    {
        if (battleHudView != null) battleHudView.ShowMessage(message);
        else Debug.Log($"[Batalla] {message}");
    }

    private void RefreshActiveSprites()
    {
        if (playerPokenView != null)
            playerPokenView.SetPoken(_battleModel.ActivePlayerPoken, isPlayerSide: true);
        if (enemyPokenView != null)
            enemyPokenView.SetPoken(_battleModel.ActiveEnemyPoken, isPlayerSide: false);
    }

    private static int FirstAliveIndex(List<PokenModel> team)
    {
        for (int i = 0; i < team.Count; i++)
            if (!team[i].IsFainted) return i;
        return -1;
    }
}
