using System.Collections.Generic;

// La fase de liberacion de Nestor: el chip se quema solo mientras el jugador
// aguanta. Motor aparte del TurnEngine, y a proposito.
//
// Por que no meterlo en ResolveTurn: en esta fase se invierten TODAS las reglas
// del combate a la vez.
//   - el rival no elige por IA, tiene un guion
//   - pegarle al boss lo CURA y retrocede el contador
//   - el boss no puede caer
//   - no hacer nada REINICIA el contador
//   - la condicion de victoria es un contador, no los PS
// Meter eso adentro del pipeline normal significaria if-earlo de punta a punta
// y poner en riesgo lo unico que hoy funciona bien.
//
// Es clase pura y emite BattleEvent normales, asi que BattleController la
// reproduce con el mismo PlayEvents de siempre.
public class LiberationEngine
{
    // Lo que puede hacer el jugador en esta fase. Atacar sigue disponible: es
    // un error castigado, no una opcion prohibida. La leccion del nivel es
    // DEJAR de atacar, y para eso hay que poder equivocarse.
    public enum Accion { Curar, Proteger, Cambiar, Atacar, Nada }

    private readonly BattleModel _model;
    private readonly IBattleRng _rng;

    // Turnos que hay que aguantar. Sale de BossBattleConfig: la mitad si
    // cortaste la energia auxiliar.
    public int TurnosTotales { get; }
    public int TurnosAguantados { get; private set; }
    public bool Terminada => TurnosAguantados >= TurnosTotales;

    private int _turno;

    // Dano del chip por turno. Tabla fija en vez de DamageCalculator: es
    // deterministico y se tunea en dos minutos sin tocar formulas.
    private const int DanoGolpeFuerte = 34;
    private const int DanoDesgaste = 12;
    private const int DanoDirigido = 22;

    // Lo que se le devuelve al chip si el jugador ataca
    private const int TurnosQueRetrocede = 2;

    // Cuanto se cura el boss cuando le pegan, como fraccion de sus PS maximos.
    // Poco a proposito: el castigo real es que el contador retrocede, no que se
    // vuelva imposible de bajar. Con 16 son ~6 PS en un boss de 98.
    private const int DivisorCuracionDelChip = 16;

    public LiberationEngine(BattleModel model, IBattleRng rng, int turnosTotales)
    {
        _model = model;
        _rng = rng;
        TurnosTotales = UnityEngine.Mathf.Max(1, turnosTotales);
    }

    // Que va a hacer el chip este turno. Ciclo de 3 para que el jugador
    // necesite las tres herramientas y no le alcance con repetir una.
    public string AvisoDelChip()
    {
        switch (_turno % 3)
        {
            case 0: return "El chip se sobrecarga: viene un golpe fuerte.";
            case 1: return "El chip larga una descarga corrosiva.";
            default: return "El chip apunta a tu Poken activo.";
        }
    }

    public List<BattleEvent> ResolveTurn(Accion accion)
    {
        var events = new List<BattleEvent>();
        string nestor = _model.ActiveEnemyPoken.Definition.pokenName;

        // --- lo que hace el jugador ---
        switch (accion)
        {
            case Accion.Atacar:
                // Pegarle repara el chip: es la trampa central de la fase.
                TurnosAguantados = UnityEngine.Mathf.Max(0, TurnosAguantados - TurnosQueRetrocede);
                _model.ActiveEnemyPoken.Heal(_model.ActiveEnemyPoken.MaxHP / DivisorCuracionDelChip);
                events.Add(BattleEvent.Of(BattleEventKind.HpChanged, false));
                BattleEvent.Add(events,
                    $"¡El golpe reparó el chip! {nestor} se recupera y la sobrecarga retrocede.");
                break;

            case Accion.Proteger:
                _model.PlayerState.Protected = true;
                BattleEvent.Add(events, "Te pusiste en guardia.");
                break;

            case Accion.Nada:
                // No actuar estabiliza el chip: la regla no es "no hacer nada",
                // es "actuar sin atacar".
                TurnosAguantados = 0;
                BattleEvent.Add(events, "Dudaste. El chip se estabiliza y la sobrecarga se reinicia.");
                break;

            case Accion.Curar:
            case Accion.Cambiar:
                // El efecto real lo aplica el controller (necesita el objeto o
                // el indice del equipo); aca solo cuentan como accion valida.
                break;
        }

        // --- ataque del chip ---
        AtacarConChip(events);

        // --- avance del contador ---
        if (accion != Accion.Atacar && accion != Accion.Nada)
        {
            TurnosAguantados++;
            if (!Terminada)
                BattleEvent.Add(events,
                    $"El chip se quema. Aguantá {TurnosTotales - TurnosAguantados} turno(s) más.");
        }

        _turno++;

        if (Terminada)
        {
            BattleEvent.Add(events, $"¡El chip de {nestor} se apagó!");
            BattleEvent.Add(events, $"{nestor} vuelve en sí. Lo liberaste.");
        }

        return events;
    }

    // El chip pega todos los turnos, escalando, y sus ataques piden una
    // herramienta distinta cada vez: fuerte -> protegés, desgaste -> curás,
    // dirigido -> lo sacás.
    private void AtacarConChip(List<BattleEvent> events)
    {
        PokenModel objetivo = _model.ActivePlayerPoken;
        if (objetivo == null || objetivo.IsFainted) return;

        if (_model.PlayerState.Protected)
        {
            BattleEvent.Add(events, $"¡{objetivo.Definition.pokenName} aguantó la descarga!");
            _model.PlayerState.Protected = false;
            return;
        }

        int dano;
        string mensaje;
        switch (_turno % 3)
        {
            case 0:
                dano = DanoGolpeFuerte;
                mensaje = "¡La sobrecarga golpea con todo!";
                break;
            case 1:
                dano = DanoDesgaste;
                mensaje = "La descarga corroe y desgasta.";
                // El desgaste ademas envenena si puede: obliga a curar
                if (objetivo.CurrentStatus == StatusEffect.None)
                {
                    objetivo.SetStatus(StatusEffect.Poisoned);
                    events.Add(BattleEvent.Of(BattleEventKind.StatusChanged, true));
                }
                break;
            default:
                dano = DanoDirigido;
                mensaje = $"El chip castiga a {objetivo.Definition.pokenName}.";
                break;
        }

        // Escala con los turnos aguantados: la fase se pone tensa hacia el final
        dano += TurnosAguantados * 2;

        objetivo.TakeDamage(dano);
        events.Add(BattleEvent.Of(BattleEventKind.HpChanged, true));
        BattleEvent.Add(events, mensaje);
    }
}
