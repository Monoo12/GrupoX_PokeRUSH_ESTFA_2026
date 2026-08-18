using System.Collections.Generic;

// Categoria de accion que Nestor anula este turno.
// Anuncia CATEGORIAS, no movimientos exactos: si dijera el movimiento exacto
// elegis otro y no te costo nada. Con categorias te obliga a salir de tu plan.
public enum PredictedCategory
{
    None,
    Ataque,   // no podes usar movimientos de ataque (los de estado siguen)
    Cambio,   // no podes cambiar de Poken
    Objeto    // no podes usar la mochila
}

// Foto del estado de la Prediccion para la UI. Es SOLO datos: no sabe de
// Text, de posiciones ni de colores.
//
// Existe para que la vista no le pregunte nada al PredictionState ni arme
// strings por su cuenta. La logica publica un snapshot, la vista decide como
// se ve. Cambiar la UI (banda, iconos, barra de progreso, animaciones) no
// obliga a tocar una linea de la logica.
public struct PredictionSnapshot
{
    // Hay Prediccion en esta pelea? (false = combate normal, la UI se apaga)
    public bool Activa;

    // Categoria anulada ESTE turno (None = turno libre)
    public PredictedCategory Categoria;

    // Categoria que viene el turno siguiente, si las camaras estan cortadas
    public PredictedCategory Anticipada;

    // Turnos que faltan para la proxima lectura
    public int TurnosParaProxima;

    // Cada cuantos turnos lee (para dibujar progreso sin hardcodear el 3)
    public int TurnosEntreLecturas;

    // Textos ya armados en español, por si la vista solo quiere mostrarlos
    public string Titulo;   // "LECTURA DE NESTOR" / "ANULADO: ATACAR"
    public string Detalle;  // "Proxima lectura en 2 turnos" / aviso anticipado

    public bool HayBloqueo => Activa && Categoria != PredictedCategory.None;
    public bool HayAviso => Activa && Anticipada != PredictedCategory.None;
}

// La Prediccion de Nestor contra el jugador.
//
// Reglas del GDD:
//   - Cada 3 turnos FIJOS, no al azar. Turnos 1 y 2 libres, el 3 condicionado.
//     Genera ritmo (guardas la jugada fuerte para los libres) y ademas lo hace
//     inmune a save/load, que era el agujero de la version probabilistica.
//   - Nunca anula dos categorias a la vez: siempre hay salida.
//   - Con las camaras cortadas el aviso llega un turno antes.
//
// Clase pura: no sabe de Unity ni de UI. El TurnEngine NO la conoce — esto es
// un gate ANTES del input, no una regla de resolucion de turno. Por eso vive
// en BattleController y no en el motor.
public class PredictionState
{
    // Cada cuantos turnos lee. Lo decide BossBattleConfig segun las camaras:
    // cortadas = Nestor tarda mas en calcular y lee menos seguido.
    public int TurnosEntrePredicciones { get; }

    // Categoria activa este turno (None = turno libre)
    public PredictedCategory Categoria { get; private set; }

    // Cuantos turnos faltan para la proxima. Se muestra en pantalla.
    public int TurnosParaProxima { get; private set; }

    // La categoria que viene el turno que sigue, cuando las camaras estan
    // cortadas. None si todavia no se sabe o no corresponde avisar.
    public PredictedCategory Anticipada { get; private set; }

    private readonly bool _conAviso;
    private int _turno;

    // Que salidas tiene el jugador aparte de atacar. Si no tiene ninguna, la
    // Prediccion NO puede anularle el ataque: el GDD dice "nunca anula dos
    // categorias a la vez, siempre hay salida", y con el rastreo activo el
    // cambio YA esta cerrado de entrada. Sin esto se puede quedar sin ninguna
    // accion valida y el combate se traba.
    public System.Func<bool> PuedeCambiar;
    public System.Func<bool> TieneObjetos;

    // Los movimientos de ESTADO tambien son una salida: Bloquea() solo anula los
    // de dano. Sin contarlos, un jugador con un solo Poken y la mochila vacia no
    // tenia ninguna categoria anulable y Nestor no leia NUNCA — pasaban los
    // turnos y no bloqueaba nada.
    public System.Func<bool> TieneMovimientoDeEstado;

    public PredictionState(bool conAviso, int turnosEntrePredicciones)
    {
        _conAviso = conAviso;
        // Menos de 2 dejaria al jugador anulado un turno por medio: no es
        // dificultad, es no poder jugar.
        TurnosEntrePredicciones = turnosEntrePredicciones < 2 ? 2 : turnosEntrePredicciones;
        TurnosParaProxima = TurnosEntrePredicciones;
    }

    private bool HaySalidaSinAtacar()
    {
        bool cambio = PuedeCambiar == null || PuedeCambiar();
        bool objetos = TieneObjetos == null || TieneObjetos();
        bool estado = TieneMovimientoDeEstado == null || TieneMovimientoDeEstado();
        return cambio || objetos || estado;
    }

    // Se llama al empezar cada turno del jugador, antes de mostrarle el menu.
    // Devuelve el mensaje a mostrar, o null si no hay nada que decir.
    public string Avanzar(BattleModel model, IBattleRng rng)
    {
        _turno++;

        bool tocaAhora = _turno % TurnosEntrePredicciones == 0;
        Categoria = tocaAhora ? Elegir(model, rng) : PredictedCategory.None;
        TurnosParaProxima = TurnosEntrePredicciones - (_turno % TurnosEntrePredicciones);
        if (TurnosParaProxima == TurnosEntrePredicciones && !tocaAhora)
            TurnosParaProxima = TurnosEntrePredicciones;

        if (tocaAhora)
        {
            Anticipada = PredictedCategory.None;
            return MensajeBloqueo(Categoria);
        }

        // Con las camaras cortadas, el turno anterior al bloqueo se avisa.
        // Es la MISMA informacion, un turno antes: alcanza para planificar.
        if (_conAviso && TurnosParaProxima == 1)
        {
            Anticipada = Elegir(model, rng);
            return $"Nestor esta calculando: el proximo turno te va a leer {Texto(Anticipada)}.";
        }

        Anticipada = PredictedCategory.None;
        return null;
    }

    // Bloquea una accion del jugador?
    public bool Bloquea(BattleActionKind tipo, MoveDefinition movimiento)
    {
        switch (Categoria)
        {
            case PredictedCategory.Ataque:
                // Solo los de ataque: los de estado son la salida que deja abierta
                return tipo == BattleActionKind.UseMove
                       && movimiento != null
                       && movimiento.attackType != AttackType.Status
                       && movimiento.power > 0;

            case PredictedCategory.Cambio:
                return tipo == BattleActionKind.SwitchPoken;

            case PredictedCategory.Objeto:
                return tipo == BattleActionKind.UseItem;

            default:
                return false;
        }
    }

    public string MensajeRechazo()
    {
        switch (Categoria)
        {
            case PredictedCategory.Ataque: return "Nestor anticipo tu ataque. Probá otra cosa.";
            case PredictedCategory.Cambio: return "Nestor bloqueo el cambio.";
            case PredictedCategory.Objeto: return "Nestor te vio buscar en la mochila.";
            default: return "Nestor anticipo tu accion.";
        }
    }

    // Foto del estado actual para la UI. La vista no llama a nada mas que esto.
    public PredictionSnapshot Snapshot()
    {
        var snap = new PredictionSnapshot
        {
            Activa = true,
            Categoria = Categoria,
            Anticipada = Anticipada,
            TurnosParaProxima = TurnosParaProxima,
            TurnosEntreLecturas = TurnosEntrePredicciones
        };

        if (Categoria != PredictedCategory.None)
        {
            snap.Titulo = "ANULADO: " + Texto(Categoria).ToUpperInvariant();
            snap.Detalle = "Nestor te leyó la jugada.";
        }
        else if (Anticipada != PredictedCategory.None)
        {
            snap.Titulo = "LECTURA DE NESTOR";
            snap.Detalle = $"El próximo turno te lee {Texto(Anticipada)}.";
        }
        else
        {
            snap.Titulo = "LECTURA DE NESTOR";
            snap.Detalle = TurnosParaProxima == 1
                ? "Próxima lectura: el turno que viene."
                : $"Próxima lectura en {TurnosParaProxima} turnos.";
        }

        return snap;
    }

    private static string MensajeBloqueo(PredictedCategory c) =>
        $"Nestor te leyo: este turno no podes {Texto(c)}.";

    // Nombre de la categoria en español. Publico porque la vista lo necesita
    // para rotular iconos sin volver a escribir la tabla.
    public static string TextoDeCategoria(PredictedCategory c) => Texto(c);

    private static string Texto(PredictedCategory c)
    {
        switch (c)
        {
            case PredictedCategory.Ataque: return "atacar";
            case PredictedCategory.Cambio: return "cambiar de Poken";
            case PredictedCategory.Objeto: return "usar objetos";
            default: return "actuar";
        }
    }

    // Elige que anular. Heuristica corta que mira el estado real para que
    // moleste de verdad, en vez de sortear al azar.
    private PredictedCategory Elegir(BattleModel model, IBattleRng rng)
    {
        var candidatas = new List<PredictedCategory>();

        if (model != null)
        {
            PokenModel activo = model.ActivePlayerPoken;

            // Con poca vida el jugador quiere curarse: anularle la mochila duele.
            // Solo si REALMENTE tiene objetos, si no la Prediccion se desperdicia
            // anulando algo que ya no podia hacer.
            if (activo != null && activo.MaxHP > 0 &&
                activo.CurrentHP / (float)activo.MaxHP < 0.35f &&
                (TieneObjetos == null || TieneObjetos()))
                candidatas.Add(PredictedCategory.Objeto);

            // Si tiene banco disponible, cortarle el cambio lo deja expuesto.
            // Con el rastreo activo el cambio ya esta cerrado: no tiene sentido.
            int vivos = 0;
            foreach (PokenModel p in model.Player.Team)
                if (!p.IsFainted) vivos++;
            if (vivos > 1 && (PuedeCambiar == null || PuedeCambiar()))
                candidatas.Add(PredictedCategory.Cambio);
        }

        // Anular el ataque solo si al jugador le queda otra salida. Si no puede
        // cambiar (rastreo activo) y no tiene objetos, atacar es lo unico que
        // le queda y quitarselo lo dejaria sin ninguna accion valida.
        if (HaySalidaSinAtacar()) candidatas.Add(PredictedCategory.Ataque);

        // Si no quedo ninguna candidata, este turno no se anula nada. Es
        // preferible una Prediccion que a veces no pega a un combate trabado.
        if (candidatas.Count == 0) return PredictedCategory.None;

        return candidatas[rng != null ? rng.Range(0, candidatas.Count) : 0];
    }
}
