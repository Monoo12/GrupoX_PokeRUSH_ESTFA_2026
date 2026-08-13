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

    // --- estado del campo ---
    public WeatherKind CurrentWeather { get; private set; }
    public int WeatherTurnsLeft { get; private set; }
    public FieldTerrain CurrentTerrain { get; private set; }
    public int TerrainTurnsLeft { get; private set; }
    // Efectos por lado (pantallas propias, trampas que sufre ese lado)
    public SideState PlayerSide { get; } = new SideState();
    public SideState EnemySide { get; } = new SideState();

    public SideState SideOf(bool isPlayer) => isPlayer ? PlayerSide : EnemySide;

    public void SetWeather(WeatherKind weather, int turns)
    {
        CurrentWeather = weather;
        WeatherTurnsLeft = weather == WeatherKind.None ? 0 : turns;
    }

    public void SetTerrain(FieldTerrain terrain, int turns)
    {
        CurrentTerrain = terrain;
        TerrainTurnsLeft = terrain == FieldTerrain.None ? 0 : turns;
    }

    public void TickWeather() { if (WeatherTurnsLeft > 0) WeatherTurnsLeft--; }
    public void TickTerrain() { if (TerrainTurnsLeft > 0) TerrainTurnsLeft--; }

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

    // El Poken del jugador cayo y el combate espera que ELIJA el reemplazo.
    // Mientras esto sea true no se resuelve ningun turno.
    public bool AwaitingPlayerReplacement { get; private set; }
    public void SetAwaitingPlayerReplacement(bool value) => AwaitingPlayerReplacement = value;

    public void SetPhase(BattlePhase phase) => CurrentPhase = phase;
    public void AdvanceTurn() => CurrentTurn++;

    // Los dos Switch VALIDAN el indice y devuelven si el cambio se hizo.
    // Un Poken debilitado nunca puede pasar a ser el activo: es la ultima
    // barrera, aunque el que llama ya deberia haber filtrado.
    public bool SwitchPlayerPoken(int index)
    {
        if (!BattleTeamRules.EsIndiceVivo(Player.Team, index)) return false;

        ActivePlayerIndex = index;
        PlayerState = new BattlePokenState(ActivePlayerPoken, isPlayerSide: true);
        PlayerParticipants.Add(ActivePlayerPoken);
        return true;
    }

    public bool SwitchEnemyPoken(int index)
    {
        if (!BattleTeamRules.EsIndiceVivo(Enemy.Team, index)) return false;

        ActiveEnemyIndex = index;
        EnemyState = new BattlePokenState(ActiveEnemyPoken, isPlayerSide: false);
        // Rival nuevo: la XP de este Poken se reparte entre quienes lo enfrenten
        PlayerParticipants.Clear();
        PlayerParticipants.Add(ActivePlayerPoken);
        return true;
    }

    // El jugador escapo de un combate salvaje: termina sin ganador ni derrota
    public bool PlayerEscaped { get; private set; }

    public void Finish(bool playerVictory)
    {
        AwaitingPlayerReplacement = false;
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
