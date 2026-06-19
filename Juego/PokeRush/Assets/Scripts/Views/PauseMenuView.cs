using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;

public class PauseMenuView : MonoBehaviour
{
    [Header("Subvistas")]
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private TeamView teamView;

    [Header("Botones")]
    [SerializeField] private Button pokenButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button closeButton;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        inventoryView.Hide();
        teamView.Hide();
    }

    public void OpenTeamView(PlayerModel player)
    {
        teamView.DisplayTeam(player.Team);
        teamView.Show();
    }

    public void OpenInventoryView(PlayerModel player)
    {
        inventoryView.DisplayInventory(player.Inventory, player.Money);
        inventoryView.Show();
    }
}