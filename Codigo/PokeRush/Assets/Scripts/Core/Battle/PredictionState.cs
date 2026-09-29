using System.Collections.Generic;

// Categoria de accion que Nestor anula este turno.
// Anuncia CATEGORIAS, no movimientos exactos: si dijera el movimiento exacto
// elegis otro y no te costo nada. Con categorias te obliga a salir de tu plan.
public enum PredictedCategory
{
    None,
    Ataque,   // no podes usar movimientos de dano (los de estado siguen)
    Cambio,   // no podes cambiar de Poken
    Objeto,   // no podes usar la mochila
    // Va ULTIMO a proposito: BattlePredictionView indexa su lista de iconos por
    // el valor del enum, meterlo en el medio corria los sprites ya asignados.
    Estado    // no podes usar movimientos de estado (los de dano siguen)
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

    // Lo que ya se ANUNCIO y todavia no se aplico. Sin esto el aviso mentia:
    // se sorteaba una categoria para avisar y otra distinta al bloquear, asi
    // que cortar las camaras podia darte informacion falsa.
    private PredictedCategory _pendiente;

    // Ultima categoria que se anulo DE VERDAD. Se usa para no repetirla dos
    // lecturas seguidas cuando hay con que variar.
    private PredictedCategory _ultima;

    // Que salidas tiene el jugador. La Prediccion no puede anular la unica que
    // le queda: el GDD dice "nunca anula dos categorias a la vez, siempre hay
    // salida", y con el rastreo activo el cambio YA esta cerrado de entrada.
    // Sin esto se puede quedar sin ninguna accion valida y el combate se traba.
    public System.Func<bool> PuedeCambiar;
    public System.Func<bool> TieneObjetos;

    // Los movimientos son DOS salidas distintas, no una: Ataque anula solo los
    // de dano y Estado solo los de estado. Sin separarlos, un jugador con un
    // solo Poken y la mochila vacia — que es como se llega al primer boss —
    // tenia una unica categoria anulable y Nestor leia siempre lo mismo.
    public System.Func<bool> TieneMovimientoDeEstado;
    public System.Func<bool> TieneMovimientoDeDano;

    // Quien lee, para los textos. En la torre 1 es Nestor; en la pelea final
    // Julieta le roba el protocolo a Nestor y lo usa contra el jugador con las
    // mismas reglas, asi que lo unico que cambia es el nombre en pantalla.
    public string Lector = "Nestor";

    public PredictionState(bool conAviso, int turnosEntrePredicciones)
    {
        _conAviso = conAviso;
        // Menos de 2 dejaria al jugador anulado un turno por medio: no es
        // dificultad, es no poder jugar.
        TurnosEntrePredicciones = turnosEntrePredicciones < 2 ? 2 : turnosEntrePredicciones;
        TurnosParaProxima = TurnosEntrePredicciones;
    }

    // Se llama al empezar cada turno del jugador, antes de mostrarle el menu.
    // Devuelve el mensaje a mostrar, o null si no hay nada que decir.
    public string Avanzar(BattleModel model, IBattleRng rng)
    {
        _turno++;

        bool tocaAhora = _turno % TurnosEntrePredicciones == 0;
        TurnosParaProxima = TurnosEntrePredicciones - (_turno % TurnosEntrePredicciones);

        if (tocaAhora)
        {
            Categoria = Confirmar(_pendiente, model, rng);
            _pendiente = PredictedCategory.None;
            Anticipada = PredictedCategory.None;

            // None = no habia nada anulable sin dejarlo sin acciones. Se dice,
            // en vez de cantar un bloqueo que despues no bloquea nada (antes
            // salia "no podes actuar" y el jugador podia hacer de todo).
            if (Categoria == PredictedCategory.None)
                return $"{Lector} no te encontró la vuelta: este turno es libre.";

            _ultima = Categoria;
            return MensajeBloqueo(Categoria);
        }

        Categoria = PredictedCategory.None;

        // Con las camaras cortadas, el turno anterior al bloqueo se avisa.
        // Es la MISMA informacion, un turno antes: alcanza para planificar.
        if (_conAviso && TurnosParaProxima == 1)
        {
            _pendiente = Elegir(model, rng);
            Anticipada = _pendiente;
            if (Anticipada != PredictedCategory.None)
                return $"{Lector} esta calculando: el proximo turno te va a leer {Texto(Anticipada)}.";
            return null;
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
                // Solo los de dano: los de estado son la salida que deja abierta
                return tipo == BattleActionKind.UseMove && EsDeDano(movimiento);

            case PredictedCategory.Estado:
                // El espejo del anterior: pegar sigue disponible
                return tipo == BattleActionKind.UseMove && EsDeEstado(movimiento);

            case PredictedCategory.Cambio:
                return tipo == BattleActionKind.SwitchPoken;

            case PredictedCategory.Objeto:
                return tipo == BattleActionKind.UseItem;

            default:
                return false;
        }
    }

    // Un movimiento es de dano o de estado, nunca las dos cosas. Publicos para
    // que el que cablea las salidas (BattleController) clasifique con el mismo
    // criterio que el que bloquea, en vez de repetir la condicion a mano.
    public static bool EsDeDano(MoveDefinition m) =>
        m != null && m.attackType != AttackType.Status && m.power > 0;

    public static bool EsDeEstado(MoveDefinition m) =>
        m != null && (m.attackType == AttackType.Status || m.power <= 0);

    public string MensajeRechazo()
    {
        switch (Categoria)
        {
            case PredictedCategory.Ataque: return $"{Lector} anticipo tu ataque. Probá otra cosa.";
            case PredictedCategory.Estado: return $"{Lector} te leyó el movimiento de estado. Probá otra cosa.";
            case PredictedCategory.Cambio: return $"{Lector} bloqueo el cambio.";
            case PredictedCategory.Objeto: return $"{Lector} te vio buscar en la mochila.";
            default: return $"{Lector} anticipo tu accion.";
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
            snap.Titulo = "ANULADO: " + TituloDe(Categoria);
            snap.Detalle = $"{Lector} te leyó la jugada.";
        }
        else if (Anticipada != PredictedCategory.None)
        {
            snap.Titulo = "LECTURA DE " + Lector.ToUpperInvariant();
            snap.Detalle = $"El próximo turno te lee {Texto(Anticipada)}.";
        }
        else
        {
            snap.Titulo = "LECTURA DE " + Lector.ToUpperInvariant();
            snap.Detalle = TurnosParaProxima == 1
                ? "Próxima lectura: el turno que viene."
                : $"Próxima lectura en {TurnosParaProxima} turnos.";
        }

        return snap;
    }

    private string MensajeBloqueo(PredictedCategory c) =>
        $"{Lector} te leyo: este turno no podes {Texto(c)}.";

    // Nombre de la categoria en español. Publico porque la vista lo necesita
    // para rotular iconos sin volver a escribir la tabla.
    public static string TextoDeCategoria(PredictedCategory c) => Texto(c);

    private static string Texto(PredictedCategory c)
    {
        switch (c)
        {
            case PredictedCategory.Ataque: return "atacar";
            case PredictedCategory.Estado: return "usar movimientos de estado";
            case PredictedCategory.Cambio: return "cambiar de Poken";
            case PredictedCategory.Objeto: return "usar objetos";
            default: return "actuar";
        }
    }

    // Version corta para la banda de la UI: "ANULADO: USAR MOVIMIENTOS DE
    // ESTADO" no entra en el panel.
    private static string TituloDe(PredictedCategory c) =>
        c == PredictedCategory.Estado
            ? "MOVIMIENTOS DE ESTADO"
            : Texto(c).ToUpperInvariant();

    // Que puede hacer el jugador ahora mismo. Es la base de las dos reglas:
    // no anular algo que ya no podia hacer (turno regalado) y no anular lo
    // unico que le queda (combate trabado).
    private struct Salidas
    {
        public bool Dano;
        public bool Estado;
        public bool Objetos;
        public bool Cambio;

        public int Cuantas =>
            (Dano ? 1 : 0) + (Estado ? 1 : 0) + (Objetos ? 1 : 0) + (Cambio ? 1 : 0);
    }

    // Los Func en null (un test que no los cablea) se leen como "esa salida
    // existe": es el mismo criterio permisivo que tenia HaySalidaSinAtacar.
    private Salidas SalidasDe(BattleModel model)
    {
        int vivos = 0;
        if (model != null)
            foreach (PokenModel p in model.Player.Team)
                if (!p.IsFainted) vivos++;

        return new Salidas
        {
            Dano = TieneMovimientoDeDano == null || TieneMovimientoDeDano(),
            Estado = TieneMovimientoDeEstado == null || TieneMovimientoDeEstado(),
            Objetos = TieneObjetos == null || TieneObjetos(),
            // Con el rastreo activo PuedeCambiar ya devuelve false: el cambio
            // esta cerrado de entrada y leerlo seria regalar el turno.
            Cambio = vivos > 1 && (PuedeCambiar == null || PuedeCambiar())
        };
    }

    // Que puede anular Nestor este turno. Una categoria entra solo si el
    // jugador PODIA usarla y ademas le queda otra cosa por hacer.
    private List<PredictedCategory> Candidatas(BattleModel model)
    {
        Salidas s = SalidasDe(model);
        var candidatas = new List<PredictedCategory>();
        if (s.Cuantas < 2) return candidatas;

        if (s.Dano) candidatas.Add(PredictedCategory.Ataque);
        if (s.Estado) candidatas.Add(PredictedCategory.Estado);
        if (s.Cambio) candidatas.Add(PredictedCategory.Cambio);

        // La mochila solo se lee si hay algo que curar: anularla con el activo
        // al maximo es un turno regalado. Antes el corte era 35% de PS, que
        // llegando al boss con el equipo entero no se cumplia nunca y dejaba la
        // categoria muerta media pelea.
        PokenModel activo = model != null ? model.ActivePlayerPoken : null;
        bool valeLaMochila = activo != null && activo.CurrentHP < activo.MaxHP;
        if (s.Objetos && valeLaMochila)
        {
            candidatas.Add(PredictedCategory.Objeto);

            // Con poca vida el jugador quiere curarse si o si: entra dos veces
            // en el sorteo para que duela mas seguido. Es peso, no obligacion:
            // forzarla la repetia lectura tras lectura mientras siguiera bajo.
            if (activo.MaxHP > 0 && activo.CurrentHP / (float)activo.MaxHP < 0.35f)
                candidatas.Add(PredictedCategory.Objeto);
        }

        return candidatas;
    }

    // Respeta lo que se anuncio el turno anterior. Solo vuelve a elegir si eso
    // dejo de tener sentido (gasto el ultimo objeto, se quedo sin banco): asi
    // el aviso no miente pero tampoco anula algo que ya no podia hacer.
    private PredictedCategory Confirmar(PredictedCategory anunciada, BattleModel model, IBattleRng rng)
    {
        if (anunciada != PredictedCategory.None && Candidatas(model).Contains(anunciada))
            return anunciada;
        return Elegir(model, rng);
    }

    // Elige que anular entre lo que el jugador realmente podia hacer.
    private PredictedCategory Elegir(BattleModel model, IBattleRng rng)
    {
        List<PredictedCategory> candidatas = Candidatas(model);

        // Nada anulable sin dejarlo sin acciones: este turno no se anula nada.
        // Es preferible una Prediccion que a veces no pega a un combate trabado.
        if (candidatas.Count == 0) return PredictedCategory.None;

        // No repetir la lectura anterior si hay con que variar. Es lo que se
        // veia en el primer nivel: con las condiciones en las que llega el
        // equipo quedaba una sola candidata y Nestor cantaba siempre lo mismo,
        // lectura tras lectura.
        if (_ultima != PredictedCategory.None && candidatas.Exists(c => c != _ultima))
            candidatas.RemoveAll(c => c == _ultima);

        return candidatas[rng != null ? rng.Range(0, candidatas.Count) : 0];
    }
}
