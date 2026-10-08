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
    BattleEnded,      // fin del combate
    Visibilidad,      // un Poken se esconde (Excavar, Vuelo...) o vuelve a aparecer (ver Visible)
    QuiereAprender,   // subio de nivel y quiere aprender Move, pero ya sabe 4: el jugador decide (ver Poken)
    RivalVaAEnviar    // cayo el del rival y el motor espera: el jugador puede cambiar antes de que entre el siguiente
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

    // En HpChanged: los PS que tenia el Poken JUSTO DESPUES de este cambio.
    // En PokenSwitched: los PS con que entro.
    // -1 = sin sellar (se cae al valor actual del modelo).
    //
    // Hace falta porque el turno se resuelve ENTERO antes de reproducirse: para
    // cuando el jugador ve la animacion, el modelo ya tiene aplicados todos los
    // cambios del turno. Sin la foto, animar "hacia el HP actual" hacia que la
    // curacion de una pocion saltara directo al valor final —ya con el golpe
    // del rival restado— y el ataque siguiente no moviera la barra.
    public int HpAfter = -1;

    // Solo en Visibilidad: false = se escondio (bajo tierra, en el aire, bajo
    // el agua), true = volvio a aparecer.
    public bool Visible = true;

    // En QuiereAprender: el Poken del jugador que subio de nivel (Move es el
    // movimiento nuevo). Puede no ser el que esta en el campo.
    // En PokenSwitched: el que entro (ver Entro).
    public PokenModel Poken;

    public static BattleEvent Msg(string text)
        => new BattleEvent { Kind = BattleEventKind.Message, Message = text };

    public static BattleEvent Of(BattleEventKind kind, bool targetIsPlayer, string text = null)
        => new BattleEvent { Kind = kind, TargetIsPlayer = targetIsPlayer, Message = text };

    // PokenSwitched con la foto de quien entro y con cuantos PS. Por lo mismo
    // que HpAfter: al reproducirse, el activo del modelo ya es el del FINAL del
    // turno. Si el que entra cae en el mismo turno y el rival manda otro, el
    // modelo ya tiene al siguiente, y dibujar "el activo" mostraba a ese en vez
    // del que de verdad entro y cayo (bug 5 del testing).
    public static BattleEvent Entro(bool targetIsPlayer, PokenModel poken, string text)
        => new BattleEvent
        {
            Kind = BattleEventKind.PokenSwitched,
            TargetIsPlayer = targetIsPlayer,
            Message = text,
            Poken = poken,
            HpAfter = poken != null ? poken.CurrentHP : -1
        };

    // Azucar para el caso mas comun
    public static void Add(List<BattleEvent> events, string text) => events.Add(Msg(text));
}
