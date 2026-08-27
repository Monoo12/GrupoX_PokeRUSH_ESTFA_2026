using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ficha del Poken: informacion, notas, estadisticas y movimientos.
//
// Las paginas son GameObjects del prefab: agregar o sacar una es prenderla o
// apagarla en Unity. Izquierda/derecha cambia de pagina, arriba/abajo cambia de
// Poken sin salir de la ficha (como en la saga).
public class SummaryScreen : MenuScreen
{
    [System.Serializable]
    public class Page
    {
        [Tooltip("Titulo que se muestra arriba.")]
        public string title;
        [Tooltip("Raiz de la pagina. Se prende solo cuando esta pagina esta activa.")]
        public GameObject root;
        [Tooltip("Pestania de esta pagina en la barra de arriba. Opcional.")]
        public MenuWidget tab;
    }

    [Header("Paginas")]
    [SerializeField] private Page[] pages;
    [SerializeField] private Text pageTitleText;

    [Header("Cabecera (comun a todas las paginas)")]
    [SerializeField] private Image pokenSprite;
    [SerializeField] private Text nameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private string levelFormat = "Nv. {0}";

    [Header("Pagina: Informacion")]
    [SerializeField] private Text speciesText;
    [SerializeField] private Image type1Icon;
    [SerializeField] private Image type2Icon;
    [SerializeField] private Text trainerText;
    [SerializeField] private Text idText;
    [SerializeField] private Text xpText;
    [SerializeField] private Text xpToNextText;
    [SerializeField] private Image xpFill;
    [SerializeField] private Text heldItemText;

    [Header("Pagina: Notas")]
    [Tooltip("Datos de procedencia. El save todavia no guarda donde ni cuando se " +
             "capturo cada Poken, asi que se muestra lo que si existe.")]
    [SerializeField] private Text encounterText;
    [SerializeField] private Text characteristicText;
    [SerializeField] private Text abilityNameText;
    [SerializeField] private Text abilityDescriptionText;

    [Header("Pagina: Estadisticas")]
    [SerializeField] private Text hpText;
    [SerializeField] private Text attackText;
    [SerializeField] private Text defenseText;
    [SerializeField] private Text specialAttackText;
    [SerializeField] private Text specialDefenseText;
    [SerializeField] private Text speedText;
    [SerializeField] private Image hpFill;

    [Header("Pagina: Movimientos")]
    [SerializeField] private MoveRowWidget moveRowPrefab;
    [SerializeField] private Transform moveContainer;
    [SerializeField] private Text moveHintText;
    [Tooltip("Indice de la pagina de movimientos dentro del array 'pages'. " +
             "Solo ahi se activa el reordenamiento.")]
    [SerializeField] private int movesPageIndex = 3;

    private WidgetPool<MoveRowWidget> _moveRows;

    private IList<PokenModel> _team;
    private int _pokenIndex;
    private int _page;

    // Reordenar movimientos: indice agarrado y si estamos en ese modo.
    private bool _inMoveMode;
    private int _moveCursor;
    private int _moveSwapFrom = -1;
    private bool _allowMoveReorder = true;

    private PokenModel Current
        => _team != null && _pokenIndex >= 0 && _pokenIndex < _team.Count ? _team[_pokenIndex] : null;

    // Que Poken se esta mostrando. Lectura publica para tests y herramientas;
    // la pantalla sigue mandando sobre el indice.
    public PokenModel CurrentPoken => Current;

    protected override void OnOpen(object payload)
    {
        var request = payload as SummaryRequest;
        _team = request?.Team;
        if (_team == null && Player != null) _team = Player.Team;

        _pokenIndex = Mathf.Clamp(request?.StartIndex ?? 0, 0, Mathf.Max(0, (_team?.Count ?? 1) - 1));
        _allowMoveReorder = request?.AllowMoveReorder ?? true;

        if (_moveRows == null && moveRowPrefab != null)
            _moveRows = new WidgetPool<MoveRowWidget>(moveRowPrefab,
                moveContainer != null ? moveContainer : transform);

        _page = 0;
        _inMoveMode = false;
        _moveSwapFrom = -1;
        _moveCursor = 0;
        Refresh();
    }

    public override void HandleAction(GameAction action)
    {
        if (_inMoveMode) { HandleMoveMode(action); return; }

        switch (action)
        {
            case GameAction.Left:
            case GameAction.PageLeft:
                ChangePage(-1);
                break;
            case GameAction.Right:
            case GameAction.PageRight:
                ChangePage(1);
                break;
            case GameAction.Up:
                ChangePoken(-1);
                break;
            case GameAction.Down:
                ChangePoken(1);
                break;
            case GameAction.Confirm:
                EnterMoveMode();
                break;
            case GameAction.Cancel:
                CloseSelf();
                break;
        }
    }

    private void ChangePage(int delta)
    {
        if (pages == null || pages.Length == 0) return;
        _page = (_page + delta + pages.Length) % pages.Length;
        Refresh();
    }

    private void ChangePoken(int delta)
    {
        if (_team == null || _team.Count == 0) return;
        _pokenIndex = (_pokenIndex + delta + _team.Count) % _team.Count;
        _moveCursor = 0;
        Refresh();
    }

    // ---------- reordenar movimientos ----------

    private void EnterMoveMode()
    {
        if (!_allowMoveReorder) return;
        if (_page != movesPageIndex) return;

        PokenModel poken = Current;
        if (poken == null || poken.Moves.Count < 2)
        {
            ShowMessage("No hay movimientos que reordenar.");
            return;
        }

        _inMoveMode = true;
        _moveSwapFrom = -1;
        _moveCursor = Mathf.Clamp(_moveCursor, 0, poken.Moves.Count - 1);
        Refresh();
    }

    private void HandleMoveMode(GameAction action)
    {
        PokenModel poken = Current;
        int cuantos = poken != null ? poken.Moves.Count : 0;
        if (cuantos == 0) { _inMoveMode = false; Refresh(); return; }

        switch (action)
        {
            case GameAction.Up:
                _moveCursor = (_moveCursor - 1 + cuantos) % cuantos;
                Refresh();
                break;

            case GameAction.Down:
                _moveCursor = (_moveCursor + 1) % cuantos;
                Refresh();
                break;

            case GameAction.Confirm:
                if (_moveSwapFrom < 0)
                {
                    _moveSwapFrom = _moveCursor;
                }
                else
                {
                    // Cambia la lista REAL del Poken: el combate y el save usan
                    // esta misma lista, asi que el orden nuevo vale de verdad.
                    poken.SwapMoves(_moveSwapFrom, _moveCursor);
                    _moveSwapFrom = -1;
                }
                Refresh();
                break;

            case GameAction.Cancel:
                if (_moveSwapFrom >= 0) _moveSwapFrom = -1;
                else _inMoveMode = false;
                Refresh();
                break;
        }
    }

    // ---------- dibujo ----------

    private void Refresh()
    {
        PokenModel poken = Current;

        if (pages != null)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;
                if (pages[i].root != null) pages[i].root.SetActive(i == _page);
                if (pages[i].tab != null) pages[i].tab.SetSelected(i == _page);
            }

            if (pageTitleText != null && _page < pages.Length && pages[_page] != null)
                pageTitleText.text = pages[_page].title;
        }

        if (poken == null) return;

        RefreshHeader(poken);
        RefreshInfo(poken);
        RefreshNotes(poken);
        RefreshStats(poken);
        RefreshMoves(poken);
    }

    private void RefreshHeader(PokenModel poken)
    {
        PokenDefinition def = poken.Definition;

        if (pokenSprite != null)
        {
            Sprite sprite = def != null ? (def.battleFrontSprite ?? def.inventorySprite) : null;
            pokenSprite.sprite = sprite;
            pokenSprite.enabled = sprite != null;
            pokenSprite.preserveAspect = true;
        }

        if (nameText != null) nameText.text = def != null ? def.pokenName : "???";
        if (levelText != null) levelText.text = string.Format(levelFormat, poken.Level);
        MenuIconLibrary.PaintStatus(statusIcon, poken.CurrentStatus);
    }

    private void RefreshInfo(PokenModel poken)
    {
        PokenDefinition def = poken.Definition;

        if (speciesText != null) speciesText.text = def != null ? def.pokenName : "???";
        MenuIconLibrary.PaintType(type1Icon, def != null ? def.type : PokenType.None);
        MenuIconLibrary.PaintType(type2Icon, def != null ? def.type2 : PokenType.None);

        if (trainerText != null)
            trainerText.text = Player != null ? Player.PlayerName : string.Empty;

        if (idText != null)
        {
            // No hay ID de entrenador en el modelo: se deriva del nombre para
            // que sea estable entre sesiones sin inventar un campo nuevo en el
            // save. Si algun dia existe un ID real, se cambia esta linea.
            int id = Player != null && Player.PlayerName != null
                ? Mathf.Abs(Player.PlayerName.GetHashCode()) % 100000 : 0;
            idText.text = id.ToString("D5");
        }

        if (xpText != null) xpText.text = poken.CurrentXP.ToString("N0");

        ExperienceTableDefinition tabla = DefinitionRepository.Instance.ExperienceTable;
        int faltan = 0;
        if (tabla != null && poken.Level < tabla.maxLevel)
        {
            int umbral = tabla.GetXPForLevel(poken.Level);
            if (umbral > 0) faltan = Mathf.Max(0, umbral - poken.CurrentXP);
        }
        if (xpToNextText != null) xpToNextText.text = faltan.ToString("N0");
        if (xpFill != null) xpFill.fillAmount = poken.XpProgress;

        if (heldItemText != null) heldItemText.text = "Ninguno";
    }

    private void RefreshNotes(PokenModel poken)
    {
        PokenDefinition def = poken.Definition;

        if (encounterText != null)
        {
            // No hay lugar ni fecha de captura en el modelo: se muestra lo que
            // el save si tiene. El dia que existan esos campos, se agregan aca.
            string entrenador = Player != null ? Player.PlayerName : "???";
            string dex = def != null && def.dexNumber > 0
                ? $"Nº {def.dexNumber} de la Pokédex." : "Especie propia de la torre.";
            string especie = def != null ? def.pokenName : "???";
            encounterText.text = especie + "\n" + dex +
                                 "\nEntrenador original: " + entrenador + ".";
        }

        if (characteristicText != null)
            characteristicText.text = Caracteristica(poken.Ivs);

        if (abilityNameText != null)
            abilityNameText.text = def != null && !string.IsNullOrEmpty(def.ability)
                ? def.ability : "Desconocida";

        if (abilityDescriptionText != null)
            abilityDescriptionText.text = def != null ? def.abilityDescription : string.Empty;
    }

    // La frase de la saga que describe el IV mas alto. Es informacion que ya
    // existe en el modelo (IvSet) y no necesita ningun campo nuevo.
    private static string Caracteristica(IvSet ivs)
    {
        int mejor = ivs.hp;
        string frase = "Le encanta comer.";

        if (ivs.attack > mejor) { mejor = ivs.attack; frase = "Orgulloso de su fuerza."; }
        if (ivs.defense > mejor) { mejor = ivs.defense; frase = "Aguanta lo que sea."; }
        if (ivs.specialAttack > mejor) { mejor = ivs.specialAttack; frase = "Muy travieso."; }
        if (ivs.specialDefense > mejor) { mejor = ivs.specialDefense; frase = "Fuerte de voluntad."; }
        if (ivs.speed > mejor) { frase = "Es muy veloz."; }

        return frase;
    }

    private void RefreshStats(PokenModel poken)
    {
        if (hpText != null) hpText.text = $"{poken.CurrentHP}/{poken.MaxHP}";
        if (attackText != null) attackText.text = poken.Attack.ToString();
        if (defenseText != null) defenseText.text = poken.Defense.ToString();
        if (specialAttackText != null) specialAttackText.text = poken.SpecialAttack.ToString();
        if (specialDefenseText != null) specialDefenseText.text = poken.SpecialDefense.ToString();
        if (speedText != null) speedText.text = poken.Speed.ToString();

        if (hpFill != null)
            hpFill.fillAmount = poken.MaxHP > 0
                ? Mathf.Clamp01((float)poken.CurrentHP / poken.MaxHP) : 0f;
    }

    private void RefreshMoves(PokenModel poken)
    {
        if (_moveRows == null) return;

        List<MoveModel> moves = poken.Moves;
        for (int i = 0; i < moves.Count; i++)
        {
            MoveRowWidget row = _moveRows.Get(i);
            row.Bind(moves[i]);
            row.SetSelected(_inMoveMode && i == _moveCursor);
            row.SetSwapping(i == _moveSwapFrom);
        }
        _moveRows.HideFrom(moves.Count);

        if (moveHintText != null)
        {
            if (!_allowMoveReorder) moveHintText.text = string.Empty;
            else if (!_inMoveMode) moveHintText.text = "Aceptar: reordenar";
            else if (_moveSwapFrom < 0) moveHintText.text = "Elegí el movimiento a mover";
            else moveHintText.text = "Elegí con cuál lo cambio";
        }
    }
}
