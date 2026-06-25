using UnityEngine;

[CreateAssetMenu(fileName = "NewEvent", menuName = "PokeRush/EventDefinition")]
public class EventDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string eventName;

    [Header("Condicion")]
    public StoryFlag requiredFlag;

    [Header("Acciones al dispararse")]
    public StoryFlag grantsFlag;
    public DialogueDefinition triggerDialogue;
    public bool triggerCinematic;
    public bool triggerBattle;
    public BossDefinition bossToFight;

    [Header("Recompensas (opcional)")]
    public int moneyReward;
    public HealItemDefinition[] itemRewards;
}

//EventDefinition es el pegamento de la historia. El EventController lo lee, evalúa si requiredFlag está activo, y ejecuta las acciones definidas — mostrar un diálogo, iniciar una batalla, activar una cinemática o simplemente otorgar un flag nuevo. Si triggerBattle es true y hay un bossToFight asignado, el BattleController sabe contra quién pelear.