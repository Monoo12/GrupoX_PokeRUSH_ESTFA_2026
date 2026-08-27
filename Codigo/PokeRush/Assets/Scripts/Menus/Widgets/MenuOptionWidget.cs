using UnityEngine;
using UnityEngine.UI;

// Una opcion de menu: icono + texto. Es la fila del menu de pausa y de los
// submenus (USAR / CANCELAR, SI / NO...).
//
// El id es lo que identifica la opcion para la logica. Agregar una opcion nueva
// al menu de pausa = duplicar este widget en el prefab y ponerle un id: no hay
// indices hardcodeados en el codigo.
public class MenuOptionWidget : MenuWidget
{
    [Header("Opcion")]
    [Tooltip("Identificador logico de la opcion (ej: Pokemon, Bolsa, Guardar). " +
             "La pantalla lo lee para saber que hacer; el texto visible puede ser otro.")]
    [SerializeField] private string optionId;
    [SerializeField] private Text label;
    [SerializeField] private Image icon;

    public string OptionId => optionId;

    public void SetText(string text)
    {
        if (label != null) label.text = text;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    // Para las opciones que se arman por codigo (submenus dinamicos).
    //
    // SetIcon se llama SIEMPRE, tambien con sprite null: si no, el submenu
    // (USAR/CANCELAR, DATOS/CANCELAR) heredaba el Image del prefab prendido y
    // se veia un cuadrado blanco al lado de cada opcion.
    public void Bind(string id, string text, Sprite sprite = null)
    {
        optionId = id;
        SetText(text);
        SetIcon(sprite);
    }
}
