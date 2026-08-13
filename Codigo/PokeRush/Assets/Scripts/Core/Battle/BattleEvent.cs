using System.Collections.Generic;

// Un paso visible del combate. El TurnEngine (logica pura) emite la lista de
// eventos del turno y el BattleController los reproduce con su ritmo/UI.
public enum BattleEventKind
{
    Message,          // solo texto
    HpChanged,        // refrescar barra de HP (+ texto opcional)
    StatusChanged,    // cambio de estado alterado
    StatStageChanged, // subio/bajo una stat
    PokenSwitched,    // cambio el Poken activo de un lado (refrescar sprites)
    MoveUsed,         // un Poken uso un movimiento
    Fainted,          // un Poken se debilito
    PlayerMustSwitch, // el Poken del jugador cayo: el motor espera que ELIJA reemplazo
    WeatherChanged,   // empezo/termino un clima (actualizar overlay)
    TerrainChanged,   // empezo/termino un terreno (actualizar overlay)
    BattleEnded       // fin del combate
}

public class BattleEvent
{
    public BattleEventKind Kind;
    // Texto en español ya formateado (puede ser null en eventos puramente visuales)
    public string Message;
    // Que lado afecta el evento (para saber que barra/sprite refrescar)
    public bool TargetIsPlayer;
    // Solo en MoveUsed: el movimiento usado, para elegir su animacion
    // (null = Forcejeo / autogolpe: se anima como fisico sin tipo)
    public MoveDefinition Move;

    public static BattleEvent Msg(string text)
        => new BattleEvent { Kind = BattleEventKind.Message, Message = text };

    public static BattleEvent Of(BattleEventKind kind, bool targetIsPlayer, string text = null)
        => new BattleEvent { Kind = kind, TargetIsPlayer = targetIsPlayer, Message = text };

    // Azucar para el caso mas comun
    public static void Add(List<BattleEvent> events, string text) => events.Add(Msg(text));
}
