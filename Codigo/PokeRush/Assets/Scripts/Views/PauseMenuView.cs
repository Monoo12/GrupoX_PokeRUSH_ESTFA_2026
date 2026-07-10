using UnityEngine;
using UnityEngine.UI;

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

    [Header("Opciones (opcional)")]
    [SerializeField] private GameObject optionsPanel;

    // Jugador actual mientras el menu esta abierto. Lo usamos para que los
    // botones de equipo e inventario sepan que datos mostrar.
    private PlayerModel _player;

    // True mientras el menu esta visible. Lo usa el input para alternar (toggle)
    // con la misma tecla.
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        // Conectamos cada boton a su accion una sola vez. AddListener por codigo
        // evita depender de que alguien enganche los eventos a mano en el Inspector.
        if (pokenButton != null) pokenButton.onClick.AddListener(OnPokenClicked);
        if (bagButton != null) bagButton.onClick.AddListener(OnBagClicked);
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        // Soltamos los listeners para no dejar referencias colgando a esta vista.
        if (pokenButton != null) pokenButton.onClick.RemoveListener(OnPokenClicked);
        if (bagButton != null) bagButton.onClick.RemoveListener(OnBagClicked);
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OnOptionsClicked);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    // Abre el menu de pausa mostrando los datos del jugador recibido.
    public void Show(PlayerModel player)
    {
        _player = player;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        inventoryView.Hide();
        teamView.Hide();
        if (optionsPanel != null) optionsPanel.SetActive(false);
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

    // --- Handlers de los botones ---

    private void OnPokenClicked()
    {
        if (_player == null) return;
        inventoryView.Hide();
        OpenTeamView(_player);
    }

    private void OnBagClicked()
    {
        if (_player == null) return;
        teamView.Hide();
        OpenInventoryView(_player);
    }

    private void OnSaveClicked()
    {
        GameManager.Instance.SaveGame();
    }

    private void OnOptionsClicked()
    {
        // El panel de opciones todavia no tiene su propia vista: por ahora solo
        // lo mostramos si esta asignado. Se reemplaza cuando exista OptionsView.
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    private void OnCloseClicked()
    {
        Hide();
    }
}