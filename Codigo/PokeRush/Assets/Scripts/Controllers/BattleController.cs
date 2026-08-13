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

    // Inicio del combate: lo usa MapBattleBridge para esconder el mapa.
    // Se dispara ANTES de la intro, con el modelo ya armado.
    public event Action BattleStarted;

    // Fin del combate: EventController/GameManager aplican flags, items y save
    public event Action<BattleOutcome> BattleEnded;

    private BattleModel _battleModel;
    private PlayerModel _playerModel;
    private EnemyModel _enemyModel;
    private TurnEngine _engine;
    private IBattleAI _enemyAI;
    private AIProfileDefinition _aiProfile;
    private readonly IBattleRng _rng = new UnityRng();

    // Como los subsistemas cortados en la torre modifican esta pelea.
    // Null en los combates normales (no todos los enemigos son el boss).
    private BossBattleConfig _bossConfig;
    private PredictionState _prediction;

    // Fase de liberacion: se dispara cuando el ULTIMO Poken del boss llega a PS
    // criticos. Null mientras no este activa.
    private LiberationEngine _liberation;
    private bool _liberationDone;
    // El motor salvo al boss de caer: la fase TIENE que arrancar si o si, o el
    // combate queda trabado con un boss inmortal.
    private bool _bossSalvadoEnUnPS;

    [Header("Fase de liberacion")]
    [Tooltip("Porcentaje de PS del ultimo Poken del boss al que arranca la fase.")]
    [SerializeField, Range(0.05f, 0.5f)] private float liberationHpThreshold = 0.15f;

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
            battleHudView.SwitchChosen += OnSwitchChosen;
            battleHudView.FleeChosen += SelectFlee;
            battleHudView.BagChosen += OnBagSelected;

            battleHudView.LiberationHealChosen += LiberationHeal;
            battleHudView.LiberationProtectChosen += LiberationProtect;
            battleHudView.LiberationAttackChosen += LiberationAttack;
        }
    }

    // Combate contra un NPC (EnemyDefinition o BossDefinition).
    //
    // 'config' dice como los subsistemas cortados en la torre modifican la
    // pelea. Es opcional para no romper a los llamadores que ya existian
    // (EventController, BattleTester) y porque solo el boss lo usa. Contra el
    // boss sin config se arma sola desde el GameManager; pasarla a mano sirve
    // para probar combinaciones de subsistemas sin caminar la torre.
    public void StartBattle(PlayerModel player, NPCDefinition enemyDefinition,
        BossBattleConfig config = null)
    {
        var enemy = new EnemyModel(enemyDefinition);

        _bossConfig = enemyDefinition is BossDefinition
            ? (config ?? BossBattleConfig.DesdeTorre(GameManager.Instance?.Tower))
            : null;

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

        // Un salvaje no es un boss. Sin esto se arrastraba la config del
        // combate ANTERIOR y el salvaje heredaba EnemyCannotFaint: no podia
        // morir y el combate no terminaba nunca.
        _bossConfig = null;
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
        // La Prediccion solo existe contra el boss. Vive aca y no en la IA
        // porque anula una categoria ANTES de que el jugador elija: es un gate
        // de input, no una regla de resolucion de turno. Por eso el TurnEngine
        // ni se entera.
        _prediction = _bossConfig != null
            ? new PredictionState(_bossConfig.PrediccionConAviso)
            : null;

        // La Prediccion necesita saber que salidas le quedan al jugador para no
        // anularle la ultima. El rastreo activo ya le cierra el cambio.
        if (_prediction != null)
        {
            _prediction.PuedeCambiar = () =>
                _bossConfig.PermiteCambio && HayBancoDisponible();
            _prediction.TieneObjetos = () =>
                _playerModel.Inventory.HealItems.Exists(s => s.Quantity > 0);
        }
        if (_bossConfig != null)
            Debug.Log($"[Combate] Config del boss: {_bossConfig.Resumen()}");

        _engine = new TurnEngine(_battleModel, _rng);
        _aiProfile = enemy.AiProfile != null
            ? enemy.AiProfile
            : BattleAIFactory.CreateDefaultProfile(enemy.IsWild ? BattleAIType.Wild : BattleAIType.Trainer);
        _enemyAI = BattleAIFactory.Create(_aiProfile);
        _engine.EnemyReplacementSelector =
            model => _enemyAI.ChooseReplacement(AiContext.ForEnemy(model, _aiProfile));

        // El boss con fase de liberacion no puede caer: queda en 1 PS y el
        // combate sigue hasta que se resuelva la fase.
        _liberation = null;
        _liberationDone = false;
        _bossSalvadoEnUnPS = false;
        if (_bossConfig != null)
        {
            // OJO: este delegado y DebeEmpezarLiberacion() TIENEN que estar de
            // acuerdo. Si el boss se salva de morir pero la fase no arranca, el
            // combate no termina nunca (no puede caer y nadie lo libera).
            // Por eso lo que dispara la fase es haberlo salvado, no un umbral
            // aparte que podria no cumplirse.
            _engine.EnemyCannotFaint = m =>
            {
                if (_liberationDone || !EsUltimoPokenDelBoss(m)) return false;
                _bossSalvadoEnUnPS = true;
                return true;
            };

            // El escudo del nucleo lo pone la torre: cuantos nodos de firewall
            // quedaron activos.
            _battleModel.EnemySide.ShieldHp = _bossConfig.EscudoInicial;
        }

        if (playerInputController != null)
            playerInputController.SetBattleMode(true);

        // Avisa antes de la intro para que el mapa se esconda a tiempo
        BattleStarted?.Invoke();

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
        yield return AvanzarPrediccion();
        battleHudView?.ShowCommandMenu(_battleModel);
    }

    // Adelanta el contador de la Prediccion y avisa por pantalla si corresponde.
    // Se llama SIEMPRE antes de abrir el menu de comandos, para que el jugador
    // vea la categoria anulada antes de elegir.
    private IEnumerator AvanzarPrediccion()
    {
        if (_prediction == null) yield break;

        string mensaje = _prediction.Avanzar(_battleModel, _rng);
        battleHudView?.SetPredictionBanner(_prediction.Banda());

        if (string.IsNullOrEmpty(mensaje)) yield break;

        Announce(mensaje);
        yield return new WaitForSeconds(messageDelay);
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

        // La Prediccion se chequea ACA y no solo en los botones del HUD: el
        // Update() de abajo tiene atajos 1-4 que saltean la UI por completo.
        if (Anticipado(BattleActionKind.UseMove, moves[moveIndex].Definition)) return;

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

        // Rastreo activo: no se puede cambiar en toda la pelea (GDD)
        if (_bossConfig != null && !_bossConfig.PermiteCambio)
        {
            Announce("El rastreo tiene marcado a tu equipo: no podés cambiar.");
            return;
        }

        if (Anticipado(BattleActionKind.SwitchPoken, null)) return;

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

    // Mochila
    private void OnBagSelected()
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;
        if (Anticipado(BattleActionKind.UseItem, null)) return;

        Announce("La mochila todavía no está disponible.");
    }

    // Devuelve true si la Prediccion de Nestor anula esta accion, y ya avisa por
    // pantalla. Se llama desde CADA punto de entrada de input, no solo desde la
    // UI, porque los atajos de teclado del Update() la saltean.
    private bool Anticipado(BattleActionKind tipo, MoveDefinition movimiento)
    {
        if (_prediction == null || !_prediction.Bloquea(tipo, movimiento)) return false;
        Announce(_prediction.MensajeRechazo());
        return true;
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

        // El boss llego a PS criticos con su ultimo Poken: no muere, entra en
        // la fase de liberacion.
        if (DebeEmpezarLiberacion())
        {
            yield return LiberationRoutine();
            yield break;
        }

        _battleModel.SetPhase(BattlePhase.PlayerTurn);
        battleHudView?.UpdateHud(_battleModel);
        yield return AvanzarPrediccion();
        battleHudView?.ShowCommandMenu(_battleModel);
    }

    // ---------- fase de liberacion ----------

    // Tiene a quien mandar aparte del que esta peleando?
    private bool HayBancoDisponible()
    {
        int vivos = 0;
        foreach (PokenModel p in _playerModel.Team)
            if (!p.IsFainted) vivos++;
        return vivos > 1;
    }

    private bool EsUltimoPokenDelBoss(BattleModel m)
    {
        int vivos = 0;
        foreach (PokenModel p in m.Enemy.Team)
            if (!p.IsFainted) vivos++;
        return vivos <= 1;
    }

    private bool DebeEmpezarLiberacion()
    {
        if (_bossConfig == null || _liberationDone || _liberation != null) return false;
        if (!EsUltimoPokenDelBoss(_battleModel)) return false;

        PokenModel boss = _battleModel.ActiveEnemyPoken;
        if (boss == null || boss.MaxHP <= 0) return false;

        // Si el motor ya lo salvo de caer, la fase arranca SI O SI: no puede
        // quedar un boss inmortal esperando un umbral que quizas no se cumpla.
        if (_bossSalvadoEnUnPS) return true;

        return boss.CurrentHP / (float)boss.MaxHP <= liberationHpThreshold;
    }

    // Arranca la fase: mensajes de entrada y primer menu. De aca en mas el
    // input pasa por LiberationTurnRoutine, no por ResolveTurnRoutine.
    private IEnumerator LiberationRoutine()
    {
        _liberation = new LiberationEngine(_battleModel, _rng, _bossConfig.TurnosLiberacion);
        _battleModel.SetPhase(BattlePhase.Liberation);
        battleHudView?.HideMenus();
        battleHudView?.SetPredictionBanner(null);

        string nestor = _battleModel.ActiveEnemyPoken.Definition.pokenName;
        Announce($"¡El chip de {nestor} se sobrecarga!");
        yield return new WaitForSeconds(messageDelay);
        Announce("Si le pegás, el chip se repara. Aguantá sin atacar.");
        yield return new WaitForSeconds(messageDelay);

        AbrirMenuLiberacion();
    }

    private void AbrirMenuLiberacion()
    {
        battleHudView?.UpdateHud(_battleModel);
        battleHudView?.ShowLiberationMenu(
            _liberation.TurnosAguantados, _liberation.TurnosTotales, _liberation.AvisoDelChip());
    }

    // Un turno de la fase. Espeja a ResolveTurnRoutine para no tocar el camino
    // de input que ya funciona.
    private IEnumerator LiberationTurnRoutine(LiberationEngine.Accion accion, int indiceExtra = -1)
    {
        _battleModel.SetPhase(BattlePhase.CalculatingResult);
        battleHudView?.HideMenus();

        // Curar y cambiar los aplica el controller: el motor de la fase solo
        // sabe que fueron acciones validas.
        if (accion == LiberationEngine.Accion.Curar)
        {
            HealItemDefinition item = PrimerObjetoDeCuracion();
            if (item == null)
            {
                Announce("No te quedan objetos de curación.");
                yield return new WaitForSeconds(messageDelay);
                AbrirMenuLiberacion();
                yield break;
            }
            PokenModel objetivo = _battleModel.ActivePlayerPoken;
            _playerModel.Inventory.ConsumeHealItem(item, 1);
            objetivo.Heal(item.fullHeal ? objetivo.MaxHP : item.healAmount);
            objetivo.CureStatus();
            Announce($"Usaste {item.itemName}.");
            yield return new WaitForSeconds(messageDelay);
        }
        else if (accion == LiberationEngine.Accion.Cambiar)
        {
            if (indiceExtra >= 0) _battleModel.SwitchPlayerPoken(indiceExtra);
        }

        yield return PlayEvents(_liberation.ResolveTurn(accion));

        // El jugador se puede quedar sin equipo aguantando: eso si es derrota
        if (!_playerModel.HasAlivePoken)
        {
            yield return EndBattleRoutine();
            yield break;
        }

        if (_liberation.Terminada)
        {
            _liberationDone = true;
            GameManager.Instance?.Story?.ActivateFlag(StoryFlag.Torre1_NestorLiberado);

            // Resuelta la fase, el combate cierra como victoria normal:
            // EndBattleRoutine + EventController aplican dinero, flags y save.
            // No pasa por HandleFaints, asi que el ultimo Poken no da XP — es
            // coherente: no lo derrotaste, lo liberaste.
            _battleModel.ActiveEnemyPoken.SetCurrentHP(0);
            _battleModel.Finish(playerVictory: true);
            yield return EndBattleRoutine();
            yield break;
        }

        AbrirMenuLiberacion();
    }

    private HealItemDefinition PrimerObjetoDeCuracion()
    {
        foreach (HealItemStack stack in _playerModel.Inventory.HealItems)
            if (stack.Quantity > 0) return stack.Definition;
        return null;
    }

    // Entradas de la UI de la fase
    public void LiberationHeal()    => TomarAccionLiberacion(LiberationEngine.Accion.Curar);
    public void LiberationProtect() => TomarAccionLiberacion(LiberationEngine.Accion.Proteger);
    public void LiberationWait()    => TomarAccionLiberacion(LiberationEngine.Accion.Nada);
    public void LiberationAttack()  => TomarAccionLiberacion(LiberationEngine.Accion.Atacar);

    public void LiberationSwitch(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= _playerModel.Team.Count) return;
        if (_playerModel.Team[teamIndex].IsFainted) return;
        if (teamIndex == _battleModel.ActivePlayerIndex) return;
        TomarAccionLiberacion(LiberationEngine.Accion.Cambiar, teamIndex);
    }

    // El menu de equipo es el mismo en las dos fases, asi que el evento del HUD
    // se desvia segun donde estemos.
    private void OnSwitchChosen(int teamIndex)
    {
        if (_battleModel != null && _battleModel.CurrentPhase == BattlePhase.Liberation)
            LiberationSwitch(teamIndex);
        else
            SelectSwitch(teamIndex);
    }

    private void TomarAccionLiberacion(LiberationEngine.Accion accion, int indiceExtra = -1)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.Liberation) return;
        StartCoroutine(LiberationTurnRoutine(accion, indiceExtra));
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
