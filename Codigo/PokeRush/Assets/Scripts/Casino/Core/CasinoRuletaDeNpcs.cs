using System.Collections.Generic;

// Cuando un NPC del casino da la ruleta de premios al ganarle.
//
// La regla pedida: sale UNA vez cada dos combates ganados, hasta cinco por
// partida (eran cuatro: en el playtest del 4-oct se llegaba al jefe final con
// muy pocos Pokemon), y no siempre con los mismos NPCs.
//
// Los combates contra NPCs de la lista se agrupan de a dos, en el orden en que
// el jugador los gana (1-2, 3-4, 5-6, 7-8). En cada par sale exactamente una:
//   - al primero del par le toca con probabilidad 1/2;
//   - si no le toco, al segundo le toca seguro.
// Es el sorteo de "k entre m" hecho de a uno, pero dentro de cada par. Asi:
//   - ganando 2, 4, 6, 8 o 10 combates salen 1, 2, 3, 4 o 5 ruletas, y despues no mas;
//   - aunque el jugador pelee siempre en el mismo orden, cual de los dos NPCs
//     del par la da cambia de partida en partida;
//   - peleando en cualquier orden, ninguno la da mas seguido que otro.
//
// Lo unico que se persiste son los flags de uso y los de NPC vencido, que
// SaveService ya guarda con el resto de los flags. Los combates ganados no se
// cuentan aparte: son los flags de vencido de la lista.
public static class CasinoRuletaDeNpcs
{
    private static readonly StoryFlag[] Usos =
    {
        StoryFlag.Casino_RuletaNpc_1,
        StoryFlag.Casino_RuletaNpc_2,
        StoryFlag.Casino_RuletaNpc_3,
        StoryFlag.Casino_RuletaNpc_4,
        StoryFlag.Casino_RuletaNpc_5
    };

    // Cada cuantos combates ganados sale una.
    public const int CombatesPorRuleta = 2;

    public static int MaximoPorPartida => Usos.Length;

    // Los flags que anotan cada uso (las pruebas los ponen y sacan a mano).
    public static IReadOnlyList<StoryFlag> FlagsDeUso => Usos;

    public static int Usadas(StoryModel story)
    {
        if (story == null) return 0;
        int n = 0;
        foreach (StoryFlag uso in Usos)
            if (story.HasFlag(uso)) n++;
        return n;
    }

    // Probabilidad de que ESTE NPC de la ruleta al ganarle ahora. Se puede
    // llamar antes o despues de marcarlo vencido: a el siempre se lo cuenta
    // como el combate que se acaba de ganar.
    public static float Probabilidad(StoryModel story, IList<StoryFlag> elegibles, StoryFlag esteNpc)
    {
        if (story == null || elegibles == null || esteNpc == StoryFlag.None) return 0f;

        var distintos = new HashSet<StoryFlag>(elegibles);
        distintos.Remove(StoryFlag.None);
        if (!distintos.Contains(esteNpc)) return 0f;

        // Que combate ganado es este: 1, 2, 3...
        int combate = 1;
        foreach (StoryFlag npc in distintos)
            if (npc != esteNpc && story.HasFlag(npc)) combate++;

        // Cuantas tendrian que haber salido al cerrar este par, y cuantos
        // combates le quedan al par contando este.
        int par = (combate - 1) / CombatesPorRuleta;
        int debidas = System.Math.Min(par + 1, MaximoPorPartida);
        int faltan = debidas - Usadas(story);
        if (faltan <= 0) return 0f;

        int quedanEnElPar = CombatesPorRuleta - (combate - 1) % CombatesPorRuleta;
        return faltan >= quedanEnElPar ? 1f : faltan / (float)quedanEnElPar;
    }

    // 'tirada' en [0, 1). Si sale la ruleta, anota el uso y devuelve true.
    public static bool Sortear(StoryModel story, IList<StoryFlag> elegibles, StoryFlag esteNpc, float tirada)
    {
        float p = Probabilidad(story, elegibles, esteNpc);
        if (p <= 0f || tirada >= p) return false;

        foreach (StoryFlag uso in Usos)
        {
            if (story.HasFlag(uso)) continue;
            story.ActivateFlag(uso);
            break;
        }
        return true;
    }
}
