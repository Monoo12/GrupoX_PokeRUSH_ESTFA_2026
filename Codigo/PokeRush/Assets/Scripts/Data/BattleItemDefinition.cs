using UnityEngine;

[CreateAssetMenu(fileName = "NewBattleItem", menuName = "PokeRush/Items/BattleItemDefinition")]
public class BattleItemDefinition : ScriptableObject
//Esta clase define las características de un objeto de batalla, incluyendo su identidad, valores de compra/venta, modificadores de stats y duración en turnos.
{
    [Header("Identidad")]
    public string itemName;
    public string description;
    public Sprite icon;

    [Header("Valores")]
    public int buyPrice;
    public int sellPrice;

    [Header("Modificadores de stats")]
    [Range(0, 100)] public int attackBoostPercent;
    [Range(0, 100)] public int defenseBoostPercent;
    [Range(0, 100)] public int specialAttackBoostPercent;
    [Range(0, 100)] public int specialDefenseBoostPercent;
    [Range(0, 100)] public int speedBoostPercent;

    [Header("Duracion")]
    public int durationInTurns;
}