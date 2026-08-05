using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Orquestador del combate: recibe el input del jugador, le pide la accion a la
// IA y REPRODUCE los eventos que emite el TurnEngine (que es quien resuelve
// las reglas). Toda la logica de combate vive en Core/Battle; aca solo hay
// presentacion y ritmo.
public class BattleController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleHudView battleHudView;
    [SerializeField] private PokenView playerPokenView;          // sprite de espalda (jugador)
    [SerializeField] private PokenView enemyPokenView;           // sprite de frente (rival)
    [SerializeField] private PlayerInputController playerInputController; // corta el movimiento en mundo

    [Header("Escenario")]
    [Tooltip("Renderer del fondo de batalla. Se escala solo para cubrir la camara.")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [Tooltip("Secuencias visuales (transicion, balls, apariciones). Opcional: sin esto el combate arranca directo.")]
    [SerializeField] private BattleFxView battleFxView;

    [Header("Ritmo")]
    [Tooltip("Segundos que se muestra cada mensaje de combate.")]
    [SerializeField] private float messageDelay = 1f;

    // Fin del combate: EventController/GameManager aplican flags, items y save
    public event Action<BattleOutcome> BattleEnded;

    private BattleModel _battleModel;
    private PlayerModel _playerModel;
    private EnemyModel _enemyModel;
    private TurnEngine _engine;
    private IBattleAI _enemyAI;
    private AIProfileDefinition _aiProfile;
    private readonly IBattleRng _rng = new UnityRng();

    // Sprite del entrenador rival para la intro (null = combate salvaje)
    private Sprite _trainerIntroSprite;
    // El Poken de este lado se debilito y espera reemplazo (el cambio no hace retirada)
    private bool _playerFaintedPendingSwitch;
    private bool _enemyFaintedPendingSwitch;

    private void Awake()
    {
        // La UI avisa lo que eligio el jugador; aca se convierte en accion de combate
        if (battleHudView != null)
        {
            battleHudView.MoveChosen += SelectMove;
            battleHudView.SwitchChosen += SelectSwitch;
            battleHudView.FleeChosen += SelectFlee;
            battleHudView.BagChosen += OnBagSelected;
        }
    }

    // Combate contra un NPC (EnemyDefinition o BossDefinition)
    public void StartBattle(PlayerModel player, NPCDefinition enemyDefinition)
    {
        var enemy = new EnemyModel(enemyDefinition);

        Sprite background = null;
        _trainerIntroSprite = null;
        if (enemyDefinition is EnemyDefinition e)
        {
            background = e.battleBackground;
            _trainerIntroSprite = e.battleFrontSprite;
        }
        else if (enemyDefinition is BossDefinition b)
        {
            background = b.battleBackground;
            _trainerIntroSprite = b.battleFrontSprite;
        }

        BeginBattle(player, enemy, background);
    }

    // Combate contra un Poken salvaje (nivel sorteado en el rango del encuentro)
    public void StartWildBattle(PlayerModel player, WildEncounterDefinition encounter)
    {
        if (encounter == null || encounter.species == null)
        {
            Debug.LogWarning("BattleController: WildEncounterDefinition sin especie asignada.");
            return;
        }
        int level = _rng.Range(encounter.minLevel, encounter.maxLevel + 1);
        var enemy = new EnemyModel(encounter.species, level, encounter.aiProfile);
        _trainerIntroSprite = null;
        BeginBattle(player, enemy, encounter.battleBackground);
    }

    private void BeginBattle(PlayerModel player, EnemyModel enemy, Sprite background = null)
    {
        ApplyBackground(background);
        _playerModel = player;
        _enemyModel = enemy;
        _battleModel = new BattleModel(_playerModel, _enemyModel);

        // El HP persiste entre combates: el primer Poken puede estar debilitado.
        // Cada lado envia a su primer Poken con vida (regla de la saga).
        int playerLead = FirstAliveIndex(_playerModel.Team);
        int enemyLead = FirstAliveIndex(_enemyModel.Team);
        if (playerLead < 0 || enemyLead < 0)
        {
            Debug.LogWarning("BattleController: algun equipo esta vacio o sin Pokens con vida. No se inicia el combate.");
            _battleModel = null;
            return;
        }
        _battleModel.SwitchPlayerPoken(playerLead);
        _battleModel.SwitchEnemyPoken(enemyLead);

        // Motor + IA segun el perfil del enemigo (default: Wild para salvajes,
        // Trainer para NPCs sin perfil asignado)
        _engine = new TurnEngine(_battleModel, _rng);
        _aiProfile = enemy.AiProfile != null
            ? enemy.AiProfile
            : BattleAIFactory.CreateDefaultProfile(enemy.IsWild ? BattleAIType.Wild : BattleAIType.Trainer);
        _enemyAI = BattleAIFactory.Create(_aiProfile);
        _engine.EnemyReplacementSelector =
            model => _enemyAI.ChooseReplacement(AiContext.ForEnemy(model, _aiProfile));

        if (playerInputController != null)
            playerInputController.SetBattleMode(true);

        if (battleHudView != null)
            battleHudView.Show();

        RefreshActiveSprites();
        battleHudView?.UpdateHud(_battleModel);

        StartCoroutine(BeginBattleRoutine(enemy));
    }

    private IEnumerator BeginBattleRoutine(EnemyModel enemy)
    {
        _playerFaintedPendingSwitch = false;
        _enemyFaintedPendingSwitch = false;

        string enemyName = _battleModel.ActiveEnemyPoken.Definition.pokenName;
        string playerName = _battleModel.ActivePlayerPoken.Definition.pokenName;

        if (battleFxView != null)
        {
            // Secuencia clasica: transicion, aparicion del rival y balls
            playerPokenView?.Hide();
            enemyPokenView?.Hide();
            yield return battleFxView.ScreenTransition();

            if (enemy.IsWild)
            {
                Announce($"¡Un {enemyName} salvaje apareció!");
                yield return battleFxView.WildAppear(enemyPokenView);
                yield return new WaitForSeconds(messageDelay * 0.5f);
            }
            else
            {
                if (_trainerIntroSprite != null && enemyPokenView != null)
                {
                    yield return battleFxView.TrainerSlideIn(
                        _trainerIntroSprite, enemyPokenView.transform.position);
                    Announce("¡Te desafían a un combate!");
                    yield return new WaitForSeconds(messageDelay);
                    yield return battleFxView.TrainerSlideOut();
                }
                Announce($"¡El rival envió a {enemyName}!");
                yield return battleFxView.ThrowBallAndAppear(enemyPokenView, fromPlayerSide: false);
            }

            Announce($"¡Adelante, {playerName}!");
            yield return battleFxView.ThrowBallAndAppear(playerPokenView, fromPlayerSide: true);
        }
        else
        {
            // Sin efectos: arranque directo (modo de respaldo)
            Announce(enemy.IsWild
                ? $"¡Un {enemyName} salvaje apareció!"
                : $"Un {enemyName} rival apareció!");
            yield return new WaitForSeconds(messageDelay);
        }

        // Habilidades de entrada (Intimidación, etc.)
        yield return PlayEvents(_engine.BeginBattle());

        _battleModel.SetPhase(BattlePhase.PlayerTurn);
        battleHudView?.ShowCommandMenu(_battleModel);
    }

    // ---------- input del jugador ----------

    // TEMPORAL: entrada de prueba con teclado hasta la UI de batalla.
    // 1-4 = movimientos, F1-F6 = cambio manual de Poken.
    private void Update()
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SelectMove(0);
        else if (kb.digit2Key.wasPressedThisFrame) SelectMove(1);
        else if (kb.digit3Key.wasPressedThisFrame) SelectMove(2);
        else if (kb.digit4Key.wasPressedThisFrame) SelectMove(3);
        else if (kb.f1Key.wasPressedThisFrame) SelectSwitch(0);
        else if (kb.f2Key.wasPressedThisFrame) SelectSwitch(1);
        else if (kb.f3Key.wasPressedThisFrame) SelectSwitch(2);
        else if (kb.f4Key.wasPressedThisFrame) SelectSwitch(3);
        else if (kb.f5Key.wasPressedThisFrame) SelectSwitch(4);
        else if (kb.f6Key.wasPressedThisFrame) SelectSwitch(5);
    }

    // Punto de entrada para elegir movimiento por indice (teclas ahora, UI despues)
    public void SelectMove(int moveIndex)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        List<MoveModel> moves = _battleModel.ActivePlayerPoken.Moves;

        // Sin PP en ningun movimiento: la unica opcion es Forcejeo
        bool anyPP = moves.Exists(m => m.HasPP);
        if (!anyPP)
        {
            StartCoroutine(ResolveTurnRoutine(BattleAction.Struggle()));
            return;
        }

        if (moveIndex < 0 || moveIndex >= moves.Count) return;
        if (!moves[moveIndex].HasPP)
        {
            Announce("No quedan PP para ese movimiento!");
            return;
        }

        StartCoroutine(ResolveTurnRoutine(BattleAction.Move(moveIndex)));
    }

    // Cambio manual de Poken: consume el turno (el rival ataca al que entra)
    public void SelectSwitch(int teamIndex)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;
        if (teamIndex < 0 || teamIndex >= _playerModel.Team.Count) return;

        if (teamIndex == _battleModel.ActivePlayerIndex)
        {
            Announce($"¡{_battleModel.ActivePlayerPoken.Definition.pokenName} ya está en combate!");
            return;
        }
        if (_playerModel.Team[teamIndex].IsFainted)
        {
            Announce($"¡{_playerModel.Team[teamIndex].Definition.pokenName} está debilitado!");
            return;
        }

        StartCoroutine(ResolveTurnRoutine(BattleAction.Switch(teamIndex)));
    }

    // Huir: solo tiene sentido contra salvajes (contra entrenadores ni se intenta)
    public void SelectFlee()
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        if (!_enemyModel.IsWild)
        {
            Announce("¡No puedes huir de un combate contra un entrenador!");
            return;
        }

        StartCoroutine(ResolveTurnRoutine(BattleAction.Flee()));
    }

    // Mochila: los items en combate llegan mas adelante
    private void OnBagSelected()
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;
        Announce("La mochila todavía no está disponible.");
    }

    // ---------- resolucion y reproduccion del turno ----------

    private IEnumerator ResolveTurnRoutine(BattleAction playerAction)
    {
        _battleModel.SetPhase(BattlePhase.CalculatingResult);
        battleHudView?.HideMenus();

        BattleAction enemyAction = _enemyAI.ChooseAction(
            AiContext.ForEnemy(_battleModel, _aiProfile), _rng);

        List<BattleEvent> events = _engine.ResolveTurn(playerAction, enemyAction);
        yield return PlayEvents(events);

        if (_battleModel.IsBattleOver)
        {
            yield return EndBattleRoutine();
            yield break;
        }

        _battleModel.SetPhase(BattlePhase.PlayerTurn);
        battleHudView?.UpdateHud(_battleModel);
        battleHudView?.ShowCommandMenu(_battleModel);
    }

    // Reproduce los eventos del motor: anima cada paso, refresca la UI y pausa
    // entre mensajes. Con battleFxView null todo degrada a refrescos instantaneos.
    private IEnumerator PlayEvents(List<BattleEvent> events)
    {
        foreach (BattleEvent ev in events)
        {
            PokenView view = ev.TargetIsPlayer ? playerPokenView : enemyPokenView;
            bool messageHandled = false;

            switch (ev.Kind)
            {
                case BattleEventKind.MoveUsed:
                {
                    // Mensaje "¡X usó Y!" primero, la animacion despues; el
                    // drenado de HP es el evento siguiente, asi que el daño
                    // visual nunca ocurre antes del impacto.
                    if (!string.IsNullOrEmpty(ev.Message))
                    {
                        Announce(ev.Message);
                        yield return new WaitForSeconds(messageDelay * 0.6f);
                    }
                    messageHandled = true;

                    PokenView attackerView = ev.TargetIsPlayer ? playerPokenView : enemyPokenView;
                    PokenView defenderView = ev.TargetIsPlayer ? enemyPokenView : playerPokenView;
                    if (battleFxView != null && attackerView != null && defenderView != null
                        && attackerView.gameObject.activeSelf && defenderView.gameObject.activeSelf)
                        yield return battleFxView.PlayAttack(attackerView, defenderView, ev.Move);
                    break;
                }

                case BattleEventKind.PokenSwitched:
                {
                    // Si el saliente se debilito, ya se fue con su animacion:
                    // no hay retirada, solo entra el nuevo.
                    bool cameFromFaint = ev.TargetIsPlayer
                        ? _playerFaintedPendingSwitch
                        : _enemyFaintedPendingSwitch;
                    if (ev.TargetIsPlayer) _playerFaintedPendingSwitch = false;
                    else _enemyFaintedPendingSwitch = false;

                    if (battleFxView != null && view != null)
                    {
                        if (!cameFromFaint && view.gameObject.activeSelf)
                            yield return battleFxView.Recall(view);
                        RefreshActiveSprites();
                        battleHudView?.UpdateHud(_battleModel);
                        yield return battleFxView.ThrowBallAndAppear(view, ev.TargetIsPlayer);
                    }
                    else
                    {
                        RefreshActiveSprites();
                        battleHudView?.UpdateHud(_battleModel);
                    }
                    break;
                }

                case BattleEventKind.HpChanged:
                    // La barra drena gradualmente hasta el valor exacto
                    if (battleHudView != null)
                        yield return battleHudView.AnimateHp(_battleModel);
                    break;

                case BattleEventKind.Fainted:
                {
                    // Asegura que la barra llego a 0 ANTES de la caida
                    if (battleHudView != null)
                        yield return battleHudView.AnimateHp(_battleModel);
                    if (ev.TargetIsPlayer) _playerFaintedPendingSwitch = true;
                    else _enemyFaintedPendingSwitch = true;

                    if (battleFxView != null && view != null && view.gameObject.activeSelf)
                        yield return battleFxView.FaintAnim(view);
                    battleHudView?.UpdateHud(_battleModel);
                    break;
                }

                case BattleEventKind.StatStageChanged:
                {
                    if (battleFxView != null && view != null && view.gameObject.activeSelf)
                    {
                        bool isRaise = ev.Message != null && ev.Message.Contains("subió");
                        yield return battleFxView.PulseTint(view,
                            isRaise ? BattleFxView.PulseStatUp : BattleFxView.PulseStatDown);
                    }
                    battleHudView?.UpdateHud(_battleModel);
                    break;
                }

                case BattleEventKind.StatusChanged:
                {
                    if (battleFxView != null && view != null && view.gameObject.activeSelf)
                    {
                        PokenModel poken = ev.TargetIsPlayer
                            ? _battleModel.ActivePlayerPoken
                            : _battleModel.ActiveEnemyPoken;
                        yield return battleFxView.PulseTint(view,
                            BattleFxView.PulseForStatus(poken.CurrentStatus));
                    }
                    battleHudView?.UpdateHud(_battleModel);
                    break;
                }

                case BattleEventKind.WeatherChanged:
                    battleFxView?.SetWeather(_battleModel.CurrentWeather);
                    break;

                case BattleEventKind.TerrainChanged:
                    battleFxView?.SetTerrain(_battleModel.CurrentTerrain);
                    break;

                case BattleEventKind.BattleEnded:
                    battleHudView?.UpdateHud(_battleModel);
                    break;
            }

            if (!messageHandled && !string.IsNullOrEmpty(ev.Message))
            {
                Announce(ev.Message);
                yield return new WaitForSeconds(messageDelay);
            }
        }
    }

    private IEnumerator EndBattleRoutine()
    {
        bool victory = _battleModel.IsPlayerVictory;
        bool escaped = _battleModel.PlayerEscaped;

        // Si escapo, el mensaje "¡Escapaste sin problemas!" ya salio como evento
        if (!escaped)
        {
            Announce(victory ? "¡Ganaste el combate!" : "Perdiste el combate...");
            yield return new WaitForSeconds(messageDelay);
        }

        var outcome = new BattleOutcome
        {
            PlayerVictory = victory,
            Escaped = escaped,
            MoneyEarned = victory ? _enemyModel.Money : 0,
            Enemy = _enemyModel
        };

        if (victory && outcome.MoneyEarned > 0)
        {
            _playerModel.AddMoney(outcome.MoneyEarned);
            Announce($"¡Ganaste ${outcome.MoneyEarned} por la victoria!");
            yield return new WaitForSeconds(messageDelay);
        }

        if (playerInputController != null)
            playerInputController.SetBattleMode(false);

        battleHudView?.Hide();
        battleFxView?.ClearFieldOverlays();
        _battleModel = null;

        // Flags, items, dialogo de derrota del boss y guardado: los aplican
        // los suscriptores (EventController / GameManager)
        BattleEnded?.Invoke(outcome);
    }

    // ---------- utilidades ----------

    // Cambia el fondo (si el enemigo trae uno) y lo escala para cubrir la camara
    private void ApplyBackground(Sprite background)
    {
        if (backgroundRenderer == null) return;
        if (background != null) backgroundRenderer.sprite = background;
        if (backgroundRenderer.sprite == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        // Cuanto abarca la camara a la altura del fondo. Con camara ortografica
        // es fijo; con perspectiva depende de la distancia, asi que hay que medir
        // el frustum en el plano donde esta parado el fondo.
        float viewHeight;
        if (cam.orthographic)
        {
            viewHeight = cam.orthographicSize * 2f;
        }
        else
        {
            // Distancia proyectada sobre el eje de vista (no la distancia recta):
            // es la que define el tamaño del frustum en ese plano.
            float distance = Vector3.Dot(
                backgroundRenderer.transform.position - cam.transform.position,
                cam.transform.forward);
            if (distance <= 0f) return; // el fondo quedo detras de la camara
            viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        float viewWidth = viewHeight * cam.aspect;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
        backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // Muestra el mensaje en el HUD si existe; si no, lo manda a la Console.
    // Asi se puede probar el combate sin armar la UI.
    private void Announce(string message)
    {
        if (battleHudView != null) battleHudView.ShowMessage(message);
        else Debug.Log($"[Batalla] {message}");
    }

    private void RefreshActiveSprites()
    {
        if (playerPokenView != null)
            playerPokenView.SetPoken(_battleModel.ActivePlayerPoken, isPlayerSide: true);
        if (enemyPokenView != null)
            enemyPokenView.SetPoken(_battleModel.ActiveEnemyPoken, isPlayerSide: false);
    }

    private static int FirstAliveIndex(List<PokenModel> team)
    {
        for (int i = 0; i < team.Count; i++)
            if (!team[i].IsFainted) return i;
        return -1;
    }
}
