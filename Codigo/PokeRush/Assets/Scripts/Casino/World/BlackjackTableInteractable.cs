using UnityEngine;

// La mesa de blackjack puesta en el mapa (piso 3 del casino).
//
// Mismo patron que el resto de las maquinas del casino: se cuelga del mueble
// que dibujo el equipo de nivel y abre la UI encima del piso, sin cambiar de
// escena. Igual que la mesa de ruleta, esta SI oscurece el fondo (lo decide
// BlackjackScreen por DimsBackground): la mesa ocupa toda la pantalla y con el
// mapa encendido detras no se lee ninguna carta.
//
// Diferencia con la ruleta en el objetivo del piso: la ruleta destraba el
// ascensor con solo JUGAR, porque atarlo a ganar dejaria al jugador encerrado
// por mala suerte. Aca el flag pide ganar una mano, y eso se puede hacer sin
// depender de la suerte: si la ronda sale mal se vuelve a jugar, y la apuesta
// minima de la mesa esta puesta para que siempre alcance. Ver
// CasinoProgressModel.BlackjackGanado.
[RequireComponent(typeof(Collider2D))]
public class BlackjackTableInteractable : MonoBehaviour, IInteractable
{
    [Header("Mesa")]
    [Tooltip("Titulo del cartel. Vacio = el que trae la pantalla.")]
    [SerializeField] private string titulo = "";
    [Tooltip("Apuesta minima por mano. 0 = la del prefab.")]
    [SerializeField] private int apuestaMinima = 0;
    [Tooltip("Apuesta maxima por mano. 0 = la del prefab.")]
    [SerializeField] private int apuestaMaxima = 0;

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, la mesa solo se puede usar teniendo ya este flag.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeBloqueada = "La mesa está cerrada.";
    [Tooltip("Con menos plata que esto, el croupier ni te deja sentarte.")]
    [SerializeField] private int plataMinima = 5;
    [SerializeField] private string mensajeSinPlata = "No te alcanza ni para la apuesta mínima.";

    [Header("Textos")]
    [Tooltip("Lo que dice antes de abrir. Vacio = abre de una.")]
    [SerializeField, TextArea(2, 3)] private string mensajeDeBienvenida =
        "El croupier baraja y te hace lugar en la mesa.";
    [Tooltip("Cartel al cerrar habiendo ganado en limpio.")]
    [SerializeField, TextArea(2, 3)] private string mensajeGanaste =
        "Te levantás con las fichas llenas. El croupier te saluda con la cabeza.";
    [Tooltip("Cartel al cerrar habiendo perdido en limpio.")]
    [SerializeField, TextArea(2, 3)] private string mensajePerdiste =
        "La casa siempre gana. Volvé cuando quieras la revancha.";

    [Header("Objetivo del piso (opcional)")]
    [Tooltip("Flag que activa al ganar una mano. Es lo que destraba el ascensor " +
             "del piso 3.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;
    [Tooltip("Con esto puesto no alcanza con ganar: hay que hacer blackjack.")]
    [SerializeField] private bool exigeBlackjack = false;
    [SerializeField, TextArea(2, 3)] private string mensajeObjetivo =
        "El de seguridad te mira distinto. El ascensor ya te deja subir.";

    private StoryModel _story;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        // Se guarda para poder otorgar el flag al cerrar la mesa: para ese
        // entonces ya no hay nadie llamando a Interact.
        _story = story;

        if (requiereFlag != StoryFlag.None && (story == null || !story.HasFlag(requiereFlag)))
        {
            Avisar(mensajeBloqueada);
            return true;
        }

        if (player.Money < Mathf.Max(1, plataMinima))
        {
            Avisar(mensajeSinPlata);
            return true;
        }

        if (string.IsNullOrEmpty(mensajeDeBienvenida)) Abrir();
        else Avisar(mensajeDeBienvenida, Abrir);

        return true;
    }

    private void Abrir()
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[Blackjack] No hay MenuManager: no se puede abrir la mesa.");
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Blackjack,
            new BlackjackScreenRequest
            {
                Titulo = titulo,
                ApuestaMinima = apuestaMinima,
                ApuestaMaxima = apuestaMaxima
            },
            resultado => AlCerrar(resultado as BlackjackScreenResult));
    }

    private void AlCerrar(BlackjackScreenResult resultado)
    {
        if (resultado == null || resultado.Manos <= 0) return;

        bool logrado = exigeBlackjack ? resultado.HizoBlackjack : resultado.GanoAlgunaMano;
        bool cumplioObjetivo = otorgaFlag != StoryFlag.None
                               && logrado
                               && _story != null
                               && !_story.HasFlag(otorgaFlag);

        if (cumplioObjetivo) _story.ActivateFlag(otorgaFlag);

        string despedida = resultado.Neto >= 0 ? mensajeGanaste : mensajePerdiste;

        // Si ademas se destrabo el piso, ese cartel es el que importa y va
        // ultimo: encadenar dos cajas seguidas se lee peor que una sola.
        if (cumplioObjetivo) Avisar(despedida, () => Avisar(mensajeObjetivo));
        else Avisar(despedida);
    }

    private void Avisar(string mensaje, System.Action alCerrar = null)
    {
        if (string.IsNullOrEmpty(mensaje)) { alCerrar?.Invoke(); return; }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Blackjack] {mensaje}"); alCerrar?.Invoke(); return; }

        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = mensaje },
            _ => alCerrar?.Invoke());
    }
}
