using UnityEngine;
using UnityEngine.UI;

// Pantalla de la maquina tragamonedas.
//
// Es una MenuScreen mas: se abre encima de la escena del casino, el MenuManager
// le corta el input al jugador mientras esta arriba de la pila y se lo devuelve
// solo al cerrarse. No hay cambio de escena en ningun momento.
//
// Reparto de responsabilidades: el resultado lo decide SlotMachineEngine, el
// dinero lo mueve PlayerModel y esta clase solo pinta y anima. Por eso se puede
// probar el motor sin abrir la UI.
public class SlotsScreen : MenuScreen
{
    [Header("Textos")]
    [SerializeField] private Text tituloText;
    [SerializeField] private Text saldoText;
    [Tooltip("Visor PREMIO del gabinete: lo que pago la ultima tirada.")]
    [SerializeField] private Text premioText;
    [SerializeField] private Text apuestaText;
    [SerializeField] private Text mensajeText;

    [Header("Grilla")]
    [Tooltip("Casillas en orden: rodillo 0 de arriba a abajo, despues rodillo 1...")]
    [SerializeField] private SlotCellWidget[] celdas;

    [Header("Sonidos (opcionales)")]
    [SerializeField] private AudioClip sonidoGiro;
    [SerializeField] private AudioClip sonidoPremio;
    [SerializeField] private AudioClip sonidoSinDinero;

    [Header("Ajustes")]
    [Tooltip("Cada cuanto cambian los simbolos mientras el rodillo gira.")]
    [SerializeField] private float refrescoDelGiro = 0.06f;

    private enum Fase { Apostando, Girando }

    private SlotMachineDefinition _maquina;
    private SlotMachineEngine _motor;
    private readonly IBattleRng _rng = new UnityRng();
    private readonly SlotsScreenResult _resultado = new SlotsScreenResult();

    private Fase _fase;
    private int _apuesta;
    private SlotSpinResult _tirada;
    private float _tiempo;
    private float _proximoRefresco;
    private int _rodillosParados;
    private int _ultimoPremio;

    protected override void OnOpen(object payload)
    {
        var pedido = payload as SlotsScreenRequest;
        _maquina = pedido != null ? pedido.Maquina : null;

        _resultado.Tiradas = 0;
        _resultado.Neto = 0;
        _resultado.MayorPremio = 0;
        Result = _resultado;

        string error = null;
        if (_maquina == null || !_maquina.Validar(out error))
        {
            // Una maquina mal configurada no puede quedar jugable: se avisa y no
            // se acepta ninguna apuesta, en vez de comerse el dinero del jugador.
            Debug.LogError("[Casino] Maquina de slots invalida: " +
                           (_maquina == null ? "no hay definicion asignada" : error) + ".");
            Mensaje("Esta maquina esta fuera de servicio.");
            _motor = null;
            _fase = Fase.Apostando;
            return;
        }

        _motor = new SlotMachineEngine(_maquina);
        _fase = Fase.Apostando;
        _tirada = null;
        _rodillosParados = 0;

        if (tituloText != null)
            tituloText.text = string.IsNullOrEmpty(pedido.Titulo)
                ? _maquina.maquinaNombre
                : pedido.Titulo;

        _ultimoPremio = 0;
        _apuesta = _maquina.AjustarApuesta(pedido.ApuestaInicial > 0
            ? pedido.ApuestaInicial
            : _maquina.apuestaMinima);

        ApagarResaltados();
        PintarGrillaAlAzar();
        RefrescarTextos();
        Mensaje("Elegi la apuesta y hace girar.");
    }

    protected override void OnClose()
    {
        _tirada = null;
        _motor = null;
    }

    public override void HandleAction(GameAction action)
    {
        // Mientras giran los rodillos no se acepta nada: ni cerrar, ni volver a
        // apostar. Si no, se podria salir con la apuesta ya descontada y el
        // premio sin cobrar.
        if (_fase == Fase.Girando) return;

        switch (action)
        {
            case GameAction.Left:
            case GameAction.Down:
                CambiarApuesta(-1);
                return;

            case GameAction.Right:
            case GameAction.Up:
                CambiarApuesta(+1);
                return;

            case GameAction.Confirm:
                Girar();
                return;

            case GameAction.Cancel:
                CloseSelf();
                return;
        }
    }

    public override void OnTick(float unscaledDeltaTime)
    {
        if (_fase != Fase.Girando || _tirada == null || _motor == null) return;

        _tiempo += unscaledDeltaTime;

        // Los rodillos frenan de a uno, de izquierda a derecha.
        int deberianEstarParados = 0;
        for (int r = 0; r < _maquina.rodillos; r++)
            if (_tiempo >= _maquina.duracionDelGiro + r * _maquina.retrasoEntreRodillos)
                deberianEstarParados++;

        while (_rodillosParados < deberianEstarParados)
        {
            PintarRodilloReal(_rodillosParados);
            _rodillosParados++;
        }

        if (_rodillosParados >= _maquina.rodillos)
        {
            Resolver();
            return;
        }

        // Los que siguen girando muestran simbolos al azar.
        if (_tiempo < _proximoRefresco) return;
        _proximoRefresco = _tiempo + refrescoDelGiro;

        for (int r = _rodillosParados; r < _maquina.rodillos; r++)
            for (int f = 0; f < _maquina.filas; f++)
                MostrarEn(r, f, _motor.SimboloEn(_motor.SortearSimbolo(_rng)));
    }

    // ---------- apuesta y tirada ----------

    private void CambiarApuesta(int direccion)
    {
        if (_motor == null) return;

        int nueva = _maquina.AjustarApuesta(_apuesta + direccion * _maquina.pasoDeApuesta);
        if (nueva == _apuesta) return;

        _apuesta = nueva;
        RefrescarTextos();
    }

    private void Girar()
    {
        if (_motor == null) return;

        PlayerModel jugador = Player;
        if (jugador == null)
        {
            Debug.LogError("[Casino] No hay partida en curso: la maquina no puede cobrar la apuesta.");
            return;
        }

        // Se cobra ANTES de girar y se paga DESPUES de resolver. Al reves, salir
        // a mitad de la animacion regalaria tiradas gratis.
        if (jugador.Money < _apuesta)
        {
            Sonar(sonidoSinDinero);
            Mensaje("No te alcanza para esa apuesta.");
            return;
        }

        jugador.SpendMoney(_apuesta);
        _ultimoPremio = 0;
        _tirada = _motor.Girar(_rng, _apuesta);

        _fase = Fase.Girando;
        _tiempo = 0f;
        _proximoRefresco = 0f;
        _rodillosParados = 0;

        ApagarResaltados();
        Sonar(sonidoGiro);
        Mensaje("Girando...");
        RefrescarTextos();
    }

    private void Resolver()
    {
        _fase = Fase.Apostando;

        PlayerModel jugador = Player;
        if (jugador != null && _tirada.Pago > 0) jugador.AddMoney(_tirada.Pago);
        _ultimoPremio = _tirada.Pago;

        _resultado.Tiradas++;
        _resultado.Neto += _tirada.Neto;
        if (_tirada.Pago > _resultado.MayorPremio) _resultado.MayorPremio = _tirada.Pago;

        if (_tirada.Gano)
        {
            ResaltarLineasGanadoras();
            Sonar(sonidoPremio);

            SlotLineHit mejor = _tirada.Lineas[0];
            foreach (SlotLineHit hit in _tirada.Lineas)
                if (hit.Pago > mejor.Pago) mejor = hit;

            Mensaje(_tirada.Lineas.Count > 1
                ? _tirada.Lineas.Count + " lineas! Ganaste " + _tirada.Pago + "."
                : mejor.Cantidad + " x " + mejor.SimboloNombre + "! Ganaste " + _tirada.Pago + ".");
        }
        else
        {
            Mensaje("Nada esta vez.");
        }

        RefrescarTextos();
    }

    // ---------- pintado ----------

    private void PintarGrillaAlAzar()
    {
        if (_motor == null) return;
        for (int r = 0; r < _maquina.rodillos; r++)
            for (int f = 0; f < _maquina.filas; f++)
                MostrarEn(r, f, _motor.SimboloEn(_motor.SortearSimbolo(_rng)));
    }

    private void PintarRodilloReal(int rodillo)
    {
        for (int f = 0; f < _maquina.filas; f++)
            MostrarEn(rodillo, f, _motor.SimboloEn(_tirada.Grilla[rodillo, f]));
    }

    private void ResaltarLineasGanadoras()
    {
        foreach (SlotLineHit hit in _tirada.Lineas)
        {
            SlotPayline linea = _maquina.lineas[hit.LineaIndex];
            for (int r = 0; r < hit.Cantidad && r < linea.filas.Length; r++)
            {
                SlotCellWidget celda = Celda(r, linea.filas[r]);
                if (celda != null) celda.Resaltar(true);
            }
        }
    }

    private void ApagarResaltados()
    {
        if (celdas == null) return;
        foreach (SlotCellWidget celda in celdas)
            if (celda != null) celda.Resaltar(false);
    }

    private void RefrescarTextos()
    {
        PlayerModel jugador = Player;
        // Los visores del gabinete son numeros pelados: la palabra CREDITO y la
        // palabra PREMIO ya estan dibujadas arriba de cada ventanita.
        if (saldoText != null) saldoText.text = jugador != null ? jugador.Money.ToString() : "0";
        if (premioText != null) premioText.text = _ultimoPremio.ToString();
        if (apuestaText != null) apuestaText.text = "APUESTA " + _apuesta;
    }

    private void Mensaje(string texto)
    {
        if (mensajeText != null) mensajeText.text = texto;
    }

    private void Sonar(AudioClip clip)
    {
        if (clip != null && AudioService.Instance != null) AudioService.Instance.PlaySFX(clip);
    }

    private void MostrarEn(int rodillo, int fila, SlotSymbol simbolo)
    {
        SlotCellWidget celda = Celda(rodillo, fila);
        if (celda != null) celda.Mostrar(simbolo);
    }

    // Indice de casilla: rodillo por rodillo, de arriba hacia abajo.
    private SlotCellWidget Celda(int rodillo, int fila)
    {
        if (celdas == null || _maquina == null) return null;
        int i = rodillo * _maquina.filas + fila;
        if (i < 0 || i >= celdas.Length) return null;
        return celdas[i];
    }
}
