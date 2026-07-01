using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "PokeRush/MoveDefinition")]
public class MoveDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string moveName;
    public PokenType type;
    public AttackType attackType;

    [Header("Stats")]
    public int power;
    public int accuracy;
    public int maxPP;
    [Tooltip("Prioridad del movimiento. >0 actua antes sin importar la velocidad (ej. Ataque Rapido = 1). Normal = 0.")]
    public int priority;

    [Header("Efecto")]
    public StatusEffect effect;
    [Range(0, 100)]
    public int effectChance;
    public string effectDescription;
}