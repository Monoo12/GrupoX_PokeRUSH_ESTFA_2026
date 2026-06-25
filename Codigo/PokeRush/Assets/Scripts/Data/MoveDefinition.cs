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

    [Header("Efecto")]
    public StatusEffect effect;
    [Range(0, 100)]
    public int effectChance;
    public string effectDescription;
}