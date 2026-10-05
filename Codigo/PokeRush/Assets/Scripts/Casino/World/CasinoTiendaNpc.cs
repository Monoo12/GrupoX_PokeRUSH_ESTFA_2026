using UnityEngine;

// El tendero del piso 3 del casino (playtest del 5-oct: "agrega un NPC que
// funcione como tienda en el piso 3"). Saluda, abre la tienda del sistema de
// menus con lo que trae su VendorDefinition (la misma pantalla que la de Tixi
// en la Quarcho Cueva) y se despide. Si se compro algo, guarda la partida: la
// plata ya se gasto.
//
// Los precios son los de cada objeto (HealItemDefinition.buyPrice), los mismos
// en todas las tiendas.
[RequireComponent(typeof(Collider2D))]
public class CasinoTiendaNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Lo que vende")]
    [SerializeField] private VendorDefinition tienda;

    [Header("Quien habla (vacio = los de la tienda)")]
    [SerializeField] private string nombre = "";
    [SerializeField] private Sprite retrato;

    [Header("Textos")]
    [SerializeField, TextArea(2, 4)] private string saludo =
        "Bienvenido a la tienda del piso. Pociones, revivires... lo que haga falta para seguir jugando.";
    [SerializeField, TextArea(2, 4)] private string despedidaConCompra = "Gracias. Que la suerte te acompañe en las mesas.";
    [SerializeField, TextArea(2, 4)] private string despedidaSinCompra = "Cuando quieras. Acá voy a estar.";
    [SerializeField, TextArea(2, 4)] private string mensajeSinTienda = "Hoy no tengo nada para vender. Volvé más tarde.";

    public VendorDefinition Tienda => tienda;

    private string Nombre => !string.IsNullOrEmpty(nombre) ? nombre
        : tienda != null ? tienda.characterName : "";
    private Sprite Retrato => retrato != null ? retrato
        : tienda != null ? tienda.portrait : null;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;
        Decir(saludo, AbrirTienda);
        return true;
    }

    private void AbrirTienda()
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null || tienda == null || tienda.healItemsForSale == null || tienda.healItemsForSale.Length == 0)
        {
            Decir(mensajeSinTienda);
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Shop,
            new ShopScreenRequest { Vendedor = Nombre, Objetos = tienda.healItemsForSale },
            resultado =>
            {
                bool compro = resultado is ShopScreenResult r && r.Compras > 0;
                if (compro) CasinoDialogo.Guardar();
                Decir(compro ? despedidaConCompra : despedidaSinCompra);
            });
    }

    private void Decir(string linea, System.Action alCerrar = null)
        => CasinoDialogo.Decir(dialogueController, Nombre, Retrato, new[] { linea }, alCerrar);
}
