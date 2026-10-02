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

    private void OnEnable()
    {
        if (battleController != null)
            battleController.BattleEnded += OnBattleEnded;
    }

    private void OnDisable()
    {
        if (battleController != null)
            battleController.BattleEnded -= OnBattleEnded;
    }

    // Al terminar un combate: aplica flag de derrota, items, dialogo del boss
    // y guarda la partida.
    private void OnBattleEnded(BattleOutcome outcome)
    {
        if (!outcome.PlayerVictory || outcome.Enemy == null) return;

        StoryFlag grantsFlag = StoryFlag.None;
        HealItemDefinition[] itemRewards = null;
        DialogueDefinition defeatDialogue = null;

        if (outcome.Enemy.Definition is BossDefinition boss)
        {
            grantsFlag = boss.defeatGrantsFlag;
            itemRewards = boss.itemRewards;
            defeatDialogue = boss.battleDefeatDialogue;
        }
        else if (outcome.Enemy.Definition is EnemyDefinition enemy)
        {
            grantsFlag = enemy.defeatGrantsFlag;
            itemRewards = enemy.itemRewards;
        }

        if (grantsFlag != StoryFlag.None && _storyModel != null)
            _storyModel.ActivateFlag(grantsFlag);

        if (itemRewards != null && _playerModel != null)
        {
            bool alguno = false;
            foreach (HealItemDefinition item in itemRewards)
                if (item != null)
                {
                    _playerModel.Inventory.AddHealItem(item, 1);
                    alguno = true;
                }
            if (alguno) Sonidos.ObjetoRecibido();
        }

        if (defeatDialogue != null && dialogueController != null)
            dialogueController.StartDialogue(defeatDialogue, _storyModel);

        // Persistir el progreso tras la victoria
        if (GameManager.Instance != null)
            GameManager.Instance.SaveGame();
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