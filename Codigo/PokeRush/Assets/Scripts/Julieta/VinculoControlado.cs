using System.Collections.Generic;
using UnityEngine;

// La vida propia del PJ: una barra aparte de la de sus Pokemon.
//
// Solo existe en la pelea contra Julieta, desde que ella se mete en un Pokemon
// del jugador y ese Pokemon empieza a atacarlo. Si llega a 0 el PJ se
// desploma y la pelea se pierde, aunque le queden Pokemon en pie. Vive lo que
// dura la pelea: no se guarda en la partida.
public class VidaDelPJ
{
    public int Maxima { get; private set; }
    public int Actual { get; private set; }

    public bool Caido => Actual <= 0;
    public int Porcentaje => Maxima > 0 ? Mathf.CeilToInt(100f * Actual / Maxima) : 0;

    public VidaDelPJ(int maxima) => Reiniciar(maxima);

    public void Reiniciar(int maxima)
    {
        Maxima = Mathf.Max(1, maxima);
        Actual = Maxima;
    }

    // Devuelve lo que saco de verdad (no baja de 0).
    public int Recibir(int dano)
    {
        int real = Mathf.Clamp(dano, 0, Actual);
        Actual -= real;
        return real;
    }

    public int Curar(int cura)
    {
        int real = Mathf.Clamp(cura, 0, Maxima - Actual);
        Actual += real;
        return real;
    }
}

public enum QueHaceElControlado
{
    Nada,         // se queda quieto mirando a Julieta
    AtacaAlPJ,    // se da vuelta y le pega a su propio entrenador
    AtacaAlRival  // pelea, pero por su cuenta: el movimiento lo elige el
}

public struct DecisionDelControlado
{
    public QueHaceElControlado Que;
    // Indice del movimiento (en los del Poken) que usa, -1 = Forcejeo o nada.
    public int Movimiento;
    public MoveDefinition Definicion;
    // Si le pega al PJ, cuanto.
    public int Dano;
}

// El Pokemon del jugador en el que se metio Julieta con el NeuraRush-LINK.
//
// Sigue en el equipo del jugador y se ve igual, pero ya no le obedece:
//   - En el campo, si el jugador le pide PELEAR, hace lo que quiere: a veces
//     se da vuelta y ataca al PJ, y si no, usa un movimiento suyo contra el
//     rival (el que elige el, no el que se le pidio).
//   - Si el jugador hace otra cosa (cambiarlo, usar un objeto) o si esta en el
//     banco, igual puede salir de la QuerryBola y pegarle al PJ, con menos
//     chance. Sacarlo del campo ayuda, pero no lo apaga.
//
// Clase pura: decide y calcula, no anima ni muestra nada. Lo que se ve lo pone
// PeleaJulieta.
public class VinculoControlado
{
    // Chance de que ataque al PJ cuando el jugador le pide pelear.
    public float ProbabilidadEnElCampo = 0.6f;
    // Chance de que salga a atacar al PJ cuando no se le pidio pelear (desde el
    // banco, o en el campo mientras el jugador cambia o usa un objeto).
    public float ProbabilidadFueraDeOrden = 0.35f;
    // El golpe al PJ, en fraccion de su vida maxima: base + potencia/1000.
    // Con 100 de vida: Placaje (40) saca ~22, Terremoto (100) ~28.
    public float FraccionBase = 0.18f;

    public PokenModel Poken { get; private set; }
    public bool Activo => Poken != null && !Poken.IsFainted;
    public int AtaquesAlPJ { get; private set; }

    public void Controlar(PokenModel poken) => Poken = poken;

    public void Liberar() => Poken = null;

    public void Reiniciar()
    {
        Poken = null;
        AtaquesAlPJ = 0;
    }

    public void ContarAtaque() => AtaquesAlPJ++;

    // En el campo, con una orden de pelear.
    public DecisionDelControlado DecidirEnElCampo(IBattleRng rng, int vidaMaximaDelPJ)
    {
        var d = new DecisionDelControlado { Que = QueHaceElControlado.Nada, Movimiento = -1 };
        if (!Activo) return d;

        int movimiento = ElegirMovimiento(Poken, rng);
        d.Movimiento = movimiento;
        d.Definicion = movimiento >= 0 ? Poken.Moves[movimiento].Definition : null;

        if (rng.Value() < ProbabilidadEnElCampo)
        {
            d.Que = QueHaceElControlado.AtacaAlPJ;
            d.Dano = DanoAlPJ(d.Definicion, vidaMaximaDelPJ, FraccionBase, rng);
            return d;
        }

        d.Que = QueHaceElControlado.AtacaAlRival;
        return d;
    }

    // Sin orden de pelear (banco, o el jugador hizo otra cosa).
    public DecisionDelControlado DecidirFueraDeOrden(IBattleRng rng, int vidaMaximaDelPJ)
    {
        var d = new DecisionDelControlado { Que = QueHaceElControlado.Nada, Movimiento = -1 };
        if (!Activo || rng.Value() >= ProbabilidadFueraDeOrden) return d;

        int movimiento = ElegirMovimiento(Poken, rng);
        d.Movimiento = movimiento;
        d.Definicion = movimiento >= 0 ? Poken.Moves[movimiento].Definition : null;
        d.Que = QueHaceElControlado.AtacaAlPJ;
        d.Dano = DanoAlPJ(d.Definicion, vidaMaximaDelPJ, FraccionBase, rng);
        return d;
    }

    // Un movimiento con PP, al azar, prefiriendo los que pegan. -1 = no le
    // queda ninguno (le toca Forcejeo).
    public static int ElegirMovimiento(PokenModel poken, IBattleRng rng)
    {
        if (poken == null || poken.Moves == null) return -1;

        var quePegan = new List<int>();
        var conPP = new List<int>();
        for (int i = 0; i < poken.Moves.Count; i++)
        {
            MoveModel m = poken.Moves[i];
            if (m == null || m.Definition == null || !m.HasPP) continue;
            conPP.Add(i);
            if (PredictionState.EsDeDano(m.Definition)) quePegan.Add(i);
        }

        List<int> lista = quePegan.Count > 0 ? quePegan : conPP;
        if (lista.Count == 0) return -1;
        return lista[rng.Range(0, lista.Count)];
    }

    public static int DanoAlPJ(MoveDefinition movimiento, int vidaMaxima, float fraccionBase, IBattleRng rng)
    {
        int potencia = movimiento != null ? Mathf.Clamp(movimiento.power, 0, 150) : 50;
        float fraccion = fraccionBase + potencia / 1000f;
        int dano = Mathf.RoundToInt(vidaMaxima * fraccion) + rng.Range(-2, 3);
        return Mathf.Max(1, dano);
    }

    // A quien agarra Julieta: al que esta en el campo si sigue en pie; si no,
    // al primero en pie del banco. -1 = no queda nadie.
    public static int ElegirVictima(BattleModel modelo)
    {
        if (modelo == null) return -1;
        List<PokenModel> equipo = modelo.Player.Team;
        if (BattleTeamRules.EsIndiceVivo(equipo, modelo.ActivePlayerIndex)) return modelo.ActivePlayerIndex;
        for (int i = 0; i < equipo.Count; i++)
            if (BattleTeamRules.EsIndiceVivo(equipo, i)) return i;
        return -1;
    }
}
