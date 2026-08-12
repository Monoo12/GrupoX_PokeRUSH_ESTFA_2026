// La accion que un lado elige para el turno. La construyen el jugador (input)
// o la IA (IBattleAI) y la resuelve el TurnEngine.
public enum BattleActionKind
{
    UseMove,
    SwitchPoken,
    UseItem,   // mochila en combate (solo objetos de curacion, al Poken activo)
    Flee,      // huir de salvajes
    Protect    // aguantar el golpe del turno. Ver nota en BattleAction.Protect()
}

public class BattleAction
{
    public BattleActionKind Kind { get; private set; }

    // Indice del movimiento en Moves del Poken activo. -1 = Forcejeo (sin PP).
    public int MoveIndex { get; private set; } = -1;

    // Indice del Poken del equipo al que se cambia.
    public int SwitchIndex { get; private set; } = -1;

    // Objeto de curacion a usar. Solo aplica al Poken activo.
    public HealItemDefinition Item { get; private set; }

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

    // Usar un objeto de curacion sobre el Poken ACTIVO.
    // Deliberadamente no apunta a otro del equipo: eso pediria un panel de
    // seleccion de objetivo entero, y la barra de PS que se anima en el HUD es
    // solo la del activo, asi que no se veria nada igual.
    public static BattleAction UseItem(HealItemDefinition item)
        => new BattleAction { Kind = BattleActionKind.UseItem, Item = item };

    // Aguantar: el ataque que reciba este turno no le hace nada.
    //
    // NO es el movimiento "Proteccion" de la saga: MoveDefinition no tiene
    // campo de proteccion y agregarlo obligaria a tocar el PokeApiMapper, que
    // es de donde salen los 595 movimientos generados. Existe solo como accion
    // de la fase de liberacion. El dia que se quiera el movimiento real, que
    // mapee a esta misma accion.
    public static BattleAction Protect()
        => new BattleAction { Kind = BattleActionKind.Protect };
}
