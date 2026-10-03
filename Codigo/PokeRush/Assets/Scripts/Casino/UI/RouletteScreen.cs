using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mesa de ruleta del casino.
//
// Es una pantalla del MenuManager como el resto, asi hereda la pila, el input
// remapeable y la caja de texto. Dos decisiones que vale la pena tener a mano:
//
//   1. El resultado sale por la MISMA caja de texto que usa todo el juego
//      (MenuScreenId.MessageBox, via ShowMessage). No hay panel de recompensa
//      propio: misma fuente, mismo marco y misma tecla para avanzar que un
//      dialogo cualquiera. Va por ahi y no por el DialogueController del mapa
//      porque PlayerInputController corta el input del mundo mientras
//      MenuManager.AnyOpen sea true, asi que una caja del mapa abierta desde
//      aca no recibiria ninguna tecla.
//   2. La plata se descuenta al GIRAR, no al poner cada ficha. Asi cerrar la
//      mesa con fichas puestas no obliga a devolver nada y no hay forma de
//      perder plata sin haber jugado.
//
// La logica de la mesa (que numero es rojo, cuanto paga cada apuesta) esta en
// RouletteBoard, que no sabe nada de Unity y se puede probar sin abrir escena.
public class RouletteScreen : MenuScreen
{
    private enum Fase { Apostando, Girando, Celebrando, Cartel }

    // En que grupo de controles esta parado el jugador.
    private enum Zona { Grilla, Colores, Fichas, Botones }

    [Header("Rueda")]
    [Tooltip("Lo unico que rota. La bola va aparte y se mueve sola.")]
    [SerializeField] private RectTransform wheel;
    [SerializeField] private RectTransform ball;
    [Tooltip("Radio del anillo exterior por el que corre la bola, en px.")]
    [SerializeField] private float ballOuterRadius = 62f;
    [Tooltip("Radio del anillo de casilleros donde termina cayendo.")]
    [SerializeField] private float ballPocketRadius = 44f;

    [Header("Casilleros")]
    [Tooltip("37 celdas: la 0 es el cero y despues van 1..36. Las crea el builder.")]
    [SerializeField] private Image[] cellBackgrounds = new Image[RouletteBoard.Casilleros];
    [SerializeField] private Image[] cellGlows = new Image[RouletteBoard.Casilleros];
    [SerializeField] private Image[] cellChips = new Image[RouletteBoard.Casilleros];
    [SerializeField] private Text[] cellAmounts = new Text[RouletteBoard.Casilleros];

    [Header("Apuestas de color")]
    [Tooltip("El circulo rojo/negro de la mesa. Es lo que late al tener el foco.")]
    [SerializeField] private Image redMarker;
    [SerializeField] private Image blackMarker;
    [SerializeField] private Image redGlow;
    [SerializeField] private Image redChip;
    [SerializeField] private Text redAmount;
    [SerializeField] private Image blackGlow;
    [SerializeField] private Image blackChip;
    [SerializeField] private Text blackAmount;

    [Header("Fichas")]
    [Tooltip("Valor de cada ficha, en el orden en que estan dibujadas.")]
    [SerializeField] private int[] chipValues = { 1, 5, 25, 100, 500, 1000 };
    [SerializeField] private Image[] chipButtons = new Image[6];
    [SerializeField] private Image[] chipGlows = new Image[6];

    [Header("Botones")]
    [SerializeField] private Image clearButton;
    [SerializeField] private Image spinButton;
    [SerializeField] private Image backButton;
    [SerializeField] private Sprite spinEnabledSprite;
    [SerializeField] private Sprite spinDisabledSprite;

    [Header("Carteles")]
    [SerializeField] private Text moneyLabel;
    [SerializeField] private Text betLabel;
    [SerializeField] private Text resultLabel;
    [SerializeField] private Image resultChip;
    [Tooltip("Fondo del numero del resultado: rojo, negro y verde, en ese orden.")]
    [SerializeField] private Sprite[] resultChipSprites = new Sprite[3];
    [SerializeField] private Text hintLabel;

    [Header("Historial")]
    [Tooltip("Los ultimos resultados, del mas viejo al mas nuevo.")]
    [SerializeField] private Image[] historyChips = new Image[10];
    [SerializeField] private Text[] historyLabels = new Text[10];

    [Header("Efectos")]
    [SerializeField] private Image sparkFx;
    [SerializeField] private Image confettiFx;
    [SerializeField] private float celebrationSeconds = 1.1f;

    [Header("Giro")]
    [SerializeField] private float spinSeconds = 4.2f;
    [SerializeField] private int minTurns = 4;
    [SerializeField] private int maxTurns = 6;
    [Tooltip("Vueltas que da la bola, siempre al reves que la rueda.")]
    [SerializeField] private int ballTurns = 7;
    [Tooltip("Como frena. Tiene que arrancar rapido y aflojar al final; una " +
             "recta se ve como una animacion rota, no como una ruleta.")]
    [SerializeField] private AnimationCurve spinCurve = DefaultCurve();

    [Header("Limites")]
    [SerializeField] private int minBet = 1;
    [SerializeField] private int maxBet = 5000;

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioClip tickClip;
    [SerializeField] private AudioClip chipClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;

    private readonly RouletteScreenResult _result = new RouletteScreenResult();

    // Lo apostado en cada lugar de la mesa. Se vacia despues de cada giro.
    private readonly Dictionary<RouletteBet, int> _apuestas = new Dictionary<RouletteBet, int>();
    private readonly List<int> _historial = new List<int>();

    private Fase _fase = Fase.Apostando;
    private Zona _zona = Zona.Grilla;
    private int _celda = 1;        // 0..36 dentro de la grilla
    private int _color;            // 0 = rojo, 1 = negro
    private int _ficha;            // indice en chipValues
    private int _boton;            // 0 = limpiar, 1 = girar, 2 = volver

    private float _t;
    private float _anguloRueda, _giroTotal;
    private float _anguloBola, _giroBola;
    private int _ticks;
    private int _ganador = -1;
    private int _pagado;
    private float _celebracion;
    private float _latido;

    private int TotalApostado
    {
        get
        {
            int t = 0;
            foreach (int v in _apuestas.Values) t += v;
            return t;
        }
    }

    private int Plata => Player != null ? Player.Money : 0;

    private int CantidadDeFichas => chipValues != null && chipValues.Length > 0 ? chipValues.Length : 1;

    private int ValorDeFicha => chipValues != null && chipValues.Length > 0
        ? chipValues[Mathf.Clamp(_ficha, 0, chipValues.Length - 1)]
        : 1;

    // Arranca rapido y se va apagando: es 1-(1-t)^4 puesto en cuatro claves.
    private static AnimationCurve DefaultCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 3.4f, 3.4f),
            new Keyframe(0.35f, 0.82f, 1.05f, 1.05f),
            new Keyframe(0.7f, 0.98f, 0.16f, 0.16f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    // ---------------------------------------------------------------- abrir

    // La ruleta de premios por ganarle fuerte al color. La trae el pedido de la
    // mesa; sin tabla, no se abre nunca.
    private CasinoPrizeTable _ruletaPorColor;
    private int _umbralRuletaPorColor = 200;
    private bool _premioPendiente;

    protected override void OnOpen(object payload)
    {
        var pedido = payload as RouletteScreenRequest;
        if (pedido != null)
        {
            if (pedido.ApuestaMinima > 0) minBet = pedido.ApuestaMinima;
            if (pedido.ApuestaMaxima > 0) maxBet = pedido.ApuestaMaxima;
        }
        _ruletaPorColor = pedido != null ? pedido.RuletaPorColor : null;
        _umbralRuletaPorColor = pedido != null ? pedido.UmbralRuletaPorColor : 200;
        _premioPendiente = false;
        _result.RuletasDePremio = 0;

        _result.Giros = 0;
        _result.Neto = 0;
        _result.MayorPremio = 0;
        _result.Historial.Clear();
        Result = _result;

        _historial.Clear();
        _apuestas.Clear();
        _ganador = -1;
        _pagado = 0;

        _zona = Zona.Grilla;
        _celda = 1;
        _color = 0;
        _ficha = 0;
        _boton = 1;

        if (wheel != null) wheel.localRotation = Quaternion.identity;
        _anguloRueda = 0f;
        _anguloBola = 0f;

        ApagarEfectos();
        PonerBola(0f, ballOuterRadius);
        Refrescar();
        RefrescarHistorial();
        MostrarResultado(-1);
    }

    // La grabacion de la rueda girando, mientras gira.
    private SfxEnCurso _sonidoDelGiro;

    protected override void OnClose()
    {
        _sonidoDelGiro?.Parar(0.2f);
        _sonidoDelGiro = null;
        ApagarEfectos();
    }

    // Vuelve el foco despues de que el jugador cerro el cartel del resultado.
    public override void OnFocusGained()
    {
        if (_fase != Fase.Cartel) return;

        // Primero el premio, si lo hubo. La ruleta se abre desde el callback
        // del aviso: para ese momento esta pantalla ya volvio a Apostando (el
        // aviso le devuelve el foco antes de correr su callback), asi que al
        // cerrar la ruleta de premios la mesa queda lista para seguir.
        if (_premioPendiente)
        {
            _premioPendiente = false;
            AbrirRuletaDePremio();
            return;
        }

        ApagarEfectos();
        _apuestas.Clear();
        _fase = Fase.Apostando;
        Refrescar();
    }

    private void AbrirRuletaDePremio()
    {
        _result.RuletasDePremio++;
        ShowMessage($"¡Le ganaste más de {_umbralRuletaPorColor} al color! Te toca la ruleta de premios.", () =>
        {
            if (Manager == null || _ruletaPorColor == null) return;
            Manager.Open(MenuScreenId.PrizeWheel,
                new PrizeWheelRequest { Prizes = _ruletaPorColor.premios, RecompensaUnica = true },
                _ => CasinoDialogo.Guardar());
        });
    }

    private void ApagarEfectos()
    {
        if (sparkFx != null) sparkFx.gameObject.SetActive(false);
        if (confettiFx != null) confettiFx.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- input

    public override void HandleAction(GameAction action)
    {
        // Mientras gira o celebra no se acepta NADA: es lo que evita que
        // machacar la tecla encadene dos giros o cierre la mesa a mitad de la
        // animacion.
        if (_fase != Fase.Apostando) return;

        switch (action)
        {
            case GameAction.Up: Mover(0, -1); return;
            case GameAction.Down: Mover(0, 1); return;
            case GameAction.Left: Mover(-1, 0); return;
            case GameAction.Right: Mover(1, 0); return;
            case GameAction.Confirm: Confirmar(); return;
            case GameAction.Cancel: CloseSelf(); return;
        }
    }

    // Navegacion entre los cuatro grupos de controles. Se mueve como se ve:
    // la grilla arriba, los colores debajo, las fichas y al final los botones.
    private void Mover(int dx, int dy)
    {
        switch (_zona)
        {
            case Zona.Grilla: MoverEnGrilla(dx, dy); break;

            case Zona.Colores:
                if (dx != 0) _color = Mathf.Clamp(_color + dx, 0, 1);
                else if (dy < 0) { _zona = Zona.Grilla; _celda = 25 + _color * 6; }
                else if (dy > 0) _zona = Zona.Fichas;
                break;

            case Zona.Fichas:
                if (dx != 0) _ficha = Mathf.Clamp(_ficha + dx, 0, CantidadDeFichas - 1);
                else if (dy < 0) _zona = Zona.Colores;
                else if (dy > 0) _zona = Zona.Botones;
                break;

            case Zona.Botones:
                if (dx != 0) _boton = Mathf.Clamp(_boton + dx, 0, 2);
                else if (dy < 0) _zona = Zona.Fichas;
                break;
        }

        Refrescar();
    }

    // La grilla son tres filas de doce (1-12, 13-24, 25-36) mas el cero, que
    // vive a la izquierda de la primera fila.
    private void MoverEnGrilla(int dx, int dy)
    {
        if (_celda == 0)
        {
            // Desde el cero solo se puede entrar a la grilla por la derecha o
            // bajar a los colores.
            if (dx > 0) _celda = 1;
            else if (dy > 0) _zona = Zona.Colores;
            return;
        }

        int fila = (_celda - 1) / 12;
        int col = (_celda - 1) % 12;

        if (dx != 0)
        {
            col += dx;
            if (col < 0) { _celda = 0; return; }
            if (col > 11) col = 11;
            _celda = fila * 12 + col + 1;
            return;
        }

        fila += dy;
        if (fila < 0) { fila = 0; }
        else if (fila > 2) { _zona = Zona.Colores; _color = col < 6 ? 0 : 1; return; }
        _celda = fila * 12 + col + 1;
    }

    private void Confirmar()
    {
        switch (_zona)
        {
            case Zona.Grilla: Apostar(RouletteBet.AlNumero(_celda)); break;
            case Zona.Colores: Apostar(_color == 0 ? RouletteBet.AlRojo() : RouletteBet.AlNegro()); break;
            case Zona.Fichas: break;   // elegir la ficha ya es el efecto
            case Zona.Botones:
                if (_boton == 0) Limpiar();
                else if (_boton == 1) Girar();
                else CloseSelf();
                break;
        }
    }

    // Los enganchan los botones del prefab, para que tambien ande con el mouse.
    public void OnCellButton(int numero)
    {
        if (_fase != Fase.Apostando) return;
        _zona = Zona.Grilla;
        _celda = Mathf.Clamp(numero, 0, RouletteBoard.Casilleros - 1);
        Apostar(RouletteBet.AlNumero(_celda));
    }

    public void OnColorButton(int cual)
    {
        if (_fase != Fase.Apostando) return;
        _zona = Zona.Colores;
        _color = Mathf.Clamp(cual, 0, 1);
        Apostar(_color == 0 ? RouletteBet.AlRojo() : RouletteBet.AlNegro());
    }

    public void OnChipButton(int indice)
    {
        if (_fase != Fase.Apostando) return;
        _zona = Zona.Fichas;
        _ficha = Mathf.Clamp(indice, 0, CantidadDeFichas - 1);
        Refrescar();
    }

    public void OnSpinButton()
    {
        if (_fase == Fase.Apostando) Girar();
    }

    public void OnClearButton()
    {
        if (_fase == Fase.Apostando) Limpiar();
    }

    public void OnBackButton()
    {
        if (_fase == Fase.Apostando) CloseSelf();
    }

    // -------------------------------------------------------------- apostar

    private void Apostar(RouletteBet donde)
    {
        int ficha = ValorDeFicha;
        int puesto = _apuestas.TryGetValue(donde, out int y) ? y : 0;

        // Nunca se puede comprometer mas plata de la que hay: como el descuento
        // recien pasa al girar, este es el unico lugar donde se controla.
        if (TotalApostado + ficha > Plata)
        {
            Avisar("No te alcanza para esa ficha.");
            return;
        }

        if (puesto + ficha > maxBet)
        {
            Avisar($"El maximo por casillero es {maxBet}.");
            return;
        }

        _apuestas[donde] = puesto + ficha;
        if (chipClip != null) AudioService.Instance.PlaySFX(chipClip);
        else Sonidos.Fichas();
        Refrescar();
    }

    private void Limpiar()
    {
        if (_apuestas.Count == 0) return;
        _apuestas.Clear();
        if (chipClip != null) AudioService.Instance.PlaySFX(chipClip);
        else Sonidos.Fichas();
        Refrescar();
    }

    private void Avisar(string texto)
    {
        if (hintLabel == null) return;
        hintLabel.text = texto;
        _latido = 1f;
    }

    // ---------------------------------------------------------------- girar

    private void Girar()
    {
        if (_fase != Fase.Apostando) return;

        int total = TotalApostado;
        if (total <= 0)
        {
            Avisar("Pone una ficha antes de girar.");
            return;
        }
        if (total < minBet)
        {
            Avisar($"La apuesta minima es {minBet}.");
            return;
        }
        if (total > Plata)
        {
            Avisar("No te alcanza la plata.");
            return;
        }

        // Recien aca se cobra: hasta este momento las fichas eran solo un
        // dibujo sobre la mesa.
        if (Player != null) Player.SpendMoney(total);
        _result.Neto -= total;

        _ganador = Random.Range(0, RouletteBoard.Casilleros);

        int vueltas = Random.Range(minTurns, maxTurns + 1);
        _anguloRueda = NormalizarAngulo(_anguloRueda);
        _giroTotal = vueltas * 360f + Random.Range(0f, 360f);

        // La bola tiene que terminar CLAVADA en el casillero ganador. La cuenta
        // esta en RouletteBoard para poder probarla sin abrir una escena.
        _giroBola = RouletteBoard.GiroDeLaBola(
            _ganador, _anguloRueda + _giroTotal, _anguloBola, ballTurns);

        _t = 0f;
        _ticks = 0;
        _fase = Fase.Girando;
        // Sin tic por casillero asignado, suena la grabacion de la rueda
        // girando, y se apaga cuando la bola cae.
        if (tickClip == null)
        {
            _sonidoDelGiro?.Parar(0f);
            _sonidoDelGiro = Sonidos.RuletaGiro();
        }
        _result.Giros++;

        Refrescar();
    }

    // ----------------------------------------------------------------- tick

    public override void OnTick(float unscaledDeltaTime)
    {
        if (_latido > 0f)
        {
            _latido = Mathf.MoveTowards(_latido, 0f, unscaledDeltaTime / 0.9f);
            // Al apagarse el aviso vuelve el texto de ayuda que corresponda.
            if (_latido <= 0f) Refrescar();
        }

        switch (_fase)
        {
            case Fase.Apostando: LatirSeleccion(unscaledDeltaTime); break;
            case Fase.Girando: AvanzarGiro(unscaledDeltaTime); break;
            case Fase.Celebrando: AvanzarCelebracion(unscaledDeltaTime); break;
        }
    }

    // La celda elegida respira un poco: es lo que deja claro donde estas
    // parado sin tener que dibujar un cursor aparte.
    private void LatirSeleccion(float dt)
    {
        float p = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6.5f);
        Image foco = FocoActual();
        if (foco != null) foco.transform.localScale = Vector3.one * p;
    }

    private void AvanzarGiro(float dt)
    {
        _t += dt;
        float p = Mathf.Clamp01(_t / Mathf.Max(0.01f, spinSeconds));

        // La curva se normaliza para que cualquiera que dibuje el equipo
        // termine igual en 1: si no, la rueda frena en un casillero que no es
        // el sorteado.
        float fin = spinCurve.Evaluate(1f);
        float avance = Mathf.Approximately(fin, 0f) ? p : spinCurve.Evaluate(p) / fin;

        float rueda = _anguloRueda + _giroTotal * avance;
        if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, 0f, -rueda);

        // La bola cae del anillo exterior al de casilleros sobre el final.
        float caida = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, p));
        float radio = Mathf.Lerp(ballOuterRadius, ballPocketRadius, caida);
        float bola = _anguloBola + _giroBola * avance;
        PonerBola(bola, radio);

        // Un tic por cada casillero que pasa la bola respecto de la rueda.
        int pasados = Mathf.FloorToInt(Mathf.Abs(bola - rueda) / RouletteBoard.PasoDeCasillero);
        if (pasados > _ticks)
        {
            _ticks = pasados;
            if (tickClip != null) AudioService.Instance.PlaySFX(tickClip);
        }

        if (p < 1f) return;

        // Clavado en el angulo exacto: acumular flotantes durante seis vueltas
        // deja el casillero corrido unos grados.
        _anguloRueda = NormalizarAngulo(_anguloRueda + _giroTotal);
        _anguloBola = NormalizarAngulo(_anguloBola + _giroBola);
        if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, 0f, -_anguloRueda);
        PonerBola(_anguloBola, ballPocketRadius);

        Cobrar();
    }

    // Reparte lo que haya salido y prende los efectos.
    private void Cobrar()
    {
        _fase = Fase.Celebrando;
        _sonidoDelGiro?.Parar(0.5f);
        _sonidoDelGiro = null;
        _celebracion = 0f;

        _pagado = 0;
        foreach (KeyValuePair<RouletteBet, int> par in _apuestas)
        {
            if (!RouletteBoard.Gana(par.Key, _ganador)) continue;
            // Se lleva la ficha mas el pago: al pleno son 36 veces la ficha.
            _pagado += par.Value + par.Value * RouletteBoard.PagoDe(par.Key.Kind);
        }

        // El premio se entrega ACA, no cuando se cierra el cartel: si el juego
        // se corta con la caja abierta, la plata ya es del jugador.
        if (_pagado > 0 && Player != null) Player.AddMoney(_pagado);
        _result.Neto += _pagado;
        if (_pagado > _result.MayorPremio) _result.MayorPremio = _pagado;

        // Ganarle fuerte al color destraba la ruleta de premios. Se abre recien
        // cuando el jugador cierra el cartel del resultado (OnFocusGained).
        int ganadoAlColor = RouletteBoard.GananciaDeColor(_apuestas, _ganador);
        _premioPendiente = _ruletaPorColor != null && _ruletaPorColor.premios != null &&
                           _ruletaPorColor.premios.Length > 0 &&
                           RouletteBoard.DaRuletaDePremios(ganadoAlColor, _umbralRuletaPorColor);

        _historial.Add(_ganador);
        int tope = historyChips != null && historyChips.Length > 0 ? historyChips.Length : 10;
        while (_historial.Count > tope) _historial.RemoveAt(0);
        _result.Historial.Add(_ganador);

        MostrarResultado(_ganador);
        RefrescarHistorial();
        PrenderEfectos();

        AudioClip clip = _pagado > 0 ? winClip : loseClip;
        if (clip != null) AudioService.Instance.PlaySFX(clip);
        else if (_pagado > 0)
        {
            Sonidos.Fichas();
            Sonidos.PremioChico();
        }
    }

    private void PrenderEfectos()
    {
        if (sparkFx != null)
        {
            sparkFx.gameObject.SetActive(true);
            // El destello sale sobre el casillero donde quedo la bola.
            if (ball != null)
                ((RectTransform)sparkFx.transform).anchoredPosition = ball.anchoredPosition;
            sparkFx.transform.localScale = Vector3.zero;
        }

        // El confeti es solo para cuando se gana: prenderlo siempre le saca
        // todo el sentido al efecto.
        if (confettiFx != null && _pagado > 0)
        {
            confettiFx.gameObject.SetActive(true);
            confettiFx.transform.localScale = Vector3.zero;
        }
    }

    private void AvanzarCelebracion(float dt)
    {
        _celebracion += dt;
        float p = Mathf.Clamp01(_celebracion / Mathf.Max(0.01f, celebrationSeconds));

        // Pop con un rebote corto y despues se queda quieto.
        float escala = p < 0.4f
            ? Mathf.SmoothStep(0f, 1.2f, p / 0.4f)
            : Mathf.Lerp(1.2f, 1f, (p - 0.4f) / 0.6f);

        if (sparkFx != null && sparkFx.gameObject.activeSelf)
        {
            sparkFx.transform.localScale = Vector3.one * escala;
            sparkFx.transform.localRotation = Quaternion.Euler(0f, 0f, _celebracion * 180f);
        }

        if (confettiFx != null && confettiFx.gameObject.activeSelf)
            confettiFx.transform.localScale = Vector3.one * escala;

        if (resultChip != null)
            resultChip.transform.localScale = Vector3.one * Mathf.Lerp(1f, escala, 0.5f);

        if (p < 1f) return;

        _fase = Fase.Cartel;
        MostrarCartel();
    }

    // El cartel va por la caja de texto de siempre. No hay panel de recompensa
    // propio: es el mismo cuadro que usa el juego para todo lo demas.
    private void MostrarCartel()
    {
        string color = ColorEnLetras(_ganador);
        string salio = _ganador == 0 ? "¡Salió el 0!" : $"¡Salió el {_ganador} {color}!";

        // Todo en UNA linea, sin '\n': la caja de mensajes del juego mide 430
        // de ancho y estos carteles no pasan de ~50 caracteres, asi que entran
        // holgados sin necesidad de cortar. (Antes era obligatorio: con
        // interlineado 1, dos renglones de 'power clear' se pisaban. Ver
        // FuentePixel.)
        string texto = _pagado > 0
            ? $"{salio} ¡Has obtenido {_pagado} PokéDólares!"
            : $"{salio} Esta vez no hubo suerte.";

        ShowMessage(texto);
    }

    private static string ColorEnLetras(int numero)
    {
        switch (RouletteBoard.ColorDe(numero))
        {
            case RouletteColor.Rojo: return "rojo";
            case RouletteColor.Negro: return "negro";
            default: return "verde";
        }
    }

    // ------------------------------------------------------------------ bola

    private void PonerBola(float grados, float radio)
    {
        if (ball == null) return;

        // Los angulos van en sentido horario desde arriba, igual que la tabla
        // de RouletteBoard.
        float rad = (90f - grados) * Mathf.Deg2Rad;
        ball.anchoredPosition = new Vector2(radio * Mathf.Cos(rad), radio * Mathf.Sin(rad));
    }

    // --------------------------------------------------------------- pintar

    private void Refrescar()
    {
        bool apostando = _fase == Fase.Apostando;

        PintarCasilleros();
        PintarColores();
        PintarFichas();
        PintarBotones(apostando);

        int total = TotalApostado;
        if (betLabel != null) betLabel.text = total.ToString();
        if (moneyLabel != null) moneyLabel.text = Plata.ToString();

        if (hintLabel != null && _latido <= 0f)
            hintLabel.text = apostando
                ? (total > 0 ? "GIRAR para tirar la bola" : "Elegí una ficha y tocá la mesa")
                : "";
    }

    private void PintarCasilleros()
    {
        for (int n = 0; n < RouletteBoard.Casilleros; n++)
        {
            bool elegido = _fase == Fase.Apostando && _zona == Zona.Grilla && _celda == n;
            if (cellGlows != null && n < cellGlows.Length && cellGlows[n] != null)
                cellGlows[n].gameObject.SetActive(elegido);

            // El casillero ganador queda marcado hasta la proxima apuesta.
            if (cellBackgrounds != null && n < cellBackgrounds.Length && cellBackgrounds[n] != null)
            {
                bool gano = _fase != Fase.Apostando && n == _ganador;
                cellBackgrounds[n].color = gano ? Color.white : new Color(0.86f, 0.88f, 0.94f, 1f);
                if (!elegido) cellBackgrounds[n].transform.localScale = Vector3.one;
            }

            int puesto = _apuestas.TryGetValue(RouletteBet.AlNumero(n), out int v) ? v : 0;
            PintarFichaPuesta(n < cellChips.Length ? cellChips[n] : null,
                              n < cellAmounts.Length ? cellAmounts[n] : null, puesto);
        }
    }

    private void PintarColores()
    {
        bool enColores = _fase == Fase.Apostando && _zona == Zona.Colores;
        if (redGlow != null) redGlow.gameObject.SetActive(enColores && _color == 0);
        if (blackGlow != null) blackGlow.gameObject.SetActive(enColores && _color == 1);

        // Sin esto el circulo se queda con la escala del latido cuando el foco
        // se va a otra zona.
        if (redMarker != null && !(enColores && _color == 0))
            redMarker.transform.localScale = Vector3.one;
        if (blackMarker != null && !(enColores && _color == 1))
            blackMarker.transform.localScale = Vector3.one;

        int rojo = _apuestas.TryGetValue(RouletteBet.AlRojo(), out int r) ? r : 0;
        int negro = _apuestas.TryGetValue(RouletteBet.AlNegro(), out int n) ? n : 0;
        PintarFichaPuesta(redChip, redAmount, rojo);
        PintarFichaPuesta(blackChip, blackAmount, negro);
    }

    // Sobre cada apuesta se apoya una ficha LISA con el total encima.
    //
    // La primera version usaba la ficha del valor elegido (la de 100, la de 500)
    // y no se leia: esas fichas traen su numero dibujado, y con el total escrito
    // arriba quedaban dos numeros pisados en 15 px. La ficha lisa la pone el
    // builder una sola vez y aca solo se prende y se le escribe el monto.
    private void PintarFichaPuesta(Image chip, Text texto, int monto)
    {
        if (chip != null) chip.gameObject.SetActive(monto > 0);

        if (texto != null)
        {
            texto.gameObject.SetActive(monto > 0);
            if (monto > 0) texto.text = monto.ToString();
        }
    }

    private void PintarFichas()
    {
        bool enFichas = _fase == Fase.Apostando && _zona == Zona.Fichas;
        for (int i = 0; i < chipButtons.Length; i++)
        {
            if (chipGlows != null && i < chipGlows.Length && chipGlows[i] != null)
                chipGlows[i].gameObject.SetActive(i == _ficha);

            if (chipButtons[i] == null) continue;
            // La ficha elegida queda mas grande SIEMPRE, este o no el foco en
            // la fila: es la que se va a poner al tocar la mesa.
            float escala = i == _ficha ? (enFichas ? 1.22f : 1.12f) : 0.92f;
            chipButtons[i].transform.localScale = Vector3.one * escala;
            chipButtons[i].color = i == _ficha ? Color.white : new Color(0.7f, 0.72f, 0.78f, 1f);
        }
    }

    private void PintarBotones(bool apostando)
    {
        bool sePuedeGirar = apostando && TotalApostado > 0;

        if (spinButton != null)
        {
            Sprite s = sePuedeGirar ? spinEnabledSprite : spinDisabledSprite;
            if (s != null) spinButton.sprite = s;
        }

        Resaltar(clearButton, apostando && _zona == Zona.Botones && _boton == 0);
        Resaltar(spinButton, apostando && _zona == Zona.Botones && _boton == 1);
        Resaltar(backButton, apostando && _zona == Zona.Botones && _boton == 2);
    }

    private static void Resaltar(Image boton, bool elegido)
    {
        if (boton == null) return;
        boton.transform.localScale = elegido ? Vector3.one * 1.07f : Vector3.one;
        boton.color = elegido ? Color.white : new Color(0.82f, 0.84f, 0.9f, 1f);
    }

    // Que control esta bajo el cursor ahora mismo, para hacerlo latir.
    private Image FocoActual()
    {
        switch (_zona)
        {
            case Zona.Grilla:
                return cellBackgrounds != null && _celda < cellBackgrounds.Length
                    ? cellBackgrounds[_celda] : null;
            case Zona.Colores:
                return _color == 0 ? redMarker : blackMarker;
            case Zona.Fichas:
                return chipButtons != null && _ficha < chipButtons.Length ? chipButtons[_ficha] : null;
            case Zona.Botones:
                return _boton == 0 ? clearButton : _boton == 1 ? spinButton : backButton;
            default:
                return null;
        }
    }

    private void MostrarResultado(int numero)
    {
        if (resultLabel != null) resultLabel.text = numero < 0 ? "-" : numero.ToString();

        if (resultChip == null || resultChipSprites == null || resultChipSprites.Length < 3) return;

        int i = numero < 0 ? 2
            : RouletteBoard.ColorDe(numero) == RouletteColor.Rojo ? 0
            : RouletteBoard.ColorDe(numero) == RouletteColor.Negro ? 1 : 2;

        if (resultChipSprites[i] != null) resultChip.sprite = resultChipSprites[i];
        resultChip.transform.localScale = Vector3.one;
    }

    private void RefrescarHistorial()
    {
        if (historyChips == null) return;

        for (int i = 0; i < historyChips.Length; i++)
        {
            // El mas nuevo va a la derecha, asi que se alinea por el final.
            int idx = _historial.Count - historyChips.Length + i;
            bool hay = idx >= 0 && idx < _historial.Count;

            if (historyChips[i] != null)
            {
                historyChips[i].gameObject.SetActive(hay);
                if (hay && resultChipSprites != null && resultChipSprites.Length >= 3)
                {
                    int n = _historial[idx];
                    int c = RouletteBoard.ColorDe(n) == RouletteColor.Rojo ? 0
                          : RouletteBoard.ColorDe(n) == RouletteColor.Negro ? 1 : 2;
                    if (resultChipSprites[c] != null) historyChips[i].sprite = resultChipSprites[c];
                }
            }

            if (historyLabels != null && i < historyLabels.Length && historyLabels[i] != null)
            {
                historyLabels[i].gameObject.SetActive(hay);
                if (hay) historyLabels[i].text = _historial[idx].ToString();
            }
        }
    }

    private static float NormalizarAngulo(float grados)
    {
        grados %= 360f;
        return grados < 0f ? grados + 360f : grados;
    }
}
