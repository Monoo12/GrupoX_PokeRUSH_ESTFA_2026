using System.Collections.Generic;
using UnityEngine;

// El jefe del ultimo piso del casino: el que espera atras de la mesa de poker,
// al final del pasillo de guardias.
//
// TODO (equipo): el personaje es Martin Reymar. La skin y el retrato que trae
// ahora son PROVISORIOS (sale del pack de Essentials); cuando esten los sprites
// definitivos se cambian desde el Inspector, sin tocar este archivo.
//
// Tiene dos cosas que no tiene ningun otro NPC del casino:
//
//   1. Durante la pelea, cada dos turnos se abre la ruleta de premios y lo que
//      sale le pega al Poken que el jugador tiene en el campo: le sube el
//      ataque, le cambia el tipo, le saca vida, le bloquea el turno... Es la
//      MISMA ruleta de los pisos de abajo (ver CasinoRuletaDeCombate): lo unico
//      que cambia es que los gajos traen un EfectoDeRuleta en vez de un objeto.
//
//   2. Cuando la pelea termina -la gane quien la gane- hay una mano de
//      blackjack 1 contra 1. La juega el jugador, porque es la unica mesa que
//      hay, pero lo que se decide es si EL DERROTADO se levanta: si gana, se le
//      revive un Pokemon al azar con la vida llena; si empata, con la mitad; y
//      si pierde, la pelea queda como estaba. Con un Pokemon revivido, la pelea
//      SIGUE con los equipos como quedaron (BattleController.Revancha), no
//      empieza de cero.
//
// Todo lo que decide algo vive en metodos estaticos y publicos (QuienGana,
// PsAlRevivir, ElegirParaRevivir): asi las reglas de la mano se prueban enteras
// desde CasinoSmokeTests sin abrir un combate ni repartir una carta.
[RequireComponent(typeof(Collider2D))]
public class CasinoJefeFinalNpc : CasinoNpcConCombate, IInteractable
{
    [Header("El jefe")]
    [SerializeField] private EnemyDefinition rival;
    [SerializeField] private StoryFlag flagVencido = StoryFlag.Casino_JefeFinalVencido;
    [Tooltip("Flag de casino terminado. Se activa junto con el de vencido.")]
    [SerializeField] private StoryFlag flagCasinoCompletado = StoryFlag.Casino_Completado;

    [Header("La ruleta de la pelea")]
    [Tooltip("Los ocho gajos que aparecen cada dos turnos durante el combate. " +
             "Cada premio lleva su EfectoDeRuleta. Vacio = la pelea va sin ruleta.")]
    [SerializeField] private CasinoPrizeTable ruletaDeCombate;
    [Tooltip("Cada cuantos turnos aparece la ruleta. 2 = turnos 2, 4, 6...")]
    [SerializeField, Min(1)] private int turnosEntreGiros = 2;

    [Header("La mano de blackjack")]
    [Tooltip("Cuantas veces se puede reabrir la pelea con una mano. 1 = una " +
             "sola revancha. En 0 no hay mano y el combate termina y listo. " +
             "Sin tope, dos jugadores parejos se quedan jugando para siempre.")]
    [SerializeField, Min(0)] private int maximoDeRevanchas = 1;

    [Header("Textos: antes de pelear")]
    [SerializeField, TextArea(2, 4)] private string pregunta =
        "Así que llegaste hasta acá. Sentate. Acá arriba las peleas las reparte la casa: cada dos turnos, la ruleta decide por vos. ¿Jugamos?";
    [SerializeField] private string opcionSi = "Jugar";
    [SerializeField] private string opcionNo = "Todavía no";
    [SerializeField, TextArea(2, 4)] private string mensajeSiRechaza =
        "La mesa no se va a ningún lado. Yo tampoco.";
    [SerializeField, TextArea(2, 4)] private string mensajeYaVencido =
        "Te llevaste el casino entero. Andá antes de que cambie de idea.";

    [Header("Textos: la mano")]
    [Tooltip("{0} = el nombre del jefe.")]
    [SerializeField, TextArea(2, 4)] private string mensajePerdisteLaPelea =
        "Te gané. Pero la casa no te deja ir sin revancha: una mano. Si ganás, uno de tus Pokémon se levanta y seguimos.";
    [SerializeField, TextArea(2, 4)] private string mensajeGanasteLaPelea =
        "Me ganaste... pero acá todavía se juega. Una mano. Si te gano, uno de los míos se levanta y seguimos.";
    [SerializeField, TextArea(2, 4)] private string mensajeSinRevancha =
        "Se acabaron las manos. La casa cierra.";

    [Header("Textos: resultado de la mano ({0} = el Pokemon revivido)")]
    [SerializeField, TextArea(2, 4)] private string mensajeRevivesEntero =
        "¡{0} se levanta como nuevo! Dale, seguimos.";
    [SerializeField, TextArea(2, 4)] private string mensajeRevivesAMedias =
        "Empate. {0} se levanta, pero a media máquina. Seguimos.";
    [SerializeField, TextArea(2, 4)] private string mensajeJefeRevive =
        "¿Viste? La casa siempre tiene una carta más. {0} vuelve a la mesa.";
    [SerializeField, TextArea(2, 4)] private string mensajeJefeReviveAMedias =
        "Empate. {0} vuelve, aunque sea a media máquina.";

    [Header("Textos: final")]
    [SerializeField, TextArea(2, 4)] private string mensajeVictoria =
        "Se terminó. El casino es tuyo. Andá a contarlo.";
    [SerializeField, TextArea(2, 4)] private string mensajeDerrota =
        "Volvé cuando tengas con qué. La mesa te espera.";

    // Cuantas manos se jugaron en ESTE encuentro. Se reinicia cuando el jugador
    // vuelve a hablarle, no entre pelea y revancha.
    private int _revanchasJugadas;
    private bool _enLaMano;

    public EnemyDefinition Rival => rival;
    public StoryFlag FlagVencido => flagVencido;

    // ---------- interaccion ----------

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || EsperandoCombate || _enLaMano) return false;

        if (flagVencido != StoryFlag.None && story != null && story.HasFlag(flagVencido))
        {
            Decir(mensajeYaVencido);
            return true;
        }

        if (rival == null)
        {
            Debug.LogError($"[JefeFinal] '{name}' no tiene rival asignado.");
            return false;
        }

        Preguntar(pregunta, opcionSi, opcionNo, acepto =>
        {
            if (!acepto) { Decir(mensajeSiRechaza); return; }

            _revanchasJugadas = 0;
            LanzarCombate(player, rival, outcome => AlTerminar(player, story, outcome), ArmarRuleta());
        });
        return true;
    }

    private CasinoRuletaDeCombate ArmarRuleta()
    {
        if (ruletaDeCombate == null || ruletaDeCombate.premios == null || ruletaDeCombate.premios.Length == 0)
        {
            Debug.LogWarning($"[JefeFinal] '{name}' no tiene ruleta de combate: la pelea va sin ella.");
            return null;
        }
        return new CasinoRuletaDeCombate(ruletaDeCombate.premios, turnosEntreGiros);
    }

    // ---------- fin de la pelea ----------

    private void AlTerminar(PlayerModel player, StoryModel story, BattleOutcome outcome)
    {
        if (outcome == null || outcome.Escaped) return;

        bool perdioElJugador = !outcome.PlayerVictory;

        // Sin manos disponibles, la pelea queda como termino.
        if (_revanchasJugadas >= maximoDeRevanchas)
        {
            AlVolverAlMapa(() => Decir(mensajeSinRevancha, () => Cerrar(story, outcome)));
            return;
        }

        AlVolverAlMapa(() =>
        {
            string aviso = perdioElJugador ? mensajePerdisteLaPelea : mensajeGanasteLaPelea;
            Decir(aviso, () => AbrirLaMano(player, story, outcome, perdioElJugador));
        });
    }

    // Una sola mano, sin fichas, contra el jefe. Es el modo de apuesta que ya
    // usa Tacconi en el piso 3 (BlackjackScreenRequest.ApuestaPokemon): reparte
    // una mano, no deja doblar ni dividir y se cierra sola.
    private void AbrirLaMano(PlayerModel player, StoryModel story, BattleOutcome outcome, bool perdioElJugador)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[JefeFinal] No hay MenuManager: no se puede repartir la mano.");
            Cerrar(story, outcome);
            return;
        }

        _enLaMano = true;
        _revanchasJugadas++;

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Blackjack,
            new BlackjackScreenRequest
            {
                Titulo = "La mesa de " + nombre,
                ApuestaPokemon = perdioElJugador ? "tu revancha" : "la revancha de " + nombre,
                Rival = nombre,
                TextoGana = perdioElJugador
                    ? "Se levanta uno de los tuyos."
                    : "La casa se queda sin cartas.",
                TextoEmpata = perdioElJugador
                    ? "Se levanta uno de los tuyos, a media máquina."
                    : "Uno de los suyos vuelve, a media máquina.",
                TextoPierde = perdioElJugador
                    ? "La casa gana y la pelea queda como estaba."
                    : "Uno de los suyos vuelve a la mesa."
            },
            resultado =>
            {
                _enLaMano = false;
                BlackjackOutcome mano = resultado is BlackjackScreenResult r
                    ? r.ResultadoApuestaPokemon
                    : BlackjackOutcome.Pendiente;

                Resolver(player, story, outcome, perdioElJugador, mano);
            });
    }

    // ---------- que pasa con lo que salio ----------

    private void Resolver(PlayerModel player, StoryModel story, BattleOutcome outcome,
                          bool perdioElJugador, BlackjackOutcome mano)
    {
        BlackjackOutcome paraElDerrotado = QuienGana(mano, perdioElJugador);

        List<PokenModel> equipo = perdioElJugador
            ? player.Team
            : outcome.Enemy != null ? outcome.Enemy.Team : null;

        PokenModel revivido = paraElDerrotado == BlackjackOutcome.Pierde
                              || paraElDerrotado == BlackjackOutcome.Pendiente
            ? null
            : ElegirParaRevivir(equipo, Random.value);

        if (revivido == null)
        {
            // Nadie se levanta: la pelea queda como termino.
            Cerrar(story, outcome);
            return;
        }

        int ps = PsAlRevivir(revivido, paraElDerrotado);
        Revivir(revivido, ps);

        string cartel = TextoDelRevivido(perdioElJugador, paraElDerrotado, revivido);
        Debug.Log($"[JefeFinal] Mano {mano} -> para el derrotado {paraElDerrotado}. " +
                  $"Revive {revivido.Definition.pokenName} con {ps}/{revivido.MaxHP} PS. Va la revancha.");

        Decir(cartel, () =>
        {
            if (LanzarRevancha(player, o => AlTerminar(player, story, o))) return;

            // No arranco (p. ej. el otro lado quedo sin nadie): se cierra como
            // termino la pelea, que es mejor que dejar al jugador esperando.
            Debug.LogWarning("[JefeFinal] La revancha no pudo arrancar.");
            Cerrar(story, outcome);
        });
    }

    private string TextoDelRevivido(bool perdioElJugador, BlackjackOutcome paraElDerrotado, PokenModel revivido)
    {
        string nombreDelPoken = revivido.Definition.pokenName;
        bool entero = paraElDerrotado != BlackjackOutcome.Empata;

        string plantilla = perdioElJugador
            ? (entero ? mensajeRevivesEntero : mensajeRevivesAMedias)
            : (entero ? mensajeJefeRevive : mensajeJefeReviveAMedias);

        return string.Format(plantilla, nombreDelPoken);
    }

    // Cierra el encuentro con el resultado que quedo.
    private void Cerrar(StoryModel story, BattleOutcome outcome)
    {
        if (outcome == null || !outcome.PlayerVictory)
        {
            Decir(mensajeDerrota);
            return;
        }

        Activar(story, flagVencido);
        Activar(story, flagCasinoCompletado);
        if (rival != null) Activar(story, rival.defeatGrantsFlag);
        CasinoDialogo.Guardar();

        Debug.Log($"[JefeFinal] '{name}' vencido: casino completado.");
        Decir(mensajeVictoria);
    }

    // ---------- reglas de la mano (puras, para los tests) ----------

    // Como se lee la mano DESDE EL LADO DEL DERROTADO.
    //
    // La mano la reparte siempre el jugador, porque es la unica mesa que hay.
    // Cuando el derrotado es el jefe, el resultado se da vuelta: que el jefe
    // gane su revancha es que el jugador pierda la mano.
    public static BlackjackOutcome QuienGana(BlackjackOutcome mano, bool perdioElJugador)
    {
        if (perdioElJugador) return mano;

        switch (mano)
        {
            case BlackjackOutcome.Blackjack:
            case BlackjackOutcome.Gana:
                return BlackjackOutcome.Pierde;
            case BlackjackOutcome.Pierde:
                return BlackjackOutcome.Gana;
            default:
                // El empate y el "se fue sin jugar" se leen igual de los dos lados.
                return mano;
        }
    }

    // Con cuantos PS se levanta: entero si gano la mano, la mitad si empato.
    // 0 = no se levanta.
    public static int PsAlRevivir(PokenModel poken, BlackjackOutcome paraElDerrotado)
    {
        if (poken == null) return 0;

        switch (paraElDerrotado)
        {
            case BlackjackOutcome.Blackjack:
            case BlackjackOutcome.Gana:
                return poken.MaxHP;
            case BlackjackOutcome.Empata:
                return Mathf.Max(1, poken.MaxHP / 2);
            default:
                return 0;
        }
    }

    // Uno de los debilitados, al azar. 'tirada' va de 0 a 1 (Random.value) y se
    // pasa de afuera para que el test pueda elegir cual sale.
    // Null si no hay ninguno debilitado.
    public static PokenModel ElegirParaRevivir(List<PokenModel> equipo, float tirada)
    {
        if (equipo == null || equipo.Count == 0) return null;

        var caidos = new List<PokenModel>();
        foreach (PokenModel p in equipo)
            if (p != null && p.IsFainted) caidos.Add(p);

        if (caidos.Count == 0) return null;

        int i = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(tirada) * caidos.Count), 0, caidos.Count - 1);
        return caidos[i];
    }

    // Lo pone en pie. Ademas de los PS le saca el estado alterado: volver del
    // KO envenenado seria una revancha de un solo turno.
    public static void Revivir(PokenModel poken, int ps)
    {
        if (poken == null || ps <= 0) return;

        poken.SetCurrentHP(ps);
        poken.CureStatus();
    }
}
