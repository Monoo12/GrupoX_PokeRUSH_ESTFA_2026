using UnityEngine;

// Un momento de historia que pasa al PISAR un lugar: las pantallas que
// parpadean al entrar a un salon, un zumbido que llega por un pasillo, una
// orden del sistema que se escucha por un parlante.
//
// Lo que se dice es un DialogueDefinition, con todo lo que trae (varios que
// hablan, variantes, lo que sigue). Suena UNA vez: la marca su grantsFlag (o el
// de la variante que toco), que es obligatorio; sin flag avisa y no suena, para
// no repetirse cada vez que se pasa por ahi.
//
// Es una zona para PISAR: Collider2D en 'Is Trigger' y en la capa Default (en
// Obstacles el jugador frena en el borde y nunca entra). Ver
// trigger-en-obstacles en la documentacion.
[RequireComponent(typeof(Collider2D))]
public class ZonaDeHistoria : MonoBehaviour
{
    [Tooltip("Lo que pasa. Su grantsFlag (o el de la variante) marca que ya paso.")]
    [SerializeField] private DialogueDefinition dialogo;
    [Tooltip("Solo suena con este flag. None = siempre.")]
    [SerializeField] private StoryFlag requiere = StoryFlag.None;
    [Tooltip("No suena con este flag. None = no excluye nada.")]
    [SerializeField] private StoryFlag excluye = StoryFlag.None;

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    public DialogueDefinition Dialogo => dialogo;

    private bool _contando;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter2D(Collider2D other) => Probar(other);

    // Si al pisarla habia una caja abierta, se prueba de nuevo mientras siga
    // adentro: si no, un evento se perderia por un cartel que justo estaba.
    private void OnTriggerStay2D(Collider2D other) => Probar(other);

    private void Probar(Collider2D other)
    {
        if (_contando || other.GetComponentInParent<PlayerController>() == null) return;
        if (MenuManager.AnyOpen || dialogueController == null || dialogueController.IsOpen) return;

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (!TocaContar(story)) return;

        _contando = true;
        dialogueController.StartDialogue(dialogo, story, () => _contando = false);
    }

    // Publico para los tests: si esta partida tiene que ver este momento.
    public bool TocaContar(StoryModel story)
    {
        if (dialogo == null || story == null) return false;
        if (requiere != StoryFlag.None && !story.HasFlag(requiere)) return false;
        if (excluye != StoryFlag.None && story.HasFlag(excluye)) return false;
        if (!dialogo.TieneQueDecir(story)) return false;

        StoryFlag marca = dialogo.FlagPara(story);
        if (marca == StoryFlag.None)
        {
            Debug.LogWarning($"[ZonaDeHistoria] '{name}': el dialogo no otorga flag, se repetiria siempre. No suena.");
            return false;
        }
        return !story.HasFlag(marca);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Bounds b = col.bounds;
        Gizmos.color = new Color(1f, 0.6f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.01f));
        UnityEditor.Handles.Label(new Vector3(b.min.x, b.max.y + 0.3f, 0f),
            dialogo != null ? "Historia: " + dialogo.name : "Historia (sin dialogo)");
    }
#endif
}
