using UnityEngine;

// Un NPC que solo habla.
//
// Lo que dice sale de un DialogueDefinition y no de campos de la escena: asi los
// textos de historia se reescriben tocando el asset, sin abrir el mapa ni
// arriesgarse a mover al personaje.
//
// Si el dialogo otorga un flag al terminar y hay un 'dialogoRepetido', las
// veces siguientes dice ese (el tipico "ya te lo conte").
[RequireComponent(typeof(Collider2D))]
public class NpcQueHabla : MonoBehaviour, IInteractable
{
    [Tooltip("Lo que dice la primera vez.")]
    [SerializeField] private DialogueDefinition dialogo;
    [Tooltip("Lo que dice cuando ya se activo el flag de 'dialogo'. Vacio = repite el mismo.")]
    [SerializeField] private DialogueDefinition dialogoRepetido;

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    public DialogueDefinition Dialogo => dialogo;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (dialogo == null) return false;

        DialogueDefinition cual = dialogo;
        if (dialogoRepetido != null && dialogo.grantsFlag != StoryFlag.None &&
            story != null && story.HasFlag(dialogo.grantsFlag))
            cual = dialogoRepetido;

        if (dialogueController != null)
        {
            dialogueController.StartDialogue(cual, story);
            return true;
        }

        CasinoDialogo.Decir(null, cual.speakerName, cual.speakerPortrait, cual.lines);
        return true;
    }
}
