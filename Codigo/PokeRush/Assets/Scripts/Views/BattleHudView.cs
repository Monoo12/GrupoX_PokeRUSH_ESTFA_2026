using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD de batalla estilo clasico (assets de Diseño/Graphics/UI/Battle):
// databoxes con HP/EXP, menu de comandos (LUCHAR/POKEMON/MOCHILA/HUIR),
// menu de lucha con panel de PP/TIPO y menu de equipo para cambiar.
//
// La vista NO decide reglas: expone eventos (MoveChosen, SwitchChosen...)
// que BattleController conecta con el combate. La escena la arma el builder
// (PokeRush > Armar combate de prueba), que cablea todos los campos.
public class BattleHudView : MonoBehaviour
{
    [Header("Fondo del panel de mensajes")]
    [SerializeField] private GameObject messageBar;

    [Header("Databox jugador")]
    [SerializeField] private GameObject playerBox;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text playerLevelText;
    [SerializeField] private Text playerHpText;
    [SerializeField] private Image playerHpFill;
    [SerializeField] private Image playerExpFill;
    [SerializeField] private Image playerStatusIcon;

    [Header("Databox rival")]
    [SerializeField] private GameObject enemyBox;
    [SerializeField] private Text enemyNameText;
    [SerializeField] private Text enemyLevelText;
    [SerializeField] private Image enemyHpFill;
    [SerializeField] private Image enemyStatusIcon;

    [Header("Mensajes")]
    [SerializeField] private Text messageText;

    [Header("Menu de comandos")]
    [SerializeField] private GameObject commandPanel;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button pokemonButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button fleeButton;

    [Header("Menu de lucha")]
    [SerializeField] private GameObject fightPanel;
    [SerializeField] private Button[] moveButtons = new Button[4];
    [SerializeField] private Image[] moveButtonImages = new Image[4];
    [SerializeField] private Text[] moveNameTexts = new Text[4];
    [SerializeField] private Text ppText;
    [SerializeField] private Image moveTypeIcon;
    [SerializeField] private Button fightBackButton;

    [Header("Menu de equipo")]
    [SerializeField] private GameObject teamPanel;
    [SerializeField] private Button[] teamButtons = new Button[6];
    [SerializeField] private Text[] teamNameTexts = new Text[6];
    [SerializeField] private Text[] teamHpTexts = new Text[6];
    [SerializeField] private Button teamBackButton;

    [Header("Sprites por tipo (indice = enum PokenType, 0=None)")]
    [SerializeField] private Sprite[] moveSpritesByType;          // boton de movimiento, estado normal
    [SerializeField] private Sprite[] moveSelectedSpritesByType;  // boton de movimiento, iluminado
    [SerializeField] private Sprite[] typeIconsByType;            // icono de tipo (panel TIPO)

    [Header("Iconos de estado (indice = enum StatusEffect, 0=None)")]
    [SerializeField] private Sprite[] statusIconsByEffect;

    // Colores clasicos de la barra de HP y umbral de cada uno
    private static readonly Color HpGreen = new Color(0.11f, 0.79f, 0.29f);
    private static readonly Color HpYellow = new Color(0.97f, 0.78f, 0.17f);
    private static readonly Color HpRed = new Color(0.94f, 0.22f, 0.16f);

    // La UI avisa lo que eligio el jugador; BattleController resuelve
    public event Action<int> MoveChosen;
    public event Action<int> SwitchChosen;
    public event Action FleeChosen;
    public event Action BagChosen;

    private BattleModel _model;

    // HP que se MUESTRA (drena gradualmente hacia el valor real del modelo)
    private float _displayedPlayerHp = -1f;
    private float _displayedEnemyHp = -1f;

    private void Awake()
    {
        // Cableado de botones (los refs los asigna el builder en el Inspector)
        if (fightButton != null) fightButton.onClick.AddListener(OpenFightMenu);
        if (pokemonButton != null) pokemonButton.onClick.AddListener(OpenTeamMenu);
        if (bagButton != null) bagButton.onClick.AddListener(() => BagChosen?.Invoke());
        if (fleeButton != null) fleeButton.onClick.AddListener(() => FleeChosen?.Invoke());
        if (fightBackButton != null) fightBackButton.onClick.AddListener(ShowCommandMenuInternal);
        if (teamBackButton != null) teamBackButton.onClick.AddListener(ShowCommandMenuInternal);

        for (int i = 0; i < moveButtons.Length; i++)
        {
            int index = i; // captura por valor
            if (moveButtons[i] == null) continue;
            moveButtons[i].onClick.AddListener(() => MoveChosen?.Invoke(index));
            AddHoverHandler(moveButtons[i].gameObject, () => ShowMoveInfo(index));
        }

        for (int i = 0; i < teamButtons.Length; i++)
        {
            int index = i;
            if (teamButtons[i] == null) continue;
            teamButtons[i].onClick.AddListener(() => SwitchChosen?.Invoke(index));
        }
    }

    // ---------- API que usa BattleController ----------

    public void Show()
    {
        gameObject.SetActive(true);
        HideMenus();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Refresco INSTANTANEO (inicio de combate, cambio de Poken). Para el daño
    // gradual usar AnimateHp.
    public void UpdateHud(BattleModel battleModel)
    {
        _model = battleModel;
        PokenModel player = battleModel.ActivePlayerPoken;
        PokenModel enemy = battleModel.ActiveEnemyPoken;

        _displayedPlayerHp = player.CurrentHP;
        _displayedEnemyHp = enemy.CurrentHP;

        // --- databox del jugador ---
        if (playerNameText != null) playerNameText.text = player.Definition.pokenName;
        if (playerLevelText != null) playerLevelText.text = $"Nv. {player.Level}";
        if (playerExpFill != null) playerExpFill.fillAmount = player.XpProgress;
        UpdateStatusIcon(playerStatusIcon, player);

        // --- databox del rival (sin numeros de HP, como en la saga) ---
        if (enemyNameText != null) enemyNameText.text = enemy.Definition.pokenName;
        if (enemyLevelText != null) enemyLevelText.text = $"Nv. {enemy.Level}";
        UpdateStatusIcon(enemyStatusIcon, enemy);

        ApplyHpVisuals(player, enemy);
    }

    // Drena/sube el HP mostrado hacia el valor real, como en la saga: velocidad
    // proporcional al daño (0.25 a 1.3 segundos), color segun porcentaje, y se
    // detiene EXACTO en el valor correcto. El evento Fainted viene despues de
    // este drenado, asi que el debilitamiento arranca con la barra ya en 0.
    public IEnumerator AnimateHp(BattleModel battleModel)
    {
        _model = battleModel;
        PokenModel player = battleModel.ActivePlayerPoken;
        PokenModel enemy = battleModel.ActiveEnemyPoken;

        if (_displayedPlayerHp < 0f) _displayedPlayerHp = player.CurrentHP;
        if (_displayedEnemyHp < 0f) _displayedEnemyHp = enemy.CurrentHP;

        float playerDelta = Mathf.Abs(player.CurrentHP - _displayedPlayerHp);
        float enemyDelta = Mathf.Abs(enemy.CurrentHP - _displayedEnemyHp);
        if (playerDelta < 0.5f && enemyDelta < 0.5f)
        {
            UpdateHud(battleModel);
            yield break;
        }

        float maxFraction = Mathf.Max(
            playerDelta / Mathf.Max(1, player.MaxHP),
            enemyDelta / Mathf.Max(1, enemy.MaxHP));
        float duration = Mathf.Clamp(maxFraction * 1.6f, 0.25f, 1.3f);
        float playerSpeed = playerDelta / duration;
        float enemySpeed = enemyDelta / duration;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            _displayedPlayerHp = Mathf.MoveTowards(_displayedPlayerHp, player.CurrentHP, playerSpeed * Time.deltaTime);
            _displayedEnemyHp = Mathf.MoveTowards(_displayedEnemyHp, enemy.CurrentHP, enemySpeed * Time.deltaTime);
            ApplyHpVisuals(player, enemy);
            // TODO: sonido de drenaje sincronizado (AudioService) cuando haya sfx
            yield return null;
        }

        // Termina exacto y refresca el resto del HUD (exp, estados)
        UpdateHud(battleModel);
    }

    private void ApplyHpVisuals(PokenModel player, PokenModel enemy)
    {
        if (playerHpText != null)
            playerHpText.text = $"{Mathf.RoundToInt(_displayedPlayerHp)}/{player.MaxHP}";
        SetHpBar(playerHpFill, _displayedPlayerHp, player.MaxHP);
        SetHpBar(enemyHpFill, _displayedEnemyHp, enemy.MaxHP);
    }

    public void ShowMessage(string message)
    {
        if (messageText != null) messageText.text = message;
        else Debug.Log($"[Batalla] {message}"); // sin HUD: al menos lo veo en consola
    }

    // Turno del jugador: muestra los comandos y la pregunta clasica
    public void ShowCommandMenu(BattleModel battleModel)
    {
        _model = battleModel;
        ShowCommandMenuInternal();
    }

    // Oculta todos los menus (mientras se resuelve el turno)
    public void HideMenus()
    {
        if (commandPanel != null) commandPanel.SetActive(false);
        if (fightPanel != null) fightPanel.SetActive(false);
        if (teamPanel != null) teamPanel.SetActive(false);
    }

    // ---------- menus internos ----------

    private void ShowCommandMenuInternal()
    {
        if (fightPanel != null) fightPanel.SetActive(false);
        if (teamPanel != null) teamPanel.SetActive(false);
        if (commandPanel != null) commandPanel.SetActive(true);
        if (_model != null)
            ShowMessage($"¿Qué hará {_model.ActivePlayerPoken.Definition.pokenName}?");
    }

    private void OpenFightMenu()
    {
        if (_model == null || fightPanel == null) return;

        List<MoveModel> moves = _model.ActivePlayerPoken.Moves;

        // Sin PP en nada: la unica opcion es Forcejeo (el controller lo convierte)
        if (!moves.Exists(m => m.HasPP))
        {
            MoveChosen?.Invoke(0);
            return;
        }

        if (commandPanel != null) commandPanel.SetActive(false);
        fightPanel.SetActive(true);
        ShowMessage("Elige un movimiento.");

        for (int i = 0; i < moveButtons.Length; i++)
        {
            bool hasMove = i < moves.Count && moves[i] != null;
            if (moveButtons[i] == null) continue;
            moveButtons[i].gameObject.SetActive(hasMove);
            if (!hasMove) continue;

            MoveDefinition def = moves[i].Definition;
            if (moveNameTexts[i] != null) moveNameTexts[i].text = def.moveName;
            moveButtons[i].interactable = moves[i].HasPP;

            // Fondo del boton segun el tipo del movimiento (normal + iluminado)
            if (moveButtonImages[i] != null)
            {
                moveButtonImages[i].sprite = SpriteForType(moveSpritesByType, def.type);
                var state = moveButtons[i].spriteState;
                state.highlightedSprite = SpriteForType(moveSelectedSpritesByType, def.type);
                state.pressedSprite = state.highlightedSprite;
                state.selectedSprite = state.highlightedSprite;
                moveButtons[i].spriteState = state;
                moveButtons[i].transition = Selectable.Transition.SpriteSwap;
            }
        }

        ShowMoveInfo(0);
    }

    // Panel lateral PP/TIPO: se actualiza al pasar el puntero por un movimiento
    private void ShowMoveInfo(int moveIndex)
    {
        if (_model == null) return;
        List<MoveModel> moves = _model.ActivePlayerPoken.Moves;
        if (moveIndex < 0 || moveIndex >= moves.Count || moves[moveIndex] == null) return;

        MoveModel move = moves[moveIndex];
        if (ppText != null) ppText.text = $"PP {move.CurrentPP}/{move.Definition.maxPP}";
        if (moveTypeIcon != null)
        {
            Sprite icon = SpriteForType(typeIconsByType, move.Definition.type);
            moveTypeIcon.sprite = icon;
            moveTypeIcon.enabled = icon != null;
        }
    }

    private void OpenTeamMenu()
    {
        if (_model == null || teamPanel == null) return;

        if (commandPanel != null) commandPanel.SetActive(false);
        teamPanel.SetActive(true);
        ShowMessage("Elige un Pokémon.");

        List<PokenModel> team = _model.Player.Team;
        for (int i = 0; i < teamButtons.Length; i++)
        {
            bool hasPoken = i < team.Count && team[i] != null;
            if (teamButtons[i] == null) continue;
            teamButtons[i].gameObject.SetActive(hasPoken);
            if (!hasPoken) continue;

            PokenModel poken = team[i];
            bool isActive = i == _model.ActivePlayerIndex;
            if (teamNameTexts[i] != null)
                teamNameTexts[i].text = isActive ? $"► {poken.Definition.pokenName}" : poken.Definition.pokenName;
            if (teamHpTexts[i] != null)
                teamHpTexts[i].text = poken.IsFainted ? "DEBILITADO" : $"PS {poken.CurrentHP}/{poken.MaxHP}";
            teamButtons[i].interactable = !poken.IsFainted && !isActive;
        }
    }

    // ---------- utilidades ----------

    private static void SetHpBar(Image fill, float currentHp, int maxHp)
    {
        if (fill == null) return;
        float ratio = maxHp > 0 ? currentHp / maxHp : 0f;
        fill.fillAmount = Mathf.Clamp01(ratio);
        fill.color = ratio > 0.5f ? HpGreen : (ratio > 0.2f ? HpYellow : HpRed);
    }

    private void UpdateStatusIcon(Image icon, PokenModel poken)
    {
        if (icon == null) return;
        Sprite sprite = null;
        int index = (int)poken.CurrentStatus;
        if (statusIconsByEffect != null && index > 0 && index < statusIconsByEffect.Length)
            sprite = statusIconsByEffect[index];
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    private static Sprite SpriteForType(Sprite[] table, PokenType type)
    {
        int index = (int)type;
        if (table == null || index < 0 || index >= table.Length) return null;
        return table[index];
    }

    // Agrega un handler de "puntero encima" por codigo (para el panel PP/TIPO)
    private static void AddHoverHandler(GameObject target, Action onEnter)
    {
        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => onEnter());
        trigger.triggers.Add(entry);
    }
}
