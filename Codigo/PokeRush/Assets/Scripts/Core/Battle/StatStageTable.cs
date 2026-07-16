// Multiplicadores oficiales de la saga para los stages de -6 a +6.
// Stats: 2/8, 2/7, ... 2/2, ... 8/2. Precision/evasion: 3/9, ... 3/3, ... 9/3.
public static class StatStageTable
{
    public const int MinStage = -6;
    public const int MaxStage = 6;

    public static int Clamp(int stage)
    {
        if (stage < MinStage) return MinStage;
        if (stage > MaxStage) return MaxStage;
        return stage;
    }

    // Multiplicador para Attack/Defense/SpecialAttack/SpecialDefense/Speed.
    public static float Multiplier(int stage)
    {
        stage = Clamp(stage);
        return stage >= 0 ? (2f + stage) / 2f : 2f / (2f - stage);
    }

    // Multiplicador para Accuracy/Evasion (usa base 3 en vez de 2).
    public static float AccuracyMultiplier(int stage)
    {
        stage = Clamp(stage);
        return stage >= 0 ? (3f + stage) / 3f : 3f / (3f - stage);
    }
}
