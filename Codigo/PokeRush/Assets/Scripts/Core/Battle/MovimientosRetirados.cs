using System.Collections.Generic;

// Movimientos que el juego NO usa aunque esten importados y aparezcan en los
// learnsets de las especies.
//
// Excavar (91) se saco en el playtest del 5-oct ("esta muy bug, sacalo"). No se
// borra el asset ni se tocan los 445 learnsets: se filtra en todos lados donde
// un Pokemon puede conseguir un movimiento (los cuatro iniciales, subir de
// nivel, el respaldo, el tutor de MTs) y al cargar una partida que ya lo tenia
// (PokenModel.QuitarMovimientosRetirados). Volver a habilitarlo es sacarlo de
// esta lista.
//
// Por pokeApiId y no por nombre: el nombre es texto que alguien puede
// retocar desde el Inspector.
public static class MovimientosRetirados
{
    private static readonly HashSet<int> PorId = new HashSet<int>
    {
        91,   // Excavar
    };

    public static bool EstaRetirado(MoveDefinition move)
        => move != null && PorId.Contains(move.pokeApiId);
}
