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
    [Tooltip("Camara propia del combate. El layout de la pelea esta hecho para " +
             "orthographicSize 1.92; usar la camara del mapa (que tiene otro " +
             "tamaño) deja los Pokens y el fondo desproporcionados contra el HUD.")]
    [SerializeField] private Camera battleCamera;
    [Tooltip("Secuencias visuales (transicion, balls, apariciones). Opcional: sin esto el combate arranca directo.")]
    [SerializeField] private BattleFxView battleFxView;

    [Header("Mochila")]
    [Tooltip("Usar la mochila compartida del sistema de menus (deja elegir a que " +
             "Poken se le da el objeto). Apagalo para volver a la lista simple " +
             "del HUD, que cura siempre al Poken activo.")]
    [SerializeField] private bool usarMochilaCompartida = true;

    [Header("Equipo")]
    [Tooltip("Usar la pantalla de equipo compartida para elegir relevo y para el " +
             "reemplazo obligado cuando cae un Poken. Apagalo para volver al " +
             "panel simple del HUD.")]
    [SerializeField] private bool usarPartyCompartida = true;

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
    // Definicion del boss en curso: de aca sale a QUIEN se libera
    private BossDefinition _bossDefinition;

    // La LECTURA ALIADA de Nestor. Solo existe en el nivel 2 (el casino), donde
    // Nestor ya esta de nuestro lado. Null en todo el resto, incluido el primer
    // Data Center entero — ver HayLecturaAliada().
    private NestorPredictionSystem _nestorAlly;
    // La pelea en curso pidio a Nestor de ayudante (los NPCs del casino lo
    // piden en todas las suyas). Se vuelve a decidir en cada Start*.
    private bool _nestorPedidoPorLaPelea;

    // La ruleta que aparece cada dos turnos en la pelea del jefe del ultimo
    // piso del casino. Null en todos los demas combates. La pasa el NPC que
    // lanza la pelea: el combate no sabe nada del casino mas alla de esto.
    private CasinoRuletaDeCombate _ruletaDeCombate;
    // Lo que dejo el ultimo giro, para reproducirlo al cerrarse la ruleta.
    private List<BattleEvent> _eventosDeLaRuleta;
    private bool _laRuletaDebeOtroGiro;

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

    [Header("Lectura aliada de Nestor (nivel 2)")]
    [Tooltip("Cuando esta disponible la ayuda de Nestor. Auto = con el flag " +
             "Torre1_NestorLiberado puesto o si la pelea lo pide (los NPCs del " +
             "casino), y nunca en la pelea contra el propio Nestor. SiempreOn es " +
             "para probar sin caminar la torre.")]
    [SerializeField] private NestorAllyMode nestorAliado = NestorAllyMode.Auto;
    [Tooltip("Chance de que Nestor lea la jugada, cumplidos el cooldown y la gracia.")]
    [SerializeField, Range(0f, 1f)] private float nestorProbabilidad = 0.20f;
    [Tooltip("Turnos minimos entre dos lecturas.")]
    [SerializeField, Min(0)] private int nestorCooldownTurnos = 4;
    [Tooltip("Turnos del arranque en los que Nestor solo mira.")]
    [SerializeField, Min(0)] private int nestorTurnosDeGracia = 2;
    [Tooltip("Tope de lecturas por combate. Es el freno mas importante.")]
    [SerializeField, Min(1)] private int nestorMaximoPorCombate = 2;
    [Tooltip("Cuanto tiene que pegar un ataque comun, en fraccion de tus PS " +
             "maximos, para que el rival pierda el turno entero en vez de caer " +
             "a su segunda opcion.")]
    [SerializeField, Range(0.1f, 1f)] private float nestorFraccionDeGolpeFuerte = 0.45f;

    // El reemplazo que se esta pidiendo viene de la fase de liberacion: al
    // elegir hay que volver a esa fase y no al menu de comandos normal.
    private bool _reemplazoDesdeLiberacion;

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
            if (usarPartyCompartida) battleHudView.TeamRequested += OnTeamRequested;
            battleHudView.ItemChosen += OnItemChosen;

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
    //
    // 'conNestorAliado' pide la lectura aliada de Nestor para ESTA pelea aunque
    // falte el flag. La usan los NPCs del casino (ver HayLecturaAliada).
    // 'ruletaDeCombate' es la ruleta que aparece cada dos turnos en la pelea
    // del jefe del casino. Null (lo normal) = no aparece ninguna.
    public void StartBattle(PlayerModel player, NPCDefinition enemyDefinition,
        BossBattleConfig config = null, bool conNestorAliado = false,
        CasinoRuletaDeCombate ruletaDeCombate = null)
    {
        _nestorPedidoPorLaPelea = conNestorAliado;
        _ruletaDeCombate = ruletaDeCombate != null && ruletaDeCombate.Configurada
            ? ruletaDeCombate
            : null;
        _bossConfig = enemyDefinition is BossDefinition
            ? (config ?? BossBattleConfig.DesdeTorre(GameManager.Instance?.Tower))
            : null;

        // Cortar los CUATRO subsistemas cambia con QUE pelea el boss, no solo
        // como. Se resuelve antes de armar el EnemyModel porque implica otro
        // equipo entero (en la torre 1: Nestor sin Melmetal).
        //
        // Todo o nada: dos de tres nodos achican el escudo pero no alcanzan
        // para esto, que es lo que obliga a terminar lo que se empieza.
        if (_bossConfig != null && _bossConfig.SinMelmetal
            && enemyDefinition is BossDefinition original
            && original.varianteTodoCortado != null)
        {
            enemyDefinition = original.varianteTodoCortado;
            Debug.Log($"[Combate] Subsistemas todos cortados: pelea la variante " +
                      $"'{enemyDefinition.name}' en vez de '{original.name}'.");
        }

        var enemy = new EnemyModel(enemyDefinition);
        _bossDefinition = enemyDefinition as BossDefinition;

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
        _bossDefinition = null;

        // Un salvaje no es un boss. Sin esto se arrastraba la config del
        // combate ANTERIOR y el salvaje heredaba EnemyCannotFaint: no podia
        // morir y el combate no terminaba nunca.
        _bossConfig = null;
        _nestorPedidoPorLaPelea = false;
        _ruletaDeCombate = null;
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
            ? new PredictionState(_bossConfig.PrediccionConAviso,
                                  _bossConfig.TurnosEntrePredicciones)
            : null;

        // La Prediccion necesita saber que salidas le quedan al jugador para no
        // anularle la ultima. El rastreo activo ya le cierra el cambio.
        if (_prediction != null)
        {
            _prediction.PuedeCambiar = () =>
                _bossConfig.PermiteCambio && HayBancoDisponible();
            _prediction.TieneObjetos = () =>
                _playerModel.Inventory.HealItems.Exists(s => s.Quantity > 0);
            // Los movimientos CON PP son dos salidas distintas, no una: la
            // lectura de Ataque anula solo los de dano y la de Estado solo los
            // de estado, asi que siempre le queda la otra mitad del menu. Sin
            // separarlas, el que llega con un solo Poken y la mochila vacia
            // tenia una unica categoria anulable y Nestor leia siempre igual.
            _prediction.TieneMovimientoDeEstado = () => TieneMovimientoConPP(false);
            _prediction.TieneMovimientoDeDano = () => TieneMovimientoConPP(true);
        }
        if (_bossConfig != null)
            Debug.Log($"[Combate] Config del boss: {_bossConfig.Resumen()}");

        // Nivel 2: aca Nestor ya no es el rival, esta de nuestro lado. Lee la
        // jugada que la IA rival ya eligio y a veces se la corta. Es el ESPEJO
        // de _prediction: aquella anula categorias del jugador ANTES de que
        // elija, esta corrige la accion del rival DESPUES de que la IA la
        // eligio. Ninguna de las dos toca el TurnEngine.
        _nestorAlly = HayLecturaAliada()
            ? new NestorPredictionSystem
            {
                Probabilidad = nestorProbabilidad,
                CooldownTurnos = nestorCooldownTurnos,
                TurnosDeGracia = nestorTurnosDeGracia,
                MaximoPorCombate = nestorMaximoPorCombate,
                FraccionDeGolpeFuerte = nestorFraccionDeGolpeFuerte
            }
            : null;
        if (_nestorAlly != null)
            Debug.Log($"[Combate] Nestor aliado activo: {nestorProbabilidad:P0} de chance, " +
                      $"cooldown {nestorCooldownTurnos}t, {nestorMaximoPorCombate} por combate.");

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
            _battleModel.EnemySide.ShieldMaxHp = _bossConfig.EscudoInicial;
        }

        if (playerInputController != null)
            playerInputController.SetBattleMode(true);

        // Los menus compartidos (mochila, party) preguntan por este contexto
        // para saber que se puede hacer y a quien devolverle el control.
        if (MenuManager.Instance != null)
            MenuManager.Instance.SetContext(MenuContextKind.Battle);

        // Avisa antes de la intro para que el mapa se esconda a tiempo
        BattleStarted?.Invoke();

        // Red de seguridad: el prefab de combate se guarda DESACTIVADO para no
        // tapar el mapa, y normalmente lo prende MapBattleBridge al escuchar
        // BattleStarted. Si no hay puente —o quedo sin cablear— el objeto sigue
        // inactivo y el StartCoroutine de abajo tira
        // "Coroutine couldn't be started because the game object is inactive".
        // Encenderse solo evita que el combate dependa de que alguien lo prenda.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.LogWarning("[Combate] El sistema de combate estaba desactivado y se encendio " +
                             "solo. Si tenes un mapa, cablea el MapBattleBridge para que ademas " +
                             "lo esconda; si no, el mapa va a quedar visible detras.");
        }

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
        _reemplazoDesdeLiberacion = false;

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
        // Nestor aliado vuelve a su cara de ambiente al empezar el turno. Va
        // antes del early return de abajo: el retrato del nivel 2 no depende
        // de que exista la prediccion del boss (nunca coexisten).
        RefrescarRetratoDeNestor();

        if (_prediction == null)
        {
            // Combate normal: la UI de prediccion se apaga sola
            battleHudView?.SetPrediction(default);
            yield break;
        }

        string mensaje = _prediction.Avanzar(_battleModel, _rng);
        battleHudView?.SetPrediction(_prediction.Snapshot());

        if (string.IsNullOrEmpty(mensaje)) yield break;

        Announce(mensaje);
        yield return new WaitForSeconds(messageDelay);
    }

    // ---------- input del jugador ----------

    // TEMPORAL: entrada de prueba con teclado hasta la UI de batalla.
    // 1-4 = movimientos, F1-F6 = cambio manual de Poken.
    private void Update()
    {
        if (_battleModel == null) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // F1-F6 tambien sirven para el cambio OBLIGADO, por eso pasan por
        // OnSwitchChosen (el mismo router que usa la UI) y no por SelectSwitch.
        if (kb.f1Key.wasPressedThisFrame) { OnSwitchChosen(0); return; }
        if (kb.f2Key.wasPressedThisFrame) { OnSwitchChosen(1); return; }
        if (kb.f3Key.wasPressedThisFrame) { OnSwitchChosen(2); return; }
        if (kb.f4Key.wasPressedThisFrame) { OnSwitchChosen(3); return; }
        if (kb.f5Key.wasPressedThisFrame) { OnSwitchChosen(4); return; }
        if (kb.f6Key.wasPressedThisFrame) { OnSwitchChosen(5); return; }

        if (_battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        if (kb.digit1Key.wasPressedThisFrame) SelectMove(0);
        else if (kb.digit2Key.wasPressedThisFrame) SelectMove(1);
        else if (kb.digit3Key.wasPressedThisFrame) SelectMove(2);
        else if (kb.digit4Key.wasPressedThisFrame) SelectMove(3);
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
        // Un Poken debilitado no puede volver a entrar al combate
        if (!BattleTeamRules.EsIndiceVivo(_playerModel.Team, teamIndex))
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

    // ---------- mochila ----------

    // Objetos que se pueden usar en combate, con stock. Se recalcula cada vez
    // que se abre la mochila para que los indices que devuelve el HUD siempre
    // apunten a esta misma lista.
    private List<HealItemStack> ObjetosUsables()
    {
        var usables = new List<HealItemStack>();
        if (_playerModel?.Inventory?.HealItems == null) return usables;

        foreach (HealItemStack stack in _playerModel.Inventory.HealItems)
            if (stack != null && stack.Definition != null && stack.Quantity > 0)
                usables.Add(stack);
        return usables;
    }

    // Abrir la mochila. Funciona en el turno normal y en la fase de liberacion
    // (ahi el boton CURAR entra por aca).
    private void OnBagSelected()
    {
        if (_battleModel == null) return;

        bool enLiberacion = _battleModel.CurrentPhase == BattlePhase.Liberation;
        if (!enLiberacion && _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        // La Prediccion se chequea al ABRIR: no tiene sentido dejar elegir un
        // objeto para rechazarlo despues.
        if (!enLiberacion && Anticipado(BattleActionKind.UseItem, null)) return;

        List<HealItemStack> usables = ObjetosUsables();
        if (usables.Count == 0)
        {
            Announce("No te quedan objetos.");
            return;
        }

        // Turno normal: se abre la MOCHILA COMPARTIDA (la misma del mundo), que
        // ademas deja elegir sobre que Poken se usa el objeto. En la fase de
        // liberacion se sigue usando la lista del HUD: ahi la curacion va si o
        // si al Poken que esta en el campo, asi que un panel de objetivo seria
        // mentira.
        if (!enLiberacion && usarMochilaCompartida && MenuManager.Instance != null)
        {
            AbrirMochilaCompartida();
            return;
        }

        if (battleHudView != null) battleHudView.ShowBagMenu(usables);
        else OnItemChosen(0); // sin HUD (modo consola): usa el primero
    }

    // Abre la mochila del sistema de menus y espera su resultado. El combate no
    // consume el turno hasta que la mochila diga que se eligio objeto Y objetivo.
    private void AbrirMochilaCompartida()
    {
        MenuManager menus = MenuManager.Instance;
        menus.SetContext(MenuContextKind.Battle);

        // El HUD se saca del medio mientras la mochila esta abierta: dos menus
        // encima del combate se tapan y confunden.
        battleHudView?.HideMenus();

        menus.Open(MenuScreenId.Bag, new BagScreenRequest
        {
            Context = MenuContextKind.Battle,
            Pocket = ItemPocket.Medicine
        }, raw =>
        {
            var resultado = raw as BagScreenResult;

            // Cancelo: se vuelve al menu de comandos sin gastar el turno.
            if (resultado == null || resultado.Cancelled || resultado.UsedHealItem == null)
            {
                if (_battleModel != null) battleHudView?.ShowCommandMenu(_battleModel);
                return;
            }

            if (_battleModel == null) return;
            if (_battleModel.CurrentPhase != BattlePhase.PlayerTurn)
            {
                battleHudView?.ShowCommandMenu(_battleModel);
                return;
            }

            if (Anticipado(BattleActionKind.UseItem, null)) return;

            StartCoroutine(ResolveTurnRoutine(
                BattleAction.UseItemOn(resultado.UsedHealItem, resultado.TargetIndex)));
        });
    }

    // El jugador eligio un objeto de la lista que se le mostro.
    private void OnItemChosen(int index)
    {
        if (_battleModel == null) return;

        List<HealItemStack> usables = ObjetosUsables();
        if (index < 0 || index >= usables.Count) return;
        HealItemDefinition item = usables[index].Definition;

        if (_battleModel.CurrentPhase == BattlePhase.Liberation)
        {
            TomarAccionLiberacion(LiberationEngine.Accion.Curar, -1, item);
            return;
        }

        if (_battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;
        if (Anticipado(BattleActionKind.UseItem, null)) return;

        StartCoroutine(ResolveTurnRoutine(BattleAction.UseItem(item)));
    }

    // Devuelve true si la Prediccion de Nestor anula esta accion, y ya avisa por
    // pantalla. Se llama desde CADA punto de entrada de input, no solo desde la
    // UI, porque los atajos de teclado del Update() la saltean.
    private bool Anticipado(BattleActionKind tipo, MoveDefinition movimiento)
    {
        if (_prediction == null || !_prediction.Bloquea(tipo, movimiento)) return false;

        // Cerrar el panel de lucha ANTES de avisar: tapa la barra de mensajes,
        // asi que el rechazo quedaba escrito abajo, donde no se lee.
        battleHudView?.ShowCommandMenu(_battleModel);
        Announce(_prediction.MensajeRechazo());
        return true;
    }

    // ---------- lectura aliada de Nestor (nivel 2) ----------

    // Esta disponible la ayuda de Nestor en ESTA pelea?
    //
    // Lo importante de este metodo es lo que NO deja pasar. El primer Data
    // Center queda afuera por dos candados independientes, a proposito: si
    // alguno se rompiera, el otro sigue tapando el agujero.
    //
    //   1. _bossConfig != null significa que el rival de esta pelea es el boss
    //      de la torre, o sea Nestor. Nestor no se puede leer a si mismo, y
    //      ademas ahi ya corre _prediction (la lectura CONTRA el jugador).
    //      Este candado vale incluso con el modo forzado en SiempreOn.
    //   2. El flag Torre1_NestorLiberado se enciende recien cuando termina la
    //      fase de liberacion, o sea al final de esa pelea. Todo lo anterior
    //      del primer nivel pasa con el flag apagado.
    //
    // La pelea que PIDE a Nestor (conNestorAliado) saltea el candado 2 pero no
    // el 1. Solo la piden los NPCs del casino, que no existen en el primer
    // nivel: asi el casino lo tiene siempre, tambien entrando directo a un piso
    // con una partida de prueba que no camino la torre.
    private bool HayLecturaAliada()
    {
        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        return LecturaAliadaDisponible(nestorAliado, _bossConfig != null, _nestorPedidoPorLaPelea,
            story != null && story.HasFlag(StoryFlag.Torre1_NestorLiberado));
    }

    // La decision de HayLecturaAliada sin escena ni GameManager, para probarla sola.
    public static bool LecturaAliadaDisponible(NestorAllyMode modo, bool contraElBoss,
        bool pedidoPorLaPelea, bool nestorLiberado)
    {
        if (modo == NestorAllyMode.SiempreOff) return false;

        // Candado 1: nunca en la pelea contra el propio Nestor.
        if (contraElBoss) return false;

        if (modo == NestorAllyMode.SiempreOn || pedidoPorLaPelea) return true;

        // Candado 2: recien despues de liberarlo.
        return nestorLiberado;
    }

    // Los tres tiempos de una lectura. Corre ANTES de resolver el turno, asi
    // que cuando termina, lo que se ve en pantalla ya es el turno con la
    // jugada rival cambiada.
    private IEnumerator LecturaDeNestorRoutine(NestorIntervention lectura)
    {
        // 1) La ve venir: cara de concentracion y lo que dice.
        battleHudView?.SetNestorPortrait(_nestorAlly.Snapshot());
        if (!string.IsNullOrEmpty(lectura.DialogoDeteccion))
        {
            Announce(lectura.DialogoDeteccion);
            yield return new WaitForSeconds(messageDelay);
        }

        // 2) El feedback del bloqueo, con el FX que YA existe: el rival
        //    parpadea en frio. No hace falta una animacion nueva para que se
        //    entienda que algo le pasa al rival y no al jugador.
        if (battleFxView != null && enemyPokenView != null
            && enemyPokenView.gameObject.activeSelf)
            yield return battleFxView.PulseTint(enemyPokenView, new Color(0.45f, 0.75f, 1f), 2);

        // 3) Que paso, en la barra de mensajes, y Nestor festejando.
        Announce(lectura.MensajeBloqueo);
        _nestorAlly.MarcarExito(lectura.DialogoExito);
        battleHudView?.SetNestorPortrait(_nestorAlly.Snapshot());
        yield return new WaitForSeconds(messageDelay);
    }

    // Cara de ambiente. Se llama al empezar cada turno del jugador: la cara de
    // la lectura ya se mostro durante la resolucion y no tiene que quedarse
    // congelada el resto de la pelea.
    private void RefrescarRetratoDeNestor()
    {
        if (_nestorAlly == null)
        {
            // Combate sin Nestor: el retrato se apaga solo.
            battleHudView?.SetNestorPortrait(default);
            return;
        }

        _nestorAlly.Ambiente(AiContext.ForEnemy(_battleModel, _aiProfile));
        battleHudView?.SetNestorPortrait(_nestorAlly.Snapshot());
    }

    // ---------- resolucion y reproduccion del turno ----------

    private IEnumerator ResolveTurnRoutine(BattleAction playerAction)
    {
        _battleModel.SetPhase(BattlePhase.CalculatingResult);
        battleHudView?.HideMenus();

        // El contexto se arma UNA vez y lo comparten la IA y Nestor: si cada
        // uno armara el suyo, Nestor podria estar leyendo una foto distinta de
        // la que uso la IA para decidir.
        AiContext contextoRival = AiContext.ForEnemy(_battleModel, _aiProfile);
        // Si el rival esta cargando (Rayo Solar, Vuelo) o tiene que descansar
        // (Hiperrayo), no elige: ni se le pregunta a la IA.
        BattleAction enemyAction = TurnEngine.AccionForzada(_battleModel.EnemyState)
                                   ?? _enemyAI.ChooseAction(contextoRival, _rng);

        // La lectura de Nestor va JUSTO ACA: la IA ya eligio, el motor todavia
        // no arranco. Es la unica ventana en la que se puede anular una accion
        // rival sin que haya empezado a ejecutarse, que es lo que se pidio.
        if (_nestorAlly != null)
        {
            NestorIntervention lectura = _nestorAlly.Evaluar(contextoRival, enemyAction, _rng);
            if (lectura.Intervino)
            {
                yield return LecturaDeNestorRoutine(lectura);
                enemyAction = lectura.AccionFinal;
            }
            else
            {
                // Turno normal: Nestor sigue mirando, solo cambia la cara.
                battleHudView?.SetNestorPortrait(_nestorAlly.Snapshot());
            }
        }

        List<BattleEvent> events = _engine.ResolveTurn(playerAction, enemyAction);
        yield return PlayEvents(events);

        yield return ContinuarDespuesDelTurno();
    }

    // Que pasa cuando termina de reproducirse un turno: fin del combate, pedir
    // reemplazo, arrancar la liberacion, o abrir el menu del turno siguiente.
    // Un solo lugar para que los tres caminos (turno normal, reemplazo, entrada
    // a la liberacion) no se desincronicen.
    private IEnumerator ContinuarDespuesDelTurno()
    {
        if (_battleModel.IsBattleOver)
        {
            yield return EndBattleRoutine();
            yield break;
        }

        // El Poken del jugador cayo: el combate se DETIENE hasta que elija.
        // Va antes que todo lo demas, porque mientras tanto no hay activo valido.
        if (_battleModel.AwaitingPlayerReplacement)
        {
            // Se conserva de donde vino: si el reemplazo anterior salio de la
            // liberacion y el que entro tambien cayo, seguimos en esa fase.
            PedirReemplazo(_reemplazoDesdeLiberacion);
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

        // La ruleta del jefe del casino: cada dos turnos, ANTES de que el
        // jugador elija. Va aca y no despues por la misma razon que la
        // Prediccion: lo que sale cambia que conviene hacer este turno, asi que
        // tiene que verse antes de abrir el menu de comandos.
        if (_ruletaDeCombate != null && _ruletaDeCombate.TocaGirar(_battleModel.CurrentTurn))
        {
            yield return RuletaDeCombateRoutine();
            if (_battleModel == null) yield break;
        }

        // Cargando un movimiento o descansando despues de Hiperrayo: este turno
        // no hay nada que elegir y se juega solo.
        BattleAction forzada = TurnEngine.AccionForzada(_battleModel.PlayerState);
        if (forzada != null)
        {
            yield return ResolveTurnRoutine(forzada);
            yield break;
        }

        battleHudView?.ShowCommandMenu(_battleModel);
    }

    // ---------- ruleta del jefe del casino ----------

    // Abre la ruleta de premios encima del combate y aplica lo que salga sobre
    // el Poken que el jugador tiene en el campo.
    //
    // Es la MISMA pantalla que las ruletas del mapa: se reusa entera (rueda,
    // puntero, giro, cartel) y lo unico que cambia es quien cobra el premio,
    // que se pasa por PrizeWheelRequest.Entregar. El efecto se aplica adentro
    // de ese callback -igual que un premio de mapa se mete en la mochila ahi
    // mismo- y los eventos que devuelve se reproducen despues, con el ritmo de
    // siempre, para que la barra de PS y los mensajes se lean como los de un
    // turno normal.
    private IEnumerator RuletaDeCombateRoutine()
    {
        MenuManager menus = MenuManager.HasInstance ? MenuManager.Instance : null;
        if (menus == null)
        {
            Debug.LogError("[Combate] No hay MenuManager: la ruleta del jefe no se puede abrir.");
            yield break;
        }

        for (int giro = 0; giro < CasinoRuletaDeCombate.MaximoDeGirosEncadenados; giro++)
        {
            _eventosDeLaRuleta = null;
            _laRuletaDebeOtroGiro = false;
            bool cerrada = false;

            // El HUD se saca del medio: la ruleta no oscurece el fondo (asi se
            // sigue viendo la pelea detras), y con el menu de comandos prendido
            // los dos se pisan.
            menus.SetContext(MenuContextKind.Battle);
            battleHudView?.HideMenus();

            MenuScreen abierta = menus.Open(MenuScreenId.PrizeWheel,
                new PrizeWheelRequest
                {
                    Prizes = _ruletaDeCombate.Premios,
                    RecompensaUnica = true,
                    Entregar = CobrarGajoDeLaRuleta
                },
                _ => cerrada = true);

            // Sin prefab registrado, Open devuelve null y NO llama al callback:
            // esperar ahi dejaria el combate colgado sin menu y sin salida. Se
            // apaga la ruleta para el resto de la pelea y se sigue jugando.
            if (abierta == null)
            {
                Debug.LogError("[Combate] No esta registrada la pantalla de la ruleta (MenuScreenId.PrizeWheel): " +
                               "la pelea sigue sin ella. Corre PokeRush/Casino/Armar la ruleta de premios.");
                _ruletaDeCombate = null;
                yield break;
            }

            // La segunda condicion es la red de seguridad: si el combate se
            // desarma mientras la ruleta esta abierta, la corrutina no se queda
            // esperando un cierre que ya no va a llegar.
            yield return new WaitUntil(() => cerrada || _battleModel == null);

            if (_battleModel == null) yield break;

            if (_eventosDeLaRuleta != null && _eventosDeLaRuleta.Count > 0)
                yield return PlayEvents(_eventosDeLaRuleta);

            if (_battleModel == null) yield break;
            battleHudView?.UpdateHud(_battleModel);

            if (!_laRuletaDebeOtroGiro) yield break;
        }

        // Se acabaron los giros encadenados. El tope existe para que un
        // TirarDeNuevo detras de otro no deje la ruleta girando sola.
        Announce("La casa cierra la ruleta por hoy.");
        yield return new WaitForSeconds(messageDelay);
    }

    // Que hace el gajo que salio. Corre adentro de la pantalla de la ruleta,
    // justo cuando frena: si el juego se corta con el cartel abierto, el efecto
    // ya esta aplicado (mismo criterio que los premios de mapa).
    private string CobrarGajoDeLaRuleta(Prize premio)
    {
        if (premio == null) return "La ruleta se detiene en un gajo vacio.";

        _eventosDeLaRuleta = CasinoRuletaDeCombate.Aplicar(premio.efectoDeCombate, _battleModel, _rng);
        _laRuletaDebeOtroGiro = premio.efectoDeCombate == EfectoDeRuleta.TirarDeNuevo;
        return premio.BuildMessage();
    }

    // ---------- revancha (jefe del casino) ----------

    // Vuelve a empezar la pelea que acaba de terminar, con los equipos COMO
    // QUEDARON: el rival es el mismo EnemyModel, no uno nuevo.
    //
    // Es lo que hace falta para la mano de blackjack del ultimo piso: al que
    // perdio se le revive un Pokemon y la pelea sigue. Rearmar el rival desde
    // su definicion lo traeria con los seis Pokemon enteros y la mano no
    // significaria nada.
    //
    // Devuelve false si no hay con que seguir (algun lado sin nadie en pie),
    // asi quien la llama puede decir algo en vez de quedarse esperando un
    // BattleEnded que no va a llegar.
    public bool Revancha(PlayerModel player)
    {
        if (_enemyModel == null)
        {
            Debug.LogError("[Combate] No hay una pelea anterior de la que hacer revancha.");
            return false;
        }

        if (player == null || !player.HasAlivePoken || !_enemyModel.HasAlivePoken) return false;

        BeginBattle(player, _enemyModel);
        return _battleModel != null;
    }

    // ---------- cambio de Poken al caer el activo ----------

    // Abre el selector de equipo en modo obligado. La barra y el sprite del
    // caido quedan como estan: el HUD del que entra se refresca recien cuando
    // el jugador elige (el evento PokenSwitched llega despues de la eleccion).
    private void PedirReemplazo(bool desdeLiberacion)
    {
        _reemplazoDesdeLiberacion = desdeLiberacion;
        _battleModel.SetPhase(BattlePhase.PlayerReplacement);
        battleHudView?.HideMenus();
        AbrirEquipo(forzado: true);
    }

    // ---------- equipo ----------

    // El jugador toco POKEMON en el menu de comandos (o CAMBIAR en liberacion).
    private void OnTeamRequested()
    {
        if (_battleModel == null) return;

        bool enLiberacion = _battleModel.CurrentPhase == BattlePhase.Liberation;
        if (!enLiberacion && _battleModel.CurrentPhase != BattlePhase.PlayerTurn) return;

        // El rastreo de Nestor bloquea los cambios: se avisa ANTES de abrir la
        // pantalla, para no hacer elegir un Poken y rechazarlo despues.
        if (!enLiberacion && _bossConfig != null && !_bossConfig.PermiteCambio)
        {
            Announce("El rastreo tiene marcado a tu equipo: no podés cambiar.");
            return;
        }

        AbrirEquipo(forzado: false);
    }

    // Abre la pantalla de equipo COMPARTIDA (la misma del mundo) para elegir
    // quien sale a pelear.
    //
    // 'forzado' es el reemplazo obligado: el Poken activo se debilito y el
    // combate esta detenido, asi que la pantalla no se puede cancelar.
    private void AbrirEquipo(bool forzado)
    {
        MenuManager menus = usarPartyCompartida ? MenuManager.Instance : null;
        if (menus == null)
        {
            battleHudView?.ShowTeamMenu(_battleModel, forzado);
            return;
        }

        menus.SetContext(MenuContextKind.Battle);
        battleHudView?.HideMenus();

        // El que ya esta en el campo no se puede volver a sacar. Los debilitados
        // los filtra sola la pantalla.
        var bloqueados = new HashSet<int> { _battleModel.ActivePlayerIndex };

        menus.Open(MenuScreenId.Party, new PartyScreenRequest
        {
            Mode = PartyScreenMode.SelectSwitch,
            Context = MenuContextKind.Battle,
            CanCancel = !forzado,
            BlockedIndices = bloqueados,
            Prompt = forzado ? "¿A quién mandás ahora?" : null
        }, raw =>
        {
            var resultado = raw as PartyScreenResult;

            // Cancelo: vuelve el menu de comandos sin gastar el turno. En el
            // reemplazo obligado esto no pasa (la pantalla no deja cancelar).
            if (resultado == null || resultado.Cancelled || resultado.SelectedIndex < 0)
            {
                if (_battleModel == null) return;
                if (_battleModel.CurrentPhase == BattlePhase.Liberation) AbrirMenuLiberacion();
                else battleHudView?.ShowCommandMenu(_battleModel);
                return;
            }

            OnSwitchChosen(resultado.SelectedIndex);
        });
    }

    // El jugador ya eligio con quien sigue.
    private IEnumerator ReemplazoRoutine(int teamIndex)
    {
        // Eleccion invalida: el menu sigue abierto, no se consume nada.
        if (!BattleTeamRules.EsIndiceVivo(_playerModel.Team, teamIndex))
        {
            Announce("Ese Poken no puede combatir.");
            yield break;
        }

        _battleModel.SetPhase(BattlePhase.CalculatingResult);
        battleHudView?.HideMenus();

        yield return PlayEvents(_engine.CompletePlayerReplacement(teamIndex));

        // Volver a la fase de liberacion si el reemplazo salio de ahi
        if (_reemplazoDesdeLiberacion && !_battleModel.IsBattleOver
            && !_battleModel.AwaitingPlayerReplacement)
        {
            _reemplazoDesdeLiberacion = false;
            _battleModel.SetPhase(BattlePhase.Liberation);
            AbrirMenuLiberacion();
            yield break;
        }

        yield return ContinuarDespuesDelTurno();
    }

    // Le queda al Poken activo algun movimiento con PP de ese tipo? Clasifica
    // con PredictionState para que la SALIDA que se le informa a la Prediccion
    // y el bloqueo que despues aplica usen exactamente el mismo criterio.
    private bool TieneMovimientoConPP(bool deDano)
    {
        PokenModel activo = _battleModel != null ? _battleModel.ActivePlayerPoken : null;
        if (activo == null) return false;
        return activo.Moves.Exists(m => m.HasPP && (deDano
            ? PredictionState.EsDeDano(m.Definition)
            : PredictionState.EsDeEstado(m.Definition)));
    }

    // ---------- fase de liberacion ----------

    // Tiene a quien mandar aparte del que esta peleando?
    private bool HayBancoDisponible()
        => _battleModel != null
           && BattleTeamRules.HayBanco(_playerModel.Team, _battleModel.ActivePlayerIndex);

    // El Poken que esta peleando es el ULTIMO del boss?
    //
    // Se pregunta por el banco (todos menos el activo) y no por el total de
    // vivos. Contar el total daba true tambien con el ANTEULTIMO: ese se
    // salvaba de caer en 1 PS turno tras turno —bucle contra un Poken muerto—
    // y la fase de liberacion nunca arrancaba, porque para ella el conteo
    // incluia al que acababa de "revivir".
    private static bool EsUltimoPokenDelBoss(BattleModel m)
        => BattleTeamRules.EsElUltimo(m.Enemy.Team, m.ActiveEnemyIndex);

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
        // El chip es de NESTOR, no del ultimo Poken de su equipo. Sin esto la
        // fase decia "Porygon-Z se apago... lo liberaste", que es justo lo
        // contrario de lo que cuenta el nivel: al que recuperas es a Nestor.
        yield return EntraElQueSeLibera();

        _liberation = new LiberationEngine(_battleModel, _rng, _bossConfig.TurnosLiberacion);
        _battleModel.SetPhase(BattlePhase.Liberation);
        battleHudView?.HideMenus();
        battleHudView?.SetPrediction(default); // en la liberacion no hay lectura

        string nestor = _battleModel.ActiveEnemyPoken.Definition.pokenName;
        Announce($"¡El chip de {nestor} se sobrecarga!");
        yield return new WaitForSeconds(messageDelay);
        Announce("Si le pegás, el chip se repara. Aguantá sin atacar.");
        yield return new WaitForSeconds(messageDelay);

        AbrirMenuLiberacion();
    }

    // Reemplaza al Poken rival en el campo por el que de verdad se libera.
    // Entra con PS al minimo: viene de la pelea, no es un rival nuevo.
    private IEnumerator EntraElQueSeLibera()
    {
        PokenDefinition quien = _bossDefinition != null ? _bossDefinition.pokenDeLiberacion : null;
        if (quien == null) yield break;   // sin asignar: se libera al que este

        int nivel = _bossDefinition.nivelDeLiberacion > 0 ? _bossDefinition.nivelDeLiberacion : 50;
        var liberado = new PokenModel(quien, nivel);
        liberado.SetCurrentHP(Mathf.Max(1, Mathf.RoundToInt(liberado.MaxHP * liberationHpThreshold)));

        // Ocupa el lugar del Poken activo del rival
        _enemyModel.Team[_battleModel.ActiveEnemyIndex] = liberado;
        _battleModel.SwitchEnemyPoken(_battleModel.ActiveEnemyIndex);

        RefreshActiveSprites();
        battleHudView?.UpdateHud(_battleModel);

        // El view pudo haber quedado escondido por una animacion de caida
        // previa; sin esto Nestor entra pero no se ve.
        enemyPokenView?.Show();
        _enemyFaintedPendingSwitch = false;

        Announce($"¡{quien.pokenName} se adelanta!");
        yield return new WaitForSeconds(messageDelay);

        if (battleFxView != null && enemyPokenView != null)
            yield return battleFxView.PulseTint(enemyPokenView, BattleFxView.PulseStatUp);
    }

    private void AbrirMenuLiberacion()
    {
        // Devolver la fase a Liberation es OBLIGATORIO: LiberationTurnRoutine la
        // pone en CalculatingResult mientras resuelve, y TomarAccionLiberacion
        // exige Liberation para aceptar input. Sin esto, del segundo turno en
        // adelante el menu se dibujaba pero ningun boton hacia nada.
        _battleModel.SetPhase(BattlePhase.Liberation);
        battleHudView?.UpdateHud(_battleModel);
        battleHudView?.ShowLiberationMenu(
            _liberation.TurnosAguantados, _liberation.TurnosTotales, _liberation.AvisoDelChip());
    }

    // Un turno de la fase. Espeja a ResolveTurnRoutine para no tocar el camino
    // de input que ya funciona.
    private IEnumerator LiberationTurnRoutine(LiberationEngine.Accion accion,
        int indiceExtra = -1, HealItemDefinition objetoElegido = null)
    {
        _battleModel.SetPhase(BattlePhase.CalculatingResult);
        battleHudView?.HideMenus();

        // Curar y cambiar los aplica el controller: el motor de la fase solo
        // sabe que fueron acciones validas.
        if (accion == LiberationEngine.Accion.Curar)
        {
            HealItemDefinition item = objetoElegido ?? PrimerObjetoDeCuracion();
            if (item == null || !_playerModel.Inventory.ConsumeHealItem(item, 1))
            {
                Announce("No te quedan objetos de curación.");
                yield return new WaitForSeconds(messageDelay);
                AbrirMenuLiberacion();
                yield break;
            }
            PokenModel objetivo = _battleModel.ActivePlayerPoken;
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
        if (!BattleTeamRules.TieneAlgunoVivo(_playerModel.Team))
        {
            _battleModel.Finish(playerVictory: false);
            yield return EndBattleRoutine();
            yield break;
        }

        // El chip debilito al activo pero al jugador le queda banco: elige el
        // reemplazo y vuelve a esta misma fase. Sin esto la fase seguiria
        // corriendo con un Poken caido en el campo.
        if (_battleModel.ActivePlayerPoken.IsFainted)
        {
            _battleModel.SetAwaitingPlayerReplacement(true);
            PedirReemplazo(desdeLiberacion: true);
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

    // Entradas de la UI de la fase. CURAR abre la mochila en vez de gastar el
    // primer objeto que aparezca: es la misma mochila del combate normal.
    public void LiberationHeal()    => OnBagSelected();
    public void LiberationProtect() => TomarAccionLiberacion(LiberationEngine.Accion.Proteger);
    public void LiberationWait()    => TomarAccionLiberacion(LiberationEngine.Accion.Nada);
    public void LiberationAttack()  => TomarAccionLiberacion(LiberationEngine.Accion.Atacar);

    public void LiberationSwitch(int teamIndex)
    {
        // Una eleccion invalida NO puede quedarse callada: el menu de equipo ya
        // se cerro, asi que sin aviso ni vuelta al panel de liberacion la fase
        // queda sin ningun boton en pantalla y el combate parece trabado.
        if (teamIndex == _battleModel.ActivePlayerIndex)
        {
            Announce($"¡{_battleModel.ActivePlayerPoken.Definition.pokenName} ya está en combate!");
            AbrirMenuLiberacion();
            return;
        }
        if (!BattleTeamRules.EsIndiceVivo(_playerModel.Team, teamIndex))
        {
            Announce("Ese Poken no puede combatir.");
            AbrirMenuLiberacion();
            return;
        }
        TomarAccionLiberacion(LiberationEngine.Accion.Cambiar, teamIndex);
    }

    // El menu de equipo es el mismo en las tres situaciones, asi que el evento
    // del HUD se desvia segun la fase.
    private void OnSwitchChosen(int teamIndex)
    {
        if (_battleModel == null) return;

        switch (_battleModel.CurrentPhase)
        {
            case BattlePhase.PlayerReplacement:
                StartCoroutine(ReemplazoRoutine(teamIndex));
                break;
            case BattlePhase.Liberation:
                LiberationSwitch(teamIndex);
                break;
            case BattlePhase.PlayerTurn:
                SelectSwitch(teamIndex);
                break;
        }
    }

    private void TomarAccionLiberacion(LiberationEngine.Accion accion,
        int indiceExtra = -1, HealItemDefinition objetoElegido = null)
    {
        if (_battleModel == null || _battleModel.CurrentPhase != BattlePhase.Liberation) return;
        StartCoroutine(LiberationTurnRoutine(accion, indiceExtra, objetoElegido));
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
                    // La barra drena hasta la foto de ESTE evento, no hasta el
                    // HP del final del turno (ver BattleEvent.HpAfter)
                    if (battleHudView != null)
                        yield return battleHudView.AnimateHp(_battleModel, ev);
                    break;

                case BattleEventKind.Fainted:
                {
                    // Asegura que la barra llego a 0 ANTES de la caida
                    if (battleHudView != null)
                        yield return battleHudView.AnimateHp(_battleModel);
                    battleHudView?.UpdateHud(_battleModel);
                    if (ev.TargetIsPlayer) _playerFaintedPendingSwitch = true;
                    else _enemyFaintedPendingSwitch = true;

                    // Nestor reacciona a la caida: festeja la del rival, se
                    // asusta con la propia. Sin esto la cara queda congelada
                    // en la del ultimo turno.
                    if (_nestorAlly != null)
                    {
                        _nestorAlly.ReaccionarACaida(!ev.TargetIsPlayer);
                        battleHudView?.SetNestorPortrait(_nestorAlly.Snapshot());
                    }

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

                case BattleEventKind.PlayerMustSwitch:
                    // El caido sigue en pantalla con su barra en 0: es lo que
                    // el jugador tiene que ver mientras elige el reemplazo. El
                    // HUD del que entra se refresca con su PokenSwitched, o
                    // sea DESPUES de la eleccion.
                    battleHudView?.UpdateHud(_battleModel);
                    break;

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

            // Respiro pedido por el evento (ver BattleEvent.ExtraPause)
            if (ev.ExtraPause > 0f)
                yield return new WaitForSeconds(ev.ExtraPause);
        }
    }

    private IEnumerator EndBattleRoutine()
    {
        bool victory = _battleModel.IsPlayerVictory;
        bool escaped = _battleModel.PlayerEscaped;

        // El retrato se apaga con el combate. Si queda prendido, el Canvas se
        // esconde con Nestor dibujado y reaparece asi en la pelea siguiente.
        battleHudView?.SetNestorPortrait(default);

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

        // Cualquier menu que hubiera quedado abierto se cierra con el combate,
        // y el contexto vuelve al mundo.
        if (MenuManager.HasInstance)
        {
            MenuManager.Instance.CloseAll();
            MenuManager.Instance.SetContext(MenuContextKind.Overworld);
        }

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

        // La del prefab manda: Camera.main en un mapa es la de exploracion y
        // tiene otro tamaño ortografico, asi que el fondo saldria mal escalado.
        Camera cam = battleCamera != null ? battleCamera : Camera.main;
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
