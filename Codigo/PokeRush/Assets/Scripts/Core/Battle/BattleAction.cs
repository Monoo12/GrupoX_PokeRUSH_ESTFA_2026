// La accion que un lado elige para el turno. La construyen el jugador (input)
// o la IA (IBattleAI) y la resuelve el TurnEngine.
public enum BattleActionKind
{
    UseMove,
    SwitchPoken,
    UseItem,   // futuro: mochila en combate
    Flee       // futuro: huir de salvajes
}

public class BattleAction
{
    public BattleActionKind Kind { get; private set; }

    // Indice del movimiento en Moves del Poken activo. -1 = Forcejeo (sin PP).
    public int MoveIndex { get; private set; } = -1;

    // Indice del Poken del equipo al que se cambia.
    public int SwitchIndex { get; private set; } = -1;

    public bool IsStruggle => Kind == BattleActionKind.UseMove && MoveIndex < 0;

    private BattleAction() { }

    public static BattleAction Move(int moveIndex)
        => new BattleAction { Kind = BattleActionKind.UseMove, MoveIndex = moveIndex };

    // Forcejeo: cuando ningun movimiento tiene PP.
    public static BattleAction Struggle()
        => new BattleAction { Kind = BattleActionKind.UseMove, MoveIndex = -1 };

    public static BattleAction Switch(int teamIndex)
        => new BattleAction { Kind = BattleActionKind.SwitchPoken, SwitchIndex = teamIndex };

    // Huir de un combate salvaje (el TurnEngine resuelve si lo logra)
    public static BattleAction Flee()
        => new BattleAction { Kind = BattleActionKind.Flee };
}
