using System.Collections.Generic;

// Corazon compartido del scoring de la IA: asigna una puntuacion a cada accion
// posible segun los pesos del AIProfileDefinition. Estatico y puro.
public struct ScoredAction
{
    public BattleAction Action;
    public float Score;
}

public static class MoveScorer
{
    // Bonus chico de STAB: solo desempata entre movimientos parecidos
    private const float StabBonus = 0.05f;
    // Peso relativo de los debuffs al rival frente a los estados alterados
    private const float DebuffFactor = 0.6f;
    // Con mas de este % de HP propio, curarse no puntua
    private const float HealCutoffHpPct = 0.85f;

    // Puntua todos los movimientos usables del Poken activo de la IA.
    public static List<ScoredAction> ScoreMoves(AiContext ctx)
    {
        var result = new List<ScoredAction>();
        List<MoveModel> moves = ctx.Self.Poken.Moves;
        for (int i = 0; i < moves.Count; i++)
        {
            if (!moves[i].HasPP) continue;
            result.Add(new ScoredAction
            {
                Action = BattleAction.Move(i),
                Score = ScoreMove(ctx, moves[i].Definition)
            });
        }
        return result;
    }

    public static float ScoreMove(AiContext ctx, MoveDefinition move)
    {
        if (move.implementation == MoveImplementation.NotImplemented) return 0f;

        AIProfileDefinition profile = ctx.Profile;
        bool isDamaging = move.attackType != AttackType.Status && move.power > 0;

        if (isDamaging)
            return ScoreDamagingMove(ctx, move, profile);
        return ScoreStatusMove(ctx, move, profile);
    }

    private static float ScoreDamagingMove(AiContext ctx, MoveDefinition move, AIProfileDefinition profile)
    {
        // Sin efecto: inmunidad por tipo o por habilidad conocida del rival
        if (FoeAbilityBlocks(ctx.Foe, move.type)) return 0f;

        DamageResult expected = DamageCalculator.CalculateExpected(ctx.Self, ctx.Foe, move);
        if (expected.Effectiveness <= 0f) return 0f;

        float dmgPct = System.Math.Min((float)expected.Damage / ctx.Foe.Poken.MaxHP, 1f);
        float hitProb = HitProbability(ctx, move);

        float score = profile.damageWeight * dmgPct * hitProb;

        // Posibilidad de debilitar al rival este turno
        if (expected.Damage >= ctx.Foe.Poken.CurrentHP)
            score += profile.koWeight * hitProb;

        if (DamageCalculator.HasStab(ctx.Self.Poken.Definition, move.type))
            score += StabBonus;

        return score;
    }

    private static float ScoreStatusMove(AiContext ctx, MoveDefinition move, AIProfileDefinition profile)
    {
        float score = 0f;

        // --- aplicar estado alterado al rival ---
        if (move.effect != StatusEffect.None)
        {
            bool useless =
                (move.effect == StatusEffect.Confused && ctx.Foe.IsConfused) ||
                (move.effect != StatusEffect.Confused && ctx.Foe.Poken.CurrentStatus != StatusEffect.None) ||
                (move.effect != StatusEffect.Confused && StatusEffects.IsImmuneByType(move.effect, ctx.Foe.Poken.Definition)) ||
                DamageCalculator.DualEffectiveness(move.type, ctx.Foe.Poken.Definition) <= 0f;

            if (useless) return 0f;
            // Un estado vale mas cuanto mas HP le queda al rival (temprano en la pelea)
            score += profile.statusWeight * ctx.FoeHpPercent * HitProbability(ctx, move);
        }

        if (move.statChanges != null)
        {
            // --- setup: subir stats propias ---
            bool selfBoostWithRoom = false;
            bool foeDebuffWithRoom = false;
            foreach (StatChangeEntry entry in move.statChanges)
            {
                if (entry.targetSelf && entry.stages > 0
                    && ctx.Self.GetStage(entry.stat) < profile.maxSetupStages)
                    selfBoostWithRoom = true;
                if (!entry.targetSelf && entry.stages < 0
                    && ctx.Foe.GetStage(entry.stat) > StatStageTable.MinStage)
                    foeDebuffWithRoom = true;
            }

            if (selfBoostWithRoom && ctx.SelfHpPercent >= profile.minHpPctForSetup)
            {
                // Vale mas con HP alto: (hp - 0.5) * 2 mapea 50%->0 y 100%->1
                float hpFactor = (ctx.SelfHpPercent - 0.5f) * 2f;
                if (hpFactor > 0f) score += profile.setupWeight * hpFactor;
            }

            if (foeDebuffWithRoom)
                score += profile.statusWeight * DebuffFactor * ctx.FoeHpPercent * HitProbability(ctx, move);
        }

        // --- curacion ---
        if (move.healPercent > 0 && ctx.SelfHpPercent < HealCutoffHpPct)
            score += profile.healWeight * (1f - ctx.SelfHpPercent);

        return score;
    }

    // Probabilidad de acertar (0-1) con stages de precision/evasion
    public static float HitProbability(AiContext ctx, MoveDefinition move)
    {
        if (move.alwaysHits || move.accuracy <= 0) return 1f;
        float chance = move.accuracy * BattlePokenState.HitChanceMultiplier(ctx.Self, ctx.Foe) / 100f;
        return chance > 1f ? 1f : chance;
    }

    // La habilidad del rival anula este tipo (informacion que un buen
    // entrenador conoce de memoria: Levitación, Absorber Agua...)
    public static bool FoeAbilityBlocks(BattlePokenState foe, PokenType moveType)
    {
        switch (foe.Ability)
        {
            case AbilityId.Levitate: return moveType == PokenType.Ground;
            case AbilityId.VoltAbsorb: return moveType == PokenType.Electric;
            case AbilityId.WaterAbsorb: return moveType == PokenType.Water;
            case AbilityId.FlashFire: return moveType == PokenType.Fire;
            default: return false;
        }
    }

    // Matchup de un candidato (activo o del banco) contra el rival actual:
    // que tan fuerte le pego menos que tan fuerte me pega su STAB estimado.
    // Solo usa informacion legitima (tipos visibles del rival).
    public static float MatchupScore(PokenModel candidate, BattlePokenState foe)
    {
        // Mejor efectividad ofensiva de mis movimientos con PP
        float bestOffense = 0f;
        foreach (MoveModel move in candidate.Moves)
        {
            if (!move.HasPP) continue;
            MoveDefinition def = move.Definition;
            if (def.attackType == AttackType.Status || def.power <= 0) continue;
            if (def.implementation == MoveImplementation.NotImplemented) continue;

            float eff = DamageCalculator.DualEffectiveness(def.type, foe.Poken.Definition);
            if (DamageCalculator.HasStab(candidate.Definition, def.type)) eff *= 1.2f;
            if (eff > bestOffense) bestOffense = eff;
        }

        // Amenaza del rival: su mejor STAB contra este candidato
        PokenDefinition foeDef = foe.Poken.Definition;
        float foeThreat = DamageCalculator.DualEffectiveness(foeDef.type, candidate.Definition);
        if (foeDef.type2 != PokenType.None)
        {
            float second = DamageCalculator.DualEffectiveness(foeDef.type2, candidate.Definition);
            if (second > foeThreat) foeThreat = second;
        }

        return bestOffense - foeThreat;
    }
}
