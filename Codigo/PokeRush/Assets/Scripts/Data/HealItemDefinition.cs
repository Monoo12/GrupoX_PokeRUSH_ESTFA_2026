using UnityEngine;
//Esta clase define las características de un objeto de curación, incluyendo su identidad, valores de compra/venta y efectos de curación.
[CreateAssetMenu(fileName = "NewHealItem", menuName = "PokeRush/Items/HealItemDefinition")]
public class HealItemDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string itemName;
    public string description;
    public Sprite icon;

    [Header("Valores")]
    public int buyPrice;
    public int sellPrice;

    [Header("Curacion")]
    public int healAmount;
    public bool fullHeal;
}