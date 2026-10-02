using UnityEngine;
using UnityEngine.SceneManagement;

// La salida de un nivel de la historia, para volver caminando a la Quarcho
// Cueva despues de terminarlo.
//
// Sin esto, VueltaAlLobby saca al jugador del nivel apenas se enciende el flag
// de completado. Con una SalidaDelNivel en la escena, VueltaAlLobby NO lo saca:
// el jugador se queda, recorre el nivel ya terminado (los NPCs reaccionan, las
// luces se apagan) y se va cuando pisa la salida. Es lo que pide el guion del
// Data Center de Nestor: "el PJ comienza a caminar hacia la salida".
//
// Mientras el nivel no esta terminado no hace nada: el jugador arranca parado
// al lado y pasar por ahi no tiene que preguntar nada.
//
// Es una zona para PISAR, como la salida de la cueva: el Collider2D va en 'Is
// Trigger' y en la capa Default (en Obstacles el jugador frena en el borde y el
// OnTriggerEnter2D no llega nunca). Tambien responde a X, por si se la pone
// sobre una pared. Un BrilloInteractuable en un hijo la marca recien cuando
// abre (toma el flag de aca).
[RequireComponent(typeof(Collider2D))]
public class SalidaDelNivel : MonoBehaviour, IInteractable
{
    [Header("Cuando abre")]
    [Tooltip("None = el flag de completado del nivel de esta escena (RecorridoDeLaHistoria).")]
    [SerializeField] private StoryFlag flagQueAbre = StoryFlag.None;

    [Header("Textos")]
    [SerializeField, TextArea(2, 3)] private string pregunta = "¿Volvés a la Quarcho Cueva?";
    [SerializeField] private string opcionVolver = "Volver";
    [SerializeField] private string opcionQuedarse = "Todavía no";

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    private bool _armado = true;
    private bool _saliendo;

    public StoryFlag FlagQueAbre
    {
        get
        {
            if (flagQueAbre != StoryFlag.None) return flagQueAbre;
            RecorridoDeLaHistoria.Nivel nivel = RecorridoDeLaHistoria.NivelDeLaEscena(gameObject.scene.name);
            return nivel != null ? nivel.FlagCompletado : StoryFlag.None;
        }
    }

    // VueltaAlLobby pregunta esto al entrar a cada escena.
    public static bool HayEnLaEscena()
        => FindAnyObjectByType<SalidaDelNivel>(FindObjectsInactive.Exclude) != null;

    public bool Abierta(StoryModel story)
    {
        StoryFlag f = FlagQueAbre;
        return f != StoryFlag.None && story != null && story.HasFlag(f);
    }

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_armado || _saliendo || other.GetComponentInParent<PlayerController>() == null) return;
        if (MenuManager.AnyOpen || (dialogueController != null && dialogueController.IsOpen)) return;

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (!Abierta(story)) return;

        _armado = false;
        Preguntar();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _armado = true;
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (_saliendo || !Abierta(story)) return false;
        Preguntar();
        return true;
    }

    private void Preguntar()
    {
        CasinoDialogo.Preguntar(dialogueController, "", null, pregunta, opcionVolver, opcionQuedarse,
            volver => { if (volver) Salir(); });
    }

    private void Salir()
    {
        string lobby = RecorridoDeLaHistoria.EscenaDelLobby;
        if (!Application.CanStreamedLevelBeLoaded(lobby))
        {
            Debug.LogError($"[Salida] '{lobby}' no esta en Build Settings.");
            return;
        }

        _saliendo = true;
        Debug.Log($"[Recorrido] Salida del nivel ({gameObject.scene.name}) hacia {lobby}.");
        CasinoDialogo.Guardar();
        Sonidos.Teletransporte();
        SceneManager.LoadScene(lobby);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : new Bounds(transform.position, Vector3.one * 0.9f);
        if (b.size.sqrMagnitude < 0.01f) b = new Bounds(transform.position, new Vector3(0.9f, 0.9f, 0f));

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.01f));
        UnityEditor.Handles.Label(new Vector3(b.min.x, b.max.y + 0.4f, 0f), "Salida -> cueva (nivel terminado)");
    }
#endif
}
