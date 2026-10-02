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

// Que sonido le corresponde a un evento. Lo marca el TurnEngine, que es el que
// sabe si un golpe fue super eficaz; lo hace sonar el BattleController.
public enum SonidoDelEvento
{
    Ninguno,
    Golpe,
    GolpeSuperEficaz,
    GolpePocoEficaz,
    Curacion,
    SubirNivel
}

public class BattleEvent
{
    public BattleEventKind Kind;
    // Solo presentacion: que sonido acompaña al evento (ver SonidoDelEvento).
    public SonidoDelEvento Sonido;
    // Texto en español ya formateado (puede ser null en eventos puramente visuales)
    public string Message;
    // Que lado afecta el evento (para saber que barra/sprite refrescar)
    public bool TargetIsPlayer;
    // Solo en MoveUsed: el movimiento usado, para elegir su animacion
    // (null = Forcejeo / autogolpe: se anima como fisico sin tipo)
    public MoveDefinition Move;

    // Segundos EXTRA de pausa despues de reproducir este evento, ademas del
    // ritmo normal. Sirve para que una jugada que el jugador tiene que ver
    // asentarse no quede pisada por lo que viene despues: sin esto la curacion
    // de una pocion se leia como si solo amortiguara el golpe del rival,
    // porque la barra subia y bajaba casi en el mismo instante.
    public float ExtraPause;

    // Solo en HpChanged: los PS que tenia el Poken JUSTO DESPUES de este cambio.
    // -1 = sin sellar (se cae al valor actual del modelo).
    //
    // Hace falta porque el turno se resuelve ENTERO antes de reproducirse: para
    // cuando el jugador ve la animacion, el modelo ya tiene aplicados todos los
    // cambios del turno. Sin la foto, animar "hacia el HP actual" hacia que la
    // curacion de una pocion saltara directo al valor final —ya con el golpe
    // del rival restado— y el ataque siguiente no moviera la barra.
    public int HpAfter = -1;

    public static BattleEvent Msg(string text)
        => new BattleEvent { Kind = BattleEventKind.Message, Message = text };

    public static BattleEvent Of(BattleEventKind kind, bool targetIsPlayer, string text = null)
        => new BattleEvent { Kind = kind, TargetIsPlayer = targetIsPlayer, Message = text };

    // Azucar para el caso mas comun
    public static void Add(List<BattleEvent> events, string text) => events.Add(Msg(text));
}
