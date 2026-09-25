using UnityEngine;

// La mesa de ruleta puesta en el mapa.
//
// Mismo patron que el resto de las maquinas del casino: se cuelga del mueble
// que dibujo el equipo de nivel y abre la UI encima del piso, sin cambiar de
// escena. La diferencia con la ruleta de premios es que esta SI oscurece el
// fondo (lo decide RouletteScreen por DimsBackground): la mesa ocupa toda la
// pantalla y con el mapa encendido detras no se lee ninguna casilla.
[RequireComponent(typeof(Collider2D))]
public class RouletteTableInteractable : MonoBehaviour, IInteractable
{
    [Header("Mesa")]
    [Tooltip("Titulo del cartel. Vacio = el que trae la pantalla.")]
    [SerializeField] private string titulo = "";
    [Tooltip("Apuesta minima para poder girar. 0 = la del prefab.")]
    [SerializeField] private int apuestaMinima = 0;
    [Tooltip("Maximo por casillero. 0 = el del prefab.")]
    [SerializeField] private int apuestaMaxima = 0;

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, la mesa solo se puede usar teniendo ya este flag.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeBloqueada = "La mesa esta cerrada.";
    [Tooltip("Con menos plata que esto, el croupier ni te deja sentarte.")]
    [SerializeField] private int plataMinima = 1;
    [SerializeField] private string mensajeSinPlata = "No te alcanza ni para una ficha.";

    [Header("Textos")]
    [Tooltip("Lo que dice antes de abrir. Vacio = abre de una.")]
    [SerializeField, TextArea(2, 3)] private string mensajeDeBienvenida =
        "El croupier te hace lugar en la mesa.";
    [Tooltip("Cartel al cerrar habiendo ganado en limpio.")]
    [SerializeField, TextArea(2, 3)] private string mensajeGanaste =
        "Te vas con las manos llenas. El croupier te saluda con la cabeza.";
    [Tooltip("Cartel al cerrar habiendo perdido en limpio.")]
    [SerializeField, TextArea(2, 3)] private string mensajePerdiste =
        "La casa siempre gana. Volvé cuando quieras la revancha.";

    [Header("Ruleta de premios por ganar al color")]
    [Tooltip("Ruleta que se abre al ganar en limpio MAS que el umbral apostando a " +
             "rojo o negro en un giro. Vacio = nunca.")]
    [SerializeField] private CasinoPrizeTable ruletaPorColor;
    [Tooltip("Cuanto hay que ganar al color en un giro (en limpio, sin contar la ficha).")]
    [Min(0)] [SerializeField] private int umbralRuletaPorColor = 200;

    public CasinoPrizeTable RuletaPorColor => ruletaPorColor;
    public int UmbralRuletaPorColor => umbralRuletaPorColor;
    public int ApuestaMaxima => apuestaMaxima;

    [Header("Objetivo del piso (opcional)")]
    [Tooltip("Flag que activa despues del primer giro. Sirve para destrabar el " +
             "ascensor sin atarlo a tener suerte.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;
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
            Debug.LogError("[Ruleta] No hay MenuManager: no se puede abrir la mesa.");
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Roulette,
            new RouletteScreenRequest
            {
                Titulo = titulo,
                ApuestaMinima = apuestaMinima,
                ApuestaMaxima = apuestaMaxima,
                RuletaPorColor = ruletaPorColor,
                UmbralRuletaPorColor = umbralRuletaPorColor
            },
            resultado => AlCerrar(resultado as RouletteScreenResult));
    }

    private void AlCerrar(RouletteScreenResult resultado)
    {
        if (resultado == null || resultado.Giros <= 0) return;

        // El objetivo del piso se cumple con haber jugado, no con lo que salio:
        // atarlo a ganar dejaria al jugador encerrado por mala suerte, y la
        // progresion no puede depender del azar.
        bool cumplioObjetivo = otorgaFlag != StoryFlag.None
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
        if (menus == null) { Debug.Log($"[Ruleta] {mensaje}"); alCerrar?.Invoke(); return; }

        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = mensaje },
            _ => alCerrar?.Invoke());
    }
}
