using UnityEngine;
/// Esta clase define las caracter�sticas de un Poken, incluyendo su identidad, stats base, movimientos, habilidad y aspecto cosm�tico.
[CreateAssetMenu(fileName = "NewPoken", menuName = "PokeRush/PokenDefinition")]
//Con esto puedo crear Objetos de tipo PokenDefinition desde el editor de Unity, lo que facilita la creaci�n y gesti�n de los datos de los Pokens en el juego.
public class PokenDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string pokenName;
    public PokenType type;
    [Tooltip("Segundo tipo. Dejar en None si el Poken es de un solo tipo.")]
    public PokenType type2;

    [Header("Sprites de batalla")]
    public Sprite battleFrontSprite;       // Vista de frente (enemigo)
    public Sprite battleBackSprite;        // Vista de espalda (jugador)
    public Sprite battleFrontShinySprite;  // Frente shiny
    public Sprite battleBackShinySprite;   // Espalda shiny

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
    public MoveDefinition[] moves;

    [Header("Habilidad")]
    public string ability;
    public string abilityDescription;

    //[Header("Cosmetico")]
    //public CosmeticDefinition defaultCosmetic;
}