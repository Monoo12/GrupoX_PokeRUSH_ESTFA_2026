using UnityEngine;

[CreateAssetMenu(fileName = "NewSpecialItem", menuName = "PokeRush/Items/SpecialItemDefinition")]
public class SpecialItemDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string itemName;
    public string description;
    public Sprite icon;

    [Header("Valores")]
    public int buyPrice;

    [Header("Efecto")]
    public string effectDescription;
    public StoryFlag triggersFlag;
}

