// Tabla de efectividad de tipos (estilo Pokemon, generacion 6+).
// Logica pura, sin dependencias de Unity. Atacante (fila) vs defensor (columna).
// Multiplicadores: 0 (inmune), 0.5 (poco eficaz), 1 (normal), 2 (super eficaz).
public static class TypeChart
{
    private static readonly float[,] _chart;

    static TypeChart()
    {
        int n = System.Enum.GetValues(typeof(PokenType)).Length;
        _chart = new float[n, n];
        for (int a = 0; a < n; a++)
            for (int d = 0; d < n; d++)
                _chart[a, d] = 1f;

        Set(PokenType.Normal, PokenType.Rock, 0.5f);
        Set(PokenType.Normal, PokenType.Ghost, 0f);
        Set(PokenType.Normal, PokenType.Steel, 0.5f);

        Set(PokenType.Fire, PokenType.Fire, 0.5f);
        Set(PokenType.Fire, PokenType.Water, 0.5f);
        Set(PokenType.Fire, PokenType.Grass, 2f);
        Set(PokenType.Fire, PokenType.Ice, 2f);
        Set(PokenType.Fire, PokenType.Bug, 2f);
        Set(PokenType.Fire, PokenType.Rock, 0.5f);
        Set(PokenType.Fire, PokenType.Dragon, 0.5f);
        Set(PokenType.Fire, PokenType.Steel, 2f);

        Set(PokenType.Water, PokenType.Fire, 2f);
        Set(PokenType.Water, PokenType.Water, 0.5f);
        Set(PokenType.Water, PokenType.Grass, 0.5f);
        Set(PokenType.Water, PokenType.Ground, 2f);
        Set(PokenType.Water, PokenType.Rock, 2f);
        Set(PokenType.Water, PokenType.Dragon, 0.5f);

        Set(PokenType.Grass, PokenType.Fire, 0.5f);
        Set(PokenType.Grass, PokenType.Water, 2f);
        Set(PokenType.Grass, PokenType.Grass, 0.5f);
        Set(PokenType.Grass, PokenType.Poison, 0.5f);
        Set(PokenType.Grass, PokenType.Ground, 2f);
        Set(PokenType.Grass, PokenType.Flying, 0.5f);
        Set(PokenType.Grass, PokenType.Bug, 0.5f);
        Set(PokenType.Grass, PokenType.Rock, 2f);
        Set(PokenType.Grass, PokenType.Dragon, 0.5f);
        Set(PokenType.Grass, PokenType.Steel, 0.5f);

        Set(PokenType.Electric, PokenType.Water, 2f);
        Set(PokenType.Electric, PokenType.Grass, 0.5f);
        Set(PokenType.Electric, PokenType.Electric, 0.5f);
        Set(PokenType.Electric, PokenType.Ground, 0f);
        Set(PokenType.Electric, PokenType.Flying, 2f);
        Set(PokenType.Electric, PokenType.Dragon, 0.5f);

        Set(PokenType.Ice, PokenType.Fire, 0.5f);
        Set(PokenType.Ice, PokenType.Water, 0.5f);
        Set(PokenType.Ice, PokenType.Grass, 2f);
        Set(PokenType.Ice, PokenType.Ice, 0.5f);
        Set(PokenType.Ice, PokenType.Ground, 2f);
        Set(PokenType.Ice, PokenType.Flying, 2f);
        Set(PokenType.Ice, PokenType.Dragon, 2f);
        Set(PokenType.Ice, PokenType.Steel, 0.5f);

        Set(PokenType.Fighting, PokenType.Normal, 2f);
        Set(PokenType.Fighting, PokenType.Ice, 2f);
        Set(PokenType.Fighting, PokenType.Poison, 0.5f);
        Set(PokenType.Fighting, PokenType.Flying, 0.5f);
        Set(PokenType.Fighting, PokenType.Psychic, 0.5f);
        Set(PokenType.Fighting, PokenType.Bug, 0.5f);
        Set(PokenType.Fighting, PokenType.Rock, 2f);
        Set(PokenType.Fighting, PokenType.Ghost, 0f);
        Set(PokenType.Fighting, PokenType.Dark, 2f);
        Set(PokenType.Fighting, PokenType.Steel, 2f);
        Set(PokenType.Fighting, PokenType.Fairy, 0.5f);

        Set(PokenType.Poison, PokenType.Grass, 2f);
        Set(PokenType.Poison, PokenType.Poison, 0.5f);
        Set(PokenType.Poison, PokenType.Ground, 0.5f);
        Set(PokenType.Poison, PokenType.Rock, 0.5f);
        Set(PokenType.Poison, PokenType.Ghost, 0.5f);
        Set(PokenType.Poison, PokenType.Steel, 0f);
        Set(PokenType.Poison, PokenType.Fairy, 2f);

        Set(PokenType.Ground, PokenType.Fire, 2f);
        Set(PokenType.Ground, PokenType.Grass, 0.5f);
        Set(PokenType.Ground, PokenType.Electric, 2f);
        Set(PokenType.Ground, PokenType.Poison, 2f);
        Set(PokenType.Ground, PokenType.Flying, 0f);
        Set(PokenType.Ground, PokenType.Bug, 0.5f);
        Set(PokenType.Ground, PokenType.Rock, 2f);
        Set(PokenType.Ground, PokenType.Steel, 2f);

        Set(PokenType.Flying, PokenType.Grass, 2f);
        Set(PokenType.Flying, PokenType.Electric, 0.5f);
        Set(PokenType.Flying, PokenType.Fighting, 2f);
        Set(PokenType.Flying, PokenType.Bug, 2f);
        Set(PokenType.Flying, PokenType.Rock, 0.5f);
        Set(PokenType.Flying, PokenType.Steel, 0.5f);

        Set(PokenType.Psychic, PokenType.Fighting, 2f);
        Set(PokenType.Psychic, PokenType.Poison, 2f);
        Set(PokenType.Psychic, PokenType.Psychic, 0.5f);
        Set(PokenType.Psychic, PokenType.Dark, 0f);
        Set(PokenType.Psychic, PokenType.Steel, 0.5f);

        Set(PokenType.Bug, PokenType.Fire, 0.5f);
        Set(PokenType.Bug, PokenType.Grass, 2f);
        Set(PokenType.Bug, PokenType.Fighting, 0.5f);
        Set(PokenType.Bug, PokenType.Poison, 0.5f);
        Set(PokenType.Bug, PokenType.Flying, 0.5f);
        Set(PokenType.Bug, PokenType.Psychic, 2f);
        Set(PokenType.Bug, PokenType.Ghost, 0.5f);
        Set(PokenType.Bug, PokenType.Dark, 2f);
        Set(PokenType.Bug, PokenType.Steel, 0.5f);
        Set(PokenType.Bug, PokenType.Fairy, 0.5f);

        Set(PokenType.Rock, PokenType.Fire, 2f);
        Set(PokenType.Rock, PokenType.Ice, 2f);
        Set(PokenType.Rock, PokenType.Fighting, 0.5f);
        Set(PokenType.Rock, PokenType.Ground, 0.5f);
        Set(PokenType.Rock, PokenType.Flying, 2f);
        Set(PokenType.Rock, PokenType.Bug, 2f);
        Set(PokenType.Rock, PokenType.Steel, 0.5f);

        Set(PokenType.Ghost, PokenType.Normal, 0f);
        Set(PokenType.Ghost, PokenType.Psychic, 2f);
        Set(PokenType.Ghost, PokenType.Ghost, 2f);
        Set(PokenType.Ghost, PokenType.Dark, 0.5f);

        Set(PokenType.Dragon, PokenType.Dragon, 2f);
        Set(PokenType.Dragon, PokenType.Steel, 0.5f);
        Set(PokenType.Dragon, PokenType.Fairy, 0f);

        Set(PokenType.Dark, PokenType.Fighting, 0.5f);
        Set(PokenType.Dark, PokenType.Psychic, 2f);
        Set(PokenType.Dark, PokenType.Ghost, 2f);
        Set(PokenType.Dark, PokenType.Dark, 0.5f);
        Set(PokenType.Dark, PokenType.Fairy, 0.5f);

        Set(PokenType.Steel, PokenType.Fire, 0.5f);
        Set(PokenType.Steel, PokenType.Water, 0.5f);
        Set(PokenType.Steel, PokenType.Electric, 0.5f);
        Set(PokenType.Steel, PokenType.Ice, 2f);
        Set(PokenType.Steel, PokenType.Rock, 2f);
        Set(PokenType.Steel, PokenType.Steel, 0.5f);
        Set(PokenType.Steel, PokenType.Fairy, 2f);

        Set(PokenType.Fairy, PokenType.Fire, 0.5f);
        Set(PokenType.Fairy, PokenType.Fighting, 2f);
        Set(PokenType.Fairy, PokenType.Poison, 0.5f);
        Set(PokenType.Fairy, PokenType.Dragon, 2f);
        Set(PokenType.Fairy, PokenType.Dark, 2f);
        Set(PokenType.Fairy, PokenType.Steel, 0.5f);
    }

    private static void Set(PokenType attacker, PokenType defender, float multiplier)
        => _chart[(int)attacker, (int)defender] = multiplier;

    // Multiplicador del tipo de ataque contra UN tipo defensor.
    public static float Effectiveness(PokenType attacker, PokenType defender)
        => _chart[(int)attacker, (int)defender];
}
