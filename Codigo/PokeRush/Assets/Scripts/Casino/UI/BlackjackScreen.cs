using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mesa de blackjack del casino (piso 3).
//
// Es una pantalla del MenuManager como el resto, asi hereda la pila, el input
// remapeable y la caja de texto. Mismas dos decisiones que la ruleta, por las
// mismas razones:
//
//   1. El cartel del final sale por la MISMA caja de texto que usa todo el
//      juego (MenuScreenId.MessageBox, via ShowMessage). Va por ahi y no por el
//      DialogueController del mapa porque PlayerInputController corta el input
//      del mundo mientras MenuManager.AnyOpen sea true, asi que una caja del
//      mapa abierta desde aca no recibiria ninguna tecla. Encima del cartel, el
//      resultado tambien se canta con el arte grande de BlackJack God
//      (¡GANASTE! / ¡PERDISTE! / EMPATE / ¡BLACKJACK!), que es lo que se ve
//      primero.
//   2. La plata se descuenta al REPARTIR, no al poner cada ficha. Asi cerrar la
//      mesa con fichas puestas no obliga a devolver nada y no hay forma de
//      perder plata sin haber jugado. Doblar y dividir cobran en el momento,
//      que es cuando el jugador se compromete.
//
// Las reglas (cuanto vale una mano, cuando pide el croupier, que paga cada
// resultado) estan en BlackjackTable, que no sabe nada de Unity y se puede
// probar sin abrir escena.
//
// Sobre los botones: el arte trae PEDIR / PLANTARSE / DOBLAR / DIVIDIR /
// REINICIAR / SALIR y no trae ninguno que diga "repartir". No hace falta: PEDIR
// significa siempre lo mismo, "dame cartas", asi que en la fase de apuestas es
// el que reparte y durante el turno es el que roba. REINICIAR arranca otra
// ronda con la misma apuesta. Para limpiar las fichas puestas se usa Cancelar,
// que ademas es lo que ya hace "volver atras" en el resto del juego.
public class BlackjackScreen : MenuScreen
{
    private enum Fase
    {
        Apostando,
        Repartiendo,
        TurnoJugador,
        TurnoDealer,
        Resultado,
        Cartel
    }

    private enum Zona { Fichas, Botones }

    // Los seis botones, en el orden en que estan dibujados.
    private enum Accion { Pedir, Plantarse, Doblar, Dividir, Reiniciar, Salir }

    private const int Botones = 6;

    // Slots de carta por mano. Con 4 barajas una mano puede llegar a muchas
    // cartas (ocho ases y un dos son 10), asi que hay de sobra; los que no se
    // usan quedan apagados.
    public const int SlotsPorMano = 10;

    [Header("Cartas")]
    [Tooltip("Las 52 cartas, por palo: picas 0-12, corazones 13-25, " +
             "diamantes 26-38, treboles 39-51. Las carga el builder.")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[52];
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private Image[] dealerCards = new Image[SlotsPorMano];
    [SerializeField] private Image[] playerCards = new Image[SlotsPorMano];
    [Tooltip("La segunda mano, la que aparece al dividir.")]
    [SerializeField] private Image[] splitCards = new Image[SlotsPorMano];

    [Header("Reparto")]
    [Tooltip("Desde donde salen volando las cartas: el mazo.")]
    [SerializeField] private RectTransform deck;
    [SerializeField] private RectTransform discard;
    [Tooltip("Centro de la fila del croupier y de la del jugador.")]
    [SerializeField] private Vector2 dealerRowCenter = new Vector2(-90f, 130f);
    [SerializeField] private Vector2 playerRowCenter = new Vector2(-90f, -8f);
    [Tooltip("Cuanto se corren las dos manos al dividir.")]
    [SerializeField] private float splitSpread = 86f;
    [SerializeField] private Vector2 cardSize = new Vector2(42f, 61f);
    [Tooltip("Separacion entre cartas. Si no entran, se apretan solas.")]
    [SerializeField] private float cardStep = 30f;
    [Tooltip("Ancho maximo de una fila de cartas.")]
    [SerializeField] private float rowMaxWidth = 176f;

    [Header("Tiempos")]
    [SerializeField] private float dealSeconds = 0.26f;
    [Tooltip("Espera entre carta y carta al repartir.")]
    [SerializeField] private float dealGap = 0.16f;
    [SerializeField] private float flipSeconds = 0.3f;
    [Tooltip("Lo que se toma el croupier antes de cada carta suya.")]
    [SerializeField] private float dealerThinkSeconds = 0.55f;
    [SerializeField] private float bannerSeconds = 1.15f;

    [Header("Fichas")]
    [SerializeField] private int[] chipValues = { 1, 5, 10, 25, 50, 100, 500, 1000 };
    [SerializeField] private Image[] chipButtons = new Image[8];
    [SerializeField] private Image[] chipGlows = new Image[8];

    [Header("Botones")]
    [Tooltip("PEDIR, PLANTARSE, DOBLAR, DIVIDIR, REINICIAR, SALIR.")]
    [SerializeField] private Image[] actionButtons = new Image[Botones];

    [Header("Croupier")]
    [Tooltip("El NPC que reparte. Se dibuja detras de la mesa, que le tapa las " +
             "piernas: es lo que lo hace ver parado del otro lado.")]
    [SerializeField] private Image dealerSprite;
    [Tooltip("Los cuadros de 'de frente' del NPC. El 0 y el 2 son el parado.")]
    [SerializeField] private Sprite[] dealerFrames = new Sprite[4];
    [Tooltip("Cuanto dura cada cuadro mientras reparte.")]
    [SerializeField] private float dealerFrameSeconds = 0.18f;

    [Header("Carteles")]
    [Tooltip("La placa 'TUS CARTAS'. Se atenua cuando el jugador esta en la otra mano.")]
    [SerializeField] private Image playerPanel;
    [Tooltip("Todo lo de la segunda mano junto: placa, marco y total. Aparece " +
             "solo al dividir, asi que se prende y se apaga de una.")]
    [SerializeField] private GameObject splitGroup;
    [SerializeField] private Image splitPanel;
    [SerializeField] private Text moneyLabel;
    [SerializeField] private Text betLabel;
    [SerializeField] private Text dealerTotalLabel;
    [SerializeField] private Text playerTotalLabel;
    [SerializeField] private Text splitTotalLabel;
    [SerializeField] private Text hintLabel;
    [Tooltip("Marca cual de las dos manos esta jugando el jugador.")]
    [SerializeField] private Image handMarker;

    [Header("Resultado")]
    [SerializeField] private Image resultBanner;
    [Tooltip("BLACKJACK, GANASTE, EMPATE, PERDISTE, en ese orden.")]
    [SerializeField] private Sprite[] resultSprites = new Sprite[4];

    [Header("Limites")]
    [SerializeField] private int minBet = 5;
    [SerializeField] private int maxBet = 5000;

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioClip chipClip;
    [SerializeField] private AudioClip dealClip;
    [SerializeField] private AudioClip flipClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;

    // Una carta en pantalla que todavia se esta acomodando. La animacion se
    // hace aca y no con un Animator porque la posicion final depende de cuantas
    // cartas haya en la fila, y eso recien se sabe al repartirla.
    private struct Vuelo
    {
        public Image Sprite;
        public Vector2 Desde;
        public Vector2 Hasta;
        public float T;          // 0..1
    }

    private readonly BlackjackHand _croupier = new BlackjackHand();
    private readonly List<BlackjackHand> _manos = new List<BlackjackHand>();
    private readonly List<BlackjackCard> _zapato = new List<BlackjackCard>();
    private readonly List<Vuelo> _vuelos = new List<Vuelo>();
    private readonly BlackjackScreenResult _result = new BlackjackScreenResult();

    // A quien le falta recibir carta, en orden. Los valores son los de abajo.
    private readonly Queue<int> _reparto = new Queue<int>();

    private const int AlaMano0 = 0;
    private const int AlaMano1 = 1;
    private const int AlCroupier = 2;

    // Que reparto es el que esta corriendo. El inicial termina mirando si hay
    // blackjack servido; el de un split, devolviendo el turno al jugador.
    private bool _repartoInicial = true;

    private Fase _fase = Fase.Apostando;
    private Zona _zona = Zona.Fichas;
    private int _ficha;
    private int _boton;
    private int _apuesta;
    private int _manoActual;

    private float _espera;         // cuenta atras generica de la fase
    private float _giro;           // 0..1 mientras se da vuelta la tapada
    private float _banner;         // 0..1 del cartel de resultado
    private float _aviso;          // mientras dura, hintLabel muestra el aviso
    private bool _tapadaAbajo = true;
    private int _pagoDeLaRonda;
    private string _cartelPendiente;
    private float _relojDelNpc;    // reloj de la animacion del croupier

    private int Plata => Player != null ? Player.Money : 0;

    // La apuesta de Pokemon de Tacconi (ver BlackjackScreenRequest.ApuestaPokemon):
    // una sola mano, sin fichas, sin doblar ni dividir, y la mesa se cierra sola.
    private BlackjackScreenRequest _pedido;
    private bool ModoPokemon => _pedido != null && !string.IsNullOrEmpty(_pedido.ApuestaPokemon);

    private BlackjackHand Mano =>
        _manoActual >= 0 && _manoActual < _manos.Count ? _manos[_manoActual] : null;

    private int ValorDeFicha => chipValues != null && chipValues.Length > 0
        ? chipValues[Mathf.Clamp(_ficha, 0, chipValues.Length - 1)]
        : 1;

    private int CantidadDeFichas =>
        chipValues != null && chipValues.Length > 0 ? chipValues.Length : 1;

    // ---------------------------------------------------------------- abrir

    protected override void OnOpen(object payload)
    {
        var pedido = payload as BlackjackScreenRequest;
        _pedido = pedido;
        if (pedido != null)
        {
            if (pedido.ApuestaMinima > 0) minBet = pedido.ApuestaMinima;
            if (pedido.ApuestaMaxima > 0) maxBet = pedido.ApuestaMaxima;
        }

        _result.Manos = 0;
        _result.Neto = 0;
        _result.MayorPremio = 0;
        _result.GanoAlgunaMano = false;
        _result.HizoBlackjack = false;
        _result.ResultadoApuestaPokemon = BlackjackOutcome.Pendiente;
        Result = _result;

        RearmarZapato();

        _manos.Clear();
        _manos.Add(new BlackjackHand());
        _croupier.Limpiar();
        _manoActual = 0;

        // La apuesta arranca en la minima que el jugador pueda pagar: abrir con
        // 0 obliga a poner fichas antes de poder hacer nada y el primer click
        // se siente como un tramite.
        _apuesta = Mathf.Min(Mathf.Max(minBet, PrimeraFichaQueEntra()), Plata);
        _ficha = IndiceDeFichaInicial();
        _zona = Zona.Fichas;
        _boton = (int)Accion.Pedir;

        // Apostando un Pokemon no hay fichas: se arranca parado en PEDIR.
        if (ModoPokemon)
        {
            _apuesta = 0;
            _zona = Zona.Botones;
        }

        _fase = Fase.Apostando;
        _tapadaAbajo = true;
        _pagoDeLaRonda = 0;
        _cartelPendiente = null;
        _vuelos.Clear();
        _reparto.Clear();

        ApagarCartas();
        ApagarBanner();
        Refrescar();
    }

    protected override void OnClose()
    {
        _vuelos.Clear();
        _reparto.Clear();
        ApagarBanner();
    }

    // Vuelve el foco despues de que el jugador cerro el cartel del resultado.
    public override void OnFocusGained()
    {
        if (_fase != Fase.Cartel) return;

        // La apuesta de un Pokemon es a una sola mano: cerrado el cartel, se
        // vuelve con Tacconi, que es quien entrega o se queda con el Pokemon.
        if (ModoPokemon)
        {
            CloseSelf();
            return;
        }

        NuevaRonda(false);
    }

    private void RearmarZapato()
    {
        _zapato.Clear();
        _zapato.AddRange(BlackjackTable.ZapatoOrdenado());
        BlackjackTable.Barajar(_zapato, n => Random.Range(0, n));
    }

    private int PrimeraFichaQueEntra()
    {
        if (chipValues == null) return minBet;
        for (int i = 0; i < chipValues.Length; i++)
            if (chipValues[i] >= minBet) return chipValues[i];
        return minBet;
    }

    private int IndiceDeFichaInicial()
    {
        if (chipValues == null) return 0;
        for (int i = 0; i < chipValues.Length; i++)
            if (chipValues[i] >= minBet) return i;
        return 0;
    }

    // ---------------------------------------------------------------- input

    public override void HandleAction(GameAction action)
    {
        // Mientras se reparte, juega el croupier o se canta el resultado no se
        // acepta NADA: es lo que evita que machacar la tecla encadene dos
        // ordenes o cierre la mesa a mitad de la animacion.
        if (_fase != Fase.Apostando && _fase != Fase.TurnoJugador) return;

        switch (action)
        {
            case GameAction.Up: Mover(0, -1); return;
            case GameAction.Down: Mover(0, 1); return;
            case GameAction.Left: Mover(-1, 0); return;
            case GameAction.Right: Mover(1, 0); return;
            case GameAction.Confirm: Confirmar(); return;
            case GameAction.Cancel: Cancelar(); return;
        }
    }

    private void Mover(int dx, int dy)
    {
        if (ModoPokemon && _zona == Zona.Fichas) _zona = Zona.Botones;

        if (_fase == Fase.Apostando && _zona == Zona.Fichas)
        {
            if (dx != 0) _ficha = Mathf.Clamp(_ficha + dx, 0, CantidadDeFichas - 1);
            else if (dy > 0) { _zona = Zona.Botones; _boton = PrimerBotonActivo(); }
            Refrescar();
            return;
        }

        if (dx != 0) _boton = BotonVecino(_boton, dx);
        else if (dy < 0 && _fase == Fase.Apostando && !ModoPokemon) _zona = Zona.Fichas;
        else if (dy != 0) _boton = BotonVecino(_boton, dy > 0 ? 1 : -1);

        Refrescar();
    }

    // Salta los botones apagados: dejar el cursor sobre uno que no hace nada se
    // lee como que el juego se colgo.
    private int BotonVecino(int desde, int paso)
    {
        for (int i = 1; i <= Botones; i++)
        {
            int candidato = ((desde + paso * i) % Botones + Botones) % Botones;
            if (Habilitado((Accion)candidato)) return candidato;
        }
        return desde;
    }

    private int PrimerBotonActivo()
    {
        for (int i = 0; i < Botones; i++)
            if (Habilitado((Accion)i)) return i;
        return (int)Accion.Salir;
    }

    // Que se puede tocar en cada fase. Es la unica fuente de verdad: la pintura
    // de los botones y el teclado preguntan aca, asi no puede pasar que un
    // boton se vea apagado y igual responda.
    private bool Habilitado(Accion accion)
    {
        switch (accion)
        {
            case Accion.Pedir:
                // En apuestas reparte; en el turno, roba.
                if (_fase == Fase.Apostando) return ModoPokemon || (_apuesta >= minBet && _apuesta <= Plata);
                return _fase == Fase.TurnoJugador && Mano != null && !Mano.Cerrada;

            case Accion.Plantarse:
                return _fase == Fase.TurnoJugador && Mano != null && !Mano.Cerrada;

            case Accion.Doblar:
                return _fase == Fase.TurnoJugador && !ModoPokemon
                       && BlackjackTable.SePuedeDoblar(Mano, Plata);

            case Accion.Dividir:
                return _fase == Fase.TurnoJugador && !ModoPokemon
                       && BlackjackTable.SePuedeDividir(Mano, _manos.Count, Plata);

            case Accion.Reiniciar:
                // Solo entre manos: a mitad de una ronda ya hay plata puesta.
                return _fase == Fase.Apostando && !ModoPokemon;

            case Accion.Salir:
                return _fase == Fase.Apostando;

            default:
                return false;
        }
    }

    private void Confirmar()
    {
        if (_fase == Fase.Apostando && _zona == Zona.Fichas && !ModoPokemon)
        {
            Apostar(ValorDeFicha);
            return;
        }

        Ejecutar((Accion)_boton);
    }

    private void Cancelar()
    {
        if (_fase != Fase.Apostando) return;

        // Con fichas puestas, cancelar limpia la apuesta; con la mesa limpia,
        // se va. Es el mismo "volver atras" de siempre, en dos pasos.
        if (_apuesta > 0 && !ModoPokemon) LimpiarApuesta();
        else CloseSelf();
    }

    private void Ejecutar(Accion accion)
    {
        if (!Habilitado(accion))
        {
            Avisar(PorQueNo(accion));
            return;
        }

        switch (accion)
        {
            case Accion.Pedir:
                if (_fase == Fase.Apostando) Repartir();
                else Pedir();
                break;
            case Accion.Plantarse: Plantarse(); break;
            case Accion.Doblar: Doblar(); break;
            case Accion.Dividir: Dividir(); break;
            case Accion.Reiniciar: LimpiarApuesta(); break;
            case Accion.Salir: CloseSelf(); break;
        }
    }

    // Por que un boton no responde. Sin esto, tocar DOBLAR con tres cartas no
    // hace nada y parece un bug.
    private string PorQueNo(Accion accion)
    {
        switch (accion)
        {
            case Accion.Pedir when _fase == Fase.Apostando && _apuesta < minBet:
                return $"La apuesta mínima es {minBet}.";
            case Accion.Pedir when _fase == Fase.Apostando:
                return "No te alcanza para esa apuesta.";
            case Accion.Doblar when Mano != null && Mano.Cards.Count > 2:
                return "Solo se dobla con las dos primeras cartas.";
            case Accion.Doblar:
                return "No te alcanza para doblar.";
            case Accion.Dividir when _manos.Count > 1:
                return "Ya dividiste esta ronda.";
            case Accion.Dividir when Mano != null && Mano.Cards.Count == 2:
                return "Solo se divide con dos cartas iguales.";
            case Accion.Dividir:
                return "No se puede dividir esta mano.";
            default:
                return "Ahora no se puede.";
        }
    }

    // Los enganchan los botones del prefab, para que tambien ande con el mouse.
    public void OnActionButton(int accion)
    {
        if (accion < 0 || accion >= Botones) return;
        if (_fase != Fase.Apostando && _fase != Fase.TurnoJugador) return;

        _zona = Zona.Botones;
        _boton = accion;
        Ejecutar((Accion)accion);
    }

    public void OnChipButton(int indice)
    {
        if (_fase != Fase.Apostando || ModoPokemon) return;

        _zona = Zona.Fichas;
        _ficha = Mathf.Clamp(indice, 0, CantidadDeFichas - 1);
        Apostar(ValorDeFicha);
    }

    // -------------------------------------------------------------- apostar

    private void Apostar(int ficha)
    {
        // Nunca se puede comprometer mas plata de la que hay: como el descuento
        // recien pasa al repartir, este es el unico lugar donde se controla.
        if (_apuesta + ficha > Plata)
        {
            Avisar("No te alcanza para esa ficha.");
            return;
        }

        if (_apuesta + ficha > maxBet)
        {
            Avisar($"El máximo por mano es {maxBet}.");
            return;
        }

        _apuesta += ficha;
        Sonar(chipClip, Sonidos.Fichas);
        Refrescar();
    }

    private void LimpiarApuesta()
    {
        if (_apuesta == 0) return;
        _apuesta = 0;
        Sonar(chipClip, Sonidos.Fichas);
        Refrescar();
    }

    private void Avisar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (hintLabel != null) hintLabel.text = texto;
        _aviso = 1.4f;
    }

    // ------------------------------------------------------------- repartir

    private void Repartir()
    {
        if (!ModoPokemon && (_apuesta < minBet || _apuesta > Plata)) return;

        // Se rearma el zapato ENTRE manos, nunca a mitad de una: cambiar las
        // cartas con una mano abierta es exactamente lo que una mesa de verdad
        // no hace.
        if (_zapato.Count < BlackjackTable.CorteDelZapato)
        {
            RearmarZapato();
            Sonidos.Mezclar();
        }

        _manos.Clear();
        BlackjackHand mano = new BlackjackHand { Bet = ModoPokemon ? 0 : _apuesta };
        _manos.Add(mano);
        _croupier.Limpiar();
        _manoActual = 0;
        _tapadaAbajo = true;
        _pagoDeLaRonda = 0;

        // Recien aca se cobra: hasta este momento la apuesta era un numero en
        // un cartel.
        // Apostando un Pokemon no se toca la plata.
        if (!ModoPokemon)
        {
            if (Player != null) Player.SpendMoney(_apuesta);
            _result.Neto -= _apuesta;
        }
        _result.Manos++;

        ApagarCartas();
        ApagarBanner();

        // Orden de una mesa de verdad: jugador, croupier, jugador, croupier
        // tapada. 0 = al jugador, 1 = al croupier.
        _reparto.Clear();
        _reparto.Enqueue(AlaMano0);
        _reparto.Enqueue(AlCroupier);
        _reparto.Enqueue(AlaMano0);
        _reparto.Enqueue(AlCroupier);
        _repartoInicial = true;

        _fase = Fase.Repartiendo;
        _espera = 0f;
        Refrescar();
    }

    private BlackjackCard Robar()
    {
        if (_zapato.Count == 0) RearmarZapato();

        BlackjackCard carta = _zapato[_zapato.Count - 1];
        _zapato.RemoveAt(_zapato.Count - 1);
        return carta;
    }

    // Le da una carta a una mano y le arma el vuelo desde el mazo.
    private void DarCarta(BlackjackHand mano, Image[] fila, Vector2 centro, bool tapada)
    {
        if (mano == null) return;

        mano.Cards.Add(Robar());
        AcomodarFila(mano, fila, centro, tapada, animarLaUltima: true);
        Sonar(dealClip, Sonidos.Carta);
    }

    private void Pedir()
    {
        BlackjackHand mano = Mano;
        if (mano == null) return;

        DarCarta(mano, FilaDe(_manoActual), CentroDe(_manoActual), false);

        // Pasarse cierra la mano sola: no tiene sentido dejar el boton PEDIR
        // encendido con 23 en la mesa.
        if (mano.SePaso || mano.Total == 21) mano.Cerrada = true;

        if (mano.Cerrada) PasarDeMano();
        else Refrescar();
    }

    private void Plantarse()
    {
        BlackjackHand mano = Mano;
        if (mano == null) return;

        mano.Cerrada = true;
        PasarDeMano();
    }

    private void Doblar()
    {
        BlackjackHand mano = Mano;
        if (mano == null) return;

        // Se cobra en el momento: es cuando el jugador se compromete.
        if (Player != null) Player.SpendMoney(mano.Bet);
        _result.Neto -= mano.Bet;

        mano.Bet *= 2;
        mano.Doblada = true;

        DarCarta(mano, FilaDe(_manoActual), CentroDe(_manoActual), false);

        // Al doblar se recibe UNA sola carta y se planta, salga lo que salga.
        mano.Cerrada = true;
        PasarDeMano();
    }

    private void Dividir()
    {
        BlackjackHand mano = Mano;
        if (mano == null || mano.Cards.Count != 2) return;

        if (Player != null) Player.SpendMoney(mano.Bet);
        _result.Neto -= mano.Bet;

        var segunda = new BlackjackHand { Bet = mano.Bet, DeSplit = true };
        segunda.Cards.Add(mano.Cards[1]);
        mano.Cards.RemoveAt(1);
        mano.DeSplit = true;
        _manos.Add(segunda);

        // Las dos cartas que ya estaban se corren DESLIZANDOSE a su nuevo lugar:
        // aparecer de golpe una a cada lado se lee como un parpadeo y no como
        // que la mano se partio en dos.
        AcomodarTodo(deslizar: true);

        // Y la carta que le toca a cada mano se reparte por la misma cola que el
        // reparto inicial, o sea con su vuelo desde el mazo. La primera version
        // las metia en la mano y las dibujaba ya puestas, y era justo lo que se
        // veia mal: dividir era la unica jugada sin animacion.
        _reparto.Clear();
        _reparto.Enqueue(AlaMano0);
        _reparto.Enqueue(AlaMano1);
        _repartoInicial = false;

        _fase = Fase.Repartiendo;
        _espera = dealGap;
        Refrescar();
    }

    // Cierra las dos manos si lo que se dividio eran ases: reciben una carta
    // cada uno y se plantan. Sin esta regla, partir ases seria la jugada obvia
    // siempre. Se resuelve al TERMINAR de repartir, que es cuando las dos manos
    // ya tienen su carta.
    private void TerminarSplit()
    {
        if (_manos.Count < 2) return;

        bool ases = _manos[0].Cards.Count > 0 && _manos[0].Cards[0].EsAs;

        for (int i = 0; i < _manos.Count; i++)
            // Los ases se cierran por regla, y cualquier mano que llegue a 21
            // se cierra porque no hay nada que decidir: es lo mismo que hace
            // Pedir() cuando la carta que sale deja la mano en 21.
            if (ases || _manos[i].Total == 21) _manos[i].Cerrada = true;
    }

    // Pasa a la segunda mano o, si ya no queda ninguna, al croupier.
    private void PasarDeMano()
    {
        for (int i = _manoActual + 1; i < _manos.Count; i++)
        {
            if (_manos[i].Cerrada) continue;
            _manoActual = i;
            AcomodarTodo();
            Refrescar();
            return;
        }

        EmpezarTurnoDelDealer();
    }

    // --------------------------------------------------------- turno dealer

    private void EmpezarTurnoDelDealer()
    {
        _fase = Fase.TurnoDealer;
        _espera = 0.35f;
        _giro = 0f;
        Refrescar();
    }

    // Si el croupier tiene que seguir jugando. Cuando todas las manos del
    // jugador se pasaron no roba nada: ya cobro, y hacerlo robar igual solo
    // alarga la espera.
    private bool AlCroupierLeImporta()
    {
        for (int i = 0; i < _manos.Count; i++)
            if (!_manos[i].SePaso) return true;
        return false;
    }

    // ------------------------------------------------------------ resultado

    private void Resolver()
    {
        _fase = Fase.Resultado;
        _banner = 0f;
        _pagoDeLaRonda = 0;

        var textos = new List<string>();
        BlackjackOutcome mejor = BlackjackOutcome.Pierde;

        for (int i = 0; i < _manos.Count; i++)
        {
            BlackjackHand mano = _manos[i];
            BlackjackOutcome resultado = BlackjackTable.Resolver(mano, _croupier);
            int pago = BlackjackTable.PagoDe(resultado, mano.Bet);
            _pagoDeLaRonda += pago;

            if (Mejor(resultado, mejor)) mejor = resultado;
            if (resultado == BlackjackOutcome.Gana || resultado == BlackjackOutcome.Blackjack)
                _result.GanoAlgunaMano = true;
            if (resultado == BlackjackOutcome.Blackjack)
                _result.HizoBlackjack = true;

            textos.Add(TextoDe(mano, resultado, pago, _manos.Count > 1, i));
        }

        // Apostando un Pokemon lo que importa es quien se queda con quien: el
        // cartel lo dice con nombres y el resultado queda para Tacconi.
        if (ModoPokemon)
        {
            _result.ResultadoApuestaPokemon = _manos.Count > 0
                ? BlackjackTable.Resolver(_manos[0], _croupier)
                : BlackjackOutcome.Pierde;
            textos.Clear();
            textos.Add(TextoDeApuestaPokemon(_result.ResultadoApuestaPokemon));
        }

        // El premio se entrega ACA, no cuando se cierra el cartel: si el juego
        // se corta con la caja abierta, la plata ya es del jugador.
        if (_pagoDeLaRonda > 0 && Player != null) Player.AddMoney(_pagoDeLaRonda);
        _result.Neto += _pagoDeLaRonda;
        if (_pagoDeLaRonda > _result.MayorPremio) _result.MayorPremio = _pagoDeLaRonda;

        MostrarBanner(mejor);
        if (mejor == BlackjackOutcome.Pierde) Sonar(loseClip, null);
        else Sonar(winClip, () => { Sonidos.Fichas(); Sonidos.PremioChico(); });

        // Todo en UNA linea, sin '\n': la caja de mensajes corta sola. (Antes
        // era obligatorio: con interlineado 1, dos renglones de 'power clear'
        // se pisaban. Ver FuentePixel.)
        _cartelPendiente = string.Join(" ", textos.ToArray());
        Refrescar();
    }

    private static bool Mejor(BlackjackOutcome a, BlackjackOutcome b) => Rango(a) > Rango(b);

    private static int Rango(BlackjackOutcome r)
    {
        switch (r)
        {
            case BlackjackOutcome.Blackjack: return 3;
            case BlackjackOutcome.Gana: return 2;
            case BlackjackOutcome.Empata: return 1;
            default: return 0;
        }
    }

    private string TextoDe(BlackjackHand mano, BlackjackOutcome resultado, int pago,
                           bool hayDos, int indice)
    {
        string quien = hayDos ? $"Mano {indice + 1}: " : "";

        switch (resultado)
        {
            case BlackjackOutcome.Blackjack:
                return $"{quien}¡Blackjack! Te llevás {pago} PokéDólares.";
            case BlackjackOutcome.Gana:
                return mano.Doblada
                    ? $"{quien}Doblaste y ganaste: {pago} PokéDólares."
                    : $"{quien}Ganaste {pago} PokéDólares.";
            case BlackjackOutcome.Empata:
                return $"{quien}Empate: te devuelven {pago}.";
            default:
                return mano.SePaso
                    ? $"{quien}Te pasaste con {mano.Total}."
                    : $"{quien}Gana la casa con {_croupier.Total}.";
        }
    }

    private string TextoDeApuestaPokemon(BlackjackOutcome resultado)
    {
        string rival = string.IsNullOrEmpty(_pedido.Rival) ? "La casa" : _pedido.Rival;
        string premio = string.IsNullOrEmpty(_pedido.PremioPokemon) ? "su Pokémon" : _pedido.PremioPokemon;

        switch (resultado)
        {
            case BlackjackOutcome.Blackjack:
                if (!string.IsNullOrEmpty(_pedido.TextoGana)) return "¡Blackjack! " + _pedido.TextoGana;
                return $"¡Blackjack! {rival} te tiene que dar {premio}.";
            case BlackjackOutcome.Gana:
                if (!string.IsNullOrEmpty(_pedido.TextoGana)) return "¡Ganaste! " + _pedido.TextoGana;
                return $"¡Ganaste! {rival} te tiene que dar {premio}.";
            case BlackjackOutcome.Empata:
                if (!string.IsNullOrEmpty(_pedido.TextoEmpata)) return "Empate. " + _pedido.TextoEmpata;
                return "Empate: nadie pierde nada.";
            default:
                if (!string.IsNullOrEmpty(_pedido.TextoPierde)) return "Perdiste. " + _pedido.TextoPierde;
                return $"Perdiste: {rival} se queda con tu {_pedido.ApuestaPokemon}.";
        }
    }

    private void MostrarBanner(BlackjackOutcome resultado)
    {
        if (resultBanner == null || resultSprites == null || resultSprites.Length < 4) return;

        int i;
        switch (resultado)
        {
            case BlackjackOutcome.Blackjack: i = 0; break;
            case BlackjackOutcome.Gana: i = 1; break;
            case BlackjackOutcome.Empata: i = 2; break;
            default: i = 3; break;
        }

        if (resultSprites[i] == null) return;

        resultBanner.sprite = resultSprites[i];
        resultBanner.gameObject.SetActive(true);
        resultBanner.transform.localScale = Vector3.zero;
    }

    private void ApagarBanner()
    {
        if (resultBanner != null) resultBanner.gameObject.SetActive(false);
        _banner = 0f;
    }

    // Deja la mesa lista para otra ronda. 'limpiando' distingue el REINICIAR
    // del jugador de la vuelta automatica despues del cartel.
    private void NuevaRonda(bool limpiando)
    {
        ApagarBanner();
        ApagarCartas();

        _manos.Clear();
        _manos.Add(new BlackjackHand());
        _croupier.Limpiar();
        _manoActual = 0;
        _tapadaAbajo = true;
        _fase = Fase.Apostando;
        _zona = Zona.Fichas;
        _boton = (int)Accion.Pedir;

        if (limpiando) _apuesta = 0;
        // La apuesta anterior se mantiene si todavia alcanza: encadenar manos
        // sin volver a poner fichas es lo que hace que la mesa se juegue rapido.
        else if (_apuesta > Plata) _apuesta = Mathf.Min(_apuesta, Plata);

        Refrescar();
    }

    // ----------------------------------------------------------------- tick

    public override void OnTick(float dt)
    {
        if (_aviso > 0f)
        {
            _aviso = Mathf.MoveTowards(_aviso, 0f, dt);
            if (_aviso <= 0f) Refrescar();
        }

        AvanzarVuelos(dt);
        AnimarCroupier(dt);
        LatirSeleccion(dt);

        switch (_fase)
        {
            case Fase.Repartiendo: AvanzarReparto(dt); break;
            case Fase.TurnoDealer: AvanzarDealer(dt); break;
            case Fase.Resultado: AvanzarBanner(dt); break;
        }
    }

    // Las cartas entran volando desde el mazo y aterrizan con un pop corto.
    private void AvanzarVuelos(float dt)
    {
        for (int i = _vuelos.Count - 1; i >= 0; i--)
        {
            Vuelo v = _vuelos[i];
            if (v.Sprite == null) { _vuelos.RemoveAt(i); continue; }

            v.T = Mathf.Clamp01(v.T + dt / Mathf.Max(0.01f, dealSeconds));

            float suave = 1f - (1f - v.T) * (1f - v.T);   // frena al llegar
            var rt = (RectTransform)v.Sprite.transform;
            rt.anchoredPosition = Vector2.Lerp(v.Desde, v.Hasta, suave);

            // Un pelin mas grande a mitad de camino: es lo que hace que la
            // carta se lea como que "cae" sobre la mesa y no que se desliza.
            float pop = v.T < 0.7f
                ? Mathf.Lerp(0.72f, 1.08f, v.T / 0.7f)
                : Mathf.Lerp(1.08f, 1f, (v.T - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * pop;

            if (v.T >= 1f)
            {
                rt.localScale = Vector3.one;
                _vuelos.RemoveAt(i);
            }
            else
            {
                _vuelos[i] = v;
            }
        }
    }

    private void AvanzarReparto(float dt)
    {
        _espera -= dt;
        if (_espera > 0f) return;

        if (_reparto.Count > 0)
        {
            int aQuien = _reparto.Dequeue();
            if (aQuien == AlCroupier)
                // La segunda del croupier va tapada: es la carta que el jugador
                // no puede ver hasta que le toque jugar.
                DarCarta(_croupier, dealerCards, dealerRowCenter, _croupier.Cards.Count == 1);
            else if (aQuien < _manos.Count)
                DarCarta(_manos[aQuien], FilaDe(aQuien), CentroDe(aQuien), false);

            _espera = dealGap;
            Refrescar();
            return;
        }

        if (!_repartoInicial)
        {
            // Fin del reparto de un split. Con ases las dos manos quedan
            // cerradas, asi que se pasa de largo derecho al croupier.
            TerminarSplit();
            if (Mano == null || Mano.Cerrada) { PasarDeMano(); return; }
        }
        // Ya estan las cuatro. Si alguno tiene blackjack servido la mano se
        // termina sin que el jugador toque nada.
        else if (_manos[0].EsBlackjack || BlackjackTable.EsBlackjack(_croupier.Cards))
        {
            EmpezarTurnoDelDealer();
            return;
        }

        _fase = Fase.TurnoJugador;
        _zona = Zona.Botones;
        _boton = PrimerBotonActivo();
        Refrescar();
    }

    private void AvanzarDealer(float dt)
    {
        // Primero se da vuelta la tapada, con un giro de verdad: la carta se
        // achica hasta desaparecer de canto, se le cambia el sprite y se vuelve
        // a abrir. Cambiar el sprite de una es lo que hace que no se entienda
        // que paso.
        if (_tapadaAbajo)
        {
            // La pausa antes de destapar es lo que hace que se lea como que el
            // croupier "va a mostrar" y no como un cambio de sprite.
            if (_espera > 0f)
            {
                _espera -= dt;
                return;
            }

            if (_giro <= 0f) Sonar(flipClip, Sonidos.Carta);
            _giro = Mathf.Clamp01(_giro + dt / Mathf.Max(0.01f, flipSeconds));

            Image tapada = dealerCards != null && dealerCards.Length > 1 ? dealerCards[1] : null;
            if (tapada != null)
            {
                float ancho = Mathf.Abs(1f - 2f * _giro);
                tapada.transform.localScale = new Vector3(Mathf.Max(0.02f, ancho), 1f, 1f);
                // Justo al pasar por el canto se cambia la cara.
                if (_giro >= 0.5f && tapada.sprite == cardBackSprite && _croupier.Cards.Count > 1)
                    tapada.sprite = SpriteDe(_croupier.Cards[1]);
            }

            if (_giro < 1f) return;

            if (tapada != null) tapada.transform.localScale = Vector3.one;
            _tapadaAbajo = false;
            _espera = dealerThinkSeconds;
            Refrescar();
            return;
        }

        _espera -= dt;
        if (_espera > 0f) return;

        if (AlCroupierLeImporta() && BlackjackTable.DealerPide(_croupier.Cards))
        {
            DarCarta(_croupier, dealerCards, dealerRowCenter, false);
            _espera = dealerThinkSeconds;
            Refrescar();
            return;
        }

        Resolver();
    }

    private void AvanzarBanner(float dt)
    {
        _banner += dt;
        float p = Mathf.Clamp01(_banner / Mathf.Max(0.01f, bannerSeconds));

        if (resultBanner != null && resultBanner.gameObject.activeSelf)
        {
            // Pop con rebote corto y despues quieto.
            float escala = p < 0.35f
                ? Mathf.SmoothStep(0f, 1.18f, p / 0.35f)
                : Mathf.Lerp(1.18f, 1f, (p - 0.35f) / 0.65f);
            resultBanner.transform.localScale = Vector3.one * escala;
        }

        if (p < 1f) return;

        _fase = Fase.Cartel;
        string texto = _cartelPendiente;
        _cartelPendiente = null;
        ShowMessage(texto);
    }

    // El croupier se mueve mientras reparte y se queda quieto el resto del
    // tiempo. Es la fila 'de frente' de la hoja del NPC: los cuadros 0 y 2 son
    // el parado y los otros dos las poses de paso, asi que recorrerlos se lee
    // como que esta manoteando las cartas, no caminando.
    private void AnimarCroupier(float dt)
    {
        if (dealerSprite == null || dealerFrames == null || dealerFrames.Length == 0) return;

        bool trabajando = _fase == Fase.Repartiendo || _fase == Fase.TurnoDealer;

        if (!trabajando)
        {
            _relojDelNpc = 0f;
            if (dealerFrames[0] != null) dealerSprite.sprite = dealerFrames[0];
            return;
        }

        _relojDelNpc += dt;
        int cuadro = Mathf.FloorToInt(_relojDelNpc / Mathf.Max(0.02f, dealerFrameSeconds))
                     % dealerFrames.Length;
        if (dealerFrames[cuadro] != null) dealerSprite.sprite = dealerFrames[cuadro];
    }

    private void LatirSeleccion(float dt)
    {
        if (_fase != Fase.Apostando && _fase != Fase.TurnoJugador) return;

        // El control elegido respira: es lo que deja claro donde estas parado
        // sin tener que dibujar un cursor aparte.
        float p = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6.5f);
        Image foco = FocoActual();
        if (foco != null) foco.transform.localScale = Vector3.one * p;
    }

    private Image FocoActual()
    {
        if (_fase == Fase.Apostando && _zona == Zona.Fichas)
            return chipButtons != null && _ficha < chipButtons.Length ? chipButtons[_ficha] : null;

        return actionButtons != null && _boton < actionButtons.Length
            ? actionButtons[_boton] : null;
    }

    // -------------------------------------------------------------- cartas

    private Image[] FilaDe(int mano) => mano == 0 ? playerCards : splitCards;

    private Vector2 CentroDe(int mano)
    {
        // Con una sola mano va centrada; al dividir, las dos se separan.
        if (_manos.Count < 2) return playerRowCenter;
        return playerRowCenter + new Vector2(mano == 0 ? -splitSpread : splitSpread, 0f);
    }

    private Sprite SpriteDe(BlackjackCard carta)
    {
        int i = (int)carta.Suit * 13 + carta.Columna;
        return cardSprites != null && i >= 0 && i < cardSprites.Length ? cardSprites[i] : null;
    }

    // Manda una carta volando de un punto a otro. Antes saca el vuelo que esa
    // misma carta pudiera tener encima: si no, los dos siguen corriendo a la vez
    // y la carta se va al destino viejo (pasa al dividir mientras todavia se
    // esta acomodando el reparto).
    private void Volar(Image carta, Vector2 desde, Vector2 hasta)
    {
        if (carta == null) return;

        for (int i = _vuelos.Count - 1; i >= 0; i--)
            if (_vuelos[i].Sprite == carta) _vuelos.RemoveAt(i);

        ((RectTransform)carta.transform).anchoredPosition = desde;
        _vuelos.Add(new Vuelo { Sprite = carta, Desde = desde, Hasta = hasta, T = 0f });
    }

    // Coloca las cartas de una mano. 'animarLaUltima' manda la recien repartida
    // volando desde el mazo; 'deslizar' hace que las que ya estaban se corran
    // solas hasta su nuevo lugar en vez de saltar (es lo que necesita el split,
    // donde la fila se parte en dos).
    private void AcomodarFila(BlackjackHand mano, Image[] fila, Vector2 centro,
                              bool ultimaTapada, bool animarLaUltima,
                              bool deslizar = false)
    {
        if (fila == null || mano == null) return;

        int n = Mathf.Min(mano.Cards.Count, fila.Length);

        // Las cartas se aprietan solas cuando son muchas: es lo que evita que
        // una mano larga se salga de la mesa. Nunca se pisan del todo, porque
        // el paso minimo deja ver el numero de la esquina.
        float paso = cardStep;
        if (n > 1) paso = Mathf.Min(paso, Mathf.Max(14f, (rowMaxWidth - cardSize.x) / (n - 1)));
        float ancho = (n - 1) * paso;

        for (int i = 0; i < fila.Length; i++)
        {
            if (fila[i] == null) continue;

            if (i >= n)
            {
                fila[i].gameObject.SetActive(false);
                continue;
            }

            bool tapada = ultimaTapada && i == n - 1;
            // Si ya se veia, se puede deslizar desde donde esta; si recien se
            // prende, no hay 'donde esta' que valga.
            bool yaEstaba = fila[i].gameObject.activeSelf;
            fila[i].sprite = tapada ? cardBackSprite : SpriteDe(mano.Cards[i]);
            fila[i].gameObject.SetActive(true);

            var rt = (RectTransform)fila[i].transform;
            rt.sizeDelta = cardSize;
            Vector2 destino = centro + new Vector2(-ancho * 0.5f + i * paso, 0f);

            if (animarLaUltima && i == n - 1 && deck != null)
            {
                Volar(fila[i], deck.anchoredPosition, destino);
            }
            else if (deslizar && yaEstaba && rt.anchoredPosition != destino)
            {
                Volar(fila[i], rt.anchoredPosition, destino);
            }
            else
            {
                rt.anchoredPosition = destino;
                rt.localScale = Vector3.one;
            }
        }
    }

    private void AcomodarTodo(bool deslizar = false)
    {
        AcomodarFila(_croupier, dealerCards, dealerRowCenter, _tapadaAbajo, false, deslizar);
        for (int i = 0; i < _manos.Count; i++)
            AcomodarFila(_manos[i], FilaDe(i), CentroDe(i), false, false, deslizar);
    }

    private void ApagarCartas()
    {
        Apagar(dealerCards);
        Apagar(playerCards);
        Apagar(splitCards);
        _vuelos.Clear();
    }

    private static void Apagar(Image[] fila)
    {
        if (fila == null) return;
        for (int i = 0; i < fila.Length; i++)
            if (fila[i] != null)
            {
                fila[i].gameObject.SetActive(false);
                fila[i].transform.localScale = Vector3.one;
            }
    }

    // --------------------------------------------------------------- pintar

    private void Refrescar()
    {
        if (moneyLabel != null) moneyLabel.text = Plata.ToString();
        if (betLabel != null) betLabel.text = ModoPokemon ? _pedido.ApuestaPokemon : ApuestaEnJuego().ToString();

        PintarTotales();
        PintarFichas();
        PintarBotones();
        PintarMarcaDeMano();

        if (hintLabel != null && _aviso <= 0f) hintLabel.text = Ayuda();
    }

    // Lo que hay comprometido ahora mismo: la apuesta de la fase de apuestas o
    // la suma de las manos en juego.
    private int ApuestaEnJuego()
    {
        if (_fase == Fase.Apostando) return _apuesta;

        int total = 0;
        for (int i = 0; i < _manos.Count; i++) total += _manos[i].Bet;
        return total;
    }

    private void PintarTotales()
    {
        // Mientras la tapada este abajo, el total del croupier se muestra con
        // la carta que se ve nada mas: cantarlo entero seria mostrar la carta.
        if (dealerTotalLabel != null)
        {
            if (_croupier.Cards.Count == 0) dealerTotalLabel.text = "-";
            else if (_tapadaAbajo)
            {
                var vistas = new List<BlackjackCard> { _croupier.Cards[0] };
                dealerTotalLabel.text = BlackjackTable.ValorDe(vistas) + " + ?";
            }
            else dealerTotalLabel.text = _croupier.Total.ToString();
        }

        PintarTotalDeMano(playerTotalLabel, 0);
        PintarTotalDeMano(splitTotalLabel, 1);

        // La segunda mano no existe hasta que se divide: su placa entera se
        // prende y se apaga, porque un cartel vacio se lee como un hueco.
        bool hayDos = _manos.Count > 1 && _manos[1].Cards.Count > 0;
        if (splitGroup != null) splitGroup.SetActive(hayDos);

        // Y con dos manos en la mesa se atenua la que no se esta jugando: es lo
        // que ata cada cartel a su mano sin tener que numerarlos.
        bool eligiendo = hayDos && _fase == Fase.TurnoJugador;
        Atenuar(playerPanel, eligiendo && _manoActual != 0);
        Atenuar(splitPanel, eligiendo && _manoActual != 1);
    }

    private static void Atenuar(Image placa, bool apagada)
    {
        if (placa == null) return;
        placa.color = apagada ? new Color(0.58f, 0.6f, 0.66f, 1f) : Color.white;
    }

    private void PintarTotalDeMano(Text etiqueta, int indice)
    {
        if (etiqueta == null) return;

        if (indice >= _manos.Count || _manos[indice].Cards.Count == 0)
        {
            etiqueta.text = "-";
            return;
        }

        BlackjackHand mano = _manos[indice];

        // Una mano suave se canta con las dos lecturas (7/17): sin eso, pedir
        // con A-6 parece un suicidio y es la jugada correcta.
        //
        // El numero va SOLO en el recuadro. La primera version le ponia delante
        // '1)' y '2)' para distinguir las dos manos de un split, y no entraba:
        // el hueco del arte es para dos o tres caracteres y el prefijo se salia
        // por los costados. Cual es cual ahora lo dice la placa que lo rodea.
        etiqueta.text = mano.EsSuave && mano.Total <= 21 && !mano.Cerrada
            ? $"{mano.Total - 10}/{mano.Total}"
            : mano.Total.ToString();
    }

    private void PintarMarcaDeMano()
    {
        if (handMarker == null) return;

        bool hayDos = _manos.Count > 1 && _fase == Fase.TurnoJugador;
        handMarker.gameObject.SetActive(hayDos);
        if (!hayDos) return;

        var rt = (RectTransform)handMarker.transform;
        rt.anchoredPosition = CentroDe(_manoActual) + new Vector2(0f, -cardSize.y * 0.5f - 10f);
    }

    private void PintarFichas()
    {
        bool apostando = _fase == Fase.Apostando && !ModoPokemon;
        bool enFichas = apostando && _zona == Zona.Fichas;

        for (int i = 0; i < chipButtons.Length; i++)
        {
            if (chipGlows != null && i < chipGlows.Length && chipGlows[i] != null)
                chipGlows[i].gameObject.SetActive(enFichas && i == _ficha);

            if (chipButtons[i] == null) continue;

            // Apostando un Pokemon no hay fichas: ni se dibujan, asi no parece
            // que se esta jugando plata. El valor escrito es un hermano suelto
            // (ver BlackjackTableBuilder), por eso se busca por nombre.
            chipButtons[i].gameObject.SetActive(!ModoPokemon);
            Transform valor = chipButtons[i].transform.parent != null
                ? chipButtons[i].transform.parent.Find($"ValorFicha{i}")
                : null;
            if (valor != null) valor.gameObject.SetActive(!ModoPokemon);
            if (ModoPokemon) continue;

            // Apagadas cuando no se puede apostar, y a media luz las que no
            // alcanzan: el jugador ve de una que fichas puede poner.
            bool alcanza = apostando && i < CantidadDeFichas &&
                           _apuesta + chipValues[i] <= Plata &&
                           _apuesta + chipValues[i] <= maxBet;

            float escala = enFichas && i == _ficha ? 1.18f : i == _ficha && apostando ? 1.08f : 0.94f;
            chipButtons[i].transform.localScale = Vector3.one * escala;
            chipButtons[i].color = !apostando ? new Color(0.45f, 0.47f, 0.52f, 0.85f)
                : alcanza ? Color.white
                : new Color(0.55f, 0.56f, 0.6f, 0.9f);
        }
    }

    private void PintarBotones()
    {
        if (actionButtons == null) return;

        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] == null) continue;

            bool activo = Habilitado((Accion)i);
            bool elegido = activo && _zona == Zona.Botones && _boton == i &&
                           (_fase == Fase.Apostando || _fase == Fase.TurnoJugador);

            // Sin transicion de UGUI: el estado lo dibuja la pantalla. El arte
            // del equipo no trae version apagada de cada boton, asi que el
            // apagado es el mismo sprite oscurecido.
            actionButtons[i].color = !activo ? new Color(0.42f, 0.44f, 0.5f, 0.8f)
                : elegido ? Color.white
                : new Color(0.86f, 0.88f, 0.93f, 1f);

            if (!elegido) actionButtons[i].transform.localScale = Vector3.one;
        }
    }

    private string Ayuda()
    {
        switch (_fase)
        {
            case Fase.Apostando:
                if (ModoPokemon) return $"Apostás a {_pedido.ApuestaPokemon}  ·  PEDIR reparte";
                if (_apuesta <= 0) return "Elegí una ficha para apostar";
                if (_apuesta < minBet) return $"La apuesta mínima es {minBet}";
                return "PEDIR reparte  ·  B limpia la apuesta";
            case Fase.Repartiendo:
                return "Repartiendo...";
            case Fase.TurnoJugador:
                return _manos.Count > 1 ? $"Jugando la mano {_manoActual + 1}" : "¿Qué harás?";
            case Fase.TurnoDealer:
                return "Juega la casa...";
            default:
                return "";
        }
    }

    // El clip asignado en el prefab manda; sin clip, el del catalogo de audio.
    private static void Sonar(AudioClip clip, System.Action delCatalogo)
    {
        if (clip != null) AudioService.Instance.PlaySFX(clip);
        else delCatalogo?.Invoke();
    }
}
