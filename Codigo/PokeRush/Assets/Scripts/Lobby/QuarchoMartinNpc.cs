using UnityEngine;

// Martin Reymar en la Quarcho Cueva, despues de rescatarlo del casino.
//
// Trae dos cosas del casino:
//   - Sus tres ruletas: de Pokemon, de objetos y de mejoras (una mejora vale
//     para la PROXIMA batalla; ver MejorasDeBatalla). Cada tiro se paga.
//   - Los tres juegos del casino: la tragaperras, la mesa de ruleta y el
//     blackjack. Son los MISMOS componentes que en los pisos del casino
//     ('juegos'); Martin solo les pasa la posta.
//
// Aparece recien con Casino_Completado: eso lo hace un BarreraPorFlag al reves
// en el mismo objeto, no este componente.
[RequireComponent(typeof(Collider2D))]
public class QuarchoMartinNpc : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class Ruleta
    {
        [Tooltip("Como sale en el menu: 'Ruleta de {nombre}'.")]
        public string nombre = "Pokémon";
        public CasinoPrizeTable tabla;
        [Min(0)] public int precio = 300;
    }

    [System.Serializable]
    public class Juego
    {
        public string nombre = "Tragaperras";
        [Tooltip("Un componente IInteractable del casino: SlotMachineInteractable, " +
                 "RouletteTableInteractable o BlackjackTableInteractable.")]
        public MonoBehaviour interactuable;
    }

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Martin")]
    [SerializeField] private string nombre = "MARTÍN";
    [SerializeField] private Sprite retrato;
    [Tooltip("Lo que dice al elegir Charlar. Lo escribe el equipo en el asset.")]
    [SerializeField] private DialogueDefinition charla;

    [Header("Lo que trajo del casino")]
    [SerializeField] private Ruleta[] ruletas = new Ruleta[0];
    [SerializeField] private Juego[] juegos = new Juego[0];

    [Header("Textos")]
    [SerializeField, TextArea(2, 4)] private string saludo = "La casa abrió sucursal. ¿Qué jugamos?";
    [Tooltip("{0} = el nombre de la ruleta, {1} = el precio.")]
    [SerializeField] private string formatoRuleta = "Ruleta de {0} (${1})";
    [SerializeField] private string opcionJuegos = "Juegos del casino";
    [SerializeField] private string opcionCharlar = "Charlar";
    [SerializeField] private string opcionNada = "Nada";
    [SerializeField, TextArea(2, 4)] private string preguntaJuegos = "¿A cuál te sentás?";
    [SerializeField] private string opcionVolver = "Volver";
    [Tooltip("{0} = el precio, {1} = lo que tiene el jugador.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSinPlata = "Cada tiro son {0} monedas y tenés {1}. La casa no fía.";
    [SerializeField, TextArea(2, 4)] private string mensajeSinCharla = "La casa siempre gana. Salvo cuando pierde, como me pasó con vos.";

    public Ruleta[] Ruletas => ruletas;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        // Menu: una opcion por ruleta, los juegos, charlar y nada (siempre la
        // ultima, que es la que elige Escape).
        int n = ruletas.Length;
        var opciones = new string[n + 3];
        for (int i = 0; i < n; i++)
            opciones[i] = string.Format(formatoRuleta, ruletas[i].nombre, ruletas[i].precio);
        opciones[n] = opcionJuegos;
        opciones[n + 1] = opcionCharlar;
        opciones[n + 2] = opcionNada;

        CasinoDialogo.Elegir(dialogueController, nombre, retrato, saludo, opciones, eleccion =>
        {
            if (eleccion >= 0 && eleccion < n) Tirar(player, ruletas[eleccion]);
            else if (eleccion == n) ElegirJuego(player, story);
            else if (eleccion == n + 1) Charlar(story);
        });
        return true;
    }

    // El cobro sobre el modelo, sin cajas. Publico para los tests. No guarda:
    // la ruleta guarda al cerrarse, con el premio ya entregado.
    public static bool Cobrar(PlayerModel player, Ruleta ruleta)
    {
        if (player == null || ruleta == null || ruleta.tabla == null || player.Money < ruleta.precio) return false;
        player.SpendMoney(ruleta.precio);
        return true;
    }

    private void Tirar(PlayerModel player, Ruleta ruleta)
    {
        if (ruleta == null || ruleta.tabla == null) return;

        if (!Cobrar(player, ruleta))
        {
            Decir(string.Format(mensajeSinPlata, ruleta.precio, player.Money));
            return;
        }
        CasinoDialogo.AbrirRuletaDePremio(ruleta.tabla);
    }

    private void ElegirJuego(PlayerModel player, StoryModel story)
    {
        int n = juegos.Length;
        var opciones = new string[n + 1];
        for (int i = 0; i < n; i++) opciones[i] = juegos[i].nombre;
        opciones[n] = opcionVolver;

        CasinoDialogo.Elegir(dialogueController, nombre, retrato, preguntaJuegos, opciones, eleccion =>
        {
            if (eleccion < 0 || eleccion >= n) { Interact(player, story); return; }
            if (juegos[eleccion].interactuable is IInteractable juego) juego.Interact(player, story);
        });
    }

    private void Charlar(StoryModel story)
    {
        if (charla != null && dialogueController != null) dialogueController.StartDialogue(charla, story);
        else Decir(mensajeSinCharla);
    }

    private void Decir(string linea)
        => CasinoDialogo.Decir(dialogueController, nombre, retrato, new[] { linea });
}
