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
        if (enemyDefinition is EnemyDefinition e) background = e.battleBackground;
        else if (enemyDefinition is BossDefinition b) background = b.battleBackground;

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
        Announce(enemy.IsWild
            ? $"¡Un {_battleModel.ActiveEnemyPoken.Definition.pokenName} salvaje apareció!"
            : $"Un {_battleModel.ActiveEnemyPoken.Definition.pokenName} rival apareció!");
        yield return new WaitForSeconds(messageDelay);

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

    // Reproduce los eventos del motor: refresca la UI y pausa entre mensajes
    private IEnumerator PlayEvents(List<BattleEvent> events)
    {
        foreach (BattleEvent ev in events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.PokenSwitched:
                    RefreshActiveSprites();
                    battleHudView?.UpdateHud(_battleModel);
                    break;

                case BattleEventKind.HpChanged:
                case BattleEventKind.StatusChanged:
                case BattleEventKind.StatStageChanged:
                case BattleEventKind.Fainted:
                case BattleEventKind.BattleEnded:
                    battleHudView?.UpdateHud(_battleModel);
                    break;
            }

            if (!string.IsNullOrEmpty(ev.Message))
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
        if (cam == null || !cam.orthographic) return;

        float viewHeight = cam.orthographicSize * 2f;
        float viewWidth = viewHeight * cam.aspect;
        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

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
