using UnityEngine;

[CreateAssetMenu(fileName = "NewVendor", menuName = "PokeRush/Characters/VendorDefinition")]
public class VendorDefinition : NPCDefinition
{
    [Header("Inventario en venta")]
    public HealItemDefinition[] healItemsForSale;
    public BattleItemDefinition[] battleItemsForSale;
}