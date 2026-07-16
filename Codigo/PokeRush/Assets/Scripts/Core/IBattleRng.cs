// Fuente de azar inyectable para el combate y la IA.
// El juego usa UnityRng; los tests usan SeededRng para resultados reproducibles.
public interface IBattleRng
{
    // Float en [0, 1)
    float Value();

    // Entero en [minInclusive, maxExclusive)
    int Range(int minInclusive, int maxExclusive);

    // Float en [minInclusive, maxInclusive]
    float Range(float minInclusive, float maxInclusive);
}

// Implementacion para el juego: delega en UnityEngine.Random.
public class UnityRng : IBattleRng
{
    public float Value() => UnityEngine.Random.value;
    public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    public float Range(float minInclusive, float maxInclusive) => UnityEngine.Random.Range(minInclusive, maxInclusive);
}

// Implementacion para tests: System.Random con semilla fija -> mismo resultado siempre.
public class SeededRng : IBattleRng
{
    private readonly System.Random _random;

    public SeededRng(int seed)
    {
        _random = new System.Random(seed);
    }

    public float Value() => (float)_random.NextDouble();
    public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    public float Range(float minInclusive, float maxInclusive)
        => minInclusive + (float)_random.NextDouble() * (maxInclusive - minInclusive);
}
