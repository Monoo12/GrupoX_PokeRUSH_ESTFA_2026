using UnityEngine;
using UnityEngine.InputSystem;

// Guardado rapido con F2 desde cualquier escena jugable.
//
// Va en un objeto suelto de la escena. Avisa por la caja de dialogo si la hay,
// asi el jugador ve que el guardado ocurrio de verdad; si no hay caja, queda
// solo en consola.
public class GuardadoRapido : MonoBehaviour
{
    [Header("Tecla")]
    [SerializeField] private Key tecla = Key.F2;

    [Header("Textos")]
    [SerializeField] private string nombreQueHabla = "";
    [SerializeField] private string mensajeOk = "Partida guardada.";
    [SerializeField] private string mensajeSinPartida = "No hay ninguna partida para guardar.";

    [Header("Referencias (se buscan solas)")]
    [SerializeField] private DialogueController dialogueController;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[tecla].wasPressedThisFrame) return;

        // Con la caja abierta la tecla la maneja el dialogo: guardar en el medio
        // de una decision dejaria el save a mitad de camino.
        if (dialogueController != null && dialogueController.IsOpen) return;

        Guardar();
    }

    public void Guardar()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentPlayer == null)
        {
            Avisar(mensajeSinPartida);
            return;
        }

        gm.SaveGame();
        Debug.Log("[Guardado] Partida de '" + gm.CurrentPlayer.PlayerName + "' guardada con " + tecla + ".");
        Avisar(mensajeOk);
    }

    private void Avisar(string mensaje)
    {
        if (dialogueController != null)
            dialogueController.StartMessage(nombreQueHabla, null, new[] { mensaje });
        else
            Debug.Log("[Guardado] " + mensaje);
    }
}
