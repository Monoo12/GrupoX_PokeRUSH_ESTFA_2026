using System.Collections.Generic;

// IA de lider/elite: todo lo de TrainerAI mas capas estrategicas que se
// evaluan en orden de prioridad:
//   1. Remate: si un movimiento garantiza el KO, lo usa siempre.
//   2. Cambio estrategico: si esta en mal matchup y un suplente esta mejor.
//   3. Scoring normal (con setup y curacion pesados por el perfil).
// Ademas elige su reemplazo por matchup en vez del primero con vida.
public class GymLeaderAI : TrainerAI
{
    // Si su mejor movimiento puntua por debajo de esto, esta "en mala posicion"
    // y considera cambiar de Poken.
    private const float BadPositionScore = 0.4f;

    public override BattleAction ChooseAction(AiContext context, IBattleRng rng)
    {
        List<ScoredAction> scored = MoveScorer.ScoreMoves(context);
        if (scored.Count == 0) return BattleAction.Struggle();

        // --- 1. Remate: KO garantizado (daño esperado alcanza y no falla) ---
        ScoredAction? finisher = FindGuaranteedKo(context, scored);
        if (finisher.HasValue) return finisher.Value.Action;

        // --- 2. Cambio estrategico ---
        if (context.Profile.canSwitch)
        {
            float bestMoveScore = 0f;
            foreach (ScoredAction s in scored)
                if (s.Score > bestMoveScore) bestMoveScore = s.Score;

            if (bestMoveScore < BadPositionScore)
            {
                int candidate = BestBenchCandidate(context, out float candidateMatchup);
                if (candidate >= 0)
                {
                    float currentMatchup = MoveScorer.MatchupScore(context.Self.Poken, context.Foe);
                    if (candidateMatchup - currentMatchup > context.Profile.switchGainThreshold)
                        return BattleAction.Switch(candidate);
                }
            }
        }

        // --- 3. Scoring normal ---
        if (rng.Value() < context.Profile.randomness)
            return RandomUsable(scored, context, rng);

        ScoredAction best = ArgMax(scored, rng);
        if (best.Score <= 0f)
            return RandomUsable(scored, context, rng);
        return best.Action;
    }

    // Mejor suplente del banco (excluyendo al activo y los debilitados).
    // Devuelve -1 si no hay candidatos; el matchup del elegido sale por out.
    private static int BestBenchCandidate(AiContext context, out float bestMatchup)
    {
        int best = -1;
        bestMatchup = float.MinValue;
        for (int i = 0; i < context.OwnTeam.Count; i++)
        {
            if (i == context.OwnActiveIndex) continue;
            PokenModel candidate = context.OwnTeam[i];
            if (candidate.IsFainted) continue;

            float matchup = MoveScorer.MatchupScore(candidate, context.Foe);
            if (matchup > bestMatchup)
            {
                bestMatchup = matchup;
                best = i;
            }
        }
        return best;
    }

    // Reemplazo al caer: el suplente con mejor matchup contra el rival actual
    public override int ChooseReplacement(AiContext context)
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < context.OwnTeam.Count; i++)
        {
            PokenModel candidate = context.OwnTeam[i];
            if (candidate.IsFainted) continue;
            float score = MoveScorer.MatchupScore(candidate, context.Foe);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    // Movimiento que garantiza el KO: daño esperado >= HP del rival, sin fallar.
    // Entre varios, elige el de mayor probabilidad de acierto (y menor overkill).
    private static ScoredAction? FindGuaranteedKo(AiContext context, List<ScoredAction> scored)
    {
        ScoredAction? best = null;
        float bestHitProb = 0f;

        foreach (ScoredAction s in scored)
        {
            if (s.Action.Kind != BattleActionKind.UseMove || s.Action.MoveIndex < 0) continue;
            MoveDefinition move = context.Self.Poken.Moves[s.Action.MoveIndex].Definition;
            if (move.attackType == AttackType.Status || move.power <= 0) continue;
            if (MoveScorer.FoeAbilityBlocks(context.Foe, move.type)) continue;

            DamageResult expected = DamageCalculator.CalculateExpected(context.Self, context.Foe, move);
            if (expected.Effectiveness <= 0f || expected.Damage < context.Foe.Poken.CurrentHP) continue;

            float hitProb = MoveScorer.HitProbability(context, move);
            if (hitProb < 0.99f) continue; // "garantizado" = no puede fallar

            if (best == null || hitProb > bestHitProb)
            {
                best = s;
                bestHitProb = hitProb;
            }
        }
        return best;
    }
}
