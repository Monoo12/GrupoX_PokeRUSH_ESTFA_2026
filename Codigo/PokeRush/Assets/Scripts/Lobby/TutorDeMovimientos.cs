using System.Collections.Generic;
using UnityEngine;

// Cuanto cobra Nestor por enseñar un movimiento. Va por escalones de potencia:
// una MT de estado o un golpe flojo sale barato, un ataque de 110 o mas sale
// caro. Se toca desde el Inspector de Nestor, sin recompilar.
[System.Serializable]
public class PreciosDelTutor
{
    [Tooltip("Movimientos de estado (sin potencia).")]
    [Min(0)] public int estado = 800;
    [Tooltip("Potencia menor que 'desdeMedio'.")]
    [Min(0)] public int debil = 600;
    [Min(0)] public int medio = 1000;
    [Min(0)] public int fuerte = 1500;
    [Min(0)] public int muyFuerte = 2000;

    [Header("Escalones de potencia")]
    [Min(1)] public int desdeMedio = 60;
    [Min(1)] public int desdeFuerte = 90;
    [Min(1)] public int desdeMuyFuerte = 110;
}

// Las reglas del tutor de MTs, sin pantalla. Las usan la pantalla del tutor,
// Nestor y los smoke tests.
//
// Que puede aprender cada especie sale de 'PokenDefinition.mtMoves': las MT y
// los tutores de los juegos originales (PokeAPI), filtrados a los movimientos
// que existen en el proyecto. Lo llena 'PokeRush > Datos > Completar MTs y
// descripciones de movimientos'.
public static class TutorDeMovimientos
{
    public const int MovimientosMaximos = 4;

    // Lo que le puede enseñar a ESE Pokemon: sus MT, sin las que ya sabe ni las
    // que el combate no implementa. En el orden del asset (alfabetico).
    public static List<MoveDefinition> Ensenables(PokenModel poken)
    {
        var lista = new List<MoveDefinition>();
        if (poken == null || poken.Definition == null || poken.Definition.mtMoves == null) return lista;

        foreach (MoveDefinition move in poken.Definition.mtMoves)
        {
            if (move == null || move.implementation == MoveImplementation.NotImplemented) continue;
            if (MovimientosRetirados.EstaRetirado(move)) continue;   // Excavar (5-oct)
            if (poken.KnowsMove(move) || lista.Contains(move)) continue;
            lista.Add(move);
        }
        return lista;
    }

    public static int Precio(MoveDefinition move, PreciosDelTutor precios)
    {
        if (move == null) return 0;
        precios ??= new PreciosDelTutor();

        if (move.attackType == AttackType.Status || move.power <= 0) return precios.estado;
        if (move.power >= precios.desdeMuyFuerte) return precios.muyFuerte;
        if (move.power >= precios.desdeFuerte) return precios.fuerte;
        if (move.power >= precios.desdeMedio) return precios.medio;
        return precios.debil;
    }

    // Por que no se puede enseñar (null = se puede). 'reemplazar' = el indice
    // del movimiento que olvida, o -1 si tiene lugar libre.
    public static string MotivoParaNoEnsenar(PlayerModel jugador, PokenModel poken, MoveDefinition move,
                                             int reemplazar, int precio)
    {
        if (jugador == null || poken == null || move == null) return "No hay nada que enseñar.";
        if (move.implementation == MoveImplementation.NotImplemented) return "Ese movimiento no se puede usar.";
        if (poken.KnowsMove(move)) return $"{poken.Definition.pokenName} ya sabe {move.moveName}.";
        if (jugador.Money < precio) return $"Son {precio} monedas y tenés {jugador.Money}.";

        if (reemplazar < 0)
        {
            if (poken.Moves.Count >= MovimientosMaximos)
                return $"{poken.Definition.pokenName} ya sabe {MovimientosMaximos} movimientos: tiene que olvidar uno.";
        }
        else if (reemplazar >= poken.Moves.Count)
        {
            return "Ese lugar no tiene ningún movimiento.";
        }
        return null;
    }

    // Cobra y enseña. Con 'reemplazar' = -1 lo suma en un lugar libre; si no,
    // reemplaza ese movimiento (con los PP llenos). Devuelve false sin tocar
    // nada si no se puede.
    public static bool Ensenar(PlayerModel jugador, PokenModel poken, MoveDefinition move,
                               int reemplazar, int precio)
    {
        if (MotivoParaNoEnsenar(jugador, poken, move, reemplazar, precio) != null) return false;

        bool ok = reemplazar < 0 ? poken.TryLearnMove(move) : poken.ReplaceMove(reemplazar, move);
        if (!ok) return false;

        jugador.SpendMoney(precio);
        return true;
    }
}
