using UnityEngine;
using UnityEngine.UI;

// Base de todo elemento seleccionable de un menu (una opcion, una fila de la
// mochila, un panel del equipo, un movimiento).
//
// La logica de los menus solo llama SetSelected(true/false). COMO se ve eso
// selecionado se arma en el Inspector: prender un objeto, cambiar el sprite de
// un fondo, cambiar el color de un texto, o las tres cosas. Asi el equipo puede
// rehacer el look sin tocar una linea de codigo.
public class MenuWidget : MonoBehaviour
{
    [Header("Seleccion: objetos que se prenden")]
    [Tooltip("Se activan cuando el elemento esta seleccionado (cursor, marco, brillo).")]
    [SerializeField] private GameObject[] selectedObjects;
    [Tooltip("Se activan cuando NO esta seleccionado.")]
    [SerializeField] private GameObject[] deselectedObjects;

    [Header("Seleccion: cambio de sprite")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Seleccion: cambio de color de texto")]
    [SerializeField] private Graphic[] tintedGraphics;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.white;

    [Header("Deshabilitado")]
    [Tooltip("Color con el que se pinta el elemento cuando no se puede elegir.")]
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private CanvasGroup canvasGroup;

    public bool IsSelected { get; private set; }
    public bool IsEnabledOption { get; private set; } = true;

    // Indice que ocupa dentro de la lista que lo creo. Lo setea la pantalla.
    public int Index { get; set; }

    public virtual void SetSelected(bool selected)
    {
        IsSelected = selected;
        ApplyVisuals();
    }

    // Un elemento deshabilitado se sigue viendo (para que se entienda por que
    // no se puede elegir) pero apagado.
    public virtual void SetOptionEnabled(bool enabledOption)
    {
        IsEnabledOption = enabledOption;
        ApplyVisuals();
    }

    protected void ApplyVisuals()
    {
        foreach (GameObject go in selectedObjects)
            if (go != null) go.SetActive(IsSelected);

        foreach (GameObject go in deselectedObjects)
            if (go != null) go.SetActive(!IsSelected);

        if (backgroundImage != null)
        {
            Sprite sprite = IsSelected && selectedSprite != null ? selectedSprite : normalSprite;
            if (sprite != null) backgroundImage.sprite = sprite;
        }

        Color color = IsSelected ? selectedColor : normalColor;
        if (!IsEnabledOption) color = disabledColor;
        foreach (Graphic g in tintedGraphics)
            if (g != null) g.color = color;

        if (canvasGroup != null) canvasGroup.alpha = IsEnabledOption ? 1f : 0.55f;
    }
}
