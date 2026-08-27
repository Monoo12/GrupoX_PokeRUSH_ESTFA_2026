using UnityEngine;
using UnityEngine.UI;

// Una fila de la mochila: nombre + cantidad + icono.
public class ItemRowWidget : MenuWidget
{
    [Header("Objeto")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text quantityText;
    [SerializeField] private Image icon;
    [Tooltip("Formato de la cantidad. {0} = numero.")]
    [SerializeField] private string quantityFormat = "x {0}";

    public void Bind(BagEntry entry)
    {
        if (nameText != null) nameText.text = entry.Name;

        if (quantityText != null)
        {
            // Los objetos clave no se apilan: mostrar "x 1" al lado de una llave
            // solo agrega ruido.
            quantityText.enabled = entry.ShowQuantity;
            if (entry.ShowQuantity)
                quantityText.text = string.Format(quantityFormat, entry.Quantity);
        }

        if (icon != null)
        {
            icon.sprite = entry.Icon;
            icon.enabled = entry.Icon != null;
            icon.preserveAspect = true;
        }

        SetOptionEnabled(true);
    }
}
