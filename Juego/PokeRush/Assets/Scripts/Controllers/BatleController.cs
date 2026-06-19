using UnityEngine;

public class BattleController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleHudView battleHudView;

    private BattleModel _battleModel;
    private PlayerModel _playerModel;
    private EnemyModel _enemyModel;

    public void StartBattle(PlayerModel player, NPCDefinition enemyDefinition)
    {
        _playerModel = player;
        _enemyModel = new EnemyModel(enemyDefinition);
        _battleModel = new BattleModel(_playerModel, _enemyModel);

        battleHudView.UpdateHud(_battleModel);
    }

    public void ExecutePlayerAction(MoveModel move)
    {
        if (_battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        // logica de accion del jugador — se implementa despues
    }

    private void ExecuteEnemyTurn()
    {
        if (_battleModel.CurrentPhase != BattlePhase.BossTurn) return;

        // logica de IA enemiga — se implementa despues
    }

    private void CheckBattleEnd()
    {
        if (!_enemyModel.HasAlivePoken)
        {
            EndBattle(playerWon: true);
            return;
        }

        if (!_playerModel.HasAlivePoken)
        {
            EndBattle(playerWon: false);
        }
    }

    private void EndBattle(bool playerWon)
    {
        // resultado, recompensas y flags — se implementa despues
    }
}