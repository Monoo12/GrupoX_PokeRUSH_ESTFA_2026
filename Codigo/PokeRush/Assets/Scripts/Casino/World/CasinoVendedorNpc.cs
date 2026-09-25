using UnityEngine;

// Un NPC del casino que vende un objeto clave. En el piso 2 es el revendedor
// de la Tarjeta VIP: una de las tres formas de conseguirla (las otras son
// ganarle al magnate o que salga en la ruleta de premios de un combate).
//
// Habla por el DialogueController del mapa, como el resto de los NPCs del casino
// (ver CasinoDialogo).
[RequireComponent(typeof(Collider2D))]
public class CasinoVendedorNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Que vende")]
    [SerializeField] private string nombre = "REVENDEDOR";
    [SerializeField] private Sprite retrato;
    [SerializeField] private SpecialItemDefinition objeto;
    [Min(0)] [SerializeField] private int precio = 1500;

    [Header("Textos")]
    [Tooltip("{0} = el objeto, {1} = el precio.")]
    [SerializeField, TextArea(2, 4)] private string pregunta =
        "Pst... ¿buscás la {0}? Te la dejo en {1} monedas. ¿Hay trato?";
    [SerializeField] private string opcionComprar = "Comprar";
    [SerializeField] private string opcionNo = "No";
    [Tooltip("{0} = el objeto, {1} = el precio, {2} = lo que tiene el jugador.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSinPlata =
        "¿La {0}? Son {1} monedas y tenés {2}. Volvé cuando tengas la plata.";
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string mensajeVendido =
        "Un placer. La {0} es tuya: al guardia del ascensor no le va a importar de dónde la sacaste.";
    [SerializeField, TextArea(2, 4)] private string mensajeRechazo = "Vos te lo perdés. Acá voy a estar.";
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string mensajeYaLoTiene =
        "Ya tenés la {0}. No hagas esperar al guardia.";

    public SpecialItemDefinition Objeto => objeto;
    public int Precio => precio;

    private string NombreDelObjeto => objeto == null ? ""
        : string.IsNullOrEmpty(objeto.itemName) ? objeto.name : objeto.itemName;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || objeto == null) return false;

        if (player.Inventory.HasSpecialItem(objeto))
        {
            Decir(string.Format(mensajeYaLoTiene, NombreDelObjeto));
            return true;
        }

        if (player.Money < precio)
        {
            Decir(string.Format(mensajeSinPlata, NombreDelObjeto, precio, player.Money));
            return true;
        }

        CasinoDialogo.Preguntar(dialogueController, nombre, retrato,
            string.Format(pregunta, NombreDelObjeto, precio), opcionComprar, opcionNo,
            comprar =>
            {
                if (!comprar) { Decir(mensajeRechazo); return; }
                if (Comprar(player)) Decir(string.Format(mensajeVendido, NombreDelObjeto));
                else Decir(string.Format(mensajeSinPlata, NombreDelObjeto, precio, player.Money));
            });
        return true;
    }

    // La compra sobre el modelo, sin cajas. Publica para los tests.
    public bool Comprar(PlayerModel player)
    {
        if (player == null || objeto == null) return false;
        if (player.Inventory.HasSpecialItem(objeto) || player.Money < precio) return false;

        player.SpendMoney(precio);
        player.Inventory.AddSpecialItem(objeto);
        CasinoDialogo.Guardar();
        return true;
    }

    private void Decir(string linea)
    {
        CasinoDialogo.Decir(dialogueController, nombre, retrato, new[] { linea });
    }
}
