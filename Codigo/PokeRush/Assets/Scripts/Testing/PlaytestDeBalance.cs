using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// BANCO DE PRUEBAS. No es parte del juego.
//
// El playtest del rebalanceo del 6-oct-2026: juega las peleas importantes EN SU
// ESCENA, con el BattleController de verdad (animaciones, la Prediccion y la
// liberacion de Nestor, la lectura aliada del casino, la ruleta y la mano de
// blackjack de Reymar, el guion entero de Julieta), con los tres iniciales y
// varias veces, y anota cuantas gana y como.
//
// El piloto juega como una persona razonable, igual que el del simulador
// (Editor/Balance/SimuladorDeBalance): pega con lo que mas rinde contra el
// rival (MoveScorer), cambia si esta muy mal parado (dos veces por pelea como
// mucho), elige el relevo por matchup, gira las ruletas, se planta en el
// blackjack, no aprende movimientos nuevos y usa pociones como una persona
// (dos por pelea cuando al activo le queda poco; en la liberacion, las que
// necesite), con la mochila que tendria en cada etapa.
//
// Las peleas se disparan como en el juego: la del tutorial y la de Nestor con
// StartBattle (lo mismo que hacen TutorialParte e InteractableTrigger; los
// subsistemas cortados se ponen como flags de la historia), los NPCs del casino
// con su Interact (charla, pelea y ruleta de premios), Reymar pisando su zona
// y Julieta con PeleaJulieta.Lanzar.
//
// Lo lanza PokeRush > Balance > Playtest en Play. Resultados en el Editor.log
// con la etiqueta [PlayBalance]. No guarda la partida.
public class PlaytestDeBalance : MonoBehaviour
{
    public const string PedidoKey = "PokeRush.PlayBalance.Pelea";
    public const string VecesKey = "PokeRush.PlayBalance.Veces";

    private static readonly string[] Iniciales = { "0448_Lucario", "0094_Gengar", "0658_Greninja" };

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearSiLoPidieron()
    {
        string pelea = UnityEditor.SessionState.GetString(PedidoKey, "");
        if (string.IsNullOrEmpty(pelea)) return;
        UnityEditor.SessionState.SetString(PedidoKey, "");
        var banco = new GameObject("[PlayBalance]").AddComponent<PlaytestDeBalance>();
        banco._pelea = pelea;
        banco._veces = Mathf.Max(1, UnityEditor.SessionState.GetInt(VecesKey, 1));
    }
#endif

    private string _pelea = "";
    private int _veces = 1;

    private BattleController _combate;
    private DialogueController _dialogo;
    private PlayerModel _jugador;
    private StoryModel _story;

    private readonly System.Random _azar = new System.Random(6102026);
    private IBattleRng _rng;
    private AIProfileDefinition _perfil;
    private IBattleAI _ia;

    // Lo que paso en la pelea en curso.
    private BattleOutcome _ultimo;
    private int _turnos, _cambios, _turnoDelUltimoCambio, _turnosDeLiberacion, _pociones;
    private bool _huboLiberacion;

    private readonly List<string> _resumen = new List<string>();

    private void Awake()
    {
        Application.runInBackground = true;
        CasinoDialogo.GuardadoAutomatico = false;
        DontDestroyOnLoad(gameObject);
        _rng = new SeededRng(6102026);
        _perfil = BattleAIFactory.CreateDefaultProfile(BattleAIType.GymLeader);
        _perfil.randomness = 0.15f;
        _perfil.switchGainThreshold = 0.6f;
        _perfil.setupWeight = 0.3f;
        _perfil.statusWeight = 0.3f;
        _ia = BattleAIFactory.Create(_perfil);
    }

    private void OnDestroy()
    {
        CasinoDialogo.GuardadoAutomatico = true;
        Time.timeScale = 1f;
    }

    private void Start() => StartCoroutine(Correr());

    private string _escena;

    // Busca el combate y la caja de la escena en curso (otra vez si se recargo).
    private bool Conectar()
    {
        _combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (_combate == null || _dialogo == null || _jugador == null) return false;
        typeof(BattleController).GetField("messageDelay", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(_combate, 0.2f);
        _combate.BattleEnded += o => _ultimo = o;
        return true;
    }

    // Ganarle al jefe de un nivel lo termina (Nestor: la salida a la cueva). Si
    // el juego cambio de escena, se vuelve a la de la pelea para la siguiente.
    private IEnumerator VolverALaEscenaSiCambio()
    {
        bool cambio = _combate == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != _escena;
        if (!cambio) yield break;
        yield return RecargarLaEscena(3f);
    }

    private IEnumerator RecargarLaEscena(float espera)
    {
        // Dejar que termine lo que el juego haya pedido (cajas, salida del nivel).
        float limite = Time.realtimeSinceStartup + espera;
        while (Time.realtimeSinceStartup < limite)
        {
            if (_dialogo != null && _dialogo.IsOpen) _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.1f);
        }
        // El nivel quedo terminado (Nestor liberado): con el flag puesto, la
        // salida del nivel vuelve a preguntar "¿Volves a la cueva?" apenas
        // carga y el piloto contesta que si. Se borra para la pelea siguiente.
        RecorridoDeLaHistoria.Nivel nivel = RecorridoDeLaHistoria.NivelDeLaEscena(_escena);
        if (nivel != null && _story != null) _story.ActiveFlags.Remove(nivel.FlagCompletado);
        UnityEngine.SceneManagement.SceneManager.LoadScene(_escena);
        yield return EsperarHasta(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == _escena
                                        && FindAnyObjectByType<BattleController>(FindObjectsInactive.Include) != null, 20f);
        yield return new WaitForSecondsRealtime(1.5f);
        _jugador = GameManager.Instance?.CurrentPlayer;
        _story = GameManager.Instance?.Story;
        Conectar();
        yield return CerrarCajas(8f);
        Debug.Log($"[PlayBalance] (se volvio a cargar {_escena})");
    }

    private IEnumerator Correr()
    {
        Debug.Log($"[PlayBalance] Arranca el playtest de '{_pelea}' ({_veces} vuelta(s) por inicial).");
        yield return EsperarHasta(() => GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null, 15f);
        _jugador = GameManager.Instance?.CurrentPlayer;
        _story = GameManager.Instance?.Story;
        _escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!Conectar())
        {
            Debug.LogError("[PlayBalance] Falta la partida, el combate o la caja de dialogo en la escena.");
            yield break;
        }

        // Lo que la escena cuenta al llegar (el despertar, la llegada a la sede...).
        yield return new WaitForSecondsRealtime(1f);
        yield return CerrarCajas(8f);

        switch (_pelea)
        {
            case "tutorial": yield return Tutorial(); break;
            case "nestor": yield return Nestor(); break;
            case "nestor-cortado": yield return Nestor(soloConCortes: true); break;
            case "casino": yield return Casino(); break;
            case "reymar": yield return Reymar(); break;
            case "julieta": yield return Julieta(); break;
            default: Debug.LogError($"[PlayBalance] No conozco la pelea '{_pelea}'."); break;
        }

        Debug.Log("[PlayBalance] RESUMEN\n" + string.Join("\n", _resumen));
        Debug.Log("[PlayBalance] TERMINADO");
        Time.timeScale = 1f;
    }

    // ---------------------------------------------------------------- las peleas

    private IEnumerator Tutorial()
    {
        var rebeca = Cargar<EnemyDefinition>("Definitions/Tutorial/RivalDelTutorial");
        foreach (string inicial in Iniciales)
            for (int v = 0; v < _veces; v++)
            {
                ArmarEquipo((inicial, 42));
                yield return Pelear($"tutorial {Corto(inicial)}", () => _combate.StartBattle(_jugador, rebeca));
            }
    }

    private IEnumerator Nestor(bool soloConCortes = false)
    {
        var nestor = Cargar<BossDefinition>("Definitions/Torre1/Nestor");
        var casos = new (string nombre, StoryFlag[] cortados)[]
        {
            ("nada cortado", new StoryFlag[0]),
            ("rastreo y un nodo", new[] { StoryFlag.Torre1_RastreoCortado, StoryFlag.Torre1_FirewallNodoA }),
            ("todo cortado", new[] { StoryFlag.Torre1_CamarasCortadas, StoryFlag.Torre1_FirewallNodoA, StoryFlag.Torre1_FirewallNodoB,
                                     StoryFlag.Torre1_FirewallNodoC, StoryFlag.Torre1_RastreoCortado, StoryFlag.Torre1_EnergiaAuxiliarCortada })
        };
        foreach (var caso in casos.Where(c => !soloConCortes || c.cortados.Length > 0))
            foreach (string inicial in Iniciales)
                for (int v = 0; v < _veces; v++)
                {
                    foreach (StoryFlag f in casos.SelectMany(c => c.cortados)) _story.ActiveFlags.Remove(f);
                    foreach (StoryFlag f in caso.cortados) _story.ActivateFlag(f);
                    ArmarEquipo((inicial, 43), ("0902_Basculegion", 42), ("0257_Blaziken", 44));
                    DarMochila(pociones: 3, super: 2, hiper: 0);   // las del tutorial y las del Data Center
                    yield return Pelear($"nestor {caso.nombre} {Corto(inicial)}", () => _combate.StartBattle(_jugador, nestor));
                    // Ganarle termina el nivel (la salida a la cueva llega unos
                    // segundos despues): se espera y se vuelve a cargar siempre.
                    yield return RecargarLaEscena(6f);
                }
    }

    // Cada NPC del piso con su Interact: la charla, la pelea con Nestor de
    // ayudante y, si toca, la ruleta de premios (que mete el Pokemon en el
    // equipo o en el PC con PrizeGranter, el sistema de entrega de siempre).
    private IEnumerator Casino()
    {
        var npcs = FindObjectsByType<CasinoTrainerNpc>(FindObjectsInactive.Exclude).OrderBy(n => n.name).ToList();
        var guardia = FindAnyObjectByType<CasinoElevatorGuard>(FindObjectsInactive.Exclude);
        foreach (string inicial in Iniciales)
        {
            foreach (CasinoTrainerNpc npc in npcs) _story.ActiveFlags.Remove(npc.FlagVencido);
            ArmarEquipo((inicial, 43), ("0902_Basculegion", 42), ("0257_Blaziken", 44));
            DarMochila(pociones: 2, super: 3, hiper: 0);
            foreach (CasinoTrainerNpc npc in npcs)
            {
                _jugador.CurarEquipo();
                string antes = Equipo();
                yield return Pelear($"casino {npc.Rival.characterName} {Corto(inicial)}",
                                    () => npc.Interact(_jugador, _story), esperarCalma: true);
                string despues = Equipo();
                if (despues != antes) Anotar($"   -> el equipo paso de [{antes}] a [{despues}]");
            }
            if (guardia != null)
            {
                _jugador.CurarEquipo();
                // Sin plata para el pase (la DEMO arranca con 100): ofrece pelear y el piloto acepta.
                _story.ActiveFlags.Remove(guardia.FlagVencido);
                _story.ActiveFlags.Remove(guardia.FlagPase);
                yield return Pelear($"casino guardia del ascensor {Corto(inicial)}",
                                    () => guardia.Interact(_jugador, _story), esperarCalma: true);
            }
        }
    }

    private IEnumerator Reymar()
    {
        var jefe = FindAnyObjectByType<CasinoJefeFinalNpc>(FindObjectsInactive.Include);
        if (jefe == null) { Debug.LogError("[PlayBalance] No esta Reymar en la escena."); yield break; }
        foreach (string inicial in Iniciales)
            for (int v = 0; v < _veces; v++)
            {
                _story.ActiveFlags.Remove(jefe.FlagVencido);
                ArmarEquipo((inicial, 46), ("0902_Basculegion", 44), ("0257_Blaziken", 46),
                            ("0212_Scizor", 44), ("0006_Charizard", 47), ("0405_Luxray", 47));
                DarMochila(pociones: 0, super: 3, hiper: 2);
                yield return Pelear($"reymar {Corto(inicial)}", () => jefe.SendMessage("Encarar"), esperarCalma: true,
                                    gano: () => _story.HasFlag(jefe.FlagVencido));
            }
    }

    private IEnumerator Julieta()
    {
        var pelea = FindAnyObjectByType<PeleaJulieta>(FindObjectsInactive.Include);
        if (pelea == null) { Debug.LogError("[PlayBalance] No esta la pelea de Julieta en la escena."); yield break; }
        foreach (string inicial in Iniciales)
            for (int v = 0; v < _veces; v++)
            {
                bool conMew = v % 2 == 0;
                if (conMew)
                    ArmarEquipo((inicial, 48), ("0902_Basculegion", 45), ("0257_Blaziken", 47),
                                ("0006_Charizard", 48), ("0445_Garchomp", 51), ("0151_Mew", 52));
                else
                    ArmarEquipo((inicial, 48), ("0902_Basculegion", 45), ("0257_Blaziken", 47),
                                ("0006_Charizard", 48), ("0445_Garchomp", 51), ("0700_Sylveon", 46));
                DarMochila(pociones: 0, super: 3, hiper: 3);
                pelea.Reiniciar();
                yield return CerrarCajas(3f);
                yield return Pelear($"julieta {Corto(inicial)}{(conMew ? " con Mew" : " sin Mew")}", () => pelea.Lanzar(_jugador),
                                    extra: () => $"fase {pelea.Fase}, vida del PJ {pelea.Vida?.Porcentaje}%");
                pelea.Reiniciar();
                yield return CerrarCajas(3f);
            }
    }

    // ---------------------------------------------------------------- una pelea

    private IEnumerator Pelear(string nombre, System.Action lanzar, bool esperarCalma = false,
                               System.Func<bool> gano = null, System.Func<string> extra = null)
    {
        _ultimo = null;
        _turnos = _cambios = _turnosDeLiberacion = _pociones = 0;
        _turnoDelUltimoCambio = -10;
        _huboLiberacion = false;
        int vivosAntes = _jugador.Team.Count(p => !p.IsFainted);

        Time.timeScale = 3f;
        lanzar();
        // La pelea puede tardar en arrancar (charla antes).
        yield return Pilotear(() => _combate.EnCurso, 40f);
        if (!_combate.EnCurso && _ultimo == null)
        {
            Anotar($"{nombre}: NO ARRANCO");
            yield break;
        }

        // Hasta que termina (con las revanchas de Reymar, hasta que todo se calma).
        yield return Pilotear(() => !_combate.EnCurso && _ultimo != null, 600f);
        if (_combate == null) _ultimo ??= new BattleOutcome { PlayerVictory = true };   // el juego salio del nivel: gano
        if (esperarCalma) yield return Pilotear(Calmo, 60f, calma: 3f);
        Time.timeScale = 1f;

        if (_turnos == 0 && !_huboLiberacion)
        {
            Anotar($"{nombre}: NO SE JUGO (el juego cambio de escena en el medio)");
            yield break;
        }
        bool victoria = gano != null ? gano() : _ultimo != null && _ultimo.PlayerVictory;
        int caidos = vivosAntes - (_jugador?.Team.Count(p => !p.IsFainted) ?? 0);
        string lib = _huboLiberacion ? $", liberacion {_turnosDeLiberacion} turnos" : "";
        string mas = extra != null ? ", " + extra() : "";
        Anotar($"{nombre}: {(victoria ? "GANO" : "PERDIO")} en {_turnos} turnos, cayeron {caidos}, pociones {_pociones}{lib}{mas}");
        yield return CerrarCajas(5f);
        _jugador.CurarEquipo();

        // MapBattleBridge esconde el sistema de combate un rato DESPUES de que
        // termina la pelea: lanzar la siguiente antes deja al BattleController
        // apagado y la pelea trabada en el turno del jugador. En el juego no
        // pasa (entre una pelea y otra se camina); aca se espera.
        yield return EsperarHasta(() => _combate == null || !_combate.gameObject.activeInHierarchy, 8f);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return VolverALaEscenaSiCambio();
    }

    private bool Calmo() => _combate != null && _dialogo != null && !_combate.EnCurso && !_dialogo.IsOpen &&
                            !(MenuManager.HasInstance && MenuManager.Instance.Top != null);

    // ---------------------------------------------------------------- el piloto

    private IEnumerator Pilotear(System.Func<bool> hasta, float segundos, float calma = 0f)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        float listoDesde = -1f, calmoDesde = -1f;
        BattlePhase faseAnterior = BattlePhase.CalculatingResult;

        while (Time.realtimeSinceStartup < limite)
        {
            if (_combate == null || _dialogo == null) yield break;   // cambio la escena
            if (hasta())
            {
                if (calma <= 0f) yield break;
                if (calmoDesde < 0f) calmoDesde = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - calmoDesde >= calma) yield break;
            }
            else calmoDesde = -1f;

            if (_dialogo.IsOpen)
            {
                _dialogo.OnConfirm();
                yield return new WaitForSecondsRealtime(0.05f);
                continue;
            }

            MenuScreen arriba = MenuManager.HasInstance ? MenuManager.Instance.Top : null;
            if (arriba is PrizeWheelController rueda)
            {
                rueda.OnSpinButton();
                yield return new WaitForSecondsRealtime(0.3f);
                continue;
            }
            if (arriba is MoveTutorScreen tutor)
            {
                tutor.HandleAction(GameAction.Cancel);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is MessageBoxScreen && MenuManager.Instance.BelowTop is MoveTutorScreen)
            {
                arriba.HandleAction(GameAction.Up);
                arriba.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is MessageBoxScreen cartel)
            {
                cartel.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is BlackjackScreen mesa)
            {
                // Una mano: se planta con lo que le dieron.
                mesa.OnActionButton(1);
                yield return new WaitForSecondsRealtime(0.6f);
                if (MenuManager.Instance.Top == mesa) mesa.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.4f);
                continue;
            }
            if (arriba is PartyScreen equipo)
            {
                yield return ElegirRelevo(equipo);
                continue;
            }

            BattleModel m = _combate.Modelo;
            BattlePhase fase = m != null ? m.CurrentPhase : BattlePhase.CalculatingResult;
            if (fase != faseAnterior) { listoDesde = -1f; faseAnterior = fase; }

            if (m != null && (fase == BattlePhase.PlayerTurn || fase == BattlePhase.Liberation))
            {
                if (listoDesde < 0f) listoDesde = Time.realtimeSinceStartup;
                bool listo = fase == BattlePhase.Liberation
                    ? Time.realtimeSinceStartup - listoDesde > 0.6f
                    : BattleHudView.IsNavigating || Time.realtimeSinceStartup - listoDesde > 2.5f;
                if (listo)
                {
                    if (fase == BattlePhase.Liberation) Liberar(m); else Jugar(m);
                    listoDesde = -1f;
                    faseAnterior = BattlePhase.CalculatingResult;
                }
            }
            yield return null;
        }
    }

    private void Jugar(BattleModel m)
    {
        _turnos++;
        PokenModel enCampo = m.ActivePlayerPoken;
        float vidaPropia = (float)enCampo.CurrentHP / enCampo.MaxHP;
        float vidaRival = (float)m.ActiveEnemyPoken.CurrentHP / m.ActiveEnemyPoken.MaxHP;
        if (vidaPropia < 0.25f && vidaRival > 0.3f && _pociones < 2 && UsarPocion(enCampo))
        {
            if (m.CurrentPhase != BattlePhase.PlayerTurn) { _pociones++; return; }
        }

        var ctx = new AiContext
        {
            Self = m.PlayerState, Foe = m.EnemyState, OwnTeam = m.Player.Team,
            OwnActiveIndex = m.ActivePlayerIndex, Profile = _perfil, Model = m
        };
        BattleAction accion = _ia.ChooseAction(ctx, _rng);

        if (accion.Kind == BattleActionKind.SwitchPoken && _cambios < 2 && _turnos - _turnoDelUltimoCambio > 2)
        {
            _combate.SelectSwitch(accion.SwitchIndex);
            if (m.CurrentPhase != BattlePhase.PlayerTurn)
            {
                _cambios++;
                _turnoDelUltimoCambio = _turnos;
                return;
            }
        }

        PokenModel activo = m.ActivePlayerPoken;
        IEnumerable<int> orden = Enumerable.Range(0, activo.Moves.Count)
            .Where(i => activo.Moves[i].HasPP)
            .OrderByDescending(i => i == accion.MoveIndex && accion.Kind == BattleActionKind.UseMove ? 1000f
                                    : MoveScorer.ScoreMove(ctx, activo.Moves[i].Definition));
        foreach (int i in orden)
        {
            _combate.SelectMove(i);
            if (m.CurrentPhase != BattlePhase.PlayerTurn) return;   // lo tomo
        }

        // La Prediccion le anulo todo lo que sabe: cambia.
        for (int i = 0; i < m.Player.Team.Count; i++)
        {
            if (i == m.ActivePlayerIndex || m.Player.Team[i].IsFainted) continue;
            _combate.SelectSwitch(i);
            if (m.CurrentPhase != BattlePhase.PlayerTurn) return;
        }
        _combate.SelectMove(0);   // sin PP en nada: Forcejeo
    }

    // La liberacion de Nestor: no atacar. El chip pega fuerte cada tres turnos
    // (el primero incluido): ahi se protege; en los otros cambia al que este
    // mas entero, o se protege si no hay a quien.
    private void Liberar(BattleModel m)
    {
        _huboLiberacion = true;
        bool golpeFuerte = _turnosDeLiberacion % 3 == 0;
        _turnosDeLiberacion++;
        _turnos++;
        if (!golpeFuerte)
        {
            PokenModel activo = m.ActivePlayerPoken;
            if ((float)activo.CurrentHP / activo.MaxHP < 0.4f && UsarPocion(activo))
            {
                _pociones++;
                return;
            }
            int mejor = -1;
            float vida = (float)m.ActivePlayerPoken.CurrentHP / m.ActivePlayerPoken.MaxHP;
            for (int i = 0; i < m.Player.Team.Count; i++)
            {
                PokenModel p = m.Player.Team[i];
                if (i == m.ActivePlayerIndex || p.IsFainted) continue;
                float v = (float)p.CurrentHP / p.MaxHP;
                if (v > vida + 0.2f) { vida = v; mejor = i; }
            }
            if (mejor >= 0) { _combate.LiberationSwitch(mejor); return; }
        }
        _combate.LiberationProtect();
    }

    private IEnumerator ElegirRelevo(PartyScreen pantalla)
    {
        BattleModel m = _combate.Modelo;
        int objetivo = -1;
        float mejor = float.MinValue;
        for (int i = 0; i < _jugador.Team.Count; i++)
        {
            PokenModel p = _jugador.Team[i];
            if (p.IsFainted || (m != null && i == m.ActivePlayerIndex)) continue;
            float s = m != null && m.EnemyState != null ? MoveScorer.MatchupScore(p, m.EnemyState) : 0f;
            s += 0.3f * p.CurrentHP / p.MaxHP;
            if (s > mejor) { mejor = s; objetivo = i; }
        }
        if (objetivo < 0) { pantalla.HandleAction(GameAction.Cancel); yield return new WaitForSecondsRealtime(0.2f); yield break; }

        for (int n = 0; n < 12 && pantalla.CursorIndex != objetivo; n++) pantalla.HandleAction(GameAction.Right);
        pantalla.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.15f);
        // Si abrio el submenu del Pokemon, la primera opcion es mandarlo.
        if (MenuManager.HasInstance && MenuManager.Instance.Top == pantalla) pantalla.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.15f);
    }

    // ---------------------------------------------------------------- utilidades

    private void ArmarEquipo(params (string especie, int nivel)[] equipo)
    {
        _jugador.Team.Clear();
        foreach (var (especie, nivel) in equipo)
        {
            var def = Cargar<PokenDefinition>($"Definitions/Pokens/{especie}");
            if (def != null) _jugador.Team.Add(new PokenModel(def, nivel));
        }
        _jugador.CurarEquipo();
    }

    // La mochila de curacion que tendria el jugador en esa etapa.
    private void DarMochila(int pociones, int super, int hiper)
    {
        foreach (HealItemStack s in _jugador.Inventory.HealItems.ToList())
            if (s.Quantity > 0) _jugador.Inventory.ConsumeHealItem(s.Definition, s.Quantity);
        void Dar(string nombre, int cantidad)
        {
            var item = Cargar<HealItemDefinition>($"Definitions/Items/{nombre}");
            if (item != null && cantidad > 0) _jugador.Inventory.AddHealItem(item, cantidad);
        }
        Dar("Pocion", pociones);
        Dar("SuperPocion", super);
        Dar("HiperPocion", hiper);
    }

    // La que menos desperdicia de las que alcanzan, por OnItemChosen: la misma
    // entrada que la lista de objetos del HUD (pasa por la Prediccion y, en la
    // liberacion, cuenta como CURAR).
    private bool UsarPocion(PokenModel activo)
    {
        List<HealItemStack> usables = _jugador.Inventory.HealItems.Where(s => s != null && s.Definition != null && s.Quantity > 0).ToList();
        if (usables.Count == 0) return false;
        int falta = activo.MaxHP - activo.CurrentHP;
        int elegido = -1, mejor = int.MaxValue;
        for (int i = 0; i < usables.Count; i++)
        {
            int cura = usables[i].Definition.fullHeal ? activo.MaxHP : usables[i].Definition.healAmount;
            if (cura <= 0) continue;
            int desperdicio = System.Math.Abs(falta - cura);
            if (desperdicio < mejor) { mejor = desperdicio; elegido = i; }
        }
        if (elegido < 0) return false;
        _combate.SendMessage("OnItemChosen", elegido);
        return true;
    }

    private string Equipo() => string.Join(", ", _jugador.Team.Select(p => $"{p.Definition.pokenName} {p.Level}"));

    private static T Cargar<T>(string ruta) where T : Object
    {
        var a = Resources.Load<T>(ruta);
        if (a == null) Debug.LogError($"[PlayBalance] No se pudo cargar {ruta}.");
        return a;
    }

    private static string Corto(string especie) => especie.Substring(5);

    private void Anotar(string linea)
    {
        _resumen.Add(linea);
        Debug.Log("[PlayBalance] " + linea);
    }

    private IEnumerator CerrarCajas(float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (Time.realtimeSinceStartup < limite && _dialogo != null)
        {
            if (_dialogo.IsOpen) _dialogo.OnConfirm();
            else if (MenuManager.HasInstance && MenuManager.Instance.Top is MessageBoxScreen caja) caja.HandleAction(GameAction.Confirm);
            else yield break;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < limite) yield return null;
    }
}
