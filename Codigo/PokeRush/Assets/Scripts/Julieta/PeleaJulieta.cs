using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// LA PELEA CONTRA JULIETA — el jefe final.
//
// Julieta no pelea solo con sus Pokemon: controla los sistemas que rodean al
// jugador y se los va sacando. La pelea avanza por FASES narrativas, no por una
// sola barra de PS:
//
//   1. Analisis           Nestor ayuda (su lectura aliada) y la ruleta del jefe
//                         del casino juega A FAVOR del jugador. Julieta mira.
//   2. Control del sistema  Activa el NeuraRush-LINK: la ruleta pasa a jugar
//                         para ella, Nestor se desconecta y ella usa el
//                         protocolo de Nestor contra el jugador (la Prediccion
//                         de la torre 1). Aparece la señal violeta.
//   3. Control del vinculo  Se mete en un Pokemon del jugador: sigue en su
//                         equipo pero no obedece, y ataca al propio PJ, que
//                         tiene una barra de vida aparte. Si llega a 0, se pierde.
//   4. Ruptura            El PJ rompe el control: todo vuelve a su lado y
//                         Julieta, recien ahora, puede caer.
//
// QUIEN HACE QUE:
//   - El GUION (asset GuionDeJulieta) dice cuando pasa cada cosa y que se dice.
//     Las fases cambian porque una linea del guion lo pide (AccionDeJulieta),
//     o sea que los dialogos disparan los eventos del combate.
//   - Este componente EJECUTA el guion: implementa IDirectorDeCombate, asi el
//     BattleController lo llama en momentos fijos del turno. No hay una sola
//     regla de Julieta dentro del combate.
//   - Las reglas puras viven aparte y se prueban sin escena: EstadoDePeleaJulieta
//     y GuionEnCurso (cuando suena cada momento), VinculoControlado (que hace el
//     Pokemon controlado), VidaDelPJ, CasinoRuletaDeCombate (a quien le cobra).
//
// REUSA lo que ya existia: la ruleta del jefe del casino (mismo asset y misma
// pantalla), la lectura aliada de Nestor, la Prediccion de la torre 1, la caja
// de dialogo, el combate y el puente mapa/combate. Lo unico nuevo que se ve es
// la barra de vida del PJ y la señal de control (UI de prueba).
//
// PARA MUDARLO A LA ARENA DEFINITIVA se copia UN objeto: 'PeleaJulieta' (este
// componente, Julieta y la zona). Nada de adentro apunta afuera; lo que falta
// en la escena lo agrega PokeRush > Jefe final > Preparar la escena abierta.
// Ver Documentacion/Documentacion del Codigo/Jefe_Final_Julieta.md.
public class PeleaJulieta : MonoBehaviour, IInteractable, IDirectorDeCombate
{
    [Header("La pelea")]
    [Tooltip("Julieta: equipo, niveles, sprites y el fondo del combate.")]
    [SerializeField] private EnemyDefinition julieta;
    [Tooltip("Que se dice y cuando cambia cada cosa. Ver GuionDeJulieta.")]
    [SerializeField] private GuionDeJulieta guion;

    [Header("La ruleta del jefe del casino")]
    [Tooltip("La MISMA de la pelea de Reymar (RuletaDelJefeFinal): sus ocho gajos.")]
    [SerializeField] private CasinoPrizeTable ruletaDelCasino;
    [Tooltip("Cada cuantos turnos gira. 2 = turnos 2, 4, 6...")]
    [SerializeField, Min(1)] private int turnosEntreGiros = 2;

    [Header("Como juega Julieta")]
    [Tooltip("Mientras estudia al jugador (fase 1).")]
    [SerializeField] private AIProfileDefinition perfilEstudiando;
    [Tooltip("Desde que toma el control (fase 2 en adelante).")]
    [SerializeField] private AIProfileDefinition perfilEnSerio;

    [Header("Nestor de ayudante")]
    [Tooltip("Mas presente que en el casino: en esta pelea tiene que notarse que esta.")]
    [SerializeField, Range(0f, 1f)] private float nestorProbabilidad = 0.45f;
    [SerializeField, Min(0)] private int nestorCooldownTurnos = 2;
    [SerializeField, Min(0)] private int nestorTurnosDeGracia = 1;
    [SerializeField, Min(1)] private int nestorMaximoPorCombate = 3;

    [Header("La lectura de Julieta (el protocolo de Nestor, dado vuelta)")]
    [Tooltip("Es la Prediccion de la torre 1: anula una categoria cada N turnos.")]
    [SerializeField, Min(2)] private int lecturaCadaTurnos = 3;
    [Tooltip("Avisa un turno antes, como con las camaras cortadas.")]
    [SerializeField] private bool lecturaConAviso = true;
    [SerializeField] private string nombreDeLaLectora = "Julieta";

    [Header("El Pokemon controlado y la vida del PJ")]
    [SerializeField, Min(1)] private int vidaDelPJ = 100;
    [SerializeField, Range(0f, 1f)] private float probabilidadEnElCampo = 0.6f;
    [SerializeField, Range(0f, 1f)] private float probabilidadFueraDeOrden = 0.35f;
    [Tooltip("Golpe al PJ en fraccion de su vida: esto + potencia/1000.")]
    [SerializeField, Range(0.02f, 0.6f)] private float fraccionDelGolpe = 0.16f;

    [Header("Final")]
    [SerializeField] private StoryFlag flagVencida = StoryFlag.Julieta_Vencida;
    [Tooltip("Lo enciende RomperServidor: el ApagonDelNivel de la arena lo escucha y apaga las luces.")]
    [SerializeField] private StoryFlag flagServidorDestruido = StoryFlag.Historia_ServidorPrincipalDestruido;
    [Tooltip("Despues de la cueva (ver FinalDelJuego). Si la cueva no esta en Build Settings, se va directo.")]
    [SerializeField] private string escenaDeCreditos = "CreditosFinales";
    [SerializeField, Min(0.05f)] private float segundosDeFundido = 1.5f;
    [Tooltip("Si pierde, el equipo vuelve a como entro a la pelea. La arena no " +
             "tiene enfermeria: sin esto, perder deja al jugador sin poder reintentar.")]
    [SerializeField] private bool restaurarEquipoAlPerder = true;
    [Tooltip("Despues de perder, ofrecer volver a la Quarcho Cueva a curarse, comprar y " +
             "reacomodar el equipo (playtest del 5-oct). Se vuelve por el camino de siempre.")]
    [SerializeField] private bool ofrecerVolverALaCueva = true;
    [SerializeField, TextArea(2, 3)] private string preguntaVolverALaCueva =
        "¿Volvés a la Quarcho Cueva a prepararte? Tixi te puede traer de nuevo hasta acá.";
    [SerializeField] private string opcionVolverALaCueva = "Volver";
    [SerializeField] private string opcionQuedarse = "Reintentar";

    [Header("Musica (sale del CatalogoDeAudio)")]
    [Tooltip("Desde que fase suena el tema final. Ruptura = recien cuando Julieta " +
             "puede caer; ControlDelVinculo = desde que controla un Pokemon del jugador. " +
             "La fase 1 tiene su tema y las del medio comparten el de la fase 2.")]
    [SerializeField] private FaseDeJulieta faseDeLaMusicaFinal = FaseDeJulieta.Ruptura;

    [Header("Presentacion")]
    [SerializeField] private Color colorDeJulieta = new Color(0.72f, 0.3f, 1f);
    [SerializeField] private string cartelDeControl = "NEURARUSH-LINK · EL SISTEMA ES DE JULIETA";
    [Tooltip("Fuente de la UI de prueba (vida del PJ, señal). Vacio = la de Unity.")]
    [SerializeField] private Font fuente;

    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private DialogueController dialogueController;

    // ---------- estado de la pelea ----------

    private readonly EstadoDePeleaJulieta _estado = new EstadoDePeleaJulieta();
    private readonly VinculoControlado _vinculo = new VinculoControlado();
    private VidaDelPJ _vida;
    private GuionEnCurso _guionEnCurso;
    private CasinoRuletaDeCombate _ruleta;
    private readonly IBattleRng _rng = new UnityRng();

    private PlayerModel _jugador;
    private readonly List<FotoDePoken> _fotoDelEquipo = new List<FotoDePoken>();
    private bool _peleando;
    private bool _reiniciando;
    private bool _conversando;
    private bool _elControladoYaActuo;
    private bool _salvadoAnunciado;
    private bool _armado = true;
    // Ya escucho la charla entera de antes de pelear: los reintentos van cortos.
    private bool _yaSeEncontraron;

    // El final va de a tramos (ver GuionDeJulieta): en que punto esta.
    public enum EtapaDelFinal { Ninguna, VaAlServidor, LoDestruye, HablaConJulieta, Terminado }
    private EtapaDelFinal _final = EtapaDelFinal.Ninguna;
    public EtapaDelFinal EtapaFinal => _final;
    private System.Action _pendiente;
    private LienzoDePeleaJulieta _lienzo;

    // Para el banco de pruebas y los tests.
    public event System.Action<FaseDeJulieta> FaseCambiada;
    public event System.Action<string> MomentoSono;

    public FaseDeJulieta Fase => _estado.Fase;
    public EstadoDePeleaJulieta Estado => _estado;
    public VinculoControlado Vinculo => _vinculo;
    public VidaDelPJ Vida => _vida;
    public bool Peleando => _peleando;
    public bool Conversando => _conversando;
    public GuionDeJulieta Guion => guion;
    public EnemyDefinition Julieta => julieta;
    public LienzoDePeleaJulieta Lienzo => _lienzo;
    public BattleController Combate => battleController;

    private static GameInputService Input => GameInputService.Instance;

    // ---------- ciclo de vida ----------

    private void Awake()
    {
        // Include: el prefab de combate esta DESACTIVADO hasta que arranca la pelea.
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _vida = new VidaDelPJ(vidaDelPJ);
    }

    private void OnEnable()
    {
        if (_pendiente != null) StartCoroutine(CorrerPendiente());
    }

    // Apagado a mitad de una charla, la corrutina muere sin llegar al Soltar
    // y el jugador quedaria frenado para siempre.
    private void OnDisable() => PlayerInputController.Soltar(this);

    private void OnDestroy()
    {
        if (battleController != null) battleController.BattleEnded -= AlTerminarElCombate;
        if (_lienzo != null) Destroy(_lienzo.gameObject);
    }

    // ---------- encuentro ----------

    // Hablarle con X.
    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (_peleando || _conversando) return false;
        Encarar(player, story);
        return true;
    }

    // Pisar la zona de delante (ZonaDePeleaJulieta). Queda desarmada hasta que
    // el jugador sale: al volver de la pelea sigue parado adentro, y Unity le
    // vuelve a mandar el OnTriggerEnter2D (mismo cuidado que el jefe del casino).
    public void AlPisarLaZona()
    {
        if (!_armado || _peleando || _conversando) return;
        // Despues de ganarle, la zona ya no arranca nada: el final se juega
        // tocando el servidor y hablandole a Julieta.
        if (_final != EtapaDelFinal.Ninguna) return;
        _armado = false;
        GameManager gm = GameManager.Instance;
        Encarar(gm != null ? gm.CurrentPlayer : null, gm != null ? gm.Story : null);
    }

    public void AlSalirDeLaZona() => _armado = true;

    private void Encarar(PlayerModel player, StoryModel story)
    {
        if (player == null)
        {
            Debug.LogWarning("[Julieta] No hay partida cargada.");
            return;
        }

        if (_final == EtapaDelFinal.HablaConJulieta)
        {
            StartCoroutine(DecirYSeguir(guion != null ? guion.conJulietaEnElSuelo : null,
                                        () => SeguirElFinal(EtapaDelFinal.Terminado)));
            return;
        }

        if (story != null && flagVencida != StoryFlag.None && story.HasFlag(flagVencida))
        {
            StartCoroutine(DecirYSeguir(guion != null ? guion.yaVencida : null, null));
            return;
        }

        if (!player.HasAlivePoken)
        {
            StartCoroutine(DecirYSeguir(new[] { new LineaDeJulieta(HablanteDeJulieta.Julieta,
                "Así no, {PJ}. Volvé cuando tu equipo pueda pararse.") }, null));
            return;
        }

        LineaDeJulieta[] charla = guion == null ? null
            : _yaSeEncontraron && guion.alReintentar != null && guion.alReintentar.Length > 0 ? guion.alReintentar
            : guion.alEncontrarla;
        StartCoroutine(DecirYSeguir(charla, () =>
        {
            _yaSeEncontraron = true;
            Lanzar(player);
        }));
    }

    // Arranca la pelea de cero. Publico para el banco de pruebas.
    public bool Lanzar(PlayerModel player)
    {
        if (julieta == null) { Debug.LogError("[Julieta] Falta el EnemyDefinition de Julieta."); return false; }
        if (battleController == null) { Debug.LogError("[Julieta] No hay BattleController en la escena."); return false; }
        if (player == null || !player.HasAlivePoken) return false;

        _jugador = player;
        SacarFotoDelEquipo(player);
        PrepararDesdeCero();
        AsegurarLienzo();

        _peleando = true;
        battleController.BattleEnded -= AlTerminarElCombate;
        battleController.BattleEnded += AlTerminarElCombate;
        SonarLaMusicaDeLaFase(FaseDeJulieta.Analisis);
        battleController.StartBattle(player, julieta, director: this);

        // StartBattle aborta callado si algun equipo no tiene a nadie en pie.
        if (!battleController.EnCurso)
        {
            battleController.BattleEnded -= AlTerminarElCombate;
            _peleando = false;
            return false;
        }

        Debug.Log($"[Julieta] Empieza la pelea: {player.PlayerName} contra {julieta.characterName}.");
        return true;
    }

    // Vuelve todo al principio: corta la pelea si esta en curso, devuelve el
    // equipo a como entro, borra fases, contadores y efectos. Para volver a
    // probar la pelea desde cero sin salir de Play.
    public void Reiniciar()
    {
        dialogueController?.Cancelar();
        _conversando = false;
        _pendiente = null;

        if (battleController != null && battleController.EnCurso && _peleando)
        {
            // Abandonar pasa por el cierre normal: el mapa vuelve y
            // AlTerminarElCombate restaura el equipo (ve _reiniciando).
            _reiniciando = true;
            battleController.Abandonar();
        }
        else
        {
            RestaurarEquipo();
        }

        PrepararDesdeCero();
        _armado = true;
        _yaSeEncontraron = false;
        _final = EtapaDelFinal.Ninguna;
        GetComponentInChildren<ServidorPrincipal>(true)?.Reparar();

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (story != null && flagVencida != StoryFlag.None) story.ActiveFlags.Remove(flagVencida);
        if (story != null && flagServidorDestruido != StoryFlag.None) story.ActiveFlags.Remove(flagServidorDestruido);
        Debug.Log("[Julieta] Pelea reiniciada desde cero.");
    }

    // Para probar sin jugar veinte turnos: la fase actual se da por cumplida y
    // en el proximo turno suena lo que le faltaba, cambio de fase incluido.
    public void AdelantarFase()
    {
        if (!_peleando) return;
        _estado.AdelantarTurnosDeLaFase(50);
        Debug.Log($"[Julieta] (prueba) Se adelanta la fase {_estado.Fase}: cambia en el proximo turno.");
    }

    private void PrepararDesdeCero()
    {
        _estado.Reiniciar();
        _guionEnCurso = new GuionEnCurso(guion != null ? guion.momentos : null);
        _vinculo.Reiniciar();
        _vinculo.ProbabilidadEnElCampo = probabilidadEnElCampo;
        _vinculo.ProbabilidadFueraDeOrden = probabilidadFueraDeOrden;
        _vinculo.FraccionBase = fraccionDelGolpe;
        if (_vida == null) _vida = new VidaDelPJ(vidaDelPJ);
        else _vida.Reiniciar(vidaDelPJ);
        _ruleta = null;
        _elControladoYaActuo = false;
        _salvadoAnunciado = false;
        if (_lienzo != null)
        {
            _lienzo.Vida.Mostrar(false);
            _lienzo.Senal.Apagar();
        }
    }

    // ---------- ganchos del combate (IDirectorDeCombate) ----------

    public IEnumerator AlEmpezar(BattleController combate)
    {
        _estado.Mirar(combate.Modelo);
        yield return Chequear(combate);
        _estado.MarcarEmpezado();
    }

    public IEnumerator AntesDeElegir(BattleController combate)
    {
        BattleModel m = combate.Modelo;
        if (m == null || m.IsBattleOver) yield break;

        // El controlado cayo (lo debilito Julieta, o el mismo al forcejear): el
        // chip se apaga con el.
        if (_vinculo.Poken != null && _vinculo.Poken.IsFainted)
        {
            string nombre = _vinculo.Poken.Definition.pokenName;
            _vinculo.Liberar();
            _estado.ControladoCayo = true;
            combate.Anunciar($"El NeuraRush-LINK se apaga con {nombre}.");
            yield return Pausa(combate);
        }

        _estado.Mirar(m);
        yield return Chequear(combate);
    }

    public IEnumerator AntesDeResolver(BattleController combate, BattleAction accion,
                                       System.Action<BattleAction> reemplazar)
    {
        _elControladoYaActuo = false;
        BattleModel m = combate.Modelo;
        if (m == null || !_vinculo.Activo || accion == null) yield break;

        bool enElCampo = m.ActivePlayerPoken == _vinculo.Poken;
        bool pidioPelear = accion.Kind == BattleActionKind.UseMove;   // incluye Forcejeo
        if (!enElCampo || !pidioPelear) yield break;          // lo ve DespuesDelTurno
        if (TurnEngine.AccionForzada(m.PlayerState) != null) yield break;   // cargando: sigue solo

        _elControladoYaActuo = true;
        DecisionDelControlado d = _vinculo.DecidirEnElCampo(_rng, _vida.Maxima);
        string nombre = _vinculo.Poken.Definition.pokenName;

        combate.Anunciar($"¡{nombre} no te obedece!");
        yield return combate.Destello(true, colorDeJulieta, 2);
        yield return Pausa(combate, 0.6f);

        switch (d.Que)
        {
            case QueHaceElControlado.AtacaAlPJ:
                yield return GolpeAlPJ(combate, d, desdeElBanco: false);
                reemplazar(BattleAction.Wait());
                break;

            case QueHaceElControlado.AtacaAlRival:
                combate.Anunciar($"{nombre} elige su propio ataque.");
                yield return Pausa(combate, 0.7f);
                reemplazar(d.Movimiento >= 0 ? BattleAction.Move(d.Movimiento) : BattleAction.Struggle());
                break;

            default:
                combate.Anunciar($"{nombre} se queda quieto, mirando a Julieta.");
                yield return Pausa(combate);
                reemplazar(BattleAction.Wait());
                break;
        }
    }

    public IEnumerator DespuesDelTurno(BattleController combate, List<BattleEvent> eventos)
    {
        BattleModel m = combate.Modelo;
        if (m == null || m.IsBattleOver) yield break;

        if (_estado.RivalSalvado && !_salvadoAnunciado)
        {
            _salvadoAnunciado = true;
            combate.Anunciar($"¡{m.ActiveEnemyPoken.Definition.pokenName} queda en pie con 1 PS: " +
                             "el NeuraRush-LINK no lo deja caer!");
            yield return combate.Destello(false, colorDeJulieta, 2);
            yield return Pausa(combate);
        }

        if (!_vinculo.Activo || _elControladoYaActuo) yield break;

        DecisionDelControlado d = _vinculo.DecidirFueraDeOrden(_rng, _vida.Maxima);
        if (d.Que != QueHaceElControlado.AtacaAlPJ) yield break;

        bool desdeElBanco = m.ActivePlayerPoken != _vinculo.Poken;
        yield return GolpeAlPJ(combate, d, desdeElBanco);
    }

    public bool ImpedirQueCaiga(BattleModel modelo)
    {
        // Julieta no pierde mientras tenga el control: su ultimo Pokemon queda en
        // 1 PS hasta la ruptura. Eso ademas dispara la ruptura (RivalSalvado),
        // asi la pelea nunca queda con un rival inmortal.
        if (_estado.Fase == FaseDeJulieta.Ruptura) return false;
        if (!BattleTeamRules.EsElUltimo(modelo.Enemy.Team, modelo.ActiveEnemyIndex)) return false;
        _estado.RivalSalvado = true;
        return true;
    }

    public void NestorIntervino() => _estado.LecturasDeNestor++;

    public void RuletaCobro(EfectoDeRuleta efecto, bool alJugador) => _estado.ContarGiro();

    public void AlTerminar(bool victoriaDelJugador)
    {
        ApagarEfectos(battleController);
        // Al ganar, el tema del final entra con el cartel de victoria y
        // acompaña el dialogo hasta el fundido a los creditos.
        if (victoriaDelJugador && !_reiniciando)
            MusicaDelJuego.Sonar(CatalogoDeAudio.Actual?.julietaDespues, true, 1.5f);
    }

    // ---------- el guion ----------

    // Hace sonar todos los momentos que se cumplen ahora, en orden. Un momento
    // puede cambiar la fase y habilitar otros: por eso vuelve a preguntar.
    private IEnumerator Chequear(BattleController combate)
    {
        if (_guionEnCurso == null) yield break;

        for (int vueltas = 0; vueltas < 16; vueltas++)
        {
            MomentoDeJulieta momento = _guionEnCurso.Siguiente(_estado);
            if (momento == null) yield break;

            Debug.Log($"[Julieta] Momento '{momento.nombre}' — {_estado.Resumen()}");
            MomentoSono?.Invoke(momento.nombre);
            yield return Reproducir(momento.lineas, combate);

            BattleModel m = combate != null ? combate.Modelo : null;
            if (m == null || m.IsBattleOver) yield break;
            _estado.Mirar(m);
        }
    }

    private IEnumerator Reproducir(LineaDeJulieta[] lineas, BattleController combate)
    {
        if (lineas == null) yield break;

        foreach (LineaDeJulieta linea in lineas)
        {
            if (linea == null) continue;
            if (!string.IsNullOrEmpty(linea.texto)) yield return Decir(linea, combate);
            if (combate != null && (combate.Modelo == null || combate.Modelo.IsBattleOver)) yield break;
            yield return Ejecutar(linea.accion, linea.parametro, combate);
        }
    }

    // Una linea en la caja de dialogo, esperando a que el jugador la cierre.
    //
    // Durante el combate el jugador (y su PlayerInputController, el que le pasa
    // las teclas a la caja) esta apagado junto con el mapa: en ese caso la
    // tecla de aceptar la lee esto mismo. Si el PlayerInputController esta
    // activo (en el mapa), no se toca nada: si leyeran los dos, una tecla se
    // saltearia dos lineas.
    private IEnumerator Decir(LineaDeJulieta linea, BattleController combate)
    {
        string nombre = NombreDe(linea.hablante);
        string texto = Completar(linea.texto);
        Sprite retrato = linea.retrato != null ? linea.retrato : RetratoDe(linea.hablante);

        if (dialogueController == null)
        {
            // Sin caja: por la barra de mensajes del combate, con tiempo para leer.
            string conNombre = string.IsNullOrEmpty(nombre) ? texto : $"{nombre}: {texto}";
            if (combate != null && combate.EnCurso) combate.Anunciar(conNombre);
            else Debug.Log($"[Julieta] {conNombre}");
            yield return new WaitForSeconds(2f);
            yield break;
        }

        if (linea.sonido != null) AudioService.Instance.PlaySFX(linea.sonido);

        bool cerrada = false;
        dialogueController.StartMessage(nombre, retrato, new[] { texto }, () => cerrada = true);

        while (!cerrada)
        {
            // La caja se pudo cerrar sin callback (Reiniciar la cancela).
            if (!dialogueController.IsOpen) yield break;

            if (!HayQuienLeaElInput() && Input.WasPressed(GameAction.Confirm))
            {
                Input.Consume(GameAction.Confirm);
                dialogueController.OnConfirm();
            }
            yield return null;
        }
    }

    private static bool HayQuienLeaElInput()
        => FindAnyObjectByType<PlayerInputController>() != null;

    // Lo que dispara una linea al cerrarse.
    private IEnumerator Ejecutar(AccionDeJulieta accion, int parametro, BattleController combate)
    {
        if (accion == AccionDeJulieta.Ninguna) yield break;
        bool enCombate = combate != null && combate.EnCurso;
        Debug.Log($"[Julieta] Accion {accion} ({parametro}).");

        switch (accion)
        {
            case AccionDeJulieta.PasarAFase:
                CambiarDeFase((FaseDeJulieta)parametro);
                break;

            case AccionDeJulieta.EncenderRuleta:
                if (!enCombate) break;
                if (_ruleta == null)
                {
                    if (ruletaDelCasino == null || ruletaDelCasino.premios == null || ruletaDelCasino.premios.Length == 0)
                    {
                        Debug.LogWarning("[Julieta] No hay ruleta del casino asignada: la pelea sigue sin ella.");
                        break;
                    }
                    _ruleta = new CasinoRuletaDeCombate(ruletaDelCasino.premios, turnosEntreGiros)
                    {
                        Cartel = CartelDeLaRuleta
                    };
                }
                _ruleta.QuienCobra = (QuienCobraLaRuleta)parametro;
                combate.PonerRuleta(_ruleta);
                break;

            case AccionDeJulieta.ApagarRuleta:
                if (enCombate) combate.PonerRuleta(null);
                break;

            case AccionDeJulieta.EncenderNestor:
                if (enCombate) combate.PonerLecturaAliada(new NestorPredictionSystem
                {
                    Probabilidad = nestorProbabilidad,
                    CooldownTurnos = nestorCooldownTurnos,
                    TurnosDeGracia = nestorTurnosDeGracia,
                    MaximoPorCombate = nestorMaximoPorCombate
                });
                break;

            case AccionDeJulieta.ApagarNestor:
                if (enCombate) combate.PonerLecturaAliada(null);
                break;

            case AccionDeJulieta.EncenderLecturaDeJulieta:
                if (enCombate) combate.PonerPrediccion(
                    new PredictionState(lecturaConAviso, lecturaCadaTurnos) { Lector = nombreDeLaLectora });
                break;

            case AccionDeJulieta.ApagarLecturaDeJulieta:
                if (enCombate) combate.PonerPrediccion(null);
                break;

            case AccionDeJulieta.ControlarPokemon:
                if (enCombate) yield return Controlar(combate);
                break;

            case AccionDeJulieta.LiberarPokemon:
                if (enCombate) yield return Liberar(combate);
                break;

            case AccionDeJulieta.MostrarVidaDelPJ:
                AsegurarLienzo();
                _lienzo.Vida.Poner(NombreDelPJ(), _vida.Actual, _vida.Maxima);
                _lienzo.Vida.Mostrar(parametro != 0);
                break;

            case AccionDeJulieta.PerfilDeIA:
                if (enCombate) combate.CambiarPerfilDeIA(parametro == 0 ? perfilEstudiando : perfilEnSerio);
                break;

            case AccionDeJulieta.SenalDeControl:
                AsegurarLienzo();
                if (parametro != 0) _lienzo.Senal.Encender(cartelDeControl);
                else _lienzo.Senal.Apagar();
                break;

            case AccionDeJulieta.Destello:
                AsegurarLienzo();
                yield return _lienzo.Senal.Destello();
                break;

            case AccionDeJulieta.RomperServidor:
                GetComponentInChildren<ServidorPrincipal>(true)?.Romper();
                // Las luces de la sala las apaga el ApagonDelNivel de la arena,
                // que mira este flag.
                StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
                if (story != null && flagServidorDestruido != StoryFlag.None) story.ActivateFlag(flagServidorDestruido);
                AsegurarLienzo();
                yield return _lienzo.Senal.Destello(1f);
                break;
        }
    }

    private void CambiarDeFase(FaseDeJulieta fase)
    {
        if (_estado.Fase == fase) return;
        _estado.CambiarDeFase(fase);
        Debug.Log($"[Julieta] === FASE: {fase} === ({_estado.Resumen()})");
        SonarLaMusicaDeLaFase(fase);
        FaseCambiada?.Invoke(fase);
    }

    // Fase 1 con su tema, las del medio con el de la fase 2 y, desde
    // 'faseDeLaMusicaFinal', el final. Pedir el que ya suena no lo reinicia.
    private void SonarLaMusicaDeLaFase(FaseDeJulieta fase)
    {
        CatalogoDeAudio c = CatalogoDeAudio.Actual;
        if (c == null) return;
        Sonido tema = fase >= faseDeLaMusicaFinal ? c.julietaFinal
                    : fase == FaseDeJulieta.Analisis ? c.julietaFase1
                    : c.julietaFase2;
        MusicaDelJuego.Sonar(tema, true, 1.2f);
    }

    // ---------- el Pokemon controlado ----------

    private IEnumerator Controlar(BattleController combate)
    {
        BattleModel m = combate.Modelo;
        int indice = VinculoControlado.ElegirVictima(m);
        if (indice < 0) yield break;

        PokenModel poken = m.Player.Team[indice];
        _vinculo.Controlar(poken);
        _estado.ControladoCayo = false;
        string nombre = poken.Definition.pokenName;

        AsegurarLienzo();
        yield return _lienzo.Senal.Destello();
        if (indice == m.ActivePlayerIndex) yield return combate.Destello(true, colorDeJulieta, 3);

        combate.Anunciar($"¡El NeuraRush-LINK se conecta con {nombre}!");
        yield return Pausa(combate);
        combate.Anunciar($"{nombre} sigue en tu equipo... pero ya no responde a tus órdenes.");
        yield return Pausa(combate, 1.3f);
    }

    private IEnumerator Liberar(BattleController combate)
    {
        if (_vinculo.Poken == null) yield break;

        PokenModel poken = _vinculo.Poken;
        bool enElCampo = combate.Modelo.ActivePlayerPoken == poken;
        _vinculo.Liberar();

        if (!poken.IsFainted)
        {
            if (enElCampo) yield return combate.Destello(true, BattleFxView.PulseHeal, 2);
            combate.Anunciar($"{poken.Definition.pokenName} vuelve a ser tuyo.");
            yield return Pausa(combate);
        }
    }

    private IEnumerator GolpeAlPJ(BattleController combate, DecisionDelControlado d, bool desdeElBanco)
    {
        string nombre = _vinculo.Poken.Definition.pokenName;
        string pj = NombreDelPJ();
        string movimiento = d.Definicion != null ? d.Definicion.moveName : "un golpe";

        _vinculo.ContarAtaque();
        _estado.AtaquesAlPJ = _vinculo.AtaquesAlPJ;

        combate.Anunciar(desdeElBanco
            ? $"¡{nombre} rompe su QuerryBola y usa {movimiento} contra {pj}!"
            : $"¡{nombre} se da vuelta y usa {movimiento} contra {pj}!");
        if (!desdeElBanco) yield return combate.Destello(true, colorDeJulieta, 2);
        else yield return Pausa(combate, 0.5f);

        AsegurarLienzo();
        if (!_lienzo.Vida.Visible)
        {
            _lienzo.Vida.Poner(pj, _vida.Actual, _vida.Maxima);
            _lienzo.Vida.Mostrar(true);
        }

        int antes = _vida.Actual;
        int real = _vida.Recibir(d.Dano);
        _estado.VidaDelPJPorcentaje = _vida.Porcentaje;
        StartCoroutineSeguro(_lienzo.Senal.Destello(0.35f));
        yield return _lienzo.Vida.Golpe(antes, _vida.Actual);

        combate.Anunciar($"{pj} recibe {real} de daño.");
        yield return Pausa(combate);

        if (!_vida.Caido) yield break;

        combate.Anunciar($"¡{pj} no puede más y cae de rodillas!");
        yield return Pausa(combate, 1.4f);
        // El combate lo cierra como derrota apenas vuelve este gancho.
        combate.Modelo.Finish(playerVictory: false);
    }

    // El lienzo vive fuera del mapa, asi que corre sus propios efectos aunque
    // este objeto este apagado.
    private void StartCoroutineSeguro(IEnumerator rutina)
    {
        if (_lienzo != null && _lienzo.isActiveAndEnabled) _lienzo.StartCoroutine(rutina);
    }

    // ---------- fin de la pelea ----------

    // Deja el combate como si Julieta nunca hubiera metido mano: sin ruleta,
    // sin Nestor, sin lectura, sin Pokemon controlado y sin señal. Corre al
    // terminar la pelea (gane quien gane) y al reiniciar.
    private void ApagarEfectos(BattleController combate)
    {
        if (combate != null && combate.EnCurso)
        {
            combate.PonerRuleta(null);
            combate.PonerLecturaAliada(null);
            combate.PonerPrediccion(null);
        }
        _vinculo.Liberar();
        if (_lienzo != null)
        {
            _lienzo.Vida.Mostrar(false);
            _lienzo.Senal.Apagar();
        }
    }

    private void AlTerminarElCombate(BattleOutcome outcome)
    {
        if (battleController != null) battleController.BattleEnded -= AlTerminarElCombate;
        _peleando = false;
        ApagarEfectos(null);

        if (_reiniciando)
        {
            _reiniciando = false;
            RestaurarEquipo();
            MusicaDelJuego.VolverALaDeLaEscena();
            return;
        }

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;

        if (outcome != null && outcome.PlayerVictory)
        {
            if (story != null && flagVencida != StoryFlag.None) story.ActivateFlag(flagVencida);
            // NO se guarda (pedido de Augusto, 2-oct-2026): el juego termina en
            // los creditos y vuelve a la pantalla de inicio, sin post-game. La
            // partida queda como estaba al entrar al ultimo nivel, asi que
            // "Continuar" lleva otra vez a las puertas del final.
            Debug.Log("[Julieta] VENCIDA. El final, de a tramos: el servidor, Julieta, la cueva y los creditos.");
            AlVolverAlMapa(() => StartCoroutine(DecirYSeguir(guion != null ? guion.finalDelJuego : null,
                                                             () => SeguirElFinal(EtapaDelFinal.VaAlServidor))));
            return;
        }

        if (restaurarEquipoAlPerder) RestaurarEquipo();
        MusicaDelJuego.VolverALaDeLaEscena();
        Debug.Log("[Julieta] El jugador perdio. Se puede reintentar o volver a la cueva.");
        AlVolverAlMapa(() => StartCoroutine(DecirYSeguir(guion != null ? guion.alPerder : null, OfrecerVolverALaCueva)));
    }

    // Perder contra Julieta no deja al jugador encerrado en la arena con lo que
    // traia (playtest del 5-oct): puede volver a la cueva, donde la enfermera
    // cura gratis, Tixi vende y Nestor enseña movimientos. La partida se guarda
    // sin el flag de Julieta, asi que Tixi lo vuelve a mandar al ultimo nivel.
    // "Reintentar" deja todo como antes: se le vuelve a hablar a Julieta.
    private void OfrecerVolverALaCueva()
    {
        if (!ofrecerVolverALaCueva) return;
        string lobby = RecorridoDeLaHistoria.EscenaDelLobby;
        if (!RecorridoDeLaHistoria.EstaEnBuildSettings(lobby)) return;

        _conversando = true;
        CasinoDialogo.Preguntar(dialogueController, "", null, preguntaVolverALaCueva,
            opcionVolverALaCueva, opcionQuedarse, volver =>
            {
                // Si vuelve, queda "conversando" durante el fundido: que nadie
                // arranque la pelea mientras se carga la cueva.
                if (!volver) { _conversando = false; return; }
                Debug.Log($"[Julieta] Vuelve a {lobby} a reequiparse.");
                CasinoDialogo.Guardar();
                Sonidos.Teletransporte();
                FundidoAEscena.Ir(lobby, segundosDeFundido);
            });
    }

    // ---------- el final, de a tramos ----------

    // Tocar el servidor con X (lo llama ServidorPrincipal). Antes de ganarle a
    // Julieta no hace nada.
    public bool TocarElServidor(PlayerModel player, StoryModel story)
    {
        if (_peleando || _conversando) return false;
        switch (_final)
        {
            case EtapaDelFinal.VaAlServidor:
                StartCoroutine(DecirYSeguir(guion != null ? guion.enElServidor : null,
                                            () => SeguirElFinal(EtapaDelFinal.LoDestruye)));
                return true;
            case EtapaDelFinal.LoDestruye:
                StartCoroutine(DecirYSeguir(guion != null ? guion.alDestruirlo : null,
                                            () => SeguirElFinal(EtapaDelFinal.HablaConJulieta)));
                return true;
            case EtapaDelFinal.HablaConJulieta:
                StartCoroutine(DecirYSeguir(new[] { new LineaDeJulieta(HablanteDeJulieta.Sistema,
                    "El núcleo está destruido. Julieta sigue en el suelo.") }, null));
                return true;
            default:
                return false;
        }
    }

    // Pasa a la etapa del final. Un tramo vacio en el guion se saltea.
    private void SeguirElFinal(EtapaDelFinal etapa)
    {
        _final = etapa;
        Debug.Log($"[Julieta] Final: {etapa}.");
        switch (etapa)
        {
            case EtapaDelFinal.VaAlServidor:
                if (Vacio(guion?.enElServidor)) SeguirElFinal(EtapaDelFinal.LoDestruye);
                break;
            case EtapaDelFinal.LoDestruye:
                if (Vacio(guion?.alDestruirlo))
                {
                    StartCoroutine(Ejecutar(AccionDeJulieta.RomperServidor, 0, null));
                    SeguirElFinal(EtapaDelFinal.HablaConJulieta);
                }
                break;
            case EtapaDelFinal.HablaConJulieta:
                if (Vacio(guion?.conJulietaEnElSuelo)) SeguirElFinal(EtapaDelFinal.Terminado);
                break;
            case EtapaDelFinal.Terminado:
                IrAlFinal();
                break;
        }
    }

    private static bool Vacio(LineaDeJulieta[] lineas)
    {
        if (lineas == null) return true;
        foreach (LineaDeJulieta l in lineas)
            if (l != null && (!string.IsNullOrEmpty(l.texto) || l.accion != AccionDeJulieta.Ninguna)) return false;
        return true;
    }

    // Fundido a la Quarcho Cueva: el epilogo, la charla con Tixi y la ultima
    // conexion (FinalDelJuego), y de ahi a los creditos.
    private void IrAlFinal()
    {
        if (!string.IsNullOrEmpty(escenaDeCreditos) && Application.CanStreamedLevelBeLoaded(escenaDeCreditos))
        {
            FinalDelJuego.Empezar(escenaDeCreditos, segundosDeFundido);
            return;
        }

        Debug.LogError($"[Julieta] La escena de creditos '{escenaDeCreditos}' no esta en Build Settings. " +
                       "Corre PokeRush > Jefe final (Julieta) > Armar la escena de créditos.");
        dialogueController?.StartMessage("", null, new[] { "FIN." });
    }

    // Corre 'accion' cuando el mapa este de vuelta: durante la pelea este objeto
    // esta apagado (vive en MapaRaiz) y una caja abierta quedaria tapada por el
    // combate. Mismo esquema que CasinoNpcConCombate.
    private void AlVolverAlMapa(System.Action accion)
    {
        _pendiente = accion;
        if (isActiveAndEnabled) StartCoroutine(CorrerPendiente());
    }

    private IEnumerator CorrerPendiente()
    {
        yield return null;

        // Si este objeto no estaba adentro del mapa, igual espera a que el
        // combate se esconda (MapBattleBridge lo hace unos instantes despues).
        float limite = Time.unscaledTime + 3f;
        while (battleController != null && battleController.gameObject.activeInHierarchy
               && Time.unscaledTime < limite)
            yield return null;

        System.Action accion = _pendiente;
        _pendiente = null;
        accion?.Invoke();
    }

    private IEnumerator DecirYSeguir(LineaDeJulieta[] lineas, System.Action despues)
    {
        _conversando = true;
        // Cada linea es su propia caja: entre que X cierra una y se abre la
        // siguiente (o mientras corre la accion de la linea) el
        // PlayerInputController ve el dialogo cerrado y deja caminar. Pasaba en
        // cada linea (bug 4 del testing). Frenado no corta el input de la caja.
        PlayerInputController.Frenar(this);
        yield return Reproducir(lineas, null);
        PlayerInputController.Soltar(this);
        _conversando = false;
        despues?.Invoke();
    }

    // ---------- el equipo ----------

    private struct FotoDePoken
    {
        public PokenModel Poken;
        public int Ps;
        public StatusEffect Estado;
        public int[] Pp;
    }

    private void SacarFotoDelEquipo(PlayerModel player)
    {
        _fotoDelEquipo.Clear();
        foreach (PokenModel p in player.Team)
        {
            if (p == null) continue;
            var pp = new int[p.Moves.Count];
            for (int i = 0; i < pp.Length; i++) pp[i] = p.Moves[i].CurrentPP;
            _fotoDelEquipo.Add(new FotoDePoken { Poken = p, Ps = p.CurrentHP, Estado = p.CurrentStatus, Pp = pp });
        }
    }

    private void RestaurarEquipo()
    {
        foreach (FotoDePoken f in _fotoDelEquipo)
        {
            if (f.Poken == null) continue;
            f.Poken.SetCurrentHP(f.Ps);
            if (f.Estado == StatusEffect.None) f.Poken.CureStatus();
            else f.Poken.SetStatus(f.Estado);
            for (int i = 0; i < f.Pp.Length && i < f.Poken.Moves.Count; i++)
                f.Poken.Moves[i].SetCurrentPP(f.Pp[i]);
        }
    }

    // ---------- textos ----------

    private string NombreDelPJ()
    {
        PlayerModel p = _jugador ?? (GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null);
        return p != null && !string.IsNullOrEmpty(p.PlayerName) ? p.PlayerName : "PJ";
    }

    private string NombreDe(HablanteDeJulieta quien)
    {
        switch (quien)
        {
            case HablanteDeJulieta.Julieta: return guion != null ? guion.nombreJulieta : "JULIETA";
            case HablanteDeJulieta.Nestor: return guion != null ? guion.nombreNestor : "NÉSTOR";
            case HablanteDeJulieta.Tixi: return guion != null ? guion.nombreTixi : "TIXI";
            case HablanteDeJulieta.PJ: return NombreDelPJ();
            default: return "";
        }
    }

    private Sprite RetratoDe(HablanteDeJulieta quien)
    {
        if (guion == null) return null;
        switch (quien)
        {
            case HablanteDeJulieta.Julieta: return guion.retratoJulieta;
            case HablanteDeJulieta.Nestor: return guion.retratoNestor;
            case HablanteDeJulieta.Tixi: return guion.retratoTixi;
            case HablanteDeJulieta.PJ:
                PlayerModel p = _jugador ?? (GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null);
                return p != null && p.Genero == Genero.Mujer ? guion.retratoPJMujer : guion.retratoPJVaron;
            default: return null;
        }
    }

    private string Completar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        string poken = _vinculo.Poken != null ? _vinculo.Poken.Definition.pokenName : null;
        if (poken == null && battleController != null && battleController.EnCurso)
            poken = battleController.Modelo.ActivePlayerPoken.Definition.pokenName;

        return texto.Replace("{PJ}", NombreDelPJ()).Replace("{POKEMON}", poken ?? "tu Pokémon");
    }

    // El cartel de la ruleta dice a quien le cayo el gajo y quien la maneja.
    private string CartelDeLaRuleta(Prize premio, bool alJugador)
    {
        string gajo = premio != null && !string.IsNullOrEmpty(premio.prizeName)
            ? premio.prizeName.ToUpperInvariant()
            : "???";

        if (premio != null && premio.efectoDeCombate == EfectoDeRuleta.TirarDeNuevo)
            return _ruleta != null && _ruleta.QuienCobra == QuienCobraLaRuleta.AFavorDelRival
                ? "La ruleta marca OTRO GIRO. Julieta vuelve a tirar: la casa quiere más."
                : "La ruleta marca OTRO GIRO. ¡Va de nuevo!";

        bool deJulieta = _ruleta != null && _ruleta.QuienCobra == QuienCobraLaRuleta.AFavorDelRival;
        if (!deJulieta)
            return alJugador
                ? $"La ruleta marca {gajo}. La casa de Reymar ahora juega para vos."
                : $"La ruleta marca {gajo}... y se lo cobra al rival.";

        return alJugador
            ? $"La ruleta marca {gajo}. Julieta te lo cobra a vos."
            : $"La ruleta marca {gajo}. JULIETA REASIGNA EL PREMIO: se lo queda ella.";
    }

    private IEnumerator Pausa(BattleController combate, float factor = 1f)
    {
        float s = combate != null ? combate.PausaDeMensaje : 1f;
        yield return new WaitForSeconds(s * factor);
    }

    private void AsegurarLienzo()
    {
        if (_lienzo == null) _lienzo = LienzoDePeleaJulieta.Crear(fuente, colorDeJulieta);
    }
}
