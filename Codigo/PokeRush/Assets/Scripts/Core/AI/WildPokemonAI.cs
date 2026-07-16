using System.Collections.Generic;

// IA de Poken salvaje: eleccion casi aleatoria con un leve sesgo hacia los
// movimientos efectivos (peso = 1 + efectividad). No cambia, no planifica,
// no evalua estados. Se siente como un salvaje de la saga.
public class WildPokemonAI : IBattleAI
{
    public BattleAction ChooseAction(AiContext context, IBattleRng rng)
    {
        List<MoveModel> moves = context.Self.Poken.Moves;

        // Junta los usables con su peso
        var indices = new List<int>();
        var weights = new List<float>();
        float totalWeight = 0f;

        for (int i = 0; i < moves.Count; i++)
        {
            if (!moves[i].HasPP) continue;
            MoveDefinition def = moves[i].Definition;
            if (def.implementation == MoveImplementation.NotImplemented) continue;

            // Sesgo suave: los movimientos de daño pesan segun su efectividad;
            // los de estado pesan 1 (los usa "porque si", como un salvaje)
            float weight = 1f;
            if (def.attackType != AttackType.Status && def.power > 0)
                weight = 1f + DamageCalculator.DualEffectiveness(def.type, context.Foe.Poken.Definition);

            indices.Add(i);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (indices.Count == 0) return BattleAction.Struggle();

        // Ruleta ponderada
        float roll = rng.Value() * totalWeight;
        for (int i = 0; i < indices.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f) return BattleAction.Move(indices[i]);
        }
        return BattleAction.Move(indices[indices.Count - 1]);
    }

    // Un salvaje es un solo Poken: no hay reemplazo posible
    public int ChooseReplacement(AiContext context)
    {
        for (int i = 0; i < context.OwnTeam.Count; i++)
            if (!context.OwnTeam[i].IsFainted) return i;
        return -1;
    }
}
