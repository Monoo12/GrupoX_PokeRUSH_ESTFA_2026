public class BattleModel
{
    public PlayerModel Player { get; private set; }
    public EnemyModel Enemy { get; private set; }

    public BattlePhase CurrentPhase { get; private set; }
    public int CurrentTurn { get; private set; }
    public bool IsPlayerVictory { get; private set; }
    public bool IsBattleOver { get; private set; }

    public BattleModel(PlayerModel player, EnemyModel enemy)
    {
        Player = player;
        Enemy = enemy;
        CurrentPhase = BattlePhase.PlayerTurn;
        CurrentTurn = 1;
        IsBattleOver = false;
        IsPlayerVictory = false;
    }
}