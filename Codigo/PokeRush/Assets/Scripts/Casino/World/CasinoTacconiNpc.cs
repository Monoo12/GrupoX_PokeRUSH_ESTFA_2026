using System.Collections.Generic;
using UnityEngine;

// Tacconi, el apostador del piso 3.
//
// Tiene el Cosmog shiny, que es lo que pide el ascensor del piso 3, y es la
// UNICA forma de conseguirlo. Lo juega a una mano de blackjack contra uno de tus
// Pokemon: si ganas, te lo da; si perdes, se queda con el tuyo; si empatan, no
// pasa nada y se puede volver a jugar.
//
// Por que un Cosmog shiny: el pedido era "un Pokemon raro, no muy poderoso,
// shiny y medio meme". Cosmog es un legendario que en los juegos NO puede salir
// shiny (mas raro imposible), pelea con Salpicadura y Teletransporte, y es el
// "Nebby, get in the bag" de Sol y Luna. Se cambia desde el Inspector.
//
// Nunca deja al jugador sin equipo: no acepta la apuesta si, perdiendola, el
// equipo se queda sin nadie que pueda pelear.
//
// En la Quarcho Cueva es el MISMO componente con otro premio (un Mew) y con
// tope de chances: cada mano perdida gasta un flag de 'chances' y, gastados
// todos, no juega mas. En el casino 'chances' queda vacio y se puede jugar
// hasta ganar, como siempre.
[RequireComponent(typeof(Collider2D))]
public class CasinoTacconiNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Tacconi")]
    [SerializeField] private string nombre = "TACCONI";
    [Tooltip("Retrato de la caja de dialogo. Provisorio: el equipo va a pasar los sprites.")]
    [SerializeField] private Sprite retrato;
    [SerializeField] private StoryFlag flagGanado = StoryFlag.Casino_TacconiVencido;

    [Header("Lo que apuesta")]
    [SerializeField] private PokenDefinition especiePremio;
    [Min(1)] [SerializeField] private int nivelPremio = 15;
    [SerializeField] private bool premioShiny = true;

    [Header("Textos ({0} = el Pokemon de Tacconi)")]
    [SerializeField, TextArea(2, 4)] private string pregunta =
        "Soy Tacconi. ¿Ves este {0}? Único en todo el casino. Te lo juego a una mano de blackjack contra uno de tus Pokémon. ¿Jugamos?";
    [SerializeField] private string opcionJugar = "Jugar";
    [SerializeField] private string opcionNo = "Paso";
    [SerializeField, TextArea(2, 4)] private string mensajeRechazo =
        "Cuando te animes, acá estoy. De esta mesa no me muevo.";
    [SerializeField, TextArea(2, 4)] private string mensajeEquipoChico =
        "Con un solo Pokémon no te juego: si perdés, te quedás sin equipo.";
    [Tooltip("{0} = tu Pokemon, {1} = el de Tacconi.")]
    [SerializeField, TextArea(2, 4)] private string preguntaConfirmar =
        "¿Apostás a tu {0}? Si ganás, te llevás mi {1}. Si perdés, tu {0} se queda conmigo.";
    [SerializeField] private string opcionApostar = "Apostar";
    [SerializeField, TextArea(2, 4)] private string mensajeGanaste =
        "¡No puede ser! Tomá, el {0} es tuyo... Cuidalo, eh.";
    [Tooltip("{0} = el Pokemon de Tacconi, {1} = la caja del PC.")]
    [SerializeField, TextArea(2, 4)] private string mensajeGanasteAlPc =
        "¡No puede ser! El {0} es tuyo. Como tenés el equipo lleno, lo mandé a tu PC ({1}).";
    [SerializeField, TextArea(2, 4)] private string mensajeSinLugar =
        "Ganaste, pero no tenés lugar ni en el equipo ni en el PC. Hacé lugar y volvé: la apuesta queda en pie.";
    [Tooltip("{0} = tu Pokemon.")]
    [SerializeField, TextArea(2, 4)] private string mensajePerdiste =
        "Tacconi siempre gana. Tu {0} se queda conmigo. ¿Revancha?";
    [SerializeField, TextArea(2, 4)] private string mensajeEmpate = "Empate. Nadie pierde nada. ¿Otra?";
    [SerializeField, TextArea(2, 4)] private string mensajeSeFue = "¿Te arrepentiste? Mejor para vos.";
    [SerializeField, TextArea(2, 4)] private string mensajeYaGanado =
        "Ya te llevaste mi {0}. No me hagas acordar.";

    [Header("Chances (vacio = sin tope, como en el casino)")]
    [Tooltip("Un flag por mano PERDIDA, en orden. Gastados todos, Tacconi no " +
             "juega mas. El empate no gasta. En la Quarcho Cueva son tres.")]
    [SerializeField] private StoryFlag[] chances = new StoryFlag[0];
    [Tooltip("{0} = cuantas chances hubo.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSinChances =
        "Ya tuviste tus {0} chances. Tacconi no vuelve a jugar con vos.";
    [Tooltip("Se suma al cartel de la mano perdida. {0} = cuantas quedan.")]
    [SerializeField, TextArea(2, 4)] private string mensajeChancesQueQuedan = "Te quedan {0} chances.";
    [SerializeField, TextArea(2, 4)] private string mensajeUltimaChance = "Era tu última chance.";

    public PokenDefinition EspeciePremio => especiePremio;
    public bool PremioShiny => premioShiny;
    public StoryFlag FlagGanado => flagGanado;

    // ---------- chances ----------

    public int ChancesTotales => chances != null ? chances.Length : 0;

    public int ChancesUsadas(StoryModel story)
    {
        if (story == null || chances == null) return 0;
        int n = 0;
        foreach (StoryFlag f in chances)
            if (f != StoryFlag.None && story.HasFlag(f)) n++;
        return n;
    }

    // Sin tope nunca se queda sin chances.
    public bool SinChances(StoryModel story)
        => ChancesTotales > 0 && ChancesUsadas(story) >= ChancesTotales;

    // Gasta la proxima chance libre. Devuelve cuantas quedan.
    private int GastarChance(StoryModel story)
    {
        if (story == null || ChancesTotales == 0) return int.MaxValue;
        foreach (StoryFlag f in chances)
        {
            if (f == StoryFlag.None || story.HasFlag(f)) continue;
            story.ActivateFlag(f);
            break;
        }
        return Mathf.Max(0, ChancesTotales - ChancesUsadas(story));
    }

    private bool _jugando;

    private string NombrePremio
    {
        get
        {
            if (especiePremio == null) return "Pokémon";
            string n = string.IsNullOrEmpty(especiePremio.pokenName) ? especiePremio.name : especiePremio.pokenName;
            return premioShiny ? n + " shiny" : n;
        }
    }

    private static string NombreDe(PokenModel poken)
        => poken == null || poken.Definition == null ? "Pokémon" : poken.Definition.pokenName;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null || _jugando) return false;

        if (flagGanado != StoryFlag.None && story.HasFlag(flagGanado))
        {
            Decir(string.Format(mensajeYaGanado, NombrePremio));
            return true;
        }

        if (SinChances(story))
        {
            Decir(string.Format(mensajeSinChances, ChancesTotales));
            return true;
        }

        if (especiePremio == null)
        {
            Debug.LogError($"[Tacconi] '{name}' no tiene especie de premio asignada.");
            return false;
        }

        Preguntar(string.Format(pregunta, NombrePremio), opcionJugar, opcionNo, jugar =>
        {
            if (!jugar) { Decir(mensajeRechazo); return; }
            ElegirApuesta(player, story);
        });
        return true;
    }

    private void ElegirApuesta(PlayerModel player, StoryModel story)
    {
        if (player.Team.Count < 2)
        {
            Decir(mensajeEquipoChico);
            return;
        }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) return;

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Party,
            new PartyScreenRequest { Mode = PartyScreenMode.SelectAny, Prompt = "¿Qué Pokémon apostás?" },
            resultado =>
            {
                var r = resultado as PartyScreenResult;
                if (r == null || r.Cancelled || r.SelectedIndex < 0 || r.SelectedIndex >= player.Team.Count)
                {
                    Decir(mensajeRechazo);
                    return;
                }

                PokenModel apuesta = player.Team[r.SelectedIndex];
                string motivo = MotivoParaNoAceptar(player, apuesta);
                if (motivo != null) { Decir(motivo); return; }

                Preguntar(string.Format(preguntaConfirmar, NombreDe(apuesta), NombrePremio), opcionApostar, opcionNo, ok =>
                {
                    if (!ok) { Decir(mensajeRechazo); return; }
                    Jugar(player, story, apuesta);
                });
            });
    }

    // Por que Tacconi no acepta esa apuesta (null = la acepta). Publico para los tests.
    public static string MotivoParaNoAceptar(PlayerModel player, PokenModel apuesta)
    {
        if (player == null || apuesta == null || !player.Team.Contains(apuesta))
            return "Ese Pokémon no está en tu equipo.";
        if (player.Team.Count < 2)
            return "Con un solo Pokémon no te juego: si perdés, te quedás sin equipo.";

        var queda = new List<PokenModel>(player.Team);
        queda.Remove(apuesta);
        if (!PcReglas.HayQuienPelee(queda))
            return "Si perdés, te quedás sin nadie que pueda pelear. Curá a tu equipo y volvé.";

        return null;
    }

    private void Jugar(PlayerModel player, StoryModel story, PokenModel apuesta)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null) return;

        _jugando = true;
        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Blackjack,
            new BlackjackScreenRequest
            {
                Titulo = "La mesa de Tacconi",
                ApuestaPokemon = NombreDe(apuesta),
                PremioPokemon = "su " + NombrePremio,
                Rival = "Tacconi"
            },
            resultado =>
            {
                _jugando = false;
                BlackjackOutcome final = resultado is BlackjackScreenResult r
                    ? r.ResultadoApuestaPokemon
                    : BlackjackOutcome.Pendiente;

                string cartel = AplicarResultado(player, story, apuesta, final);
                if (final != BlackjackOutcome.Pendiente && final != BlackjackOutcome.Empata)
                    CasinoDialogo.Guardar();
                Decir(cartel);
            });
    }

    // Aplica lo que salio sobre el modelo y devuelve lo que dice Tacconi.
    // Publico para los tests: es toda la regla de la apuesta sin cajas ni mesa.
    public string AplicarResultado(PlayerModel player, StoryModel story, PokenModel apuesta, BlackjackOutcome resultado)
    {
        switch (resultado)
        {
            case BlackjackOutcome.Gana:
            case BlackjackOutcome.Blackjack:
            {
                var premio = new PokenModel(especiePremio, nivelPremio);
                if (premioShiny) premio.SetShiny(true);

                PokenRecibido recibido = player.RecibirPokemon(premio);
                if (recibido.Destino == DestinoDePokemon.SinLugar) return mensajeSinLugar;

                if (flagGanado != StoryFlag.None && story != null) story.ActivateFlag(flagGanado);
                Debug.Log($"[Tacconi] Ganaste: {NombrePremio} -> {recibido.Destino}.");

                return recibido.Destino == DestinoDePokemon.Pc
                    ? string.Format(mensajeGanasteAlPc, NombrePremio, PokenStorageModel.NombreDeCaja(recibido.Caja))
                    : string.Format(mensajeGanaste, NombrePremio);
            }

            case BlackjackOutcome.Pierde:
            {
                // Se va de verdad: Tacconi se lo queda. Por referencia y no por
                // indice, por si el orden del equipo cambio mientras tanto.
                player.Team.Remove(apuesta);
                Debug.Log($"[Tacconi] Perdiste: se queda con {NombreDe(apuesta)}.");

                string cartel = string.Format(mensajePerdiste, NombreDe(apuesta));
                if (ChancesTotales == 0) return cartel;

                int quedan = GastarChance(story);
                return cartel + " " + (quedan > 0
                    ? string.Format(mensajeChancesQueQuedan, quedan)
                    : mensajeUltimaChance);
            }

            case BlackjackOutcome.Empata:
                return mensajeEmpate;

            default:
                return mensajeSeFue;
        }
    }

    private void Decir(string linea)
    {
        CasinoDialogo.Decir(dialogueController, nombre, retrato, new[] { linea });
    }

    private void Preguntar(string texto, string si, string no, System.Action<bool> respuesta)
    {
        CasinoDialogo.Preguntar(dialogueController, nombre, retrato, texto, si, no, respuesta);
    }
}
