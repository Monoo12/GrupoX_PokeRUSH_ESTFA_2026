using System.Collections.Generic;

public class InventoryModel
{
    public List<HealItemStack> HealItems { get; private set; }
    public List<BattleItemStack> BattleItems { get; private set; }
    public List<SpecialItemStack> SpecialItems { get; private set; }
    public List<CosmeticDefinition> Cosmetics { get; private set; }

    public InventoryModel()
    {
        HealItems = new List<HealItemStack>();
        BattleItems = new List<BattleItemStack>();
        SpecialItems = new List<SpecialItemStack>();
        Cosmetics = new List<CosmeticDefinition>();
    }
}

public class HealItemStack
{
    public HealItemDefinition Definition { get; private set; }
    public int Quantity { get; private set; }

    public HealItemStack(HealItemDefinition definition, int quantity)
    {
        Definition = definition;
        Quantity = quantity;
    }
}

public class BattleItemStack
{
    public BattleItemDefinition Definition { get; private set; }
    public int Quantity { get; private set; }

    public BattleItemStack(BattleItemDefinition definition, int quantity)
    {
        Definition = definition;
        Quantity = quantity;
    }
}

public class SpecialItemStack
{
    public SpecialItemDefinition Definition { get; private set; }
    public int Quantity { get; private set; }

    public SpecialItemStack(SpecialItemDefinition definition, int quantity)
    {
        Definition = definition;
        Quantity = quantity;
    }
}