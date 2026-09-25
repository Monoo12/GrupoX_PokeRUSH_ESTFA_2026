using UnityEngine;
using UnityEngine.UI;

// Una fila de la tienda: nombre y precio. La ultima fila es SALIR, sin precio.
//
// El marco rojo de la seleccion (Graphics/UI/Mart/cursor) va en los
// 'selectedObjects' de MenuWidget: se prende solo en la fila elegida.
public class ShopRowWidget : MenuWidget
{
    [Header("Fila")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text priceText;
    [Tooltip("{0} = el precio.")]
    [SerializeField] private string priceFormat = "$ {0}";

    public void Bind(string nombre, int precio, bool mostrarPrecio, bool alcanza)
    {
        if (nameText != null) nameText.text = nombre;
        if (priceText != null)
        {
            priceText.enabled = mostrarPrecio;
            if (mostrarPrecio) priceText.text = string.Format(priceFormat, precio);
        }

        // Lo que no alcanza se sigue viendo (para que se entienda que existe)
        // pero apagado.
        SetOptionEnabled(alcanza);
    }
}
