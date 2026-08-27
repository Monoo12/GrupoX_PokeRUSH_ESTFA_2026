using System.Collections.Generic;
using UnityEngine;

// Una linea de la mochila, ya normalizada.
//
// Existe porque el inventario guarda TRES listas de tipos distintos
// (HealItemStack / BattleItemStack / SpecialItemStack) y la UI necesita
// recorrerlas como si fueran una sola. La alternativa era llenar la pantalla de
// 'if (es de curacion) ... else if (es de batalla)', que es exactamente el tipo
// de acoplamiento que hay que evitar.
public class BagEntry
{
    public ItemPocket Pocket;
    public string Name;
    public string Description;
    public Sprite Icon;
    public int Quantity;
    public bool ShowQuantity = true;

    // Solo uno de los tres esta lleno; cual, lo dice Pocket.
    public HealItemDefinition Heal;
    public BattleItemDefinition Battle;
    public SpecialItemDefinition Special;

    public bool IsHeal => Heal != null;

    // Un objeto de curacion hay que aplicarlo sobre alguien: al elegirlo se abre
    // la party. Los de batalla y los clave no.
    public bool NeedsTarget => IsHeal;
}

// Arma las listas de la mochila a partir del inventario real del jugador.
// No cachea nada: la mochila se redibuja desde el modelo cada vez que se abre o
// se usa algo, asi nunca puede quedar mostrando cantidades viejas.
public static class BagModel
{
    public static readonly ItemPocket[] Pockets =
    {
        ItemPocket.Medicine, ItemPocket.Battle, ItemPocket.Key
    };

    public static string PocketName(ItemPocket pocket)
    {
        switch (pocket)
        {
            case ItemPocket.Medicine: return "Medicinas";
            case ItemPocket.Battle: return "Obj. Combate";
            case ItemPocket.Key: return "Obj. Claves";
            default: return pocket.ToString();
        }
    }

    public static List<BagEntry> Build(InventoryModel inventory, ItemPocket pocket)
    {
        var entries = new List<BagEntry>();
        if (inventory == null) return entries;

        switch (pocket)
        {
            case ItemPocket.Medicine:
                foreach (HealItemStack stack in inventory.HealItems)
                {
                    if (stack?.Definition == null || stack.Quantity <= 0) continue;
                    entries.Add(new BagEntry
                    {
                        Pocket = pocket,
                        Name = stack.Definition.itemName,
                        Description = stack.Definition.description,
                        Icon = stack.Definition.icon,
                        Quantity = stack.Quantity,
                        Heal = stack.Definition
                    });
                }
                break;

            case ItemPocket.Battle:
                foreach (BattleItemStack stack in inventory.BattleItems)
                {
                    if (stack?.Definition == null || stack.Quantity <= 0) continue;
                    entries.Add(new BagEntry
                    {
                        Pocket = pocket,
                        Name = stack.Definition.itemName,
                        Description = stack.Definition.description,
                        Icon = stack.Definition.icon,
                        Quantity = stack.Quantity,
                        Battle = stack.Definition
                    });
                }
                break;

            case ItemPocket.Key:
                foreach (SpecialItemStack stack in inventory.SpecialItems)
                {
                    if (stack?.Definition == null) continue;
                    entries.Add(new BagEntry
                    {
                        Pocket = pocket,
                        Name = stack.Definition.itemName,
                        Description = stack.Definition.description,
                        Icon = stack.Definition.icon,
                        Quantity = stack.Quantity,
                        // Los objetos clave son unicos: la cantidad no aporta nada.
                        ShowQuantity = false,
                        Special = stack.Definition
                    });
                }
                break;
        }

        return entries;
    }
}
