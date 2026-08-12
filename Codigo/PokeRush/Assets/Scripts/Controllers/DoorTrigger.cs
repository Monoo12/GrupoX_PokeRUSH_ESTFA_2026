using UnityEngine;

// Pasaje entre salas. El jugador entra al collider y aparece en el destino.
//
// Por que teletransporte y no cambio de escena: las salas de la torre viven
// todas en la misma escena a distintos offsets del Grid. Cambiar de escena
// obligaria a re-bootstrapear los controladores y a persistir la posicion del
// jugador, y SaveService hoy no guarda ni escena ni posicion.
//
// Necesita un Collider2D con isTrigger = true, y que el jugador tenga
// Rigidbody2D (aunque sea Kinematic) para que Unity dispare OnTriggerEnter2D.
[RequireComponent(typeof(Collider2D))]
public class DoorTrigger : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Donde aparece el jugador. Ponelo del lado de ADENTRO de la sala destino.")]
    [SerializeField] private Transform destino;
    [Tooltip("Hacia donde queda mirando al llegar.")]
    [SerializeField] private Vector2 miraHacia = Vector2.down;

    [Header("Condicion")]
    [Tooltip("Si esta puesto, la puerta solo abre teniendo este flag. None = siempre.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeCerrada = "La puerta esta bloqueada.";

    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] private SubsystemFeedbackView feedback;

    private void Awake()
    {
        if (feedback == null) feedback = FindFirstObjectByType<SubsystemFeedbackView>();
        if (destino == null)
            Debug.LogError($"[Puerta] '{name}' no tiene destino asignado: no lleva a ningun lado.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || destino == null) return;

        StoryModel story = GameManager.Instance?.Story;
        if (requiereFlag != StoryFlag.None && (story == null || !story.HasFlag(requiereFlag)))
        {
            if (feedback != null) feedback.Show("CERRADO", mensajeCerrada);
            return;
        }

        player.TeleportTo(destino.position, miraHacia);
    }
}
