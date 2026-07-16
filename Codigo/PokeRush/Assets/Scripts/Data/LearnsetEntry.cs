// Una entrada del learnset de una especie: que movimiento aprende y a que nivel.
// Mismo patron que EnemyTeamEntry: un objeto serializable evita arrays paralelos.
[System.Serializable]
public class LearnsetEntry
{
    public MoveDefinition move;
    public int level;   // nivel al que lo aprende (1 = lo conoce desde el inicio)
}
