using UnityEngine;

[CreateAssetMenu(fileName = "NewNurse", menuName = "PokeRush/Characters/NurseDefinition")]
public class NurseDefinition : NPCDefinition
{
    [Header("Dialogo de curacion")]
    public DialogueDefinition healingDialogue;
    public DialogueDefinition afterHealingDialogue;
}