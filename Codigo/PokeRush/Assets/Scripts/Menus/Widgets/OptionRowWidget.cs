using UnityEngine;
using UnityEngine.UI;

// Una fila del menu de Opciones: etiqueta a la izquierda, valor a la derecha y
// (opcional) una barra para los valores numericos como el volumen.
public class OptionRowWidget : MenuWidget
{
    [Header("Fila")]
    [SerializeField] private Text labelText;
    [SerializeField] private Text valueText;
    [Tooltip("Barra de relleno para valores 0-1 (volumen). Opcional.")]
    [SerializeField] private Image valueFill;
    [Tooltip("Raiz de la barra (el fondo). Se apaga entera en las filas que no " +
             "tienen valor numerico; si solo se apagara el relleno, el fondo " +
             "quedaria dibujado en todas las filas.")]
    [SerializeField] private GameObject valueBar;
    [Tooltip("Flechitas de < y >. Se prenden solo en las filas que se ajustan.")]
    [SerializeField] private GameObject arrows;
    [Tooltip("Se prende en las filas que son solo un titulo de seccion.")]
    [SerializeField] private GameObject headerMark;

    public void Bind(string label, string value, float fill01 = -1f,
                     bool showArrows = false, bool isHeader = false)
    {
        if (labelText != null) labelText.text = label;

        if (valueText != null)
        {
            valueText.enabled = value != null;
            if (value != null) valueText.text = value;
        }

        if (valueBar != null) valueBar.SetActive(fill01 >= 0f);

        if (valueFill != null)
        {
            valueFill.enabled = fill01 >= 0f;
            if (fill01 >= 0f) valueFill.fillAmount = Mathf.Clamp01(fill01);
        }

        if (arrows != null) arrows.SetActive(showArrows);
        if (headerMark != null) headerMark.SetActive(isHeader);

        SetOptionEnabled(!isHeader);
    }
}
