using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TeamView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform pokenSlotContainer;
    [SerializeField] private GameObject pokenSlotPrefab;

    [Header("Detalle del seleccionado")]
    [SerializeField] private Text selectedNameText;
    [SerializeField] private Text selectedLevelText;
    [SerializeField] private HealthBarView selectedHealthBar;
    [SerializeField] private Image selectedSprite;

    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void DisplayTeam(List<PokenModel> team)
    {
        ClearSlots();

        foreach (PokenModel poken in team)
            CreateSlot(poken);

        if (team.Count > 0)
            ShowDetail(team[0]);
    }

    public void ShowDetail(PokenModel poken)
    {
        selectedNameText.text = poken.Definition.pokenName;
        selectedLevelText.text = $"Nv. {poken.Level}";
        selectedHealthBar.SetHealth(poken.CurrentHP, poken.MaxHP);
        selectedSprite.sprite = poken.Definition.inventorySprite;
    }

    private void CreateSlot(PokenModel poken)
    {
        GameObject slot = Instantiate(pokenSlotPrefab, pokenSlotContainer);
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