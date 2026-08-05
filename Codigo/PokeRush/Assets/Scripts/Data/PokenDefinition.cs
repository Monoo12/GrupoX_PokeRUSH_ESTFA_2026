using UnityEngine;
/// Esta clase define las caracter�sticas de un Poken, incluyendo su identidad, stats base, movimientos, habilidad y aspecto cosm�tico.
[CreateAssetMenu(fileName = "NewPoken", menuName = "PokeRush/PokenDefinition")]
//Con esto puedo crear Objetos de tipo PokenDefinition desde el editor de Unity, lo que facilita la creaci�n y gesti�n de los datos de los Pokens en el juego.
public class PokenDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string pokenName;
    [Tooltip("Numero de Pokedex nacional. 0 para especies propias del juego (Chispa, Aquita, Bulbi).")]
    public int dexNumber;
    public PokenType type;
    [Tooltip("Segundo tipo. Dejar en None si el Poken es de un solo tipo.")]
    public PokenType type2;

    [Header("Sprites de batalla")]
    public Sprite battleFrontSprite;       // Vista de frente (enemigo)
    public Sprite battleBackSprite;        // Vista de espalda (jugador)
    public Sprite battleFrontShinySprite;  // Frente shiny
    public Sprite battleBackShinySprite;   // Espalda shiny

    [Header("Animacion de batalla (frames recortados de la tira, los llena SpriteAssigner)")]
    public Sprite[] battleFrontFrames;
    public Sprite[] battleBackFrames;
    public Sprite[] battleFrontShinyFrames;
    public Sprite[] battleBackShinyFrames;

    [Header("Modelo 3D de batalla (opcional)")]
    [Tooltip("Modelo .glb de la especie. Si esta cargado, PokenView dibuja el modelo " +
             "en vez del sprite. Vacio = se usa el sprite, que es el caso de la mayoria " +
             "de las especies. Lo llena el importador de modelos 3D.")]
    public GameObject battleModel;

    [Tooltip("Clip que se reproduce en loop mientras el Poken espera su turno. Lo elige " +
             "el importador entre los clips del modelo (busca 'idle', 'wait', etc). " +
             "Vacio = el modelo se queda quieto.")]
    public AnimationClip battleIdleClip;

    [Header("Sprites de mundo")]
    public Sprite worldFrontSprite;
    public Sprite worldBackSprite;
    public Sprite worldSideSprite;
    public Sprite worldFrontShinySprite;
    public Sprite worldBackShinySprite;
    public Sprite worldSideShinySprite;

    [Header("Sprites de UI")]
    public Sprite inventorySprite;         // Icono peque�o para el equipo e inventario
    public Sprite inventoryShinySprite;    // Variante shiny para UI

    [Header("Animaciones (opcional)")]
    public AnimatorOverrideController battleAnimator;
    public AnimatorOverrideController worldAnimator;

    [Header("Stats base")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpecialAttack;
    public int baseSpecialDefense;
    public int baseSpeed;

    [Header("Movimientos")]
    [Tooltip("Learnset por nivel (lo llena el importador de PokeAPI). Tiene prioridad sobre 'moves'.")]
    public LearnsetEntry[] learnset;
    [Tooltip("LEGADO: lista plana de movimientos. Solo se usa si 'learnset' esta vacio (Pokens de prueba).")]
    public MoveDefinition[] moves;

    [Header("Experiencia")]
    [Tooltip("XP base que otorga al ser derrotado (base_experience de PokeAPI).")]
    public int baseXpYield = 60;

    [Header("Habilidad")]
    [Tooltip("Habilidad con efecto en combate. None = sin efecto (solo se muestra el nombre de 'ability').")]
    public AbilityId abilityId = AbilityId.None;
    public string ability;
    public string abilityDescription;

    //[Header("Cosmetico")]
    //public CosmeticDefinition defaultCosmetic;
}