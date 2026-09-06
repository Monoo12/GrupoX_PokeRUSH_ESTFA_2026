using System.Collections.Generic;

// Identidad de cada pantalla. El MenuManager arma su registro con esto en vez
// de con nombres de GameObject, asi renombrar el prefab no rompe nada.
public enum MenuScreenId
{
    Pause,
    Bag,
    Party,
    Summary,
    Options,
    MessageBox,

    // Casino (nivel 2). Son pantallas del mismo sistema a proposito: asi los
    // minijuegos heredan la pila, el input y la caja de mensajes, y el jugador
    // recupera el control al cerrarlas sin que nadie tenga que acordarse.
    Slots,
    Roulette,

    // Ruleta de premios: no es una mesa de apuestas, es el minijuego de regalos
    // que se abre encima del mapa sin tapar el escenario.
    PrizeWheel
}

// Desde donde se abrio el sistema de menus. Las pantallas compartidas (mochila,
// party) preguntan esto para decidir que se puede hacer; NO existe una version
// de la mochila para el mundo y otra para el combate.
public enum MenuContextKind
{
    Overworld,
    Battle
}

// ---------- mochila ----------

// Bolsillos de la mochila. Se corresponden 1 a 1 con las tres listas de
// InventoryModel; el enum existe para que la UI no tenga que saber cual lista
// es cual.
public enum ItemPocket
{
    Medicine,   // InventoryModel.HealItems
    Battle,     // InventoryModel.BattleItems
    Key         // InventoryModel.SpecialItems
}

public class BagScreenRequest
{
    // Si viene en null, la pantalla usa MenuManager.Context.
    public MenuContextKind? Context;
    // Bolsillo en el que abrir. Null = el ultimo que se estaba mirando.
    public ItemPocket? Pocket;
}

public class BagScreenResult
{
    public bool Cancelled = true;
    // Objeto de curacion efectivamente usado (null si no se uso ninguno).
    public HealItemDefinition UsedHealItem;
    // Indice en el equipo del Poken sobre el que se aplico. -1 = ninguno.
    public int TargetIndex = -1;
    // El combate tiene que consumir el turno por este uso.
    public bool ConsumesBattleTurn;
}

// ---------- party ----------

public enum PartyScreenMode
{
    // Administrar el equipo: ver resumen, reordenar.
    Browse,
    // Elegir un Poken como objetivo de un objeto.
    SelectTarget,
    // Elegir un Poken para mandar al campo (cambio en combate).
    SelectSwitch
}

public class PartyScreenRequest
{
    public PartyScreenMode Mode = PartyScreenMode.Browse;
    public MenuContextKind? Context;
    // Objeto que se va a aplicar en modo SelectTarget.
    public HealItemDefinition Item;
    // Texto de la barra inferior. Null = el que corresponda al modo.
    public string Prompt;
    // Indices que no se pueden elegir (ej: el Poken que ya esta en el campo).
    public HashSet<int> BlockedIndices;
    // Con esto en false la pantalla no se cierra con "volver": el jugador tiene
    // que elegir si o si (el caso del Poken que se debilita en combate).
    public bool CanCancel = true;
}

public class PartyScreenResult
{
    public bool Cancelled = true;
    public int SelectedIndex = -1;
    // En SelectTarget: si el objeto se llego a aplicar de verdad.
    public bool ItemApplied;
}

// ---------- resumen ----------

public class SummaryRequest
{
    public IList<PokenModel> Team;
    public int StartIndex;
    // Si es false, no deja reordenar movimientos (ej: abierto en combate).
    public bool AllowMoveReorder = true;
}

// ---------- caja de mensajes ----------

public class MessageBoxRequest
{
    public string Message;
    // Con pregunta muestra SI/NO; sin pregunta espera un Confirmar para cerrar.
    public bool IsQuestion;
    public string YesLabel = "SI";
    public string NoLabel = "NO";
    // Si es > 0, la caja se cierra sola pasado ese tiempo (para "Guardando...").
    public float AutoCloseSeconds;
    // Con esto en false la caja no acepta input: la cierra quien la abrio.
    public bool ClosableByPlayer = true;
}

public class MessageBoxResult
{
    public bool Confirmed;
}
