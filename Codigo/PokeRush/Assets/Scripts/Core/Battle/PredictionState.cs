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
    public const int TurnosEntrePredicciones = 3;

    // Categoria activa este turno (None = turno libre)
    public PredictedCategory Categoria { get; private set; }

    // Cuantos turnos faltan para la proxima. Se muestra en pantalla.
    public int TurnosParaProxima { get; private set; }

    // La categoria que viene el turno que sigue, cuando las camaras estan
    // cortadas. None si todavia no se sabe o no corresponde avisar.
    public PredictedCategory Anticipada { get; private set; }

    private readonly bool _conAviso;
    private int _turno;

    public PredictionState(bool conAviso)
    {
        _conAviso = conAviso;
        TurnosParaProxima = TurnosEntrePredicciones;
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

    // Texto corto para la banda de arriba
    public string Banda()
    {
        if (Categoria == PredictedCategory.None)
            return $"Lectura de Nestor en {TurnosParaProxima}";
        return "ANULADO: " + Texto(Categoria).ToUpperInvariant();
    }

    private static string MensajeBloqueo(PredictedCategory c) =>
        $"Nestor te leyo: este turno no podes {Texto(c)}.";

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
    private static PredictedCategory Elegir(BattleModel model, IBattleRng rng)
    {
        var candidatas = new List<PredictedCategory>();

        if (model != null)
        {
            PokenModel activo = model.ActivePlayerPoken;

            // Con poca vida el jugador quiere curarse: anularle la mochila duele
            if (activo != null && activo.MaxHP > 0 &&
                activo.CurrentHP / (float)activo.MaxHP < 0.35f)
                candidatas.Add(PredictedCategory.Objeto);

            // Si tiene banco disponible, cortarle el cambio lo deja expuesto
            int vivos = 0;
            foreach (PokenModel p in model.Player.Team)
                if (!p.IsFainted) vivos++;
            if (vivos > 1) candidatas.Add(PredictedCategory.Cambio);
        }

        // Anular el ataque siempre es una opcion valida
        candidatas.Add(PredictedCategory.Ataque);

        return candidatas[rng != null ? rng.Range(0, candidatas.Count) : 0];
    }
}
