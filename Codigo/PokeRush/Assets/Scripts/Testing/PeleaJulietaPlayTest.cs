using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Juega la pelea contra Julieta como un jugador, en la escena de prueba:
//   1. El encuentro (hablarle abre la charla y despues la pelea) y el REINICIO
//      a mitad de pelea: el equipo vuelve a como entro y todo arranca de cero.
//   2. La DERROTA por la vida del PJ: el Pokemon controlado lo termina.
//   3. La pelea ENTERA: las cuatro fases con lo que prende y apaga cada una, el
//      golpe al PJ, la ruptura, la victoria, el final en la arena (el servidor
//      destruido y la sala a oscuras), la vuelta a la cueva (epilogo, charla
//      con Tixi, la ultima conexion) y los creditos.
//
// Sirve tambien sobre la arena de verdad (BossFinal): abrirla y lanzarlo.
//
// Resultados en el Editor.log con la etiqueta [PlayJulieta] (la consola del MCP
// no es confiable) y capturas en Capturas/Julieta/. Se lanza desde PokeRush >
// Jefe final (Julieta) > Probar la pelea en Play. No guarda la partida.
public class PeleaJulietaPlayTest : MonoBehaviour
{
    public const string PedidoKey = "PokeRush.PlayJulieta.Pendiente";

    public string carpeta = "Capturas/Julieta";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearSiLoPidieron()
    {
        if (!UnityEditor.SessionState.GetBool(PedidoKey, false)) return;
        UnityEditor.SessionState.SetBool(PedidoKey, false);
        new GameObject("[PlayJulieta]").AddComponent<PeleaJulietaPlayTest>();
    }
#endif

    private int _ok;
    private int _fallos;
    private PeleaJulieta _pelea;
    private BattleController _combate;
    private DialogueController _dialogo;
    private PlayerModel _jugador;
    private StoryModel _story;
    private readonly List<FaseDeJulieta> _fases = new List<FaseDeJulieta>();
    private readonly List<string> _momentos = new List<string>();

    private void Awake()
    {
        Application.runInBackground = true;
        CasinoDialogo.GuardadoAutomatico = false;
        DontDestroyOnLoad(gameObject);   // tiene que llegar a los creditos
    }

    private void OnDestroy()
    {
        CasinoDialogo.GuardadoAutomatico = true;
        Time.timeScale = 1f;
    }

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        System.IO.Directory.CreateDirectory(carpeta);
        Debug.Log("[PlayJulieta] Arranca la prueba de la pelea contra Julieta.");

        yield return EsperarHasta(() => GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null, 10f);
        _jugador = GameManager.Instance?.CurrentPlayer;
        _story = GameManager.Instance?.Story;
        _pelea = FindAnyObjectByType<PeleaJulieta>(FindObjectsInactive.Include);
        _combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);

        Check("Hay partida DEMO con seis Pokemon", _jugador != null && _jugador.Team.Count == 6);
        Check("Estan la pelea, el combate y la caja de dialogo", _pelea && _combate && _dialogo);
        if (_jugador == null || !_pelea || !_combate || !_dialogo) { Terminar(); yield break; }

        _pelea.FaseCambiada += f => _fases.Add(f);
        _pelea.MomentoSono += m => _momentos.Add(m);

        // El combate a otro ritmo: con la pausa de siempre la pelea entera tarda
        // diez minutos.
        typeof(BattleController).GetField("messageDelay", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(_combate, 0.3f);

        // En la arena de verdad, al llegar se cuenta la llegada al Ameghino
        // (una ZonaDeHistoria): se cierra antes de hablarle a Julieta.
        yield return new WaitForSecondsRealtime(1f);
        yield return EsperarHasta(() => { if (_dialogo.IsOpen) _dialogo.OnConfirm(); return !_dialogo.IsOpen; }, 15f);

        yield return Encuentro();
        yield return DerrotaPorLaVidaDelPJ();
        yield return PeleaEntera();
        Terminar();
    }

    // ---------------------------------------------------------------- 1

    private IEnumerator Encuentro()
    {
        Debug.Log("[PlayJulieta] --- 1. Encuentro y reinicio ---");
        int[] psAntes = _jugador.Team.Select(p => p.CurrentHP).ToArray();

        _pelea.Interact(_jugador, _story);
        yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
        Check("Hablarle abre la charla de antes de pelear", _dialogo.IsOpen);
        yield return Foto("00-encuentro");
        Check("La pelea todavia no empezo mientras habla", !_combate.EnCurso);

        yield return EsperarHasta(() => { if (_dialogo.IsOpen) _dialogo.OnConfirm(); return _combate.EnCurso; }, 20f);
        Check("Terminada la charla, arranca la pelea", _combate.EnCurso);

        yield return EsperarMenuDeComandos(40f);
        Check("Fase 1: Analisis", _pelea.Fase == FaseDeJulieta.Analisis);
        Check("Fase 1: Nestor de ayudante", _combate.LecturaAliadaActiva);
        Check("Fase 1: la ruleta del casino juega a favor del jugador",
              _combate.Ruleta != null && _combate.Ruleta.QuienCobra == QuienCobraLaRuleta.AFavorDelJugador);
        Check("Fase 1: sonaron los momentos del arranque", _momentos.Contains("Inicio: Nestor y la ruleta en linea"));
        Check("Fase 1: sin señal de control ni vida del PJ", !_pelea.Lienzo.Senal.Encendida && !_pelea.Lienzo.Vida.Visible);
        Check("Fase 1: suena el tema de la fase 1", SuenaElTema(c => c.julietaFase1));
        yield return Foto("01-fase1");

        // Un par de turnos y se reinicia en el medio.
        yield return Pilotear(() => _combate.Modelo != null && _combate.Modelo.CurrentTurn >= 3, 90f);
        _pelea.Reiniciar();
        yield return EsperarHasta(() => !_combate.EnCurso && !_combate.gameObject.activeInHierarchy, 10f);
        yield return new WaitForSecondsRealtime(0.3f);

        Check("Reiniciar corta la pelea", !_combate.EnCurso);
        Check("Reiniciar devuelve el equipo a como entro",
              _jugador.Team.Select(p => p.CurrentHP).SequenceEqual(psAntes));
        Check("Reiniciar vuelve a la fase 1 y borra lo que paso",
              _pelea.Fase == FaseDeJulieta.Analisis && _pelea.Estado.GirosTotales == 0 && !_pelea.Vinculo.Activo);
        Check("Reiniciar apaga la ruleta y a Nestor", _combate.Ruleta == null && !_combate.LecturaAliadaActiva);
    }

    // ---------------------------------------------------------------- 2

    private IEnumerator DerrotaPorLaVidaDelPJ()
    {
        Debug.Log("[PlayJulieta] --- 2. Derrota por la vida del PJ ---");
        int[] psAntes = _jugador.Team.Select(p => p.CurrentHP).ToArray();

        Check("Se puede volver a lanzar despues de reiniciar", _pelea.Lanzar(_jugador));
        yield return EsperarMenuDeComandos(40f);

        // Directo a la fase 3.
        for (int i = 0; i < 2; i++)
        {
            FaseDeJulieta antes = _pelea.Fase;
            _pelea.AdelantarFase();
            yield return Pilotear(() => _pelea.Fase != antes || !_combate.EnCurso, 120f);
        }
        Check("Adelantando dos veces se llega a la fase 3", _pelea.Fase == FaseDeJulieta.ControlDelVinculo);
        Check("Fase 3: hay un Pokemon controlado", _pelea.Vinculo.Activo);
        if (!_pelea.Vinculo.Activo) yield break;

        // El PJ con 1 de vida y el controlado sin ninguna duda.
        _pelea.Vida.Recibir(_pelea.Vida.Actual - 1);
        _pelea.Vinculo.ProbabilidadEnElCampo = 1f;
        _pelea.Vinculo.ProbabilidadFueraDeOrden = 1f;

        yield return Pilotear(() => !_combate.EnCurso, 90f);
        Check("La pelea termina cuando el PJ se queda sin vida", !_combate.EnCurso && _pelea.Vida.Caido);
        Check("Perder no da el flag de vencida", !_story.HasFlag(StoryFlag.Julieta_Vencida));

        yield return EsperarHasta(() => _dialogo.IsOpen, 6f);
        Check("Al perder, Julieta habla en la arena", _dialogo.IsOpen);
        yield return Foto("02-derrota");
        yield return CerrarDialogos();
        Check("Al perder, el equipo vuelve a como entro",
              _jugador.Team.Select(p => p.CurrentHP).SequenceEqual(psAntes));
    }

    // ---------------------------------------------------------------- 3

    private IEnumerator PeleaEntera()
    {
        Debug.Log("[PlayJulieta] --- 3. La pelea entera ---");
        _fases.Clear();
        _momentos.Clear();
        _pelea.Reiniciar();
        yield return new WaitForSecondsRealtime(0.3f);

        Check("Tercera vez: arranca de cero", _pelea.Lanzar(_jugador));
        // Poco riesgo para el PJ: esta corrida es para ver las fases, la derrota
        // ya se probo.
        _pelea.Vinculo.ProbabilidadEnElCampo = 0.25f;
        _pelea.Vinculo.ProbabilidadFueraDeOrden = 0.1f;
        yield return EsperarMenuDeComandos(40f);

        // --- fase 1 -> 2
        yield return JugarFase(FaseDeJulieta.Analisis, 7);
        yield return EsperarMenuDeComandos(40f);
        Check("Fase 2: Julieta tomo el control", _pelea.Fase == FaseDeJulieta.ControlDelSistema);
        Check("Fase 2: la ruleta juega para Julieta",
              _combate.Ruleta != null && _combate.Ruleta.QuienCobra == QuienCobraLaRuleta.AFavorDelRival);
        Check("Fase 2: Nestor desconectado", !_combate.LecturaAliadaActiva);
        Check("Fase 2: la lectura de Julieta (el protocolo de Nestor) contra el jugador",
              _combate.Prediccion != null && _combate.Prediccion.Lector == "Julieta");
        Check("Fase 2: la señal de control en pantalla", _pelea.Lienzo.Senal.Encendida);
        Check("Fase 2: suena el tema de la fase 2", SuenaElTema(c => c.julietaFase2));
        yield return Foto("03-fase2");

        // --- fase 2 -> 3
        yield return JugarFase(FaseDeJulieta.ControlDelSistema, 7);
        yield return EsperarMenuDeComandos(40f);
        Check("Fase 3: Julieta se metio en un Pokemon del jugador", _pelea.Fase == FaseDeJulieta.ControlDelVinculo && _pelea.Vinculo.Activo);
        Check("Fase 3: el controlado sigue en el equipo del jugador",
              _pelea.Vinculo.Poken != null && _jugador.Team.Contains(_pelea.Vinculo.Poken));
        Check("Fase 3: la vida del PJ en pantalla", _pelea.Lienzo.Vida.Visible);
        Check("Fase 3: sigue el tema de la fase 2 (el final arranca en la ruptura)", SuenaElTema(c => c.julietaFase2));
        yield return Foto("04-fase3");

        // Que ataque al PJ al menos una vez.
        _pelea.Vinculo.ProbabilidadEnElCampo = 1f;
        _pelea.Vinculo.ProbabilidadFueraDeOrden = 1f;
        // Se espera a la barra, no al contador: el golpe se cuenta antes de
        // que la barra drene.
        yield return Pilotear(() => _pelea.Vida.Actual < _pelea.Vida.Maxima || !_combate.EnCurso, 60f);
        Check("El controlado ataca al PJ y le saca vida", _pelea.Vinculo.AtaquesAlPJ >= 1 && _pelea.Vida.Actual < _pelea.Vida.Maxima);
        _pelea.Vinculo.ProbabilidadEnElCampo = 0.25f;
        _pelea.Vinculo.ProbabilidadFueraDeOrden = 0f;
        yield return Foto("05-golpe-al-pj");

        // --- fase 3 -> ruptura
        yield return JugarFase(FaseDeJulieta.ControlDelVinculo, 6);
        yield return EsperarMenuDeComandos(40f);
        Check("Ruptura: el PJ rompio el control", _pelea.Fase == FaseDeJulieta.Ruptura);
        Check("Ruptura: el controlado vuelve a ser del jugador", !_pelea.Vinculo.Activo);
        Check("Ruptura: vuelve Nestor", _combate.LecturaAliadaActiva);
        Check("Ruptura: la ruleta vuelve a jugar para el jugador",
              _combate.Ruleta != null && _combate.Ruleta.QuienCobra == QuienCobraLaRuleta.AFavorDelJugador);
        Check("Ruptura: sin lectura de Julieta ni señal", _combate.Prediccion == null && !_pelea.Lienzo.Senal.Encendida);
        Check("Ruptura: suena el tema final", SuenaElTema(c => c.julietaFinal));
        yield return Foto("06-ruptura");

        // --- hasta ganar. El piloto pega sin mirar tipos y llega a la ruptura
        // con medio equipo caido: se lo cura para que esta parte pruebe el
        // final y no la puntería del piloto.
        foreach (PokenModel p in _jugador.Team) { p.SetCurrentHP(p.MaxHP); p.CureStatus(); }
        _combate.RefrescarHud();
        // Para no depender del equipo DEMO: el rival activo queda en 1 PS justo
        // antes de cada ataque del piloto (ver Pegar). Antes se ponia al empezar
        // la vuelta, pero ahi el turno casi nunca estaba en PlayerTurn y la
        // pelea se jugaba de verdad: a veces el equipo DEMO perdia (2-oct-2026).
        _rivalAUnPS = true;
        for (int i = 0; i < 40 && _combate.EnCurso; i++)
        {
            int turno = _combate.Modelo != null ? _combate.Modelo.CurrentTurn : 0;
            yield return Pilotear(() => !_combate.EnCurso || (_combate.Modelo != null && _combate.Modelo.CurrentTurn > turno), 60f);
        }
        _rivalAUnPS = false;

        Check("Las fases fueron en orden 2, 3, ruptura",
              _fases.SequenceEqual(new[] { FaseDeJulieta.ControlDelSistema, FaseDeJulieta.ControlDelVinculo, FaseDeJulieta.Ruptura }));
        Check("Ganarle termina la pelea con el flag de vencida", !_combate.EnCurso && _story.HasFlag(StoryFlag.Julieta_Vencida));
        Check("Al terminar no queda ningun efecto prendido",
              !_pelea.Vinculo.Activo && !_pelea.Lienzo.Senal.Encendida && !_pelea.Lienzo.Vida.Visible);
        Debug.Log($"[PlayJulieta] Momentos que sonaron ({_momentos.Count}): {string.Join(" | ", _momentos)}");

        // --- el final en la arena
        yield return EsperarHasta(() => _dialogo.IsOpen, 8f);
        Check("El final se cuenta en la arena, despues de la pelea", _dialogo.IsOpen && !_combate.EnCurso);
        Check("Despues de ganarle suena el tema del final", SuenaElTema(c => c.julietaDespues));
        yield return Foto("07-final");
        // Entre una caja y la otra puede haber un rato sin ninguna abierta (el
        // destello del nucleo, el del chip de Julieta): la charla termino recien
        // cuando pasan unos segundos sin cajas, o cuando cambia la escena.
        int lineas = 0;
        bool sinLuz = false;
        float sinCaja = 0f;
        while (lineas < 300 && sinCaja < 3f && _dialogo != null)
        {
            if (_dialogo.IsOpen) { _dialogo.OnConfirm(); lineas++; sinCaja = 0f; }
            else sinCaja += 0.06f;
            yield return new WaitForSecondsRealtime(0.06f);
            if (!sinLuz && _story.HasFlag(StoryFlag.Historia_ServidorPrincipalDestruido))
            {
                sinLuz = true;
                ServidorPrincipal servidor = _pelea.GetComponentInChildren<ServidorPrincipal>(true);
                Check("Al final el PJ destruye el servidor", servidor == null || servidor.Roto);
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Foto("07b-servidor-destruido");
            }
        }
        Check("El final tiene su charla entera (la del PDF)", lineas >= 100);
        Check("Al final se apagan las luces de la sala", sinLuz);

        // --- la cueva: epilogo, charla con Tixi y la ultima conexion
        yield return EsperarHasta(() => SceneManager.GetActiveScene().name == FinalDelJuego.EscenaDeLaCueva, 12f);
        Check("Despues del final se vuelve a la Quarcho Cueva", SceneManager.GetActiveScene().name == FinalDelJuego.EscenaDeLaCueva);
        EscenaFinalEnLaCueva escena = null;
        yield return EsperarHasta(() => (escena = FindAnyObjectByType<EscenaFinalEnLaCueva>()) != null, 3f);
        Check("La cueva arranca la escena final", escena != null);
        if (escena != null)
        {
            escena.Ritmo = 6f;
            Check("Mientras dura el final no cambia la musica", SuenaElTema(c => c.julietaDespues));
            yield return EsperarHasta(() => escena == null || escena.Etapa == "epilogo", 8f);
            Check("Primero el epilogo, sobre negro", escena != null && escena.Etapa == "epilogo");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Foto("08-epilogo");

            yield return EsperarHasta(() => escena == null || escena.Etapa == "charla", 30f);
            DialogueController dialogoDeLaCueva = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
            yield return EsperarHasta(() => dialogoDeLaCueva != null && dialogoDeLaCueva.IsOpen, 3f);
            Check("En la cueva charla Tixi", dialogoDeLaCueva != null && dialogoDeLaCueva.IsOpen);
            yield return Foto("09-cueva");
            int lineasDeTixi = 0;
            float sinCajaEnLaCueva = 0f;
            while (dialogoDeLaCueva != null && lineasDeTixi < 120 && sinCajaEnLaCueva < 1.5f)
            {
                if (dialogoDeLaCueva.IsOpen) { dialogoDeLaCueva.OnConfirm(); lineasDeTixi++; sinCajaEnLaCueva = 0f; }
                else sinCajaEnLaCueva += 0.06f;
                yield return new WaitForSecondsRealtime(0.06f);
            }
            Check("La charla con Tixi esta entera", lineasDeTixi >= 30);

            yield return EsperarHasta(() => escena == null ||
                (escena.UltimaLineaDeTerminal ?? "").Contains("VENTRESCA"), 30f);
            Check("La ultima conexion termina en VENTRESCA AI",
                  escena != null && (escena.UltimaLineaDeTerminal ?? "").Contains("VENTRESCA"));
            Check("La camara se aleja", escena != null && escena.AlejamientoDeLaCamara > 1.1f);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Foto("10-ultima-conexion");
        }

        // --- los creditos
        yield return EsperarHasta(() => SceneManager.GetActiveScene().name == "CreditosFinales", 20f);
        Check("Despues del final vienen los creditos", SceneManager.GetActiveScene().name == "CreditosFinales");
        yield return new WaitForSecondsRealtime(2.5f);
        CreditosFinales creditos = FindAnyObjectByType<CreditosFinales>();
        Check("La escena de creditos tiene sus creditos", creditos != null);
        yield return new WaitForSecondsRealtime(3f);
        Check("En los creditos suena su tema", SuenaElTema(c => c.creditos));
        yield return Foto("11-creditos");
        if (creditos != null)
        {
            creditos.Adelantar();
            yield return EsperarHasta(() => creditos.Terminado, 3f);
            Check("Los creditos terminan", creditos.Terminado);
        }
    }

    // Juega hasta que la fase cambie. Si en 'turnos' no cambio sola (el equipo
    // DEMO no llego a los umbrales de KOs), la adelanta como haria F10.
    // El tema que esta sonando es el que el catalogo dice para ese momento.
    private static bool SuenaElTema(System.Func<CatalogoDeAudio, Sonido> cual)
    {
        CatalogoDeAudio c = CatalogoDeAudio.Actual;
        Sonido s = c != null ? cual(c) : null;
        AudioClip sonando = AudioService.Instance.CurrentMusic;
        Debug.Log($"[PlayJulieta] musica: {(sonando != null ? sonando.name : "nada")} (esperada {(s != null && s.clip != null ? s.clip.name : "-")})");
        return s != null && s.clip != null && sonando == s.clip;
    }

    private IEnumerator JugarFase(FaseDeJulieta fase, int turnos)
    {
        int desde = _combate.Modelo != null ? _combate.Modelo.CurrentTurn : 0;
        yield return Pilotear(() => _pelea.Fase != fase || !_combate.EnCurso ||
                                    (_combate.Modelo != null && _combate.Modelo.CurrentTurn - desde >= turnos), 240f);
        if (_pelea.Fase == fase && _combate.EnCurso)
        {
            Debug.Log($"[PlayJulieta] La fase {fase} no cambio sola en {turnos} turnos: se adelanta.");
            _pelea.AdelantarFase();
            yield return Pilotear(() => _pelea.Fase != fase || !_combate.EnCurso, 120f);
        }
        else Debug.Log($"[PlayJulieta] La fase {fase} cambio sola.");
    }

    // ---------------------------------------------------------------- el piloto

    // Juega como un jugador que no piensa mucho: cierra las cajas, gira la
    // ruleta, elige el relevo cuando cae uno y pega con el movimiento mas fuerte.
    private IEnumerator Pilotear(System.Func<bool> hasta, float segundos)
    {
        Time.timeScale = 2.5f;
        float limite = Time.realtimeSinceStartup + segundos;
        float turnoListoDesde = -1f;

        while (!hasta() && Time.realtimeSinceStartup < limite)
        {
            if (_dialogo.IsOpen)
            {
                _dialogo.OnConfirm();
                yield return new WaitForSecondsRealtime(0.06f);
                continue;
            }

            MenuScreen arriba = MenuManager.HasInstance ? MenuManager.Instance.Top : null;
            if (arriba is PrizeWheelController rueda)
            {
                rueda.OnSpinButton();
                yield return new WaitForSecondsRealtime(0.3f);
                continue;
            }
            if (arriba is MessageBoxScreen cartel)
            {
                cartel.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is PartyScreen equipo)
            {
                yield return ElegirRelevo(equipo);
                continue;
            }

            BattleModel m = _combate.Modelo;
            if (m != null && m.CurrentPhase == BattlePhase.PlayerTurn)
            {
                if (turnoListoDesde < 0f) turnoListoDesde = Time.realtimeSinceStartup;
                // El menu de comandos ya esta (o paso un rato sin que nada se mueva).
                if (BattleHudView.IsNavigating || Time.realtimeSinceStartup - turnoListoDesde > 2.5f)
                {
                    Pegar(m);
                    turnoListoDesde = -1f;
                }
            }
            else turnoListoDesde = -1f;

            yield return null;
        }
        Time.timeScale = 1f;
    }

    private bool _rivalAUnPS;

    private void Pegar(BattleModel m)
    {
        if (_rivalAUnPS) m.ActiveEnemyPoken.SetCurrentHP(1);
        PokenModel activo = m.ActivePlayerPoken;
        IEnumerable<int> orden = Enumerable.Range(0, activo.Moves.Count)
            .Where(i => activo.Moves[i].HasPP)
            .OrderByDescending(i => activo.Moves[i].Definition.power);

        foreach (int i in orden)
        {
            _combate.SelectMove(i);
            if (m.CurrentPhase != BattlePhase.PlayerTurn) return;   // lo tomo
        }

        // La lectura de Julieta le anulo todo lo que sabe: cambia.
        for (int i = 0; i < _jugador.Team.Count; i++)
        {
            if (i == m.ActivePlayerIndex || _jugador.Team[i].IsFainted) continue;
            _combate.SelectSwitch(i);
            if (m.CurrentPhase != BattlePhase.PlayerTurn) return;
        }

        _combate.SelectMove(0);   // sin PP en nada: Forcejeo
    }

    private IEnumerator ElegirRelevo(PartyScreen pantalla)
    {
        BattleModel m = _combate.Modelo;
        int objetivo = -1;
        for (int i = 0; i < _jugador.Team.Count; i++)
            if (!_jugador.Team[i].IsFainted && (m == null || i != m.ActivePlayerIndex)) { objetivo = i; break; }
        if (objetivo < 0) { yield return new WaitForSecondsRealtime(0.2f); yield break; }

        for (int n = 0; n < 12 && pantalla.CursorIndex != objetivo; n++) pantalla.HandleAction(GameAction.Right);
        pantalla.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.15f);
        // Si abrio el submenu del Pokemon, la primera opcion es mandarlo.
        if (MenuManager.HasInstance && MenuManager.Instance.Top == pantalla) pantalla.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.15f);
    }

    private IEnumerator EsperarMenuDeComandos(float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (Time.realtimeSinceStartup < limite)
        {
            if (_dialogo.IsOpen) _dialogo.OnConfirm();
            BattleModel m = _combate.Modelo;
            if (m != null && m.CurrentPhase == BattlePhase.PlayerTurn && BattleHudView.IsNavigating && !_dialogo.IsOpen)
                yield break;
            yield return new WaitForSecondsRealtime(0.06f);
        }
    }

    private IEnumerator CerrarDialogos()
    {
        float limite = Time.realtimeSinceStartup + 10f;
        while (_dialogo.IsOpen && Time.realtimeSinceStartup < limite)
        {
            _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    // ---------------------------------------------------------------- utilidades

    private void Terminar()
    {
        Time.timeScale = 1f;
        if (MenuManager.HasInstance) MenuManager.Instance.CloseAll();
        Debug.Log($"[PlayJulieta] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[PlayJulieta] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[PlayJulieta] FALLO: {nombre}"); }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < limite) yield return null;
    }

    private IEnumerator Foto(string nombre)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot($"{carpeta}/{nombre}.png");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
    }
}
