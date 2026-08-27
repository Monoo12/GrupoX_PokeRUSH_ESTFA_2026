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

    // Suma un item de curacion, apilando sobre el stack existente si ya lo tiene
    public void AddHealItem(HealItemDefinition definition, int quantity)
    {
        if (definition == null || quantity <= 0) return;
        HealItemStack stack = HealItems.Find(s => s.Definition == definition);
        if (stack != null) stack.AddQuantity(quantity);
        else HealItems.Add(new HealItemStack(definition, quantity));
    }

    // Gasta objetos de curacion. Devuelve false si no habia suficientes, asi el
    // que llama puede abortar sin haber consumido nada.
    public bool ConsumeHealItem(HealItemDefinition definition, int quantity = 1)
    {
        if (definition == null || quantity <= 0) return false;

        HealItemStack stack = HealItems.Find(s => s.Definition == definition);
        if (stack == null || stack.Quantity < quantity) return false;

        stack.Consume(quantity);
        // El stack vacio se saca de la lista para que la mochila no muestre 0
        if (stack.Quantity <= 0) HealItems.Remove(stack);
        return true;
    }

    // Cuantos hay de un objeto (0 si no esta)
    public int CountHealItem(HealItemDefinition definition)
    {
        HealItemStack stack = HealItems.Find(s => s.Definition == definition);
        return stack?.Quantity ?? 0;
    }

    // ---------- objetos de batalla ----------
    // Mismo patron que los de curacion. Existian como lista pero sin forma de
    // sumarlos ni gastarlos: la mochila los mostraba y no se podian tocar.

    public void AddBattleItem(BattleItemDefinition definition, int quantity)
    {
        if (definition == null || quantity <= 0) return;
        BattleItemStack stack = BattleItems.Find(s => s.Definition == definition);
        if (stack != null) stack.AddQuantity(quantity);
        else BattleItems.Add(new BattleItemStack(definition, quantity));
    }

    public bool ConsumeBattleItem(BattleItemDefinition definition, int quantity = 1)
    {
        if (definition == null || quantity <= 0) return false;
        BattleItemStack stack = BattleItems.Find(s => s.Definition == definition);
        if (stack == null || stack.Quantity < quantity) return false;

        stack.Consume(quantity);
        if (stack.Quantity <= 0) BattleItems.Remove(stack);
        return true;
    }

    public int CountBattleItem(BattleItemDefinition definition)
        => BattleItems.Find(s => s.Definition == definition)?.Quantity ?? 0;

    // ---------- objetos clave ----------
    // No se gastan al usarse (son llaves, tickets): solo se suman.

    public void AddSpecialItem(SpecialItemDefinition definition, int quantity = 1)
    {
        if (definition == null || quantity <= 0) return;
        SpecialItemStack stack = SpecialItems.Find(s => s.Definition == definition);
        if (stack != null) stack.AddQuantity(quantity);
        else SpecialItems.Add(new SpecialItemStack(definition, quantity));
    }

    public bool HasSpecialItem(SpecialItemDefinition definition)
        => definition != null && SpecialItems.Exists(s => s.Definition == definition);

    // Cuantos objetos hay en total (para saber si la mochila esta vacia).
    public int TotalItemCount => HealItems.Count + BattleItems.Count + SpecialItems.Count;
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

    public void AddQuantity(int amount)
    {
        Quantity += amount;
    }

    public void Consume(int amount)
    {
        Quantity = System.Math.Max(0, Quantity - amount);
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

    public void AddQuantity(int amount)
    {
        Quantity += amount;
    }

    public void Consume(int amount)
    {
        Quantity = System.Math.Max(0, Quantity - amount);
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

    public void AddQuantity(int amount)
    {
        Quantity += amount;
    }
}