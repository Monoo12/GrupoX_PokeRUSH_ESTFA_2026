using System.Collections.Generic;

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

    // Estado volatil de cada Poken activo (stages, confusion...). Se recrea en
    // cada cambio. Null hasta el primer SwitchPlayerPoken/SwitchEnemyPoken.
    public BattlePokenState PlayerState { get; private set; }
    public BattlePokenState EnemyState { get; private set; }

    // Pokens del jugador que enfrentaron al Poken rival ACTUAL (para repartir
    // su XP). Se reinicia cada vez que el rival envia uno nuevo.
    public HashSet<PokenModel> PlayerParticipants { get; } = new HashSet<PokenModel>();

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

    public void SwitchPlayerPoken(int index)
    {
        ActivePlayerIndex = index;
        PlayerState = new BattlePokenState(ActivePlayerPoken, isPlayerSide: true);
        PlayerParticipants.Add(ActivePlayerPoken);
    }

    public void SwitchEnemyPoken(int index)
    {
        ActiveEnemyIndex = index;
        EnemyState = new BattlePokenState(ActiveEnemyPoken, isPlayerSide: false);
        // Rival nuevo: la XP de este Poken se reparte entre quienes lo enfrenten
        PlayerParticipants.Clear();
        PlayerParticipants.Add(ActivePlayerPoken);
    }

    // El jugador escapo de un combate salvaje: termina sin ganador ni derrota
    public bool PlayerEscaped { get; private set; }

    public void Finish(bool playerVictory)
    {
        IsBattleOver = true;
        IsPlayerVictory = playerVictory;
        CurrentPhase = BattlePhase.End;
    }

    public void FinishEscaped()
    {
        PlayerEscaped = true;
        IsBattleOver = true;
        IsPlayerVictory = false;
        CurrentPhase = BattlePhase.End;
    }
}
