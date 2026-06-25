using UnityEngine;

public class CharacterDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string characterName;
    public Sprite portrait;

    [Header("Sprites de mundo")]
    public Sprite worldFrontSprite;
    public Sprite worldBackSprite;
    public Sprite worldSideSprite;

    [Header("Animaciones de mundo (opcional)")]
    public AnimatorOverrideController worldAnimator;
}