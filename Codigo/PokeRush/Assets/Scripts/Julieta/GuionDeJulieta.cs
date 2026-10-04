using UnityEngine;

// Los tipos del guion de la pelea contra Julieta.
//
// La pelea NO esta escrita en codigo: es una lista de MOMENTOS (asset
// GuionDeJulieta). Cada momento dice CUANDO pasa (una o varias condiciones:
// alcanza con que se cumpla una), en QUE FASE, y trae sus LINEAS de dialogo.
// Cada linea puede disparar una ACCION del combate al cerrarse: pasar de fase,
// dar vuelta la ruleta, apagar a Nestor, meterse en un Pokemon del jugador...
//
// Asi los dialogos disparan los eventos del combate, y cambiar el ritmo de la
// pelea (cuando habla, cuando cambia de fase, que dice) es tocar el asset desde
// el Inspector, sin recompilar. Quien ejecuta todo esto es PeleaJulieta.

// Las fases de la pelea. Los valores nuevos van SIEMPRE al final: el asset
// guarda la fase de cada momento como numero.
public enum FaseDeJulieta
{
    // Fase 1: Julieta estudia al jugador. Nestor ayuda y la ruleta juega a favor.
    Analisis,
    // Fase 2: activa el NeuraRush-LINK y da vuelta las reglas: la ruleta pasa a
    // jugar para ella y usa el protocolo de Nestor contra el jugador.
    ControlDelSistema,
    // Fase 3: se mete en un Pokemon del jugador, que deja de obedecer y ataca
    // al propio PJ (la barra de vida del PJ).
    ControlDelVinculo,
    // El PJ rompe el control: todo vuelve a su lado y Julieta ya puede caer.
    Ruptura
}

// Cuando se dispara un momento. 'valor' es el umbral de cada uno.
// Los valores nuevos van SIEMPRE al final (el asset los guarda como numero).
public enum DisparadorDeJulieta
{
    AlEmpezar,             // antes del primer turno (valor no se usa)
    AlEntrarALaFase,       // apenas empieza la fase del momento
    TurnosEnLaFase,        // se jugaron 'valor' turnos desde que empezo la fase
    AlRivalLeQuedan,       // a Julieta le quedan 'valor' Pokemon o menos en pie
    RivalActivoBajo,       // el Pokemon activo de Julieta tiene 'valor'% de PS o menos
    UltimoDelRival,        // Julieta mando a su ultimo Pokemon
    JugadorPerdio,         // se le debilitaron 'valor' Pokemon al jugador
    NestorLeyo,            // Nestor corto 'valor' jugadas en toda la pelea
    RuletaGiroEnLaFase,    // la ruleta giro 'valor' veces en esta fase
    VidaDelPJBaja,         // la vida del PJ esta en 'valor'% o menos
    ControladoAtacoAlPJ,   // el Pokemon controlado ataco al PJ 'valor' veces
    ControladoCayo,        // el Pokemon controlado se debilito
    RivalSalvado           // el ultimo de Julieta no cayo porque todavia no hubo ruptura
}

// Que le pasa al combate cuando se cierra una linea. 'parametro' segun cada una.
// Los valores nuevos van SIEMPRE al final (el asset los guarda como numero).
public enum AccionDeJulieta
{
    Ninguna,
    PasarAFase,                // parametro = FaseDeJulieta
    EncenderRuleta,            // parametro = QuienCobraLaRuleta (la cambia si ya estaba)
    ApagarRuleta,
    EncenderNestor,            // la lectura aliada, con los numeros de PeleaJulieta
    ApagarNestor,
    EncenderLecturaDeJulieta,  // la Prediccion de la torre 1, pero contra el jugador y firmada por Julieta
    ApagarLecturaDeJulieta,
    ControlarPokemon,          // se mete en el Pokemon activo del jugador (o en el primero en pie)
    LiberarPokemon,            // el controlado vuelve a obedecer
    MostrarVidaDelPJ,          // parametro 1 = mostrar, 0 = esconder
    PerfilDeIA,                // parametro 0 = el de estudiar, 1 = el de pelear en serio
    SenalDeControl,            // parametro 1 = prende la señal de "Julieta controla", 0 = la apaga
    Destello,                  // golpe visual (glitch) sin tocar reglas
    RomperServidor             // el PJ destruye el nucleo: el servidor se apaga y la sala queda a oscuras
}

public enum HablanteDeJulieta
{
    Julieta,
    PJ,       // el nombre lo pone la partida
    Nestor,
    Sistema,  // sin cartel de nombre: los avisos del NeuraRush-LINK y lo que se narra
    Tixi      // por el transmisor que le dio al PJ en la cueva
}

[System.Serializable]
public class LineaDeJulieta
{
    public HablanteDeJulieta hablante;

    [Tooltip("{PJ} se reemplaza por el nombre del jugador y {POKEMON} por el del " +
             "Pokemon controlado (o el activo del jugador si no hay ninguno).")]
    [TextArea(2, 4)] public string texto;

    [Tooltip("Opcional: otra cara para ESTA linea (Nestor asustado, por ejemplo). " +
             "Vacio = el retrato del hablante.")]
    public Sprite retrato;

    [Tooltip("Opcional: suena apenas aparece la linea (una alarma, el nucleo que explota).")]
    public AudioClip sonido;

    [Tooltip("Que le pasa al combate apenas se cierra esta linea.")]
    public AccionDeJulieta accion;
    public int parametro;

    public LineaDeJulieta() { }

    public LineaDeJulieta(HablanteDeJulieta quien, string que,
                          AccionDeJulieta hace = AccionDeJulieta.Ninguna, int con = 0)
    {
        hablante = quien;
        texto = que;
        accion = hace;
        parametro = con;
    }
}

[System.Serializable]
public class CondicionDeJulieta
{
    public DisparadorDeJulieta cuando;
    public int valor;

    public CondicionDeJulieta() { }
    public CondicionDeJulieta(DisparadorDeJulieta c, int v = 0) { cuando = c; valor = v; }
}

[System.Serializable]
public class MomentoDeJulieta
{
    [Tooltip("Para leerlo en el Inspector y en los logs. Tiene que ser unico.")]
    public string nombre;

    [Tooltip("Solo se dispara en esta fase (salvo que 'cualquierFase' este prendido).")]
    public FaseDeJulieta fase;
    public bool cualquierFase;

    [Tooltip("Alcanza con que se cumpla UNA. Cada momento suena una sola vez por pelea.")]
    public CondicionDeJulieta[] cuando = new CondicionDeJulieta[0];

    public LineaDeJulieta[] lineas = new LineaDeJulieta[0];
}

// El guion entero de la pelea: lo que dice antes, durante y despues, y el
// cierre del juego (epilogo, la charla final en la cueva y la ultima conexion).
[CreateAssetMenu(fileName = "GuionDeJulieta", menuName = "PokeRush/Julieta/Guion de la pelea")]
public class GuionDeJulieta : ScriptableObject
{
    [Header("Nombres en la caja de dialogo")]
    public string nombreJulieta = "JULIETA";
    public string nombreNestor = "NÉSTOR";
    public string nombreTixi = "TIXI";

    [Header("Retratos (opcionales)")]
    public Sprite retratoJulieta;
    public Sprite retratoNestor;
    public Sprite retratoTixi;
    [Tooltip("Retrato del PJ segun el genero elegido. Vacio = caja sin cara.")]
    public Sprite retratoPJVaron;
    public Sprite retratoPJMujer;

    [Header("En la arena, antes de pelear")]
    public LineaDeJulieta[] alEncontrarla = new LineaDeJulieta[0];
    [Tooltip("Las veces siguientes (despues de perder): la charla entera no se repite. " +
             "Vacio = vuelve a decir 'alEncontrarla'.")]
    public LineaDeJulieta[] alReintentar = new LineaDeJulieta[0];

    [Header("Durante la pelea (en orden: si dos se cumplen juntos, suena primero el de arriba)")]
    public MomentoDeJulieta[] momentos = new MomentoDeJulieta[0];

    [Header("Si el jugador pierde")]
    public LineaDeJulieta[] alPerder = new LineaDeJulieta[0];

    [Header("Final del juego: despues de ganarle, antes de los creditos")]
    public LineaDeJulieta[] finalDelJuego = new LineaDeJulieta[0];

    [Header("Si se vuelve a hablarle despues de ganarle (sin ir a los creditos)")]
    public LineaDeJulieta[] yaVencida = new LineaDeJulieta[0];

    // Lo que sigue lo cuenta EscenaFinalEnLaCueva, en la Quarcho Cueva, entre
    // el fundido de la arena y los creditos (ver FinalDelJuego).

    [Header("El cierre (despues de la arena)")]
    [Tooltip("La letra del epilogo y de la ultima conexion. Vacio = la de Unity.")]
    public Font fuente;

    [Header("Epilogo: texto sobre negro, antes de volver a la cueva")]
    [Tooltip("Una linea por pantalla, con {PJ} para el nombre del jugador. Vacio = " +
             "se saltea.")]
    [TextArea(2, 4)] public string[] epilogo = new string[0];

    [Header("La vuelta a la Quarcho Cueva")]
    [Tooltip("La charla final con Tixi. Vacio = de la cueva se pasa directo a la ultima conexion.")]
    public DialogueDefinition enLaCueva;
    [Tooltip("Lo ultimo que aparece en una pantalla de la cueva, linea por linea, " +
             "mientras la camara se aleja. Vacio = de la charla se va a los creditos.")]
    [TextArea(1, 2)] public string[] ultimaConexion = new string[0];
}
