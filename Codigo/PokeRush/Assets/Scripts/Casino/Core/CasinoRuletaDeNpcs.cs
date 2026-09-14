using System.Collections.Generic;

// Cuando un NPC del casino da la ruleta de premios al ganarle.
//
// La regla pedida: sale como MAXIMO dos veces por partida, al ganarle a dos NPCs
// que no sean siempre los mismos.
//
// Como se resuelve sin guardar "quienes son los elegidos": cada vez que se le
// gana a un NPC elegible, la ruleta sale con probabilidad k/m, donde k son las
// ruletas que quedan (2 - usadas) y m los NPCs elegibles que todavia no fueron
// vencidos, contando al de recien. Es el sorteo de "k entre m" hecho de a uno:
//   - si el jugador les gana a todos, salen EXACTAMENTE dos ruletas;
//   - cualquier par de NPCs tiene la misma chance de ser el que la dio, sin
//     importar en que orden los venza;
//   - si se saltea NPCs, salen menos, nunca mas de dos.
//
// Lo unico que se persiste son los flags de uso y los de NPC vencido, que
// SaveService ya guarda con el resto de los flags.
public static class CasinoRuletaDeNpcs
{
    private static readonly StoryFlag[] Usos =
    {
        StoryFlag.Casino_RuletaNpc_1,
        StoryFlag.Casino_RuletaNpc_2
    };

    public static int MaximoPorPartida => Usos.Length;

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
    // como pendiente.
    public static float Probabilidad(StoryModel story, IList<StoryFlag> elegibles, StoryFlag esteNpc)
    {
        if (story == null || elegibles == null || esteNpc == StoryFlag.None) return 0f;

        var distintos = new HashSet<StoryFlag>(elegibles);
        distintos.Remove(StoryFlag.None);
        if (!distintos.Contains(esteNpc)) return 0f;

        int quedan = MaximoPorPartida - Usadas(story);
        if (quedan <= 0) return 0f;

        int sinVencer = 1; // el de recien
        foreach (StoryFlag npc in distintos)
            if (npc != esteNpc && !story.HasFlag(npc)) sinVencer++;

        return quedan >= sinVencer ? 1f : quedan / (float)sinVencer;
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
