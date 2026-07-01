using UnityEngine;

// Resultado de calcular el daño de un movimiento contra un Poken.
public struct DamageResult
{
    public int Damage;
    public float Effectiveness; // 0, 0.25, 0.5, 1, 2, 4...
    public bool IsCritical;
}

// Calculo de daño estilo Pokemon. Logica pura: solo numeros, no toca la escena.
public static class DamageCalculator
{
    private const float CritChance = 1f / 16f;
    private const float CritMultiplier = 1.5f;
    private const float StabMultiplier = 1.5f;

    public static DamageResult Calculate(PokenModel attacker, PokenModel defender, MoveDefinition move)
    {
        var result = new DamageResult { Damage = 0, Effectiveness = 1f, IsCritical = false };

        // Movimientos de estado o sin potencia no hacen daño directo.
        if (move.attackType == AttackType.Status || move.power <= 0)
            return result;

        // Fisico usa Ataque/Defensa; especial usa Atq.Esp/Def.Esp.
        int atkStat = move.attackType == AttackType.Physical ? attacker.Attack : attacker.SpecialAttack;
        int defStat = move.attackType == AttackType.Physical ? defender.Defense : defender.SpecialDefense;
        if (defStat <= 0) defStat = 1;

        // Daño base (formula principal de la saga).
        float baseDamage = ((2f * attacker.Level / 5f + 2f) * move.power * atkStat / defStat) / 50f + 2f;

        // STAB: bonus si el tipo del movimiento coincide con algun tipo del atacante.
        PokenType atkType2 = attacker.Definition.type2;
        bool stab = move.type == attacker.Definition.type
                    || (atkType2 != PokenType.None && move.type == atkType2);
        float stabMult = stab ? StabMultiplier : 1f;

        // Efectividad por tipo del defensor. Si tiene un segundo tipo real
        // (distinto de None y del primero), se multiplican ambas efectividades.
        float eff = TypeChart.Effectiveness(move.type, defender.Definition.type);
        PokenType defType2 = defender.Definition.type2;
        if (defType2 != PokenType.None && defType2 != defender.Definition.type)
            eff *= TypeChart.Effectiveness(move.type, defType2);

        bool crit = Random.value < CritChance;
        float critMult = crit ? CritMultiplier : 1f;
        float randomMult = Random.Range(0.85f, 1f);

        float total = baseDamage * stabMult * eff * critMult * randomMult;

        result.Effectiveness = eff;
        result.IsCritical = crit;
        // Si no es inmune (eff > 0), siempre hace al menos 1 de daño.
        result.Damage = eff <= 0f ? 0 : Mathf.Max(1, Mathf.FloorToInt(total));
        return result;
    }
}
