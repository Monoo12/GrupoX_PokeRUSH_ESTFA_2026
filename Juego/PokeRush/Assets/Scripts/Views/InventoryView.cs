using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InventoryView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform itemListContainer;
    [SerializeField] private GameObject itemSlotPrefab;
    [SerializeField] private Text moneyText;

    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void DisplayInventory(InventoryModel inventory, int playerMoney)
    {
        ClearSlots();

        moneyText.text = $"${playerMoney}";

        foreach (HealItemStack stack in inventory.HealItems)
            CreateSlot(stack.Definition.itemName, stack.Definition.icon, stack.Quantity);

        foreach (BattleItemStack stack in inventory.BattleItems)
            CreateSlot(stack.Definition.itemName, stack.Definition.icon, stack.Quantity);

        foreach (SpecialItemStack stack in inventory.SpecialItems)
            CreateSlot(stack.Definition.itemName, stack.Definition.icon, stack.Quantity);
    }

    private void CreateSlot(string itemName, Sprite icon, int quantity)
    {
        GameObject slot = Instantiate(itemSlotPrefab, itemListContainer);
        // configuracion del prefab del slot — se implementa cuando exista el prefab
        _spawnedSlots.Add(slot);
    }

    private void ClearSlots()
    {
        foreach (GameObject slot in _spawnedSlots)
            Destroy(slot);

        _spawnedSlots.Clear();
    }
}

//itemSlotPrefab es un prefab de UI que todavía no existe — representa una fila o casillero por ítem. ClearSlots() se llama antes de redibujar para no acumular slots viejos cada vez que se abre el inventario. CreateSlot queda con un comentario porque depende del diseño visual del prefab, que el equipo define después.