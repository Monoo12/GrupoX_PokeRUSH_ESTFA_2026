using UnityEngine;

public class EventController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private DialogueController dialogueController;
    [SerializeField] private BattleController battleController;
    [SerializeField] private CinematicView cinematicView;

    [Header("Eventos del juego")]
    [SerializeField] private EventDefinition[] allEvents;

    private StoryModel _storyModel;
    private PlayerModel _playerModel;

    public void Initialize(StoryModel storyModel, PlayerModel playerModel)
    {
        _storyModel = storyModel;
        _playerModel = playerModel;
    }

    public void EvaluateZoneEvents(string zoneId, StoryModel storyModel)
    {
        foreach (EventDefinition eventDef in allEvents)
        {
            if (ShouldTrigger(eventDef))
                TriggerEvent(eventDef);
        }
    }

    public void EvaluateNPCEvents(NPCDefinition npcDefinition)
    {
        foreach (EventDefinition eventDef in allEvents)
        {
            if (ShouldTrigger(eventDef))
                TriggerEvent(eventDef);
        }
    }

    private bool ShouldTrigger(EventDefinition eventDef)
    {
        if (eventDef.requiredFlag == StoryFlag.None) return true;
        return _storyModel.HasFlag(eventDef.requiredFlag);
    }

    private void TriggerEvent(EventDefinition eventDef)
    {
        if (eventDef.grantsFlag != StoryFlag.None)
            _storyModel.ActivateFlag(eventDef.grantsFlag);

        if (eventDef.triggerDialogue != null)
            dialogueController.StartDialogue(eventDef.triggerDialogue, _storyModel);

        if (eventDef.triggerCinematic)
            cinematicView.Play();

        if (eventDef.triggerBattle && eventDef.bossToFight != null)
            battleController.StartBattle(_playerModel, eventDef.bossToFight);
    }
}