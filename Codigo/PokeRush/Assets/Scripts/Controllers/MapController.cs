using UnityEngine;

public class MapController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private EventController eventController;
    [SerializeField] private DialogueController dialogueController;

    private StoryModel _storyModel;

    public void Initialize(StoryModel storyModel)
    {
        _storyModel = storyModel;
    }

    public void OnPlayerEntersZone(string zoneId)
    {
        // evaluar eventos de zona — delega a EventController
        eventController.EvaluateZoneEvents(zoneId, _storyModel);
    }

    public void OnPlayerInteractsWithNPC(NPCDefinition npcDefinition)
    {
        // iniciar dialogo o batalla segun el tipo de NPC
        dialogueController.StartDialogue(npcDefinition.defaultDialogue, _storyModel);
    }
}