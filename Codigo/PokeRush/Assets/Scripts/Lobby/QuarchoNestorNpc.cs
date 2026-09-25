using UnityEngine;

// Nestor en la Quarcho Cueva, despues de rescatarlo en su Data Center.
//
// Es el tutor de MTs: le enseña a un Pokemon del equipo un movimiento que su
// especie aprende por MT o tutor en los juegos originales, a cambio de plata
// (ver TutorDeMovimientos y la pantalla MoveTutorScreen).
//
// Aparece recien con Torre1_NestorLiberado: eso lo hace un BarreraPorFlag al
// reves en el mismo objeto, no este componente.
[RequireComponent(typeof(Collider2D))]
public class QuarchoNestorNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Nestor")]
    [SerializeField] private string nombre = "NÉSTOR";
    [SerializeField] private Sprite retrato;
    [Tooltip("Lo que dice al elegir Charlar. Lo escribe el equipo en el asset.")]
    [SerializeField] private DialogueDefinition charla;

    [Header("El tutor")]
    [SerializeField] private PreciosDelTutor precios = new PreciosDelTutor();

    [Header("Textos")]
    [SerializeField, TextArea(2, 4)] private string saludo = "¿Qué hacemos? Por unas monedas le enseño una MT a cualquiera de tus Pokémon.";
    [SerializeField] private string opcionEnsenar = "Enseñar una MT";
    [SerializeField] private string opcionCharlar = "Charlar";
    [SerializeField] private string opcionNada = "Nada";
    [SerializeField] private string preguntaQuien = "¿A quién le enseño?";
    [SerializeField, TextArea(2, 4)] private string mensajeSinEquipo = "No tenés ningún Pokémon para entrenar.";
    [SerializeField, TextArea(2, 4)] private string mensajeSinCharla = "Estar de este lado de la pelea me sienta bien.";
    [Tooltip("{0} = el Pokemon, {1} = el movimiento.")]
    [SerializeField, TextArea(2, 4)] private string mensajeDespues = "{0} ya domina {1}. Cuando quieras, otra.";
    [SerializeField, TextArea(2, 4)] private string mensajeSinAprender = "Cuando te decidas, acá estoy.";

    public PreciosDelTutor Precios => precios;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        CasinoDialogo.Elegir(dialogueController, nombre, retrato, saludo,
            new[] { opcionEnsenar, opcionCharlar, opcionNada },
            eleccion =>
            {
                if (eleccion == 0) ElegirPokemon(player);
                else if (eleccion == 1) Charlar(story);
            });
        return true;
    }

    private void ElegirPokemon(PlayerModel player)
    {
        if (player.Team.Count == 0) { Decir(mensajeSinEquipo); return; }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) return;

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Party,
            new PartyScreenRequest { Mode = PartyScreenMode.SelectAny, Prompt = preguntaQuien },
            resultado =>
            {
                var r = resultado as PartyScreenResult;
                if (r == null || r.Cancelled || r.SelectedIndex < 0 || r.SelectedIndex >= player.Team.Count) return;
                AbrirTutor(player.Team[r.SelectedIndex]);
            });
    }

    private void AbrirTutor(PokenModel poken)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null || poken == null) return;

        menus.Open(MenuScreenId.MoveTutor,
            new MoveTutorRequest { Poken = poken, Maestro = nombre, Precios = precios },
            resultado =>
            {
                var r = resultado as MoveTutorResult;
                if (r == null || r.Aprendido == null) { Decir(mensajeSinAprender); return; }

                CasinoDialogo.Guardar();
                Decir(string.Format(mensajeDespues, poken.Definition.pokenName, r.Aprendido.moveName));
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
