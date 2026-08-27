using UnityEngine;

// La maquina tragamonedas parada en el piso 1 del casino.
//
// Se cuelga del objeto visual que ya haya puesto el equipo de nivel: no dibuja
// nada ni mueve nada, solo agrega la interaccion. Necesita un Collider2D en una
// capa que este dentro del 'interactableMask' del jugador, igual que el resto
// de los interactuables del juego.
//
// Abre la UI encima de la escena (MenuManager) y NO carga ninguna escena nueva:
// mientras la pantalla esta arriba de la pila el jugador no se mueve, y al
// cerrarse vuelve a tener el control exactamente donde estaba.
[RequireComponent(typeof(Collider2D))]
public class SlotMachineInteractable : MonoBehaviour, IInteractable
{
    [Header("Maquina")]
    [Tooltip("Configuracion de esta maquina: simbolos, pagos, apuestas. " +
             "Dos maquinas del piso pueden tener assets distintos.")]
    [SerializeField] private SlotMachineDefinition maquina;
    [Tooltip("Titulo del cartel. Vacio = el nombre que trae la definicion.")]
    [SerializeField] private string titulo = "";

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, la maquina solo se puede usar teniendo ya este flag. " +
             "None = siempre disponible.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeBloqueada = "Esta maquina esta apagada.";

    [Header("Al terminar")]
    [Tooltip("Mostrar un resumen de la sesion al cerrar la maquina.")]
    [SerializeField] private bool mostrarResumen = true;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        if (requiereFlag != StoryFlag.None && (story == null || !story.HasFlag(requiereFlag)))
        {
            Avisar(mensajeBloqueada);
            return true;
        }

        if (maquina == null)
        {
            Debug.LogError($"[Casino] La maquina '{name}' no tiene definicion asignada.");
            Avisar("Esta maquina esta fuera de servicio.");
            return true;
        }

        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[Casino] No hay MenuManager: no se puede abrir la maquina.");
            return true;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Slots,
            new SlotsScreenRequest { Maquina = maquina, Titulo = titulo },
            resultado => AlCerrar(resultado as SlotsScreenResult));

        return true;
    }

    private void AlCerrar(SlotsScreenResult resultado)
    {
        if (!mostrarResumen || resultado == null || resultado.Tiradas <= 0) return;

        string texto;
        if (resultado.Neto > 0) texto = $"Te fuiste con {resultado.Neto} de ganancia.";
        else if (resultado.Neto < 0) texto = $"La maquina se quedo con {-resultado.Neto}.";
        else texto = "Empataste con la maquina.";

        Avisar(texto);
    }

    // El resumen y los avisos salen por la caja de mensajes del sistema de
    // menus, la misma que usa el resto del juego.
    private void Avisar(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Casino] {mensaje}"); return; }

        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje });
    }
}
