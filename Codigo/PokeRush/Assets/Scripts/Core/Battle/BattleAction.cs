// La accion que un lado elige para el turno. La construyen el jugador (input)
// o la IA (IBattleAI) y la resuelve el TurnEngine.
public enum BattleActionKind
{
    UseMove,
    SwitchPoken,
    UseItem,   // mochila en combate (solo objetos de curacion, al Poken activo)
    Flee,      // huir de salvajes
    Protect,   // aguantar el golpe del turno. Ver nota en BattleAction.Protect()

    // Este lado NO hace nada este turno. Va ULTIMO: el enum se serializa por
    // valor, meterlo en el medio corre a Flee y Protect en todo lo ya guardado.
    //
    // Existe para la lectura aliada de Nestor (nivel 2): cuando le lee una
    // jugada fuerte al rival, la accion que la IA ya habia elegido se cambia
    // por esta. El TurnEngine no la nombra en ningun lado, y por eso el lado
    // que la trae se queda quieto: no cambia, no usa objeto, no ataca y no
    // aguanta. Es exactamente lo que tiene que pasar, sin tocar una sola
    // regla del motor.
    Wait,

    // Turno de descanso despues de Hiperrayo y compania. Nadie lo elige: lo
    // pone el TurnEngine (ver TurnEngine.AccionForzada). Tambien va al final,
    // por lo mismo que Wait.
    Recharge
}

public class BattleAction
{
    public BattleActionKind Kind { get; private set; }

    // Indice del movimiento en Moves del Poken activo. -1 = Forcejeo (sin PP).
    public int MoveIndex { get; private set; } = -1;

    // Indice del Poken del equipo al que se cambia.
    public int SwitchIndex { get; private set; } = -1;

    // Objeto de curacion a usar.
    public HealItemDefinition Item { get; private set; }

    // A quien se le aplica el objeto: indice en Player.Team.
    // -1 = al Poken ACTIVO (comportamiento historico, lo usa la IA y el modo
    // consola). El menu de la mochila siempre manda un indice concreto.
    public int ItemTargetIndex { get; private set; } = -1;

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

    // Usar un objeto de curacion. Sin indice va al Poken ACTIVO.
    public static BattleAction UseItem(HealItemDefinition item)
        => new BattleAction { Kind = BattleActionKind.UseItem, Item = item, ItemTargetIndex = -1 };

    // Usar un objeto sobre un Poken concreto del equipo.
    //
    // Antes esto no existia: el objeto iba si o si al activo porque la unica UI
    // era la lista de objetos del HUD, sin panel de objetivo. Ahora la mochila
    // abre la party y elige, asi que la accion tiene que poder decir a quien.
    // La animacion de la barra de PS sigue siendo solo la del activo; curar a
    // uno del banquillo cambia el modelo sin animar nada, que es lo correcto.
    public static BattleAction UseItemOn(HealItemDefinition item, int teamIndex)
        => new BattleAction { Kind = BattleActionKind.UseItem, Item = item, ItemTargetIndex = teamIndex };

    // Aguantar: el ataque que reciba este turno no le hace nada.
    //
    // NO es el movimiento "Proteccion" de la saga: MoveDefinition no tiene
    // campo de proteccion y agregarlo obligaria a tocar el PokeApiMapper, que
    // es de donde salen los 595 movimientos generados. Existe solo como accion
    // de la fase de liberacion. El dia que se quiera el movimiento real, que
    // mapee a esta misma accion.
    public static BattleAction Protect()
        => new BattleAction { Kind = BattleActionKind.Protect };

    // Quedarse quieto: este lado pierde el turno.
    //
    // La usa NestorPredictionSystem para reemplazar la jugada que la IA rival
    // ya habia elegido, cuando Nestor le lee una jugada fuerte. No es una
    // accion que nadie pueda elegir desde un menu.
    public static BattleAction Wait()
        => new BattleAction { Kind = BattleActionKind.Wait };

    // Descansar el turno despues de Hiperrayo y compania.
    public static BattleAction Recharge()
        => new BattleAction { Kind = BattleActionKind.Recharge };
}
