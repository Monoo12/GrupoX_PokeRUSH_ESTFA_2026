using System.Collections.Generic;

// Recompensas de combate: XP por derrotar a un Poken y participantes elegibles.
// Estatica y pura.
public static class RewardCalculator
{
    // Formula clasica de la saga: XP = baseXpYield * nivel / 7
    public const int XpDivisor = 7;

    public static int XpForDefeating(PokenModel fainted)
    {
        int xp = fainted.Definition.baseXpYield * fainted.Level / XpDivisor;
        return xp < 1 ? 1 : xp;
    }

    // Participantes que siguen en pie: solo ellos reciben XP
    public static List<PokenModel> EligibleParticipants(HashSet<PokenModel> participants)
    {
        var eligible = new List<PokenModel>();
        foreach (PokenModel poken in participants)
            if (poken != null && !poken.IsFainted)
                eligible.Add(poken);
        return eligible;
    }
}
