using UnityEngine;
using UnityEngine.UI;

// Pantalla de la ruleta del piso 2.
//
// Misma idea que la tragaperras: es una MenuScreen que se abre encima del piso,
// sin cambiar de escena, y el jugador recupera el control al cerrarla. El motor
// elige la casilla (RouletteEngine), el servicio la aplica sobre la partida
// (RouletteRewardService) y esta clase solo gira la rueda y cuenta lo que paso.
public class RouletteScreen : MenuScreen
{
    [Header("Textos")]
    [SerializeField] private Text tituloText;
    [SerializeField] private Text saldoText;
    [SerializeField] private Text costoText;
    [SerializeField] private Text mensajeText;
    [Tooltip("Nombre del premio que acaba de salir.")]
    [SerializeField] private Text premioText;
    [Tooltip("Simbolo del premio que acaba de salir.")]
    [SerializeField] private Image premioIcono;

    [Header("Rueda")]
    [SerializeField] private RouletteWheelWidget rueda;

    [Header("Sonidos (opcionales)")]
    [SerializeField] private AudioClip sonidoGiro;
    [SerializeField] private AudioClip sonidoPremio;
    [SerializeField] private AudioClip sonidoSinDinero;

    private enum Fase { Esperando, Girando }

    private RouletteTableDefinition _mesa;
    private RouletteEngine _motor;
    private readonly IBattleRng _rng = new UnityRng();
    private readonly RouletteScreenResult _resultado = new RouletteScreenResult();

    private Fase _fase;
    private RouletteSpinResult _tirada;
    private float _tiempo;
    private float _anguloInicial;
    private float _anguloFinal;

    protected override void OnOpen(object payload)
    {
        var pedido = payload as RouletteScreenRequest;
        _mesa = pedido != null ? pedido.Mesa : null;

        _resultado.Giros = 0;
        _resultado.Neto = 0;
        _resultado.ObjetivoCumplido = false;
        Result = _resultado;

        string error = null;
        if (_mesa == null || !_mesa.Validar(out error))
        {
            Debug.LogError("[Casino] Mesa de ruleta invalida: " +
                           (_mesa == null ? "no hay definicion asignada" : error) + ".");
            Mensaje("Esta mesa esta fuera de servicio.");
            _motor = null;
            _fase = Fase.Esperando;
            return;
        }

        _motor = new RouletteEngine(_mesa);
        _fase = Fase.Esperando;
        _tirada = null;

        if (tituloText != null)
            tituloText.text = string.IsNullOrEmpty(pedido.Titulo) ? _mesa.mesaNombre : pedido.Titulo;

        if (rueda != null)
        {
            rueda.Construir(_mesa);
            rueda.SetAngulo(0f);
        }

        RefrescarTextos();
        MostrarPremio(null);
        Mensaje("Apoya la ficha y gira.");
    }

    protected override void OnClose()
    {
        _tirada = null;
        _motor = null;
    }

    public override void HandleAction(GameAction action)
    {
        // Con la rueda girando no se acepta nada: salir a mitad dejaria la
        // apuesta cobrada y el premio sin entregar.
        if (_fase == Fase.Girando) return;

        if (action == GameAction.Confirm) Girar();
        else if (action == GameAction.Cancel) CloseSelf();
    }

    public override void OnTick(float unscaledDeltaTime)
    {
        if (_fase != Fase.Girando || _mesa == null) return;

        _tiempo += unscaledDeltaTime;
        float t = Mathf.Clamp01(_tiempo / _mesa.duracionDelGiro);

        // Frenada suave: arranca rapido y se va muriendo, como una ruleta.
        float suave = 1f - Mathf.Pow(1f - t, 3f);
        if (rueda != null)
            rueda.SetAngulo(Mathf.Lerp(_anguloInicial, _anguloFinal, suave));

        if (t < 1f) return;

        Resolver();
    }

    // ---------- giro ----------

    private void Girar()
    {
        if (_motor == null) return;

        PlayerModel jugador = Player;
        if (jugador == null)
        {
            Debug.LogError("[Casino] No hay partida en curso: la ruleta no puede cobrar.");
            return;
        }

        if (jugador.Money < _mesa.costoPorGiro)
        {
            Sonar(sonidoSinDinero);
            Mensaje("No te alcanza para girar.");
            return;
        }

        // Se cobra antes de girar, igual que la tragaperras.
        jugador.SpendMoney(_mesa.costoPorGiro);
        _tirada = _motor.Girar(_rng);

        if (_tirada.Premio == null)
        {
            Mensaje("La rueda no arranco. Raro.");
            return;
        }

        _fase = Fase.Girando;
        _tiempo = 0f;
        _anguloInicial = 0f;

        // El motor ya decidio QUE premio salio. Aca solo se elige en cual de sus
        // casillas frenar, para que lo que se ve coincida con lo que toco.
        int casilla = rueda != null ? rueda.CasillaDe(_tirada.PremioIndex, _rng) : -1;
        _anguloFinal = _mesa.vueltas * 360f +
                       (rueda != null ? rueda.AnguloDeCasilla(casilla) : 0f);

        Sonar(sonidoGiro);
        Mensaje("Girando...");
        RefrescarTextos();
    }

    private void Resolver()
    {
        _fase = Fase.Esperando;

        PlayerModel jugador = Player;
        int saldoAntes = jugador != null ? jugador.Money : 0;

        RouletteApplyResult aplicado = RouletteRewardService.Aplicar(_tirada.Premio, jugador);

        _resultado.Giros++;
        // El neto cuenta la apuesta, que ya se cobro antes de girar.
        _resultado.Neto += (jugador != null ? jugador.Money - saldoAntes : 0) - _mesa.costoPorGiro;

        if (aplicado.CumpleObjetivo)
        {
            _resultado.ObjetivoCumplido = true;

            // El objetivo del piso se anota en la historia, que es lo que mira
            // el ascensor. Se hace aca y no en el servicio para que el servicio
            // siga siendo pura economia.
            StoryModel historia = GameManager.Instance != null ? GameManager.Instance.Story : null;
            if (historia != null && _mesa.flagObjetivo != StoryFlag.None)
                historia.ActivateFlag(_mesa.flagObjetivo);
        }

        if (aplicado.Aplicado) Sonar(sonidoPremio);

        MostrarPremio(_tirada.Premio);
        Mensaje(aplicado.Mensaje);
        RefrescarTextos();
    }

    // ---------- pintado ----------

    private void RefrescarTextos()
    {
        PlayerModel jugador = Player;
        if (saldoText != null) saldoText.text = jugador != null ? $"${jugador.Money}" : "$0";
        // El numero va sobre la ficha: no hace falta la palabra "giro".
        if (costoText != null && _mesa != null) costoText.text = _mesa.costoPorGiro.ToString();
    }

    // El cartel del resultado: el simbolo de la casilla que salio y su nombre.
    // No hay tabla de probabilidades a proposito: la rueda ya cuenta la historia
    // con cuantas casillas ocupa cada premio.
    private void MostrarPremio(RoulettePrize premio)
    {
        if (premioText != null) premioText.text = premio != null ? premio.nombre : "";

        if (premioIcono == null) return;
        premioIcono.sprite = premio != null ? premio.icono : null;
        premioIcono.enabled = premioIcono.sprite != null;
    }

    private void Mensaje(string texto)
    {
        if (mensajeText != null) mensajeText.text = texto;
    }

    private void Sonar(AudioClip clip)
    {
        if (clip != null && AudioService.Instance != null) AudioService.Instance.PlaySFX(clip);
    }
}
