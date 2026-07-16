using System.Collections.Generic;

// IA de entrenador comun: puntua todos sus movimientos (daño esperado,
// efectividad, posibilidad de KO, estados utiles) y elige el mejor, con una
// probabilidad configurable de "torpeza" (eleccion aleatoria). No cambia de
// Poken voluntariamente: eso llega con GymLeaderAI.
public class TrainerAI : IBattleAI
{
    public virtual BattleAction ChooseAction(AiContext context, IBattleRng rng)
    {
        List<ScoredAction> scored = MoveScorer.ScoreMoves(context);
        if (scored.Count == 0) return BattleAction.Struggle();

        // Torpeza configurable: a veces elige al azar (entre opciones validas)
        if (rng.Value() < context.Profile.randomness)
            return RandomUsable(scored, context, rng);

        // Mejor puntuacion; empates al azar para no ser predecible
        ScoredAction best = ArgMax(scored, rng);
        if (best.Score <= 0f)
            return RandomUsable(scored, context, rng); // nada puntua: pega algo

        return best.Action;
    }

    // Reemplazo al caer: el primero con vida (los entrenadores comunes no eligen)
    public virtual int ChooseReplacement(AiContext context)
    {
        for (int i = 0; i < context.OwnTeam.Count; i++)
            if (!context.OwnTeam[i].IsFainted) return i;
        return -1;
    }

    // Eleccion aleatoria respetando avoidsIneffective: si el perfil lo pide,
    // nunca elige movimientos sin efecto (score 0).
    protected static BattleAction RandomUsable(List<ScoredAction> scored, AiContext context, IBattleRng rng)
    {
        List<ScoredAction> pool = scored;
        if (context.Profile.avoidsIneffective)
        {
            var effective = scored.FindAll(s => s.Score > 0f);
            if (effective.Count > 0) pool = effective;
        }
        return pool[rng.Range(0, pool.Count)].Action;
    }

    protected static ScoredAction ArgMax(List<ScoredAction> scored, IBattleRng rng)
    {
        ScoredAction best = scored[0];
        int ties = 1;
        for (int i = 1; i < scored.Count; i++)
        {
            if (scored[i].Score > best.Score)
            {
                best = scored[i];
                ties = 1;
            }
            else if (scored[i].Score == best.Score)
            {
                // Reservoir sampling entre empatados
                ties++;
                if (rng.Value() < 1f / ties) best = scored[i];
            }
        }
        return best;
    }
}
