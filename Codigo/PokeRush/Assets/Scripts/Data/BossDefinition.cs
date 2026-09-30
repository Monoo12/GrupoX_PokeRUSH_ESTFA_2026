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

    [Header("Escenario")]
    [Tooltip("Fondo de batalla (de Assets/Sprites/Battlebacks). Vacio = fondo por defecto de la escena.")]
    public Sprite battleBackground;

    [Header("Subsistemas de la torre")]
    [Tooltip("Variante que se usa cuando el jugador corta TODOS los subsistemas " +
             "(en la torre 1: Nestor sin Melmetal). Vacio = siempre pelea con " +
             "este equipo, sin importar los subsistemas.\n\n" +
             "Es una BossDefinition entera y no solo un equipo porque la " +
             "variante puede cambiar tambien sprites, dialogo y recompensas.\n\n" +
             "Lo que la variante deja vacio (a quien se libera, el fondo, el " +
             "sprite de entrenador) lo toma de este boss.")]
    public BossDefinition varianteTodoCortado;

    [Tooltip("Quien aparece en la FASE DE LIBERACION. En la torre 1 es Nestor: " +
             "el chip es suyo, no del ultimo Poken de su equipo. " +
             "Vacio = se libera al ultimo Poken que quede en el campo, que es " +
             "lo que hacia antes y se leia mal (\"liberaste a Porygon-Z\").")]
    public PokenDefinition pokenDeLiberacion;

    [Tooltip("Nivel con el que entra el Poken de liberacion.")]
    public int nivelDeLiberacion = 50;
}