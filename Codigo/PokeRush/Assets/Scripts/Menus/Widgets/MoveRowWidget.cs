using UnityEngine;
using UnityEngine.UI;

// Una fila de movimiento: nombre, icono de tipo y PP.
public class MoveRowWidget : MenuWidget
{
    [Header("Movimiento")]
    [SerializeField] private Text nameText;
    [SerializeField] private Image typeIcon;
    [SerializeField] private Text ppText;
    [SerializeField] private Image ppFill;
    [SerializeField] private string ppFormat = "PP  {0}/{1}";
    [Tooltip("Se prende cuando este movimiento esta agarrado para intercambiar.")]
    [SerializeField] private GameObject swapMark;

    public MoveModel Move { get; private set; }

    public void Bind(MoveModel move)
    {
        Move = move;
        gameObject.SetActive(true);

        if (move == null || move.Definition == null)
        {
            if (nameText != null) nameText.text = "-";
            if (ppText != null) ppText.text = string.Empty;
            if (typeIcon != null) typeIcon.enabled = false;
            if (ppFill != null) ppFill.fillAmount = 0f;
            SetOptionEnabled(false);
            SetSwapping(false);
            return;
        }

        MoveDefinition def = move.Definition;
        if (nameText != null) nameText.text = def.moveName;
        if (ppText != null) ppText.text = string.Format(ppFormat, move.CurrentPP, def.maxPP);
        if (ppFill != null)
            ppFill.fillAmount = def.maxPP > 0 ? Mathf.Clamp01((float)move.CurrentPP / def.maxPP) : 0f;

        MenuIconLibrary.PaintType(typeIcon, def.type);
        SetOptionEnabled(true);
        SetSwapping(false);
    }

    public void SetSwapping(bool swapping)
    {
        if (swapMark != null) swapMark.SetActive(swapping);
    }
}
