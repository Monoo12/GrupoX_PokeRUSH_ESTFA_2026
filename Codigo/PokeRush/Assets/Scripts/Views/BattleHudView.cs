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

    [Header("Escudo del nucleo (solo boss)")]
    [Tooltip("Se prende solo si la pelea tiene escudo. Todas las referencias son " +
             "opcionales: sin ellas el combate anda igual, solo que sin barra.")]
    [SerializeField] private GameObject shieldBox;
    [SerializeField] private Image shieldFill;
    [SerializeField] private Text shieldText;

    [Header("Prediccion del boss")]
    [Tooltip("Panel de la lectura de Nestor. Presentacion aparte: recibe un " +
             "PredictionSnapshot y decide como se ve. Opcional.")]
    [SerializeField] private BattlePredictionView predictionView;
    [Tooltip("Respaldo si no hay BattlePredictionView: un solo Text con el titulo.")]
    [SerializeField] private Text predictionBannerText;

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

    [Header("Menu de MOCHILA")]
    [Tooltip("Panel con los objetos de curacion. Mismo patron que el menu de " +
             "equipo: N slots fijos cableados en el Inspector, se muestran solo " +
             "los que tienen objeto. Reemplazar el arte no toca la logica.")]
    [SerializeField] private GameObject bagPanel;
    [SerializeField] private Button[] bagButtons = new Button[6];
    [SerializeField] private Text[] bagNameTexts = new Text[6];
    [SerializeField] private Text[] bagQuantityTexts = new Text[6];
    [SerializeField] private Text bagEmptyText;
    [SerializeField] private Button bagBackButton;

    [Header("Menu de la fase de liberacion")]
    [Tooltip("Panel propio con CURAR / PROTEGER / CAMBIAR / ATACAR. No reusa el " +
             "commandPanel porque sus botones tienen el texto horneado en el sprite.")]
    [SerializeField] private GameObject liberationPanel;
    [SerializeField] private Button liberationHealButton;
    [SerializeField] private Button liberationProtectButton;
    [SerializeField] private Button liberationSwitchButton;
    [SerializeField] private Button liberationAttackButton;
    [SerializeField] private Text liberationTurnsText;
    [SerializeField] private Text liberationHintText;

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

    // Cian: se distingue de la barra de PS de un vistazo y pega con el tema
    // "nucleo/firewall" de la torre
    private static readonly Color ShieldFull = new Color(0.35f, 0.80f, 0.95f);
    private static readonly Color ShieldLow = new Color(0.45f, 0.50f, 0.62f);

    // La UI avisa lo que eligio el jugador; BattleController resuelve
    public event Action<int> MoveChosen;
    public event Action<int> SwitchChosen;
    public event Action FleeChosen;
    public event Action BagChosen;
    // Indice DENTRO de la lista que se le paso a ShowBagMenu, no del inventario
    public event Action<int> ItemChosen;

    // Fase de liberacion. El cambio de Poken reusa el menu de equipo y su
    // evento SwitchChosen: BattleController lo redirige segun la fase.
    public event Action LiberationHealChosen;
    public event Action LiberationProtectChosen;
    public event Action LiberationAttackChosen;

    private BattleModel _model;

    // En liberacion, "volver" desde el menu de equipo tiene que regresar al
    // panel de liberacion y no al de comandos.
    private bool _liberationMode;

    // Cambio OBLIGADO (el Poken activo se debilito): no hay "volver", el
    // jugador tiene que elegir si o si.
    private bool _forcedSwitch;

    // Cuantos objetos tiene la mochila abierta ahora mismo
    private int _bagCount;

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
        if (teamBackButton != null) teamBackButton.onClick.AddListener(VolverDesdeEquipo);
        if (bagBackButton != null) bagBackButton.onClick.AddListener(ShowCommandMenuInternal);

        for (int i = 0; i < bagButtons.Length; i++)
        {
            int index = i;
            if (bagButtons[i] == null) continue;
            bagButtons[i].onClick.AddListener(() => ItemChosen?.Invoke(index));
        }

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

        // Botones de la fase de liberacion
        if (liberationHealButton != null)
            liberationHealButton.onClick.AddListener(() => LiberationHealChosen?.Invoke());
        if (liberationProtectButton != null)
            liberationProtectButton.onClick.AddListener(() => LiberationProtectChosen?.Invoke());
        if (liberationAttackButton != null)
            liberationAttackButton.onClick.AddListener(() => LiberationAttackChosen?.Invoke());
        if (liberationSwitchButton != null)
            liberationSwitchButton.onClick.AddListener(OpenTeamMenu);
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
        ApplyShieldVisuals(battleModel);
    }

    // Escudo del nucleo. Se dibuja como barra y no como mensaje porque el
    // mensaje pasaba desapercibido entre el resto del texto de combate, y el
    // escudo es justamente lo que el jugador se gano cortando el firewall:
    // tiene que verse bajar golpe a golpe.
    private void ApplyShieldVisuals(BattleModel battleModel)
    {
        SideState lado = battleModel.EnemySide;
        bool visible = lado != null && lado.UsaEscudo;

        if (shieldBox != null) shieldBox.SetActive(visible);
        if (!visible) return;

        float ratio = Mathf.Clamp01(lado.ShieldHp / (float)Mathf.Max(1, lado.ShieldMaxHp));
        if (shieldFill != null)
        {
            shieldFill.fillAmount = ratio;
            shieldFill.color = ratio > 0f ? ShieldFull : ShieldLow;
        }
        if (shieldText != null)
            shieldText.text = lado.ShieldHp > 0 ? $"ESCUDO {lado.ShieldHp}" : "ESCUDO ROTO";
    }

    // Drena/sube el HP mostrado hacia el valor real, como en la saga: velocidad
    // proporcional al daño (0.25 a 1.3 segundos), color segun porcentaje, y se
    // detiene EXACTO en el valor correcto. El evento Fainted viene despues de
    // este drenado, asi que el debilitamiento arranca con la barra ya en 0.
    public IEnumerator AnimateHp(BattleModel battleModel) => AnimateHp(battleModel, null);

    // 'ev' trae la foto de PS del instante en que se emitio (BattleEvent.HpAfter).
    // Sin ella se anima hacia el HP actual del modelo, que al reproducir ya es
    // el del FINAL del turno: por eso una curacion saltaba directo al valor con
    // el golpe del rival ya restado, y el golpe despues no movia la barra.
    public IEnumerator AnimateHp(BattleModel battleModel, BattleEvent ev)
    {
        _model = battleModel;
        PokenModel player = battleModel.ActivePlayerPoken;
        PokenModel enemy = battleModel.ActiveEnemyPoken;

        float objetivoPlayer = player.CurrentHP;
        float objetivoEnemy = enemy.CurrentHP;
        if (ev != null && ev.HpAfter >= 0)
        {
            // Solo el lado que cambio va a la foto; el otro se queda donde esta
            if (ev.TargetIsPlayer) { objetivoPlayer = ev.HpAfter; objetivoEnemy = _displayedEnemyHp; }
            else { objetivoEnemy = ev.HpAfter; objetivoPlayer = _displayedPlayerHp; }
        }

        if (_displayedPlayerHp < 0f) _displayedPlayerHp = player.CurrentHP;
        if (_displayedEnemyHp < 0f) _displayedEnemyHp = enemy.CurrentHP;

        float playerDelta = Mathf.Abs(objetivoPlayer - _displayedPlayerHp);
        float enemyDelta = Mathf.Abs(objetivoEnemy - _displayedEnemyHp);
        if (playerDelta < 0.5f && enemyDelta < 0.5f) yield break;

        float maxFraction = Mathf.Max(
            playerDelta / Mathf.Max(1, player.MaxHP),
            enemyDelta / Mathf.Max(1, enemy.MaxHP));
        // Piso de 0.45s: un cambio chico (una pocion de 20 PS sobre 100) daba
        // 0.32s y se leia como un salto instantaneo. La barra tiene que MOVERSE
        // para que se entienda que curo.
        float duration = Mathf.Clamp(maxFraction * 1.8f, 0.45f, 1.3f);
        float playerSpeed = playerDelta / duration;
        float enemySpeed = enemyDelta / duration;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            _displayedPlayerHp = Mathf.MoveTowards(_displayedPlayerHp, objetivoPlayer, playerSpeed * Time.deltaTime);
            _displayedEnemyHp = Mathf.MoveTowards(_displayedEnemyHp, objetivoEnemy, enemySpeed * Time.deltaTime);
            ApplyHpVisuals(player, enemy);
            // TODO: sonido de drenaje sincronizado (AudioService) cuando haya sfx
            yield return null;
        }

        // Termina exacto en la foto. Ojo: NO se llama UpdateHud, porque ese
        // resincroniza las barras con el HP final del turno y volveria a pisar
        // los pasos intermedios que justamente queremos mostrar.
        _displayedPlayerHp = objetivoPlayer;
        _displayedEnemyHp = objetivoEnemy;
        ApplyHpVisuals(player, enemy);
        ApplyShieldVisuals(battleModel);
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

    // Estado de la Prediccion. Recibe DATOS, no texto armado: la presentacion
    // la decide BattlePredictionView (o el Text de respaldo).
    public void SetPrediction(PredictionSnapshot snapshot)
    {
        if (predictionView != null)
        {
            predictionView.Render(snapshot);
            return;
        }

        if (predictionBannerText == null) return;
        string texto = snapshot.Activa ? snapshot.Titulo : null;
        predictionBannerText.text = texto;
        predictionBannerText.gameObject.SetActive(!string.IsNullOrEmpty(texto));
    }

    // Turno del jugador: muestra los comandos y la pregunta clasica
    public void ShowCommandMenu(BattleModel battleModel)
    {
        _model = battleModel;
        _liberationMode = false;
        _forcedSwitch = false;
        ShowCommandMenuInternal();
    }

    // Oculta todos los menus (mientras se resuelve el turno)
    public void HideMenus()
    {
        if (commandPanel != null) commandPanel.SetActive(false);
        if (fightPanel != null) fightPanel.SetActive(false);
        if (teamPanel != null) teamPanel.SetActive(false);
        if (bagPanel != null) bagPanel.SetActive(false);
        if (liberationPanel != null) liberationPanel.SetActive(false);
    }

    // Selector de Poken. 'forzado' = el activo se debilito: no hay como volver
    // atras y el mensaje lo dice. La UI no decide NADA de reglas: solo pinta lo
    // que el modelo ya sabe (quien esta vivo, quien es el activo) y avisa por
    // SwitchChosen.
    public void ShowTeamMenu(BattleModel battleModel, bool forzado = false)
    {
        _model = battleModel;
        _forcedSwitch = forzado;
        OpenTeamMenu();
    }

    // Mochila. Recibe la lista YA filtrada por el controller (solo lo usable en
    // combate): la vista no sabe que es usable y que no.
    public void ShowBagMenu(List<HealItemStack> objetos)
    {
        HideMenus();
        _bagCount = objetos != null ? objetos.Count : 0;

        if (bagPanel == null)
        {
            Debug.LogWarning("[HUD] Falta bagPanel: la mochila no se puede abrir.");
            return;
        }

        bagPanel.SetActive(true);
        ShowMessage(_bagCount > 0 ? "¿Qué objeto vas a usar?" : "La mochila está vacía.");

        if (bagEmptyText != null)
        {
            bagEmptyText.gameObject.SetActive(_bagCount == 0);
            bagEmptyText.text = "No te quedan objetos.";
        }

        for (int i = 0; i < bagButtons.Length; i++)
        {
            if (bagButtons[i] == null) continue;

            bool hasItem = i < _bagCount;
            bagButtons[i].gameObject.SetActive(hasItem);
            if (!hasItem) continue;

            // Los arrays de textos se pueden redimensionar a mano en el
            // Inspector: se accede con guardas para que un slot sin Text no
            // rompa la mochila entera.
            HealItemStack stack = objetos[i];
            Text nombre = At(bagNameTexts, i);
            Text cantidad = At(bagQuantityTexts, i);
            if (nombre != null) nombre.text = stack.Definition.itemName;
            if (cantidad != null) cantidad.text = $"x{stack.Quantity}";
            bagButtons[i].interactable = stack.Quantity > 0;
        }
    }

    // Menu de la fase de liberacion: las tres herramientas mas atacar.
    // Atacar queda HABILITADO a proposito: es un error castigado, no una opcion
    // prohibida. La leccion del nivel es dejar de atacar, y para aprenderla hay
    // que poder equivocarse.
    public void ShowLiberationMenu(int aguantados, int totales, string avisoDelChip)
    {
        _liberationMode = true;
        _forcedSwitch = false;
        HideMenus();

        if (liberationTurnsText != null)
            liberationTurnsText.text = $"Sobrecarga {aguantados}/{totales}";
        if (liberationHintText != null)
            liberationHintText.text = avisoDelChip;

        if (liberationPanel != null) liberationPanel.SetActive(true);
        else Debug.LogWarning("[HUD] Falta liberationPanel: la fase corre pero sin botones.");

        ShowMessage("Aguantá sin atacar.");
    }

    // ---------- menus internos ----------

    // Apaga TODOS los submenus. Existe como metodo y no suelto en cada lugar
    // porque agregar un panel nuevo y olvidarse de apagarlo en uno solo de los
    // caminos deja la pantalla trabada: fue exactamente lo que paso con la
    // mochila, que al tocar ATRAS mostraba el menu de comandos por debajo pero
    // el overlay de la mochila quedaba encima y parecia que no salia.
    private void CerrarSubmenus()
    {
        if (fightPanel != null) fightPanel.SetActive(false);
        if (teamPanel != null) teamPanel.SetActive(false);
        if (bagPanel != null) bagPanel.SetActive(false);
    }

    private void ShowCommandMenuInternal()
    {
        CerrarSubmenus();

        // En liberacion el "volver" del menu de equipo regresa a su propio
        // panel, no al de comandos (que no aplica en esa fase).
        if (_liberationMode)
        {
            if (liberationPanel != null) liberationPanel.SetActive(true);
            return;
        }

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

        CerrarSubmenus();
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

    // Vuelve al menu anterior desde el equipo. En un cambio OBLIGADO no hace
    // nada: el jugador tiene que elegir con quien sigue.
    private void VolverDesdeEquipo()
    {
        if (_forcedSwitch) return;
        ShowCommandMenuInternal();
    }

    private void OpenTeamMenu()
    {
        if (_model == null || teamPanel == null) return;

        CerrarSubmenus();
        if (commandPanel != null) commandPanel.SetActive(false);
        if (liberationPanel != null) liberationPanel.SetActive(false);
        teamPanel.SetActive(true);

        // En un cambio obligado el "volver" se esconde: no hay a donde volver.
        if (teamBackButton != null) teamBackButton.gameObject.SetActive(!_forcedSwitch);
        ShowMessage(_forcedSwitch ? "¿Qué Poken vas a enviar?" : "Elige un Pokémon.");

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

    // Acceso seguro a los arrays cableados en el Inspector: si alguien los
    // redimensiona o deja un hueco, devuelve null en vez de tirar excepcion.
    private static T At<T>(T[] array, int index) where T : class
        => (array != null && index >= 0 && index < array.Length) ? array[index] : null;

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
