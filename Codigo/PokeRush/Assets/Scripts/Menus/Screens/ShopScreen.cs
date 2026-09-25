using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La tienda de Tixi en la Quarcho Cueva.
//
// Es la tienda de Pokemon Essentials (Graphics/UI/Mart): la lista de objetos a
// la derecha con el precio, el icono y la descripcion abajo, y la plata en una
// ventanita arriba a la izquierda. Se abre ENCIMA del mapa, como en los juegos:
// la esquina de arriba a la izquierda del fondo es transparente y deja ver la
// cueva (por eso no oscurece).
//
// Elegir un objeto abre el contador (arriba/abajo de a 1, izquierda/derecha de
// a 10); aceptar pregunta el total y recien ahi cobra. Las reglas de la compra
// son de ReglasDeTienda, no de esta pantalla.
public class ShopScreen : MenuScreen
{
    [Header("Lista")]
    [SerializeField] private ShopRowWidget rowPrefab;
    [SerializeField] private Transform rowContainer;
    [Tooltip("Cuantas filas entran en la ventana. La lista scrollea de a una.")]
    [SerializeField] private int visibleRows = 7;
    [SerializeField] private string exitLabel = "SALIR";

    [Header("Detalle")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text moneyText;
    [Tooltip("{0} = la plata del jugador.")]
    [SerializeField] private string moneyFormat = "$ {0}";
    [SerializeField] private string exitDescription = "Salir de la tienda.";

    [Header("Contador")]
    [SerializeField] private GameObject quantityWindow;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text quantityPriceText;
    [SerializeField] private string quantityFormat = "x {0:00}";
    [SerializeField] private string quantityPriceFormat = "$ {0}";

    [Header("Textos ({0} = el vendedor)")]
    [Tooltip("{0} = vendedor, {1} = objeto.")]
    [SerializeField] private string sinPlata = "{0}: No te alcanza para {1}.";
    [Tooltip("{0} = cantidad, {1} = objeto, {2} = total.")]
    [SerializeField] private string confirmar = "{0} x {1}. Son $ {2}. ¿Está bien?";
    [Tooltip("{0} = vendedor, {1} = cantidad, {2} = objeto.")]
    [SerializeField] private string gracias = "{0}: ¡Acá tenés! {1} x {2} a la mochila.";

    private WidgetPool<ShopRowWidget> _rows;
    private readonly List<HealItemDefinition> _objetos = new List<HealItemDefinition>();
    private readonly ShopScreenResult _result = new ShopScreenResult();

    private string _vendedor = "TIENDA";
    private int _cursor;
    private int _scrollTop;
    private bool _contando;
    private int _cantidad;

    // La tienda deja ver el mapa, como en los juegos.
    public override bool DimsBackground => false;

    // Filas totales: los objetos mas SALIR.
    private int Filas => _objetos.Count + 1;
    private bool EnSalir => _cursor >= _objetos.Count;
    private HealItemDefinition Actual => EnSalir ? null : _objetos[_cursor];

    protected override void OnOpen(object payload)
    {
        var pedido = payload as ShopScreenRequest;
        _vendedor = pedido != null && !string.IsNullOrEmpty(pedido.Vendedor) ? pedido.Vendedor : "TIENDA";

        _objetos.Clear();
        if (pedido?.Objetos != null)
            foreach (HealItemDefinition o in pedido.Objetos)
                if (o != null && !_objetos.Contains(o)) _objetos.Add(o);

        _result.Gastado = 0;
        _result.Compras = 0;
        Result = _result;

        if (_rows == null && rowPrefab != null)
            _rows = new WidgetPool<ShopRowWidget>(rowPrefab, rowContainer != null ? rowContainer : transform);

        _cursor = 0;
        _scrollTop = 0;
        SalirDelContador();
        Refresh();
    }

    public override void OnFocusGained() => Refresh();

    // ---------- dibujo ----------

    private void Refresh()
    {
        if (moneyText != null)
            moneyText.text = string.Format(moneyFormat, Player != null ? Player.Money : 0);

        ClampScroll();
        RefreshRows();
        RefreshDetail();
        RefreshQuantity();
    }

    private void RefreshRows()
    {
        if (_rows == null) return;

        int plata = Player != null ? Player.Money : 0;
        int cuantas = Mathf.Min(visibleRows, Filas);
        for (int fila = 0; fila < cuantas; fila++)
        {
            int indice = _scrollTop + fila;
            ShopRowWidget widget = _rows.Get(fila);

            if (indice >= _objetos.Count)
            {
                widget.Bind(exitLabel, 0, false, true);
            }
            else
            {
                HealItemDefinition o = _objetos[indice];
                int precio = ReglasDeTienda.Precio(o);
                widget.Bind(Nombre(o), precio, true, plata >= precio);
            }
            widget.SetSelected(indice == _cursor);
        }
        _rows.HideFrom(cuantas);
    }

    private void RefreshDetail()
    {
        HealItemDefinition o = Actual;
        if (descriptionText != null)
            descriptionText.text = o != null ? o.description : exitDescription;

        if (itemIcon != null)
        {
            Sprite icono = o != null ? o.icon : null;
            itemIcon.sprite = icono;
            itemIcon.enabled = icono != null;
            itemIcon.preserveAspect = true;
        }
    }

    private void RefreshQuantity()
    {
        if (quantityWindow != null) quantityWindow.SetActive(_contando);
        if (!_contando) return;

        if (quantityText != null) quantityText.text = string.Format(quantityFormat, _cantidad);
        if (quantityPriceText != null)
            quantityPriceText.text = string.Format(quantityPriceFormat, ReglasDeTienda.Total(Actual, _cantidad));
    }

    private void ClampScroll()
    {
        if (_cursor >= Filas) _cursor = Filas - 1;
        if (_cursor < 0) _cursor = 0;

        if (Filas <= visibleRows) { _scrollTop = 0; return; }
        _scrollTop = Mathf.Clamp(_scrollTop, 0, Filas - visibleRows);
        if (_cursor < _scrollTop) _scrollTop = _cursor;
        else if (_cursor >= _scrollTop + visibleRows) _scrollTop = _cursor - visibleRows + 1;
    }

    private static string Nombre(HealItemDefinition o)
        => o == null ? "" : string.IsNullOrEmpty(o.itemName) ? o.name : o.itemName;

    // ---------- input ----------

    public override void HandleAction(GameAction action)
    {
        if (_contando) { HandleContador(action); return; }

        switch (action)
        {
            case GameAction.Up:
                _cursor = (_cursor - 1 + Filas) % Filas;
                Refresh();
                break;
            case GameAction.Down:
                _cursor = (_cursor + 1) % Filas;
                Refresh();
                break;
            case GameAction.Confirm:
                Elegir();
                break;
            case GameAction.Cancel:
                CloseSelf();
                break;
        }
    }

    private void Elegir()
    {
        if (EnSalir) { CloseSelf(); return; }

        HealItemDefinition o = Actual;
        int plata = Player != null ? Player.Money : 0;
        if (ReglasDeTienda.MaximoQueAlcanza(plata, o) <= 0)
        {
            ShowMessage(string.Format(sinPlata, _vendedor, Nombre(o)));
            return;
        }

        _contando = true;
        _cantidad = 1;
        RefreshQuantity();
    }

    private void HandleContador(GameAction action)
    {
        int maximo = Mathf.Max(1, ReglasDeTienda.MaximoQueAlcanza(Player != null ? Player.Money : 0, Actual));

        switch (action)
        {
            case GameAction.Up:
                _cantidad = _cantidad >= maximo ? 1 : _cantidad + 1;
                break;
            case GameAction.Down:
                _cantidad = _cantidad <= 1 ? maximo : _cantidad - 1;
                break;
            case GameAction.Right:
                _cantidad = Mathf.Min(maximo, _cantidad + 10);
                break;
            case GameAction.Left:
                _cantidad = Mathf.Max(1, _cantidad - 10);
                break;
            case GameAction.Confirm:
                Confirmar();
                return;
            case GameAction.Cancel:
                SalirDelContador();
                RefreshQuantity();
                return;
        }
        RefreshQuantity();
    }

    private void Confirmar()
    {
        HealItemDefinition o = Actual;
        int cantidad = _cantidad;
        int total = ReglasDeTienda.Total(o, cantidad);

        Ask(string.Format(confirmar, cantidad, Nombre(o), total), si =>
        {
            SalirDelContador();
            if (!si) { Refresh(); return; }

            if (!ReglasDeTienda.Comprar(Player, o, cantidad))
            {
                Refresh();
                ShowMessage(string.Format(sinPlata, _vendedor, Nombre(o)));
                return;
            }

            _result.Gastado += total;
            _result.Compras++;
            Refresh();
            ShowMessage(string.Format(gracias, _vendedor, cantidad, Nombre(o)));
        });
    }

    private void SalirDelContador()
    {
        _contando = false;
        _cantidad = 1;
    }
}
