using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "PokeRush/DialogueDefinition")]
public class DialogueDefinition : ScriptableObject
{
    [Header("Hablante")]
    public string speakerName;
    public Sprite speakerPortrait;

    [Header("Lineas")]
    [TextArea(2, 5)]
    //Nota: [TextArea(2, 5)] hace que cada línea de diálogo se edite en un campo de texto grande en el Inspector en lugar de una línea chica — más cómodo para escribir diálogos largos. El primer número es el mínimo de líneas visibles y el segundo el máximo antes de aparecer el scroll.    
    public string[] lines;

    [Header("Flag que activa este dialogo (opcional)")]
    public StoryFlag requiredFlag;

    [Header("Flag que otorga al terminar (opcional)")]
    public StoryFlag grantsFlag;
}