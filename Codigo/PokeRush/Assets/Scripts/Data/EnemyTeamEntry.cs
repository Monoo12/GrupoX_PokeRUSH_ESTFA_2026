// Una entrada del equipo de un enemigo o boss: que especie usa y a que nivel.
// Pega especie + nivel en un solo objeto serializable para que no se puedan
// desincronizar (a diferencia de dos arrays paralelos).
[System.Serializable]
public class EnemyTeamEntry
{
    public PokenDefinition species;   // referencia a la especie (asset compartido)
    public int level;                 // nivel propio de este enemigo
}
