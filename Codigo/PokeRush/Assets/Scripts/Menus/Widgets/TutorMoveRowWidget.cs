using UnityEngine;
using UnityEngine.UI;

// Una fila del tutor de movimientos: tipo, nombre y, en el recuadro celeste del
// fondo, el precio (al elegir que aprender) o los PP (al elegir que olvidar).
//
// El marco amarillo (Graphics/UI/Move Reminder/cursor) va en los
// 'selectedObjects' de MenuWidget.
public class TutorMoveRowWidget : MenuWidget
{
    [Header("Movimiento")]
    [SerializeField] private Text nameText;
    [SerializeField] private Image typeIcon;
    [SerializeField] private Text slotText;
    [Tooltip("Se prende en la fila del movimiento NUEVO, cuando hay que elegir cual olvidar.")]
    [SerializeField] private GameObject newMark;

    public MoveDefinition Move { get; private set; }

    public void Bind(MoveDefinition move, string slot, bool habilitada, bool esNuevo)
    {
        Move = move;
        if (nameText != null) nameText.text = move != null ? move.moveName : "-";
        if (slotText != null) slotText.text = slot ?? "";
        if (typeIcon != null)
        {
            if (move != null) MenuIconLibrary.PaintType(typeIcon, move.type);
            else typeIcon.enabled = false;
        }
        if (newMark != null) newMark.SetActive(esNuevo);
        SetOptionEnabled(habilitada);
    }
}
