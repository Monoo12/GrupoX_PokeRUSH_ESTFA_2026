using UnityEngine;

// El PC de almacenamiento puesto en un mapa.
//
// Abre la pantalla del PC (PcStorageScreen) encima del nivel, sin cambiar de
// escena, igual que las maquinas del casino. Va uno en cada nivel jugable: lo
// pone PokeRush > Poner el PC en todos los niveles.
[RequireComponent(typeof(Collider2D))]
public class PcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField, TextArea(2, 3)] private string mensajeAlEncender = "Encendiste el PC.";
    [Tooltip("Guardar la partida al apagar el PC: mover Pokemon entre el equipo y las cajas es progreso.")]
    [SerializeField] private bool guardarAlApagar = true;

    // Los bancos de prueba lo apagan para no pisar el save de verdad con la partida DEMO.
    public static bool GuardadoAutomatico = true;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[PC] No hay MenuManager: no se puede abrir el PC.");
            return false;
        }

        menus.SetContext(MenuContextKind.Overworld);
        if (string.IsNullOrEmpty(mensajeAlEncender)) Abrir(menus);
        else menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensajeAlEncender }, _ => Abrir(menus));
        return true;
    }

    private void Abrir(MenuManager menus)
    {
        menus.Open(MenuScreenId.PcStorage, null, _ =>
        {
            if (guardarAlApagar && GuardadoAutomatico && GameManager.Instance != null)
                GameManager.Instance.SaveGame();
        });
    }
}
