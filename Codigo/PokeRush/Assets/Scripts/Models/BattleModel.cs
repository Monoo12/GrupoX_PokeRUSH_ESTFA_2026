public class BattleModel
{
    public PlayerModel Player { get; private set; }
    public EnemyModel Enemy { get; private set; }

    public BattlePhase CurrentPhase { get; private set; }
    public int CurrentTurn { get; private set; }
    public bool IsPlayerVictory { get; private set; }
    public bool IsBattleOver { get; private set; }

    // Poken activo de cada lado (combate 1 contra 1). El resto del equipo espera.
    public int ActivePlayerIndex { get; private set; }
    public int ActiveEnemyIndex { get; private set; }
    public PokenModel ActivePlayerPoken => Player.Team[ActivePlayerIndex];
    public PokenModel ActiveEnemyPoken => Enemy.Team[ActiveEnemyIndex];

    public BattleModel(PlayerModel player, EnemyModel enemy)
    {
        Player = player;
        Enemy = enemy;
        CurrentPhase = BattlePhase.PlayerTurn;
        CurrentTurn = 1;
        IsBattleOver = false;
        IsPlayerVictory = false;
        ActivePlayerIndex = 0;
        ActiveEnemyIndex = 0;
    }

    public void SetPhase(BattlePhase phase) => CurrentPhase = phase;
    public void AdvanceTurn() => CurrentTurn++;
    public void SwitchPlayerPoken(int index) => ActivePlayerIndex = index;
    public void SwitchEnemyPoken(int index) => ActiveEnemyIndex = index;

    public void Finish(bool playerVictory)
    {
        IsBattleOver = true;
        IsPlayerVictory = playerVictory;
        CurrentPhase = BattlePhase.End;
    }
}
