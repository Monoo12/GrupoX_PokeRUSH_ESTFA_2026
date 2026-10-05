using UnityEngine;

// Un NPC del casino que vende tiros de la ruleta de premios. En el piso 3 es el
// empresario: por 'precio' monedas (300 de arranque) se gira una vez su ruleta,
// que siempre da un Pokemon. Se puede tirar todas las veces que se quiera
// mientras alcance la plata.
//
// Sigue siendo un NPC de historia: si el jugador dice que no, cuenta lo suyo
// ('charla'). Asi el dialogo de historia se sigue escribiendo en el asset, como
// el de los demas (ver NpcQueHabla), y no se pierde por venderle la ruleta.
//
// Nombre y retrato salen de 'charla' si no se cargan a mano.
[RequireComponent(typeof(Collider2D))]
public class CasinoRuletaPagaNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("La ruleta")]
    [SerializeField] private CasinoPrizeTable ruleta;
    [Tooltip("Lo que cuesta cada tiro.")]
    [Min(0)] [SerializeField] private int precio = 300;

    [Header("Quien habla (vacio = los de 'charla')")]
    [SerializeField] private string nombre = "";
    [SerializeField] private Sprite retrato;

    [Header("Textos")]
    [Tooltip("Lo que cuenta si el jugador no quiere tirar. Vacio = 'mensajeRechazo'.")]
    [SerializeField] private DialogueDefinition charla;
    [Tooltip("{0} = el precio.")]
    [SerializeField, TextArea(2, 4)] private string pregunta =
        "¿Te animás a la ruleta de la casa? Por {0} monedas girás una vez, y siempre sale un Pokémon.";
    [SerializeField] private string opcionTirar = "Tirar";
    [SerializeField] private string opcionNo = "No";
    [Tooltip("{0} = el precio, {1} = lo que tiene el jugador.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSinPlata =
        "Cada tiro son {0} monedas y tenés {1}. Volvé cuando tengas la plata.";
    [SerializeField, TextArea(2, 4)] private string mensajeRechazo = "Cuando quieras. La ruleta no se va a ningún lado.";

    public CasinoPrizeTable Ruleta => ruleta;
    public int Precio => precio;

    private string Nombre => !string.IsNullOrEmpty(nombre) ? nombre
        : charla != null ? charla.speakerName : "";
    private Sprite Retrato => retrato != null ? retrato
        : charla != null ? charla.speakerPortrait : null;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || ruleta == null) return false;

        if (player.Money < precio)
        {
            Decir(string.Format(mensajeSinPlata, precio, player.Money));
            return true;
        }

        CasinoDialogo.Preguntar(dialogueController, Nombre, Retrato,
            string.Format(pregunta, precio), opcionTirar, opcionNo,
            tirar =>
            {
                if (!tirar) { Charlar(story); return; }
                if (Cobrar(player)) CasinoDialogo.AbrirRuletaDePremio(ruleta);
                else Decir(string.Format(mensajeSinPlata, precio, player.Money));
            });
        return true;
    }

    // El cobro sobre el modelo, sin cajas. Publica para los tests. No guarda:
    // la ruleta guarda al cerrarse, con el premio ya entregado.
    public bool Cobrar(PlayerModel player)
    {
        if (player == null || ruleta == null || player.Money < precio) return false;
        player.SpendMoney(precio);
        return true;
    }

    private void Charlar(StoryModel story)
    {
        if (charla == null) { Decir(mensajeRechazo); return; }

        if (dialogueController != null) dialogueController.StartDialogue(charla, story);
        else CasinoDialogo.Decir(null, charla.speakerName, charla.speakerPortrait, charla.lines);
    }

    private void Decir(string linea)
    {
        CasinoDialogo.Decir(dialogueController, Nombre, Retrato, new[] { linea });
    }
}
