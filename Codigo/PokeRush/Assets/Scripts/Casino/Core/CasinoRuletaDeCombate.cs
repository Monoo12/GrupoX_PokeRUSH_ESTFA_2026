using System.Collections.Generic;
using UnityEngine;

// Que hace un gajo de la ruleta cuando se gira DENTRO del combate del jefe del
// ultimo piso.
//
// Los valores nuevos van SIEMPRE al final: los premios se guardan en assets
// (CasinoPrizeTable) y meter uno en el medio le cambia el efecto a todos los de
// abajo.
public enum EfectoDeRuleta
{
    // Premio normal de mapa (objeto, plata, Pokemon). Es lo que tienen todos
    // los premios que ya existian: adentro del combate no hacen nada.
    Ninguno,
    SubirAtaque,
    BajarAtaque,
    BajarDefensa,
    CambiarTipo,
    SacarVida,
    DarPocion,
    BloquearTurno,
    TirarDeNuevo
}

// A quien le cae cada gajo cuando la ruleta se gira dentro de un combate.
//
// La del jefe del casino es SiempreElJugador: todo le pega al Poken del jugador,
// lo bueno y lo malo, y el azar decide. Las otras dos existen para la pelea de
// Julieta, donde la MISMA ruleta primero juega para el jugador y despues ella
// la da vuelta: lo que era a favor pasa a ser en contra.
public enum QuienCobraLaRuleta
{
    SiempreElJugador,
    // Lo bueno (ataque, pocion) al jugador; lo malo (bajas, mordisco, turno
    // perdido, cambio de tipo) al rival. Ningun gajo le juega en contra.
    AFavorDelJugador,
    // El espejo: lo bueno al rival, lo malo al jugador.
    AFavorDelRival
}

// La ruleta que aparece cada dos turnos en la pelea contra el jefe del casino.
//
// Es la MISMA ruleta de premios de los pisos de abajo: mismo prefab, misma
// animacion, mismos ocho gajos. Lo unico que cambia es que en vez de entregar
// un objeto, cada gajo aplica un efecto sobre el Poken que el jugador tiene en
// el campo (ver EfectoDeRuleta y PrizeWheelRequest.Entregar).
//
// Es una clase pura a proposito: no toca la escena ni la UI, asi las reglas se
// prueban enteras desde CasinoSmokeTests sin abrir un combate. Quien la usa es
// BattleController, que es el unico que sabe de ritmo y de pantallas.
public class CasinoRuletaDeCombate
{
    // Los gajos tienen que ser ocho: la rueda esta DIBUJADA con ocho, y pasarle
    // otra cantidad hace que el puntero frene entre dos.
    public const int Gajos = 8;

    // Cuanto mueve un stage cada gajo. Era dos ("subio mucho"), pero la rueda
    // gira cada dos turnos y los gajos se acumulan: en el playtest del 4-oct
    // quedaba demasiado pesada. Uno por gajo sigue notandose al acumularse.
    public const int EtapasPorGajo = 1;

    // El mordisco de SacarVida y la cura de DarPocion, en fraccion de los PS
    // maximos del Poken. La cura es mas grande que el mordisco porque el
    // jugador ya esta peleando contra seis Pokemon y contra la ruleta.
    public const float FraccionDeMordisco = 0.125f;  // un octavo
    public const float FraccionDeCura = 0.35f;       // un tercio largo

    // Tope de TirarDeNuevo encadenados. Sin esto, con mala suerte la ruleta se
    // podria quedar girando sola para siempre.
    public const int MaximoDeGirosEncadenados = 3;

    // Los ocho gajos de esta pelea.
    public Prize[] Premios;

    // Cada cuantos turnos aparece. 2 = turnos 2, 4, 6... El turno 1 queda libre
    // para que el jugador vea con quien pelea antes de que la casa meta mano.
    //
    // Es fijo y no sorteado por la misma razon que la Prediccion del boss de la
    // torre: asi se puede planificar (guardas la jugada fuerte para el turno
    // limpio) y no cambia al cargar la partida.
    public int CadaCuantosTurnos = 2;

    // A quien le cae cada gajo. Por defecto, como en el casino: todo al jugador.
    // Se puede cambiar con la pelea en curso (Julieta la da vuelta a mitad).
    public QuienCobraLaRuleta QuienCobra = QuienCobraLaRuleta.SiempreElJugador;

    // Texto del cartel: recibe el premio y si le toco al jugador. Null = el del
    // premio (customMessage o "¡Has obtenido...!"), que es lo que usa el casino.
    public System.Func<Prize, bool, string> Cartel;

    public CasinoRuletaDeCombate(Prize[] premios, int cadaCuantosTurnos = 2)
    {
        Premios = premios;
        CadaCuantosTurnos = Mathf.Max(1, cadaCuantosTurnos);
    }

    public bool Configurada => Premios != null && Premios.Length > 0;

    // ---------- a quien le toca ----------

    // Lo que le conviene recibir a quien lo recibe. El resto (las bajas, el
    // mordisco, el turno perdido y el cambio de tipo, que casi siempre le saca
    // el STAB) es malo para el que lo recibe. TirarDeNuevo no le cae a nadie.
    public static bool EsFavorable(EfectoDeRuleta efecto)
        => efecto == EfectoDeRuleta.SubirAtaque || efecto == EfectoDeRuleta.DarPocion;

    public static bool LeTocaAlJugador(EfectoDeRuleta efecto, QuienCobraLaRuleta quien)
    {
        switch (quien)
        {
            case QuienCobraLaRuleta.AFavorDelJugador: return EsFavorable(efecto);
            case QuienCobraLaRuleta.AFavorDelRival: return !EsFavorable(efecto);
            default: return true;
        }
    }

    // Cobra el gajo que salio segun QuienCobra: elige el lado, aplica el efecto
    // y arma el texto del cartel. Es lo que llama el combate al frenar la rueda.
    public List<BattleEvent> Cobrar(Prize premio, BattleModel modelo, IBattleRng rng,
                                    out bool alJugador, out string cartel)
    {
        alJugador = true;
        if (premio == null)
        {
            cartel = "La ruleta se detiene en un gajo vacio.";
            return new List<BattleEvent>();
        }

        alJugador = LeTocaAlJugador(premio.efectoDeCombate, QuienCobra);
        cartel = Cartel != null ? Cartel(premio, alJugador) : premio.BuildMessage();
        return Aplicar(premio.efectoDeCombate, modelo, rng, alJugador);
    }

    // Se llama al EMPEZAR cada turno del jugador, con el numero del turno que
    // esta por jugarse.
    public bool TocaGirar(int turno)
    {
        if (!Configurada) return false;
        return turno > 1 && turno % CadaCuantosTurnos == 0;
    }

    // ---------- efectos ----------

    // Aplica el gajo sobre el Poken que el jugador tiene en el campo y devuelve
    // los eventos para que el combate los reproduzca con su ritmo de siempre
    // (barra de mensajes, refresco del HUD, animacion de la barra de PS).
    //
    // Devuelve lista vacia si no hay nada que hacer: el gajo era un premio de
    // mapa, o la pelea ya termino.
    public static List<BattleEvent> Aplicar(EfectoDeRuleta efecto, BattleModel modelo, IBattleRng rng)
        => Aplicar(efecto, modelo, rng, alJugador: true);

    // Igual, pero eligiendo el lado: 'alJugador' false aplica el gajo sobre el
    // Poken que el RIVAL tiene en el campo. Los efectos son los mismos de los
    // dos lados (el mordisco tampoco debilita al rival).
    public static List<BattleEvent> Aplicar(EfectoDeRuleta efecto, BattleModel modelo, IBattleRng rng,
                                            bool alJugador)
    {
        var eventos = new List<BattleEvent>();
        if (modelo == null || modelo.IsBattleOver) return eventos;

        BattlePokenState objetivo = alJugador ? modelo.PlayerState : modelo.EnemyState;
        if (objetivo == null || objetivo.Poken == null || objetivo.Poken.IsFainted) return eventos;

        string nombre = objetivo.Poken.Definition.pokenName;

        switch (efecto)
        {
            case EfectoDeRuleta.SubirAtaque:
                MoveEffectExecutor.ApplyStatChange(objetivo, Stat.Attack, EtapasPorGajo, eventos);
                break;

            case EfectoDeRuleta.BajarAtaque:
                MoveEffectExecutor.ApplyStatChange(objetivo, Stat.Attack, -EtapasPorGajo, eventos);
                break;

            case EfectoDeRuleta.BajarDefensa:
                MoveEffectExecutor.ApplyStatChange(objetivo, Stat.Defense, -EtapasPorGajo, eventos);
                break;

            case EfectoDeRuleta.CambiarTipo:
                CambiarTipo(objetivo, nombre, eventos, rng);
                break;

            case EfectoDeRuleta.SacarVida:
                Morder(objetivo, nombre, eventos);
                break;

            case EfectoDeRuleta.DarPocion:
                Curar(objetivo, nombre, eventos);
                break;

            case EfectoDeRuleta.BloquearTurno:
                // Se reusa el retroceso, que es exactamente esto: el Poken
                // pierde su accion de ESTE turno y se limpia solo al final.
                objetivo.Flinched = true;
                BattleEvent.Add(eventos, "Las luces del casino encandilan a " + nombre + ": pierde el turno.");
                break;

            case EfectoDeRuleta.TirarDeNuevo:
                BattleEvent.Add(eventos, "La casa te debe otro giro.");
                break;
        }

        return eventos;
    }

    // Pasa a ser de UN solo tipo, sorteado entre todos menos los que ya tiene.
    // Le puede salir bien o mal: es el gajo mas de casino de los ocho.
    private static void CambiarTipo(BattlePokenState objetivo, string nombre,
                                    List<BattleEvent> eventos, IBattleRng rng)
    {
        PokenType nuevo = SortearTipo(objetivo, rng);
        if (nuevo == PokenType.None)
        {
            BattleEvent.Add(eventos, "La ruleta le busca un tipo nuevo a " + nombre + "... y le toca el suyo.");
            return;
        }

        objetivo.TipoCambiado = nuevo;
        eventos.Add(BattleEvent.Of(BattleEventKind.StatStageChanged, objetivo.IsPlayerSide,
            nombre + " pasó a ser de tipo " + NombreDeTipo(nuevo) + "."));
    }

    // Sortea un tipo distinto de los que ya tiene. None si no queda ninguno (no
    // pasa con la tabla de tipos de hoy, pero el que llama no tiene por que
    // confiar en eso).
    public static PokenType SortearTipo(BattlePokenState objetivo, IBattleRng rng)
    {
        var candidatos = new List<PokenType>();
        foreach (PokenType t in System.Enum.GetValues(typeof(PokenType)))
        {
            if (t == PokenType.None) continue;
            if (objetivo.EsDeTipo(t)) continue;
            candidatos.Add(t);
        }

        if (candidatos.Count == 0) return PokenType.None;
        return candidatos[rng.Range(0, candidatos.Count)];
    }

    // Nunca debilita: deja 1 PS de piso. Un KO fuera de turno dejaria el
    // combate pidiendo reemplazo en un momento en el que el motor no lo espera,
    // y encima se leeria como una trampa de la casa.
    private static void Morder(BattlePokenState objetivo, string nombre, List<BattleEvent> eventos)
    {
        PokenModel poken = objetivo.Poken;
        int mordisco = Mathf.Max(1, Mathf.RoundToInt(poken.MaxHP * FraccionDeMordisco));
        if (mordisco > poken.CurrentHP - 1) mordisco = poken.CurrentHP - 1;

        if (mordisco <= 0)
        {
            BattleEvent.Add(eventos, "La casa le quiere cobrar a " + nombre + ", pero ya no le queda nada.");
            return;
        }

        poken.TakeDamage(mordisco);
        eventos.Add(new BattleEvent
        {
            Kind = BattleEventKind.HpChanged,
            TargetIsPlayer = objetivo.IsPlayerSide,
            Message = "La casa le cobra " + mordisco + " PS a " + nombre + ".",
            HpAfter = poken.CurrentHP,
            ExtraPause = 0.35f
        });
    }

    private static void Curar(BattlePokenState objetivo, string nombre, List<BattleEvent> eventos)
    {
        PokenModel poken = objetivo.Poken;
        if (poken.CurrentHP >= poken.MaxHP)
        {
            BattleEvent.Add(eventos, "Te invitan una poción, pero " + nombre + " está impecable.");
            return;
        }

        int cura = Mathf.Max(1, Mathf.RoundToInt(poken.MaxHP * FraccionDeCura));
        poken.Heal(cura);
        eventos.Add(new BattleEvent
        {
            Kind = BattleEventKind.HpChanged,
            TargetIsPlayer = objetivo.IsPlayerSide,
            Message = "La casa invita una poción: " + nombre + " recuperó PS.",
            HpAfter = poken.CurrentHP,
            ExtraPause = 0.35f
        });
    }

    public static string NombreDeTipo(PokenType tipo)
    {
        switch (tipo)
        {
            case PokenType.Normal: return "Normal";
            case PokenType.Fire: return "Fuego";
            case PokenType.Water: return "Agua";
            case PokenType.Grass: return "Planta";
            case PokenType.Electric: return "Eléctrico";
            case PokenType.Ice: return "Hielo";
            case PokenType.Fighting: return "Lucha";
            case PokenType.Poison: return "Veneno";
            case PokenType.Ground: return "Tierra";
            case PokenType.Flying: return "Volador";
            case PokenType.Psychic: return "Psíquico";
            case PokenType.Bug: return "Bicho";
            case PokenType.Rock: return "Roca";
            case PokenType.Ghost: return "Fantasma";
            case PokenType.Dragon: return "Dragón";
            case PokenType.Dark: return "Siniestro";
            case PokenType.Steel: return "Acero";
            case PokenType.Fairy: return "Hada";
            default: return "???";
        }
    }
}
