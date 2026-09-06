using UnityEngine;
using UnityEngine.UI;

// Un premio de la ruleta. Se configura entero desde el Inspector: nombre,
// icono, cantidad y peso. El icono es el que se dibuja dentro del gajo, asi
// que cambiar el premio cambia la rueda sin tocar codigo.
[System.Serializable]
public class Prize
{
    [Tooltip("Como se llama el premio en el cartel. Ej: Hiperpocion")]
    public string prizeName = "Poke Ball";
    [Tooltip("Plural para cuando la cantidad es mayor a 1. Vacio = nombre + s.")]
    public string prizePlural = "";
    [Tooltip("Articulo del singular: una, un, o vacio para los que no llevan.")]
    public string article = "una";

    public Sprite icon;
    [Min(1)] public int amount = 1;

    [Tooltip("Peso relativo. No hace falta que la suma de 100: se normaliza sola.")]
    [Min(0f)] public float probability = 10f;

    [Tooltip("Objeto que se mete en la mochila al ganarlo. Vacio = premio solo " +
             "de cartel (util para el premio misterioso).")]
    public HealItemDefinition item;

    [Tooltip("Pisa el texto del cartel. Vacio = se arma solo con nombre y cantidad.")]
    [TextArea(2, 3)] public string customMessage = "";

    // "¡Has obtenido una Hiperpoción!" / "¡Has obtenido 3 Poké Balls!"
    public string BuildMessage()
    {
        if (!string.IsNullOrEmpty(customMessage)) return customMessage;

        if (amount > 1)
        {
            string plural = string.IsNullOrEmpty(prizePlural) ? prizeName + "s" : prizePlural;
            return $"¡Has obtenido {amount} {plural}!";
        }

        string art = string.IsNullOrEmpty(article) ? "" : article + " ";
        return $"¡Has obtenido {art}{prizeName}!";
    }
}

// La ruleta de premios.
//
// Es una pantalla del MenuManager como cualquier otra (asi hereda la pila, el
// input remapeable y la caja de texto), pero con dos diferencias a proposito:
//
//   1. DimsBackground = false. La ruleta se abre ENCIMA del mapa: no hay panel,
//      no hay fondo oscuro y el escenario se sigue viendo detras. Tiene que
//      leerse como una maquina del mundo, no como una ventana aparte.
//   2. El resultado NO tiene panel propio. Sale por la MISMA caja de texto que
//      usa el resto del juego (MenuScreenId.MessageBox, via el atajo
//      ShowMessage de MenuScreen): misma fuente, mismo marco, misma tecla para
//      avanzar.
//
// Por que la caja del MessageBox y no el DialogueController del mapa:
// PlayerInputController corta el input del mundo mientras MenuManager.AnyOpen
// sea true, asi que una caja del mapa abierta desde aca no recibiria ninguna
// tecla y se colgaria con el cartel prendido. El MessageBox es esa misma caja
// pero viviendo en la pila de menus, que es donde esta la ruleta.
public class PrizeWheelController : MenuScreen
{
    private enum Fase { Listo, Girando, Celebrando, Cartel }

    [Header("Rueda")]
    [Tooltip("Lo unico que rota. El puntero va aparte y se queda quieto.")]
    [SerializeField] private RectTransform wheel;
    [Tooltip("Un Image por gajo, en el orden de los premios. Los crea el builder.")]
    [SerializeField] private Image[] prizeIcons = new Image[8];

    [Header("Puntero")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private Image pointerImage;
    [Tooltip("Reposo + los cuadros del golpe. El primero es el sprite quieto.")]
    [SerializeField] private Sprite[] pointerFrames = new Sprite[4];
    [Tooltip("Cuanto se inclina el puntero en cada tic, en grados.")]
    [SerializeField] private float pointerKickAngle = 13f;
    [Tooltip("Cuanto tarda en volver a su lugar despues de un tic.")]
    [SerializeField] private float pointerKickRecovery = 0.085f;

    [Header("Botones")]
    [SerializeField] private Button spinButton;
    [SerializeField] private Image spinButtonImage;
    [SerializeField] private Sprite spinEnabledSprite;
    [SerializeField] private Sprite spinDisabledSprite;
    [SerializeField] private Button backButton;
    [SerializeField] private Image backButtonImage;

    [Header("Celebracion")]
    [Tooltip("Pokeball con destellos que aparece en el centro al ganar.")]
    [SerializeField] private Image prizeFx;
    [Tooltip("Chispas que salen sobre el gajo ganador.")]
    [SerializeField] private Image sparkFx;
    [SerializeField] private Sprite[] sparkFrames = new Sprite[3];
    [SerializeField] private float celebrationSeconds = 0.85f;

    [Header("Premios")]
    [Tooltip("Uno por gajo, en el orden en que estan dibujados en la rueda.")]
    [SerializeField] private Prize[] prizes = new Prize[0];

    [Header("Giro")]
    [SerializeField] private float spinSeconds = 3.6f;
    [Tooltip("Vueltas enteras antes de frenar en el premio.")]
    [SerializeField] private int minTurns = 4;
    [SerializeField] private int maxTurns = 6;
    [Tooltip("Como frena. Tiene que arrancar rapido y aflojar al final; una " +
             "recta se ve como una animacion rota, no como una ruleta.")]
    [SerializeField] private AnimationCurve spinCurve = DefaultCurve();

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioClip tickClip;
    [SerializeField] private AudioClip winClip;

    // La ruleta se abre sobre el mapa: nada de oscurecer el escenario.
    public override bool DimsBackground => false;

    private readonly PrizeWheelResult _result = new PrizeWheelResult();

    private Fase _fase = Fase.Listo;
    private float _t;
    private float _anguloInicial;
    private float _anguloRecorrido;
    private int _ticksPasados;
    private float _tick;              // 1 = recien golpeado, 0 = en reposo
    private int _indiceGanador = -1;
    private float _celebracion;
    private bool _volverSeleccionado;
    private Prize[] _enJuego;

    // Grados que ocupa cada gajo.
    private float PasoDeGajo => _enJuego != null && _enJuego.Length > 0
        ? 360f / _enJuego.Length
        : 45f;

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

    protected override void OnOpen(object payload)
    {
        var pedido = payload as PrizeWheelRequest;

        // Los premios pueden venir de quien abre (una maquina distinta por
        // piso) o quedarse los que tenga cargados el prefab.
        _enJuego = pedido != null && pedido.Prizes != null && pedido.Prizes.Length > 0
            ? pedido.Prizes
            : prizes;

        _result.Giros = 0;
        _result.UltimoPremio = null;
        Result = _result;

        RepartirIconos();
        Reiniciar();
    }

    // Cada apertura empieza de cero: sin efectos prendidos, sin premio viejo,
    // la rueda en su posicion inicial y GIRAR habilitado.
    private void Reiniciar()
    {
        _fase = Fase.Listo;
        _t = 0f;
        _tick = 0f;
        _ticksPasados = 0;
        _anguloRecorrido = 0f;
        _indiceGanador = -1;
        _celebracion = 0f;
        _volverSeleccionado = false;

        if (wheel != null) wheel.localRotation = Quaternion.identity;
        _anguloInicial = 0f;

        ApagarEfectos();
        AcomodarPuntero();
        RefrescarBotones();
    }

    protected override void OnClose()
    {
        ApagarEfectos();
    }

    // Vuelve el foco despues de que el jugador cerro el cartel del premio.
    public override void OnFocusGained()
    {
        if (_fase != Fase.Cartel) return;

        ApagarEfectos();
        _fase = Fase.Listo;
        RefrescarBotones();
    }

    private void ApagarEfectos()
    {
        if (prizeFx != null) prizeFx.gameObject.SetActive(false);
        if (sparkFx != null) sparkFx.gameObject.SetActive(false);
    }

    // Pone el icono de cada premio en su gajo. El gajo 0 es el que queda debajo
    // del puntero cuando la rueda esta en cero.
    private void RepartirIconos()
    {
        if (prizeIcons == null) return;

        int n = _enJuego != null ? _enJuego.Length : 0;
        for (int i = 0; i < prizeIcons.Length; i++)
        {
            Image slot = prizeIcons[i];
            if (slot == null) continue;

            bool hay = i < n && _enJuego[i] != null && _enJuego[i].icon != null;
            slot.gameObject.SetActive(hay);
            if (!hay) continue;

            slot.sprite = _enJuego[i].icon;
            // Tamano nativo y no un cuadro fijo: los iconos miden distinto cada
            // uno y estirarlos a una caja comun rompe la grilla del pixel art.
            slot.SetNativeSize();
        }
    }

    // ---------------------------------------------------------------- input

    public override void HandleAction(GameAction action)
    {
        // Mientras gira o celebra no se acepta NADA: es lo que evita que
        // machacar la tecla encadene dos giros o cierre la pantalla a mitad de
        // la animacion.
        if (_fase == Fase.Girando || _fase == Fase.Celebrando) return;

        switch (action)
        {
            case GameAction.Left:
            case GameAction.Up:
                _volverSeleccionado = false;
                RefrescarBotones();
                return;

            case GameAction.Right:
            case GameAction.Down:
                _volverSeleccionado = true;
                RefrescarBotones();
                return;

            case GameAction.Confirm:
                if (_volverSeleccionado) CloseSelf();
                else Girar();
                return;

            case GameAction.Cancel:
                CloseSelf();
                return;
        }
    }

    // Los enganchan los botones del prefab, para que tambien ande con el mouse.
    public void OnSpinButton()
    {
        if (_fase == Fase.Listo) Girar();
    }

    public void OnBackButton()
    {
        if (_fase == Fase.Listo) CloseSelf();
    }

    // ---------------------------------------------------------------- girar

    private void Girar()
    {
        if (_fase != Fase.Listo) return;
        if (_enJuego == null || _enJuego.Length == 0)
        {
            Debug.LogError("[Ruleta] No hay premios cargados: no se puede girar.");
            return;
        }

        _indiceGanador = Sortear();

        float paso = PasoDeGajo;
        int vueltas = Random.Range(minTurns, maxTurns + 1);

        // El gajo i esta a -paso*i del puntero, asi que hay que girar +paso*i
        // para traerlo arriba. El ruidito evita que siempre frene clavado en el
        // centro del gajo, que se nota falso.
        float jitter = Random.Range(-paso * 0.34f, paso * 0.34f);
        float destino = vueltas * 360f + paso * _indiceGanador + jitter;

        _anguloInicial = NormalizarAngulo(wheel != null ? wheel.localEulerAngles.z : 0f);
        _anguloRecorrido = destino;
        _ticksPasados = 0;
        _t = 0f;
        _fase = Fase.Girando;
        _result.Giros++;

        RefrescarBotones();
    }

    // Sorteo por peso. Los pesos son relativos: se normalizan solos.
    private int Sortear()
    {
        float total = 0f;
        foreach (Prize p in _enJuego)
            if (p != null) total += Mathf.Max(0f, p.probability);

        // Sin pesos utiles, todos los gajos valen lo mismo.
        if (total <= 0f) return Random.Range(0, _enJuego.Length);

        float tirada = Random.value * total;
        for (int i = 0; i < _enJuego.Length; i++)
        {
            if (_enJuego[i] == null) continue;
            tirada -= Mathf.Max(0f, _enJuego[i].probability);
            if (tirada <= 0f) return i;
        }
        return _enJuego.Length - 1;
    }

    // ----------------------------------------------------------------- tick

    public override void OnTick(float unscaledDeltaTime)
    {
        AflojarPuntero(unscaledDeltaTime);

        switch (_fase)
        {
            case Fase.Girando: AvanzarGiro(unscaledDeltaTime); break;
            case Fase.Celebrando: AvanzarCelebracion(unscaledDeltaTime); break;
        }
    }

    private void AvanzarGiro(float dt)
    {
        _t += dt;
        float p = Mathf.Clamp01(_t / Mathf.Max(0.01f, spinSeconds));

        // La curva se normaliza para que cualquiera que dibuje el equipo termine
        // igual en 1: si no, la rueda frena en un gajo que no es el sorteado.
        float fin = spinCurve.Evaluate(1f);
        float avance = Mathf.Approximately(fin, 0f) ? p : spinCurve.Evaluate(p) / fin;

        float recorrido = _anguloRecorrido * avance;
        if (wheel != null)
            wheel.localRotation = Quaternion.Euler(0f, 0f, _anguloInicial + recorrido);

        // Un tic por cada gajo que pasa por el puntero.
        int pasados = Mathf.FloorToInt(recorrido / PasoDeGajo);
        if (pasados > _ticksPasados)
        {
            _ticksPasados = pasados;
            _tick = 1f;
            if (tickClip != null) AudioService.Instance.PlaySFX(tickClip);
        }

        if (p < 1f) return;

        // Clavado en el angulo exacto: acumular flotantes durante seis vueltas
        // deja el gajo corrido unos grados.
        if (wheel != null)
            wheel.localRotation = Quaternion.Euler(0f, 0f, _anguloInicial + _anguloRecorrido);

        Ganar();
    }

    private void Ganar()
    {
        _fase = Fase.Celebrando;
        _celebracion = 0f;

        Prize premio = _enJuego[_indiceGanador];
        _result.UltimoPremio = premio;

        // El premio se entrega ACA, no cuando se cierra el cartel: si el juego
        // se corta con la caja abierta, el objeto ya es del jugador.
        if (premio != null && premio.item != null && Player != null)
            Player.Inventory.AddHealItem(premio.item, premio.amount);

        PrenderEfectos();
        if (winClip != null) AudioService.Instance.PlaySFX(winClip);
    }

    private void PrenderEfectos()
    {
        if (prizeFx != null)
        {
            prizeFx.gameObject.SetActive(true);
            prizeFx.transform.localScale = Vector3.zero;
        }

        if (sparkFx != null)
        {
            sparkFx.gameObject.SetActive(true);
            // Las chispas salen sobre el gajo que quedo debajo del puntero.
            var rt = (RectTransform)sparkFx.transform;
            rt.anchoredPosition = new Vector2(0f, RadioDeLosIconos());
        }
    }

    private float RadioDeLosIconos()
    {
        // El gajo ganador siempre termina arriba, asi que alcanza con el radio
        // al que estan puestos los iconos.
        if (prizeIcons != null && _indiceGanador >= 0 && _indiceGanador < prizeIcons.Length
            && prizeIcons[_indiceGanador] != null)
            return ((RectTransform)prizeIcons[_indiceGanador].transform).anchoredPosition.magnitude;
        return 44f;
    }

    private void AvanzarCelebracion(float dt)
    {
        _celebracion += dt;
        float p = Mathf.Clamp01(_celebracion / Mathf.Max(0.01f, celebrationSeconds));

        // Pop con un rebote corto y despues se queda quieta.
        if (prizeFx != null)
        {
            float escala = p < 0.45f
                ? Mathf.SmoothStep(0f, 1.18f, p / 0.45f)
                : Mathf.Lerp(1.18f, 1f, (p - 0.45f) / 0.55f);
            prizeFx.transform.localScale = Vector3.one * escala;
        }

        if (sparkFx != null && sparkFrames != null && sparkFrames.Length > 0)
        {
            int cuadro = Mathf.FloorToInt(_celebracion / 0.11f) % sparkFrames.Length;
            if (sparkFrames[cuadro] != null) sparkFx.sprite = sparkFrames[cuadro];
        }

        if (p < 1f) return;

        _fase = Fase.Cartel;
        MostrarCartelDelPremio();
    }

    // El cartel del premio va por la caja de texto de siempre. No hay panel de
    // recompensa: es el mismo cuadro que usa el juego para todo lo demas.
    private void MostrarCartelDelPremio()
    {
        Prize premio = _enJuego[_indiceGanador];
        string texto = premio != null ? premio.BuildMessage() : "¡Has obtenido un premio!";
        ShowMessage(texto);
    }

    // -------------------------------------------------------------- puntero

    private void AflojarPuntero(float dt)
    {
        if (_tick > 0f)
            _tick = Mathf.MoveTowards(_tick, 0f, dt / Mathf.Max(0.01f, pointerKickRecovery));

        AcomodarPuntero();
    }

    private void AcomodarPuntero()
    {
        if (pointer != null)
            pointer.localRotation = Quaternion.Euler(0f, 0f, pointerKickAngle * _tick);

        if (pointerImage == null || pointerFrames == null || pointerFrames.Length == 0) return;

        // Cuadro 0 = quieto; los otros son el golpe volviendo a su lugar.
        int cuadro = 0;
        if (_tick > 0.66f) cuadro = 1;
        else if (_tick > 0.33f) cuadro = 2;
        else if (_tick > 0f) cuadro = 3;

        cuadro = Mathf.Min(cuadro, pointerFrames.Length - 1);
        if (pointerFrames[cuadro] != null) pointerImage.sprite = pointerFrames[cuadro];
    }

    // -------------------------------------------------------------- botones

    private void RefrescarBotones()
    {
        bool sePuedeGirar = _fase == Fase.Listo;

        if (spinButton != null) spinButton.interactable = sePuedeGirar;
        if (spinButtonImage != null)
        {
            Sprite s = sePuedeGirar ? spinEnabledSprite : spinDisabledSprite;
            if (s != null) spinButtonImage.sprite = s;
        }

        if (backButton != null) backButton.interactable = sePuedeGirar;

        // La seleccion con teclado se marca agrandando un poco el boton elegido.
        Resaltar(spinButtonImage, sePuedeGirar && !_volverSeleccionado);
        Resaltar(backButtonImage, sePuedeGirar && _volverSeleccionado);
    }

    private static void Resaltar(Image boton, bool elegido)
    {
        if (boton == null) return;
        boton.transform.localScale = elegido ? Vector3.one * 1.06f : Vector3.one;
        boton.color = elegido ? Color.white : new Color(0.82f, 0.82f, 0.86f, 1f);
    }

    private static float NormalizarAngulo(float grados)
    {
        grados %= 360f;
        return grados < 0f ? grados + 360f : grados;
    }
}
