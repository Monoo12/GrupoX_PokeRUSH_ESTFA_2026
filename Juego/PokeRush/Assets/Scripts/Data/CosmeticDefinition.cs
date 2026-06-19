using UnityEngine;
//Esra clase define las características de un cosmético, incluyendo su identidad, sprites para diferentes contextos (batalla, mundo, UI) y animaciones opcionales. También puede tener una restricción para ser usado solo por un Poken específico.
[CreateAssetMenu(fileName = "NewCosmetic", menuName = "PokeRush/CosmeticDefinition")]
public class CosmeticDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string cosmeticName;
    public string description;
    public Sprite icon;

    [Header("Sprites de batalla")]
    public Sprite battleFrontSprite;
    public Sprite battleBackSprite;

    [Header("Sprites de mundo")]
    public Sprite worldFrontSprite;
    public Sprite worldBackSprite;
    public Sprite worldSideSprite;

    [Header("Sprites de UI")]
    public Sprite inventorySprite;

    [Header("Animaciones (opcional)")]
    public AnimatorOverrideController battleAnimator;
    public AnimatorOverrideController worldAnimator;

    [Header("Restriccion")]
    public PokenDefinition exclusiveToPoken;
}