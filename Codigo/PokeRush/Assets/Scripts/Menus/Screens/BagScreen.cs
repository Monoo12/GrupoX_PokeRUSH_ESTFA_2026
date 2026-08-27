using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mochila. UNA sola, para el mundo y para el combate.
//
// Lo que cambia entre contextos no es la pantalla sino que hace al elegir:
//  - Mundo:   el objeto se aplica ahi mismo y se vuelve a la mochila.
//  - Combate: la mochila devuelve "usar ESTE objeto sobre ESTE Poken" y el
//             TurnEngine lo resuelve como la accion del turno.
// Por eso no hay una BagBattleScreen: seria la misma lista dos veces.
public class BagScreen : MenuScreen
{
    [Header("Lista")]
    [Tooltip("Prefab de una fila de objeto.")]
    [SerializeField] private ItemRowWidget itemRowPrefab;
    [Tooltip("Padre de las filas.")]
    [SerializeField] private Transform itemContainer;
    [Tooltip("Cuantas filas entran en pantalla. La lista scrollea de a una.")]
    [SerializeField] private int visibleRows = 8;

    [Header("Bolsillo")]
    [SerializeField] private Text pocketNameText;
    [Tooltip("Imagen grande de la mochila. Un sprite por bolsillo, en el orden " +
             "Medicinas / Combate / Claves. Opcional.")]
    [SerializeField] private Image bagImage;
    [SerializeField] private Sprite[] bagSpritesByPocket;
    [Tooltip("Fondo que cambia por bolsillo. Opcional.")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite[] backgroundsByPocket;

    [Header("Detalle")]
    [SerializeField] private Text descriptionText;
    [SerializeField] private Image descriptionIcon;
    [SerializeField] private Text emptyText;
    [SerializeField] private Text moneyText;
    [SerializeField] private string moneyFormat = "$ {0}";

    [Header("Submenu del objeto")]
    [SerializeField] private GameObject actionMenuRoot;
    [SerializeField] private MenuOptionWidget actionOptionPrefab;
    [SerializeField] private Transform actionContainer;
    [Tooltip("Texto de la accion que aplica el objeto. Renombrar a 'DAR' si el " +
             "equipo prefiere ese termino: la logica no depende del texto.")]
    [SerializeField] private string useLabel = "USAR";
    [SerializeField] private string cancelLabel = "CANCELAR";

    // Bolsillo en el que se quedo la ultima vez. Estatico a proposito: volver a
    // abrir la mochila donde la dejaste es lo que espera cualquiera.
    private static ItemPocket _lastPocket = ItemPocket.Medicine;

    private const string ActionUse = "use";
    private const string ActionCancel = "cancel";

    private WidgetPool<ItemRowWidget> _rows;
    private WidgetPool<MenuOptionWidget> _actions;

    private readonly List<BagEntry> _entries = new List<BagEntry>();
    private readonly List<string> _actionIds = new List<string>();

    private ItemPocket _pocket;
    private int _cursor;
    private int _scrollTop;
    private int _actionCursor;
    private bool _inActionMenu;

    private readonly BagScreenResult _result = new BagScreenResult();

    protected override void OnOpen(object payload)
    {
        var request = payload as BagScreenRequest;
        _pocket = request?.Pocket ?? _lastPocket;

        _result.Cancelled = true;
        _result.UsedHealItem = null;
        _result.TargetIndex = -1;
        _result.ConsumesBattleTurn = false;
        Result = _result;

        EnsurePools();
        CloseActionMenu();
        _cursor = 0;
        _scrollTop = 0;
        Refresh();
    }

    protected override void OnClose()
    {
        _lastPocket = _pocket;
    }

    public override void OnFocusGained()
    {
        // Volver de la party: las cantidades pueden haber cambiado.
        Refresh();
    }

    private void EnsurePools()
    {
        if (_rows == null && itemRowPrefab != null)
            _rows = new WidgetPool<ItemRowWidget>(itemRowPrefab,
                itemContainer != null ? itemContainer : transform);

        if (_actions == null && actionOptionPrefab != null)
            _actions = new WidgetPool<MenuOptionWidget>(actionOptionPrefab,
                actionContainer != null ? actionContainer : transform);
    }

    // ---------- dibujo ----------

    private void Refresh()
    {
        _entries.Clear();
        InventoryModel inventario = Player != null ? Player.Inventory : null;
        _entries.AddRange(BagModel.Build(inventario, _pocket));

        if (_cursor >= _entries.Count) _cursor = Mathf.Max(0, _entries.Count - 1);
        ClampScroll();

        if (pocketNameText != null) pocketNameText.text = BagModel.PocketName(_pocket);
        PaintPocketSprite(bagImage, bagSpritesByPocket);
        PaintPocketSprite(backgroundImage, backgroundsByPocket);

        if (moneyText != null)
            moneyText.text = Player != null ? string.Format(moneyFormat, Player.Money) : string.Empty;

        if (emptyText != null)
        {
            emptyText.enabled = _entries.Count == 0;
            if (_entries.Count == 0) emptyText.text = "No hay objetos.";
        }

        RefreshRows();
        RefreshDescription();
    }

    private void PaintPocketSprite(Image image, Sprite[] table)
    {
        if (image == null || table == null) return;
        int index = System.Array.IndexOf(BagModel.Pockets, _pocket);
        if (index < 0 || index >= table.Length || table[index] == null) return;
        image.sprite = table[index];
    }

    private void RefreshRows()
    {
        if (_rows == null) return;

        int cuantas = Mathf.Min(visibleRows, _entries.Count);
        for (int fila = 0; fila < cuantas; fila++)
        {
            int indice = _scrollTop + fila;
            ItemRowWidget widget = _rows.Get(fila);
            widget.Bind(_entries[indice]);
            widget.SetSelected(!_inActionMenu && indice == _cursor);
        }
        _rows.HideFrom(cuantas);
    }

    private void RefreshDescription()
    {
        BagEntry actual = Current;
        if (descriptionText != null)
            descriptionText.text = actual != null ? actual.Description : string.Empty;

        if (descriptionIcon != null)
        {
            Sprite icon = actual != null ? actual.Icon : null;
            descriptionIcon.sprite = icon;
            descriptionIcon.enabled = icon != null;
            descriptionIcon.preserveAspect = true;
        }
    }

    private BagEntry Current
        => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;

    private void ClampScroll()
    {
        if (_entries.Count <= visibleRows) { _scrollTop = 0; return; }
        _scrollTop = Mathf.Clamp(_scrollTop, 0, _entries.Count - visibleRows);
        if (_cursor < _scrollTop) _scrollTop = _cursor;
        else if (_cursor >= _scrollTop + visibleRows) _scrollTop = _cursor - visibleRows + 1;
    }

    // ---------- input ----------

    public override void HandleAction(GameAction action)
    {
        if (_inActionMenu) { HandleActionMenu(action); return; }

        switch (action)
        {
            case GameAction.Up:
                MoveCursor(-1);
                break;
            case GameAction.Down:
                MoveCursor(1);
                break;
            case GameAction.Left:
            case GameAction.PageLeft:
                ChangePocket(-1);
                break;
            case GameAction.Right:
            case GameAction.PageRight:
                ChangePocket(1);
                break;
            case GameAction.Confirm:
                OpenActionMenu();
                break;
            case GameAction.Cancel:
                CloseSelf();
                break;
        }
    }

    private void MoveCursor(int delta)
    {
        if (_entries.Count == 0) return;
        _cursor = (_cursor + delta + _entries.Count) % _entries.Count;
        ClampScroll();
        RefreshRows();
        RefreshDescription();
    }

    private void ChangePocket(int delta)
    {
        int index = System.Array.IndexOf(BagModel.Pockets, _pocket);
        if (index < 0) index = 0;
        index = (index + delta + BagModel.Pockets.Length) % BagModel.Pockets.Length;
        _pocket = BagModel.Pockets[index];
        _cursor = 0;
        _scrollTop = 0;
        Refresh();
    }

    // ---------- submenu del objeto ----------

    private void OpenActionMenu()
    {
        BagEntry entry = Current;
        if (entry == null || _actions == null) return;

        _actionIds.Clear();
        _actionIds.Add(ActionUse);
        _actionIds.Add(ActionCancel);

        for (int i = 0; i < _actionIds.Count; i++)
        {
            MenuOptionWidget widget = _actions.Get(i);
            widget.Bind(_actionIds[i], _actionIds[i] == ActionUse ? useLabel : cancelLabel);
        }
        _actions.HideFrom(_actionIds.Count);

        _inActionMenu = true;
        _actionCursor = 0;
        if (actionMenuRoot != null) actionMenuRoot.SetActive(true);
        RefreshActionSelection();
        RefreshRows();
    }

    private void CloseActionMenu()
    {
        _inActionMenu = false;
        if (actionMenuRoot != null) actionMenuRoot.SetActive(false);
        RefreshRows();
    }

    private void RefreshActionSelection()
    {
        if (_actions == null) return;
        for (int i = 0; i < _actionIds.Count; i++)
            _actions.Get(i).SetSelected(i == _actionCursor);
    }

    private void HandleActionMenu(GameAction action)
    {
        switch (action)
        {
            case GameAction.Up:
                _actionCursor = (_actionCursor - 1 + _actionIds.Count) % _actionIds.Count;
                RefreshActionSelection();
                break;
            case GameAction.Down:
                _actionCursor = (_actionCursor + 1) % _actionIds.Count;
                RefreshActionSelection();
                break;
            case GameAction.Confirm:
                if (_actionIds[_actionCursor] == ActionUse) UseCurrent();
                else CloseActionMenu();
                break;
            case GameAction.Cancel:
                CloseActionMenu();
                break;
        }
    }

    // ---------- usar ----------

    private void UseCurrent()
    {
        BagEntry entry = Current;
        if (entry == null) return;

        CloseActionMenu();

        if (entry.NeedsTarget)
        {
            AbrirSeleccionDeObjetivo(entry.Heal);
            return;
        }

        if (entry.Pocket == ItemPocket.Battle)
        {
            // Los objetos de batalla todavia no tienen efecto en el TurnEngine:
            // decirlo es mejor que gastarlos sin que pase nada.
            ShowMessage(Context == MenuContextKind.Battle
                ? $"{entry.Name} todavía no se puede usar."
                : $"{entry.Name} solo sirve en combate.");
            return;
        }

        // Objetos clave: no se gastan. Mostramos que hacen; enchufar el efecto
        // real es agregar el caso aca (ver la documentacion).
        string efecto = entry.Special != null && !string.IsNullOrEmpty(entry.Special.effectDescription)
            ? entry.Special.effectDescription
            : $"{entry.Name} no se puede usar acá.";
        ShowMessage(efecto);
    }

    private void AbrirSeleccionDeObjetivo(HealItemDefinition item)
    {
        if (Player == null || Player.Team.Count == 0)
        {
            ShowMessage("No tenés ningún Poken.");
            return;
        }

        Manager.Open(MenuScreenId.Party, new PartyScreenRequest
        {
            Mode = PartyScreenMode.SelectTarget,
            Context = Context,
            Item = item,
            Prompt = $"¿A quién le doy {item.itemName}?"
        }, raw =>
        {
            var resultado = raw as PartyScreenResult;
            if (resultado == null || resultado.Cancelled) { Refresh(); return; }

            if (Context == MenuContextKind.Battle)
            {
                // En combate NO se aplica aca: usar un objeto es la accion del
                // turno y la resuelve el TurnEngine. La mochila solo informa
                // que se eligio y se cierra para devolverle el control al
                // combate.
                _result.Cancelled = false;
                _result.UsedHealItem = item;
                _result.TargetIndex = resultado.SelectedIndex;
                _result.ConsumesBattleTurn = true;
                CloseSelf();
                return;
            }

            // En el mundo el objeto ya se aplico dentro de la party: aca solo
            // hay que redibujar las cantidades.
            Refresh();
        });
    }
}
