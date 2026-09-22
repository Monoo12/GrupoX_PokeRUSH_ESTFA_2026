using System.Collections.Generic;

// Que expresion tiene Nestor mirando la pelea. El indice del enum es el que
// usa NestorPortraitView para elegir el sprite, asi que los valores nuevos van
// SIEMPRE al final: meter uno en el medio corre todos los retratos ya
// asignados en el Inspector (la misma trampa que documenta PredictedCategory).
public enum NestorMood
{
    Neutral,      // NESTOR_retrato_pensando: la cara base, mirando el combate
    Pensando,     // NESTOR_retrato_pensando: esta atando cabos
    Concentrado,  // NESTOR_retrato_enojado: el ceño fruncido del que ya lo vio
    Confiado,     // NESTOR_retrato_feliz: le salio la lectura
    Sorprendido,  // NESTOR_retrato_shock: paso algo que no esperaba
    Preocupado,   // NESTOR_retrato_preocupado: vas perdiendo
    Furioso       // NESTOR_retrato_furioso: el rival esta contra las cuerdas
}

// Cuando esta disponible la ayuda de Nestor. Lo elige BattleController desde
// el Inspector; los dos modos forzados son para probar sin caminar la torre.
public enum NestorAllyMode
{
    Auto,       // segun el flag Torre1_NestorLiberado, o si la pelea lo pide (casino)
    SiempreOn,  // forzado, para probar la mecanica en cualquier combate
    SiempreOff  // apagado, para comparar contra el combate sin Nestor
}

// En que consiste la jugada que eligio la IA rival. No es una etiqueta
// decorativa: de esto sale si Nestor le hace perder el turno entero o solo lo
// corre de su mejor opcion.
public enum EnemyPlanKind
{
    Ninguna,      // forcejeo, accion nula o algo que no vale la pena leer
    Ataque,       // movimiento de daño comun
    Remate,       // movimiento de daño que TE MATA este turno
    Curacion,     // se cura
    Preparacion,  // se sube stats, pone clima/terreno/pantallas
    Estado,       // te aplica un estado alterado o te baja stats
    Cambio        // se va y manda otro
}

// La jugada rival ya clasificada. Es lo que Nestor "lee": sale de la accion
// que la IA REALMENTE eligio, no de un sorteo aparte.
public struct EnemyPlan
{
    public EnemyPlanKind Kind;
    public BattleAction Accion;

    // Nombre del movimiento, para el mensaje. Vacio si la jugada no es un
    // movimiento (un cambio, por ejemplo).
    public string Movimiento;

    // Daño esperado sobre el activo del jugador, como fraccion de sus PS
    // maximos. 0 si la jugada no pega.
    public float DanoEsperado;

    // Vale la pena quemarle el turno entero? Las jugadas fuertes (remate,
    // curacion, preparacion, estado, o un golpe que se lleva medio equipo)
    // dejan al rival sin turno; el resto solo pierde su MEJOR opcion.
    public bool EsJugadaFuerte;

    public bool Existe => Kind != EnemyPlanKind.Ninguna;
}

// Foto para la UI. Igual que PredictionSnapshot: son SOLO datos, la vista
// decide como se dibuja. Cambiar el retrato entero (otro layout, animaciones,
// un globo de dialogo distinto) no obliga a tocar una linea de esta clase.
public struct NestorPortraitSnapshot
{
    // Hay lectura aliada en esta pelea? false = el retrato se apaga entero.
    public bool Activa;

    public NestorMood Animo;

    // Frase de Nestor, o null si este turno no dice nada. La mayoria de los
    // turnos va en null a proposito: si habla siempre, deja de escucharse.
    public string Dialogo;

    // Este turno intervino: la vista lo destaca (color, latido, lo que sea).
    public bool Destacado;

    // Cuantas lecturas le quedan en el combate, para dibujar carga si se quiere.
    public int LecturasRestantes;
}

// Lo que Nestor decidio sobre la jugada rival de este turno.
public struct NestorIntervention
{
    public bool Intervino;

    // La jugada que leyo (ya clasificada).
    public EnemyPlan Plan;

    // Con que se reemplaza la jugada rival. Si Nestor no intervino es la
    // accion original, intacta.
    public BattleAction AccionFinal;

    // El rival se quedo sin turno (jugada fuerte) o solo cayo a su segunda
    // opcion.
    public bool PerdioElTurno;

    // Lo que Nestor dice al detectar la jugada, antes de que se resuelva.
    public string DialogoDeteccion;

    // Lo que dice cuando el bloqueo ya paso.
    public string DialogoExito;

    // Mensaje de sistema del bloqueo ("X se quedo sin poder atacar").
    public string MensajeBloqueo;
}

// LA LECTURA ALIADA DE NESTOR — nivel 2 (el casino).
//
// Es el espejo de PredictionState: alla Nestor era el jefe y te leia a vos,
// aca esta de tu lado y lee al rival. Se separan a proposito en dos clases:
// las reglas no se parecen en nada (aquella anula CATEGORIAS antes de que
// elijas y es un gate de input; esta corrige la accion YA elegida por la IA)
// y mezclarlas dejaria una clase con dos modos y cero claridad.
//
// Diferencias de diseño con la del primer Data Center:
//   - Alla la cadencia es FIJA cada 3 turnos, porque contra un jefe el ritmo
//     predecible es lo que te deja planificar. Aca Nestor es una AYUDA: si
//     fuese fijo, el jugador guardaria la jugada floja para el turno de Nestor
//     y la mecanica se convertiria en un metronomo. Por eso va por
//     probabilidad + cooldown + tope por combate.
//   - Alla nunca puede dejarte sin salida. Aca nunca le quema el turno al
//     rival por una jugada floja: si lo que iba a hacer no era peligroso, solo
//     lo corre a su segunda opcion.
//
// Clase pura: no sabe de Unity ni de UI. El TurnEngine tampoco la conoce —
// esto pasa ANTES de resolver el turno, sobre la accion que devolvio la IA.
public class NestorPredictionSystem
{
    // ---------- balance: todo lo que se toca esta aca ----------

    // Chance de que Nestor lea la jugada, una vez pasados el cooldown y la
    // gracia inicial. Conservador a proposito: con el cooldown de 4 y el tope
    // de 2, en una pelea normal de 8-12 turnos interviene 0, 1 o 2 veces.
    // Que a veces NO aparezca es parte de que se sienta especial.
    public float Probabilidad = 0.20f;

    // Turnos que tienen que pasar entre dos lecturas. Es un minimo duro: aunque
    // la probabilidad salga favorable, antes de esto no lee.
    public int CooldownTurnos = 4;

    // Turnos del arranque en los que Nestor mira y no hace nada. Sin esto la
    // lectura puede caer en el turno 1 y arruina la entrada del combate.
    public int TurnosDeGracia = 2;

    // Tope por combate. Es el freno mas importante: sin el, una pelea larga
    // termina con Nestor anulando media docena de turnos.
    public int MaximoPorCombate = 2;

    // Cuanto tiene que pegar un ataque comun para contar como jugada fuerte,
    // en fraccion de los PS MAXIMOS del Poken del jugador. Por debajo de esto
    // el rival no pierde el turno: solo se le tapa su mejor opcion.
    public float FraccionDeGolpeFuerte = 0.45f;

    // ---------- estado ----------

    public int Turno { get; private set; }
    public int Intervenciones { get; private set; }
    public int LecturasRestantes => MaximoPorCombate - Intervenciones;

    public NestorMood Animo { get; private set; }
    public string Dialogo { get; private set; }
    public bool DestacadoEsteTurno { get; private set; }

    private int _turnoDeLaUltima = -999;
    private string _ultimaFrase;

    // Evalua la jugada que la IA rival ya eligio y decide si Nestor la corta.
    //
    // Se llama UNA vez por turno resuelto, despues de IBattleAI.ChooseAction y
    // antes de TurnEngine.ResolveTurn. Ese orden es la garantia de que Nestor
    // nunca corta una accion que ya empezo a ejecutarse: cuando el motor
    // arranca, la accion final ya esta decidida.
    public NestorIntervention Evaluar(AiContext ctx, BattleAction accionEnemiga, IBattleRng rng)
    {
        Turno++;
        DestacadoEsteTurno = false;
        Dialogo = null;

        var resultado = new NestorIntervention
        {
            Intervino = false,
            AccionFinal = accionEnemiga
        };

        EnemyPlan plan = Clasificar(ctx, accionEnemiga);
        resultado.Plan = plan;

        if (!PuedeIntervenir(plan, rng))
        {
            Animo = AnimoAmbiente(ctx);
            return resultado;
        }

        // Con que se reemplaza la jugada. Se resuelve ANTES de dar la lectura
        // por buena: si la jugada era floja y el rival no tiene con que
        // reemplazarla, Nestor deja pasar y no gasta la lectura. Quemarle el
        // turno por un placaje seria justo lo que se pidio evitar.
        BattleAction reemplazo;
        bool pierdeElTurno;
        if (plan.EsJugadaFuerte)
        {
            reemplazo = BattleAction.Wait();
            pierdeElTurno = true;
        }
        else
        {
            reemplazo = SegundaOpcion(ctx, accionEnemiga);
            if (reemplazo == null)
            {
                Animo = AnimoAmbiente(ctx);
                return resultado;
            }
            pierdeElTurno = false;
        }

        Intervenciones++;
        _turnoDeLaUltima = Turno;
        DestacadoEsteTurno = true;

        resultado.Intervino = true;
        resultado.AccionFinal = reemplazo;
        resultado.PerdioElTurno = pierdeElTurno;
        resultado.DialogoDeteccion = Frase(DialogosDeteccion(plan.Kind), rng);
        resultado.DialogoExito = Frase(DialogosExito, rng);
        resultado.MensajeBloqueo = MensajeDeBloqueo(ctx, plan, pierdeElTurno);

        Dialogo = resultado.DialogoDeteccion;
        Animo = NestorMood.Concentrado;
        return resultado;
    }

    // Los frenos, en orden de mas barato a mas caro. El sorteo va ULTIMO a
    // proposito: asi una tirada favorable no se "gasta" en un turno que igual
    // estaba bloqueado por cooldown.
    private bool PuedeIntervenir(EnemyPlan plan, IBattleRng rng)
    {
        // Forcejeo, accion nula, o el rival sin nada que valga la pena leer.
        if (!plan.Existe) return false;

        if (Intervenciones >= MaximoPorCombate) return false;
        if (Turno <= TurnosDeGracia) return false;
        if (Turno - _turnoDeLaUltima <= CooldownTurnos) return false;

        return rng != null && rng.Value() < Probabilidad;
    }

    // ---------- clasificacion de la jugada rival ----------

    // Traduce la accion que devolvio la IA a algo sobre lo que se pueda decidir.
    // Usa el MISMO DamageCalculator que usa MoveScorer para puntuar, asi que
    // "esto te mata" significa lo mismo para Nestor que para la IA.
    public EnemyPlan Clasificar(AiContext ctx, BattleAction accion)
    {
        var plan = new EnemyPlan { Accion = accion, Kind = EnemyPlanKind.Ninguna };
        if (accion == null || ctx == null) return plan;

        if (accion.Kind == BattleActionKind.SwitchPoken)
        {
            plan.Kind = EnemyPlanKind.Cambio;
            // Un cambio no es peligroso por si solo: lo caro para el rival es
            // quedarse. Por eso nunca cuenta como jugada fuerte — Nestor lo
            // obliga a pelear en vez de regalarle un turno perdido.
            plan.EsJugadaFuerte = false;
            return plan;
        }

        // Forcejeo (sin PP) no se lee: el rival ya esta en su peor momento y
        // anularselo no demuestra nada.
        if (accion.Kind != BattleActionKind.UseMove || accion.IsStruggle) return plan;

        List<MoveModel> movimientos = ctx.Self.Poken.Moves;
        if (accion.MoveIndex < 0 || accion.MoveIndex >= movimientos.Count) return plan;

        MoveDefinition move = movimientos[accion.MoveIndex].Definition;
        if (move == null) return plan;

        plan.Movimiento = string.IsNullOrEmpty(move.moveName) ? move.name : move.moveName;

        if (PredictionState.EsDeDano(move))
        {
            DamageResult esperado =
                DamageCalculator.CalculateExpected(ctx.Self, ctx.Foe, move, ctx.Model);

            int maxHp = ctx.Foe.Poken.MaxHP;
            plan.DanoEsperado = maxHp > 0 ? (float)esperado.Damage / maxHp : 0f;

            // Sin efecto por tipo o por habilidad: no hay nada que leer.
            if (esperado.Effectiveness <= 0f
                || MoveScorer.FoeAbilityBlocks(ctx.Foe, move.type))
                return plan;

            bool remata = esperado.Damage >= ctx.Foe.Poken.CurrentHP;
            plan.Kind = remata ? EnemyPlanKind.Remate : EnemyPlanKind.Ataque;
            plan.EsJugadaFuerte = remata || plan.DanoEsperado >= FraccionDeGolpeFuerte;
            return plan;
        }

        // --- movimientos de estado ---

        // Curacion, y solo si de verdad se va a curar algo.
        if (move.healPercent > 0 && ctx.Self.Poken.CurrentHP < ctx.Self.Poken.MaxHP)
        {
            plan.Kind = EnemyPlanKind.Curacion;
            plan.EsJugadaFuerte = true;
            return plan;
        }

        bool seSube = false;
        bool teBaja = false;
        if (move.statChanges != null)
        {
            foreach (StatChangeEntry entry in move.statChanges)
            {
                if (entry.targetSelf && entry.stages > 0) seSube = true;
                if (!entry.targetSelf && entry.stages < 0) teBaja = true;
            }
        }

        bool cambiaElCampo =
            move.weatherEffect != WeatherKind.None || move.fieldEffect != FieldEffectKind.None;

        if (seSube || cambiaElCampo)
        {
            plan.Kind = EnemyPlanKind.Preparacion;
            plan.EsJugadaFuerte = true;
            return plan;
        }

        // Estado alterado, o bajarte stats: la "habilidad peligrosa". Solo
        // cuenta si te lo puede aplicar de verdad — dormir a alguien que ya
        // duerme no es una jugada que valga leer.
        bool aplicaEstado = move.effect != StatusEffect.None && PuedeAplicar(ctx, move);
        if (aplicaEstado || teBaja)
        {
            plan.Kind = EnemyPlanKind.Estado;
            plan.EsJugadaFuerte = true;
            return plan;
        }

        return plan;
    }

    // El estado que trae el movimiento le entra al Poken del jugador? Mismo
    // criterio que MoveScorer usa para no puntuar estados redundantes.
    private static bool PuedeAplicar(AiContext ctx, MoveDefinition move)
    {
        if (move.effect == StatusEffect.Confused) return !ctx.Foe.IsConfused;
        if (ctx.Foe.Poken.CurrentStatus != StatusEffect.None) return false;
        return !StatusEffects.IsImmuneByType(move.effect, ctx.Foe);
    }

    // La mejor jugada del rival SIN contar la que Nestor le tapo. Sale del
    // mismo MoveScorer que usa la IA, asi que el rival sigue jugando con su
    // propio criterio: Nestor le saca la mejor opcion, no le elige la peor.
    //
    // Devuelve null si no le queda nada util.
    private static BattleAction SegundaOpcion(AiContext ctx, BattleAction bloqueada)
    {
        List<ScoredAction> puntuadas = MoveScorer.ScoreMoves(ctx);
        if (puntuadas.Count == 0) return null;

        int indiceBloqueado = bloqueada != null && bloqueada.Kind == BattleActionKind.UseMove
            ? bloqueada.MoveIndex
            : -1;

        BattleAction mejor = null;
        float mejorPuntaje = 0f;
        foreach (ScoredAction s in puntuadas)
        {
            if (s.Action.MoveIndex == indiceBloqueado) continue;
            if (s.Score <= 0f) continue;
            if (mejor == null || s.Score > mejorPuntaje)
            {
                mejor = s.Action;
                mejorPuntaje = s.Score;
            }
        }
        return mejor;
    }

    // ---------- retrato y dialogo ----------

    // Como esta Nestor cuando NO interviene. La idea es que se note que mira la
    // pelea: se preocupa si vas perdiendo, se entusiasma si el rival se cae.
    // No cambia todos los turnos por gusto — con los mismos PS da la misma cara.
    public NestorMood AnimoAmbiente(AiContext ctx)
    {
        if (ctx == null) return NestorMood.Neutral;

        float tuyo = ctx.FoeHpPercent;      // "Foe" de la IA rival = el jugador
        float delRival = ctx.SelfHpPercent;

        if (tuyo <= 0.25f) return NestorMood.Preocupado;
        if (delRival <= 0.20f) return NestorMood.Furioso;
        if (delRival <= 0.45f) return NestorMood.Confiado;

        // A punto de poder leer: se lo ve pensando.
        bool cerca = Intervenciones < MaximoPorCombate
                     && Turno - _turnoDeLaUltima > CooldownTurnos
                     && Turno >= TurnosDeGracia;
        return cerca ? NestorMood.Pensando : NestorMood.Neutral;
    }

    // Segundo tiempo de la lectura: el bloqueo ya paso y Nestor lo festeja.
    public void MarcarExito(string dialogo)
    {
        Animo = NestorMood.Confiado;
        Dialogo = dialogo;
        DestacadoEsteTurno = true;
    }

    // Reacciones a cosas que no son la lectura, para que el retrato no quede
    // congelado el resto del combate.
    public void ReaccionarACaida(bool cayoElRival)
    {
        Animo = cayoElRival ? NestorMood.Confiado : NestorMood.Sorprendido;
        Dialogo = cayoElRival ? "¡Así se hace!" : "¡No! Mandá otro, rápido.";
        DestacadoEsteTurno = false;
    }

    public void Ambiente(AiContext ctx)
    {
        Animo = AnimoAmbiente(ctx);
        Dialogo = null;
        DestacadoEsteTurno = false;
    }

    public NestorPortraitSnapshot Snapshot()
    {
        return new NestorPortraitSnapshot
        {
            Activa = true,
            Animo = Animo,
            Dialogo = Dialogo,
            Destacado = DestacadoEsteTurno,
            LecturasRestantes = LecturasRestantes < 0 ? 0 : LecturasRestantes
        };
    }

    // ---------- textos ----------

    private static string MensajeDeBloqueo(AiContext ctx, EnemyPlan plan, bool pierdeElTurno)
    {
        string rival = ctx != null && ctx.Self != null && ctx.Self.Poken != null
                       && ctx.Self.Poken.Definition != null
            ? ctx.Self.Poken.Definition.pokenName
            : "El rival";

        if (!pierdeElTurno)
        {
            return plan.Kind == EnemyPlanKind.Cambio
                ? $"¡Néstor le corta la retirada! {rival} tiene que quedarse."
                : $"¡Néstor le tapa la jugada a {rival}!";
        }

        switch (plan.Kind)
        {
            case EnemyPlanKind.Remate:
                return $"¡Néstor le leyó el remate! {rival} pierde el turno.";
            case EnemyPlanKind.Curacion:
                return $"¡Néstor lo vio venir! {rival} no llega a curarse.";
            case EnemyPlanKind.Preparacion:
                return $"¡Néstor le arruina la preparación a {rival}!";
            case EnemyPlanKind.Estado:
                return $"¡Néstor se le adelanta! {rival} no llega a usar {plan.Movimiento}.";
            default:
                return $"¡Néstor anticipó el golpe! {rival} se queda sin turno.";
        }
    }

    private static string[] DialogosDeteccion(EnemyPlanKind kind)
    {
        switch (kind)
        {
            case EnemyPlanKind.Remate:
                return new[]
                {
                    "Pará... va a rematar.",
                    "Esa es la grande. No lo dejes.",
                    "Ya sé lo que va a hacer."
                };
            case EnemyPlanKind.Curacion:
                return new[]
                {
                    "Se quiere curar. Cortalo.",
                    "Ahí va a estirar la pelea.",
                    "No lo dejes recuperarse."
                };
            case EnemyPlanKind.Preparacion:
                return new[]
                {
                    "Se está preparando. Esperá...",
                    "Si lo dejás armarse, no lo agarrás más.",
                    "Ojo, viene la subida."
                };
            case EnemyPlanKind.Estado:
                return new[]
                {
                    "Te va a ensuciar la pelea.",
                    "Ahí viene el sucio. Lo veo.",
                    "No, eso no."
                };
            case EnemyPlanKind.Cambio:
                return new[]
                {
                    "Se quiere ir. No lo dejes.",
                    "Ahí quiere cambiar. Ni en pedo.",
                    "Se está por escapar."
                };
            default:
                return new[]
                {
                    "La próxima acción es esa.",
                    "Concentrate... ya lo tengo.",
                    "Esperá. Ahí va."
                };
        }
    }

    private static readonly string[] DialogosExito =
    {
        "Lo vi venir.",
        "Leído.",
        "Te dije.",
        "Ese lo tenía marcado.",
        "Demasiado obvio."
    };

    // Elige una frase sin repetir la anterior: escuchar dos veces seguidas la
    // misma linea es lo que hace que un personaje suene a maquina.
    private string Frase(string[] opciones, IBattleRng rng)
    {
        if (opciones == null || opciones.Length == 0) return null;
        if (opciones.Length == 1) return opciones[0];

        var candidatas = new List<string>(opciones);
        candidatas.RemoveAll(f => f == _ultimaFrase);
        if (candidatas.Count == 0) candidatas.AddRange(opciones);

        string elegida = candidatas[rng != null ? rng.Range(0, candidatas.Count) : 0];
        _ultimaFrase = elegida;
        return elegida;
    }
}
