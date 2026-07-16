using UnityEngine;

[CreateAssetMenu(fileName = "NewBoss", menuName = "PokeRush/Characters/BossDefinition")]
public class BossDefinition : NPCDefinition
{
    [Header("Equipo")]
    public EnemyTeamEntry[] pokenTeam;

    [Header("Sprites de batalla")]
    public Sprite battleIntroSprite;
    public Sprite battleFrontSprite;
    public Sprite battleFrontShinySprite;

    [Header("Dialogo de batalla")]
    public DialogueDefinition battleIntroDialogue;
    public DialogueDefinition battleDefeatDialogue;

    [Header("Audio")]
    public AudioClip battleMusic;

    [Header("Cinematica")]
    public bool triggerElaborateCinematic;

    [Header("Recompensa al derrotar")]
    public int moneyReward;
    public HealItemDefinition[] itemRewards;
    public StoryFlag defeatGrantsFlag;

    [Header("Comportamiento")]
    [Tooltip("Perfil de IA que usa en combate. Vacio = perfil Trainer por defecto.")]
    public AIProfileDefinition aiProfile;
}