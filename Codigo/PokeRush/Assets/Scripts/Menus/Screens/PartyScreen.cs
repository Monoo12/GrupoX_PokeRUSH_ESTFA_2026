using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// El equipo del jugador. UNA sola pantalla para los tres usos:
//   Browse       -> administrar (ver resumen, reordenar)
//   SelectTarget -> elegir a quien se le aplica un objeto
//   SelectSwitch -> elegir relevo en combate
//
// Se hizo asi porque el enunciado prohibe explicitamente tener una party para
// el mundo y otra para el combate: lo unico que cambia entre los tres casos es
// que pasa al apretar Aceptar.
public class PartyScreen : MenuScreen
{
    [Header("Paneles")]
    [Tooltip("Prefab de un panel de Poken.")]
    [SerializeField] private PartyPanelWidget panelPrefab;
    [Tooltip("Padre de los paneles.")]
    [SerializeField] private Transform panelContainer;
    [Tooltip("Cuantos paneles hay por fila. Solo afecta como se mueve el cursor " +
             "con izquierda/derecha; el layout real lo define el contenedor.")]
    [SerializeField] private int columns = 2;

    [Header("Textos")]
    [SerializeField] private Text promptText;
    [SerializeField] private MenuOptionWidget cancelOption;

    [Header("Submenu del Poken")]
    [SerializeField] private GameObject actionMenuRoot;
    [SerializeField] private MenuOptionWidget actionOptionPrefab;
    [SerializeField] private Transform actionContainer;
    [SerializeField] private string summaryLabel = "DATOS";
    [Tooltip("Accion que manda el Poken al campo. Solo aparece cuando la " +
             "pantalla se abrio para elegir relevo en combate.")]
    [SerializeField] private string sendOutLabel = "SACAR";
    [SerializeField] private string swapLabel = "CAMBIAR";
    [SerializeField] private string cancelLabel = "CANCELAR";

    private const string ActionSendOut = "sendout";
    private const string ActionSummary = "summary";
    private const string ActionSwap = "swap";
    private const string ActionCancel = "cancel";

    private WidgetPool<PartyPanelWidget> _panels;
    private WidgetPool<MenuOptionWidget> _actions;

    private readonly List<string> _actionIds = new List<string>();
    private readonly PartyScreenResult _result = new PartyScreenResult();

    private PartyScreenRequest _request;
    private int _cursor;
    private int _actionCursor;
    private bool _inActionMenu;

    // Indice del Poken agarrado para intercambiar. -1 = no hay ninguno.
    private int _swapFrom = -1;

    private List<PokenModel> Team => Player != null ? Player.Team : null;

    // Sobre que slot esta el cursor. Lectura publica para tests y herramientas;
    // la pantalla sigue mandando sobre el valor.
    public int CursorIndex => _cursor;

    protected override void OnOpen(object payload)
    {
        _request = payload as PartyScreenRequest ?? new PartyScreenRequest();

        _result.Cancelled = true;
        _result.SelectedIndex = -1;
        _result.ItemApplied = false;
        Result = _result;

        EnsurePools();
        _inActionMenu = false;
        _swapFrom = -1;
        _cursor = 0;
        if (actionMenuRoot != null) actionMenuRoot.SetActive(false);

        // Arrancar sobre alguien que SI se pueda elegir. En el reemplazo
        // obligado el slot 0 suele ser justo el que acaba de caer.
        int cuantos = Team != null ? Team.Count : 0;
        for (int i = 0; i < cuantos && !EsSeleccionable(_cursor); i++)
            _cursor = (_cursor + 1) % cuantos;

        Refresh();
    }

    public override void OnFocusGained()
    {
        // Volver del resumen: pudo cambiar el orden de movimientos o los PS.
        Refresh();
    }

    private void EnsurePools()
    {
        if (_panels == null && panelPrefab != null)
            _panels = new WidgetPool<PartyPanelWidget>(panelPrefab,
                panelContainer != null ? panelContainer : transform);

        if (_actions == null && actionOptionPrefab != null)
            _actions = new WidgetPool<MenuOptionWidget>(actionOptionPrefab,
                actionContainer != null ? actionContainer : transform);
    }

    // ---------- dibujo ----------

    private void Refresh()
    {
        List<PokenModel> team = Team;
        int cuantos = team != null ? team.Count : 0;

        if (promptText != null) promptText.text = PromptForMode();

        if (_panels != null)
        {
            for (int i = 0; i < cuantos; i++)
            {
                PartyPanelWidget panel = _panels.Get(i);
                panel.Bind(team[i]);
                panel.SetLeader(i == 0);
                panel.SetSwapping(i == _swapFrom);
                panel.SetOptionEnabled(EsSeleccionable(i));
                panel.SetSelected(!_inActionMenu && i == _cursor);
            }
            _panels.HideFrom(cuantos);
        }

        if (cancelOption != null)
        {
            // Sin salida posible, el boton de salir no se muestra: prometeria
            // algo que la pantalla no va a hacer.
            cancelOption.gameObject.SetActive(_request.CanCancel);
            cancelOption.SetSelected(false);
        }
    }

    private string PromptForMode()
    {
        if (!string.IsNullOrEmpty(_request.Prompt)) return _request.Prompt;
        if (_swapFrom >= 0) return "¿Con cuál lo cambio?";

        switch (_request.Mode)
        {
            case PartyScreenMode.SelectTarget: return "Elegí un Pokémon.";
            case PartyScreenMode.SelectSwitch: return "¿Quién sale a pelear?";
            default: return "Elegí un Pokémon.";
        }
    }

    // Un Poken puede estar bloqueado por quien abrio la pantalla (ej: el que ya
    // esta en el campo no puede volver a salir) o por el objeto que se le
    // quiere dar.
    private bool EsSeleccionable(int index)
    {
        if (_request.BlockedIndices != null && _request.BlockedIndices.Contains(index))
            return false;

        if (_request.Mode == PartyScreenMode.SelectTarget && _request.Item != null)
        {
            List<PokenModel> team = Team;
            if (team == null || index >= team.Count) return false;
            return ItemUsageService.Evaluate(_request.Item, team[index]).Applied;
        }

        if (_request.Mode == PartyScreenMode.SelectSwitch)
        {
            List<PokenModel> team = Team;
            if (team == null || index >= team.Count) return false;
            return !team[index].IsFainted;
        }

        return true;
    }

    // ---------- input ----------

    public override void HandleAction(GameAction action)
    {
        if (_inActionMenu) { HandleActionMenu(action); return; }

        int cuantos = Team != null ? Team.Count : 0;
        if (cuantos == 0)
        {
            if (action == GameAction.Cancel) CloseSelf();
            return;
        }

        switch (action)
        {
            case GameAction.Up:
                MoveCursor(-Mathf.Max(1, columns));
                break;
            case GameAction.Down:
                MoveCursor(Mathf.Max(1, columns));
                break;
            case GameAction.Left:
                MoveCursor(-1);
                break;
            case GameAction.Right:
                MoveCursor(1);
                break;
            case GameAction.Confirm:
                Confirm();
                break;
            case GameAction.Cancel:
                Cancel();
                break;
        }
    }

    private void MoveCursor(int delta)
    {
        int cuantos = Team.Count;
        // Se recorre como una lista aunque se dibuje en dos columnas: con 5 o 6
        // paneles el salto de columna nunca deja un hueco al que no se llegue.
        _cursor = (_cursor + delta % cuantos + cuantos) % cuantos;
        Refresh();
    }

    private void Cancel()
    {
        if (_swapFrom >= 0)
        {
            _swapFrom = -1;
            Refresh();
            return;
        }

        // Reemplazo obligado: el combate esta detenido esperando la eleccion.
        if (!_request.CanCancel)
        {
            ShowMessage("Tenés que elegir un Pokémon.");
            return;
        }

        _result.Cancelled = true;
        CloseSelf();
    }

    private void Confirm()
    {
        // Segunda mitad de un intercambio de orden.
        if (_swapFrom >= 0)
        {
            TerminarIntercambio();
            return;
        }

        switch (_request.Mode)
        {
            case PartyScreenMode.SelectTarget:
                AplicarObjeto();
                break;

            case PartyScreenMode.SelectSwitch:
                if (!EsSeleccionable(_cursor))
                {
                    ShowMessage("¡Ese Pokémon no puede pelear!");
                    return;
                }
                // No se manda al campo de una: primero el submenu, como en la
                // saga (SACAR A BATALLA / DATOS / CANCELAR).
                OpenActionMenu();
                break;

            default:
                OpenActionMenu();
                break;
        }
    }

    // ---------- objetos ----------

    private void AplicarObjeto()
    {
        List<PokenModel> team = Team;
        HealItemDefinition item = _request.Item;
        if (item == null || team == null || _cursor >= team.Count) return;

        PokenModel objetivo = team[_cursor];
        ItemUseResult check = ItemUsageService.Evaluate(item, objetivo);
        if (!check.Applied)
        {
            // No se consume nada y no se cierra: el jugador elige otro.
            ShowMessage(check.Message);
            return;
        }

        // En combate NO se aplica aca. Usar un objeto es la accion del turno y
        // la resuelve el TurnEngine; aplicarlo tambien aca lo curaria dos veces.
        if (Context == MenuContextKind.Battle)
        {
            _result.Cancelled = false;
            _result.SelectedIndex = _cursor;
            _result.ItemApplied = false;
            CloseSelf();
            return;
        }

        ItemUseResult resultado = ItemUsageService.Apply(item, objetivo, Player.Inventory);
        _result.Cancelled = false;
        _result.SelectedIndex = _cursor;
        _result.ItemApplied = resultado.Applied;

        Refresh();
        ShowMessage(resultado.Message, CloseSelf);
    }

    // ---------- submenu ----------

    private void OpenActionMenu()
    {
        if (_actions == null) return;

        _actionIds.Clear();

        // En combate lo primero es mandarlo a pelear; administrar el equipo no
        // aplica en medio de un turno.
        if (_request.Mode == PartyScreenMode.SelectSwitch)
        {
            _actionIds.Add(ActionSendOut);
            _actionIds.Add(ActionSummary);
        }
        else
        {
            _actionIds.Add(ActionSummary);
            if (Team.Count > 1) _actionIds.Add(ActionSwap);
        }
        _actionIds.Add(ActionCancel);

        for (int i = 0; i < _actionIds.Count; i++)
        {
            MenuOptionWidget widget = _actions.Get(i);
            widget.Bind(_actionIds[i], LabelFor(_actionIds[i]));
        }
        _actions.HideFrom(_actionIds.Count);

        _inActionMenu = true;
        _actionCursor = 0;
        if (actionMenuRoot != null) actionMenuRoot.SetActive(true);
        RefreshActionSelection();
        Refresh();
    }

    private string LabelFor(string id)
    {
        switch (id)
        {
            case ActionSendOut: return sendOutLabel;
            case ActionSummary: return summaryLabel;
            case ActionSwap: return swapLabel;
            default: return cancelLabel;
        }
    }

    private void CloseActionMenu()
    {
        _inActionMenu = false;
        if (actionMenuRoot != null) actionMenuRoot.SetActive(false);
        Refresh();
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
                EjecutarAccion(_actionIds[_actionCursor]);
                break;
            case GameAction.Cancel:
                CloseActionMenu();
                break;
        }
    }

    private void EjecutarAccion(string id)
    {
        CloseActionMenu();

        switch (id)
        {
            case ActionSendOut:
                if (!EsSeleccionable(_cursor))
                {
                    ShowMessage("¡Ese Pokémon no puede pelear!");
                    return;
                }
                _result.Cancelled = false;
                _result.SelectedIndex = _cursor;
                CloseSelf();
                break;

            case ActionSummary:
                Manager.Open(MenuScreenId.Summary, new SummaryRequest
                {
                    Team = Team,
                    StartIndex = _cursor,
                    AllowMoveReorder = Context == MenuContextKind.Overworld
                });
                break;

            case ActionSwap:
                // Agarra este y espera el segundo. El intercambio real se hace
                // sobre PlayerModel.Team, no sobre una copia de la UI.
                _swapFrom = _cursor;
                Refresh();
                break;
        }
    }

    private void TerminarIntercambio()
    {
        if (_swapFrom == _cursor)
        {
            _swapFrom = -1;
            Refresh();
            return;
        }

        Player.SwapTeamSlots(_swapFrom, _cursor);
        _swapFrom = -1;
        Refresh();
    }
}
