using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "PokeRush/Characters/EnemyDefinition")]
public class EnemyDefinition : NPCDefinition
{
    [Header("Equipo")]
    public EnemyTeamEntry[] pokenTeam;

    [Header("Sprites de batalla")]
    public Sprite battleFrontSprite;
    public Sprite battleBackSprite;

    [Header("Audio")]
    public AudioClip battleMusic;

    [Header("Cinematica de entrada")]
    public DialogueDefinition battleIntroDialogue;

    [Header("Recompensa al derrotar")]
    public int moneyReward;
    public HealItemDefinition[] itemRewards;
    public StoryFlag defeatGrantsFlag;

    [Header("Comportamiento")]
    public bool fleeIfWeak;
    [Tooltip("Perfil de IA que usa en combate. Vacio = perfil Trainer por defecto.")]
    public AIProfileDefinition aiProfile;
}