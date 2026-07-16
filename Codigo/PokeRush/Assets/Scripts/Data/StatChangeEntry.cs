// Un cambio de estadistica que produce un movimiento (ej: Danza Espada = +2 Ataque propio).
// Un movimiento puede tener varios (ej: Ancient Power sube todo).
[System.Serializable]
public class StatChangeEntry
{
    public Stat stat;
    [UnityEngine.Tooltip("Stages que sube (positivo) o baja (negativo). Rango tipico: -2 a +2.")]
    public int stages;
    [UnityEngine.Tooltip("true = afecta al usuario del movimiento; false = afecta al rival.")]
    public bool targetSelf;
}
