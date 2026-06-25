using UnityEngine;

public class NPCDefinition : CharacterDefinition
{
    [Header("Dialogo")]
    public DialogueDefinition defaultDialogue;

    [Header("Flags de historia")]
    public StoryFlag requiredFlag;
    public StoryFlag grantsFlag;

    [Header("Batalla futura (opcional)")]
    public bool canBattleLater;
}