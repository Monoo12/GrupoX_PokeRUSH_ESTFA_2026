using UnityEngine;
using UnityEngine.UI;

// Caja de texto del mapa. Usa Text legacy a proposito: es lo mismo que usa el
// HUD de combate (BarraMensajes), asi los dos se ven igual sin arrastrar TMP.
//
// Ademas de mostrar lineas, sabe mostrar una pregunta con dos opciones (Si/No).
// La seleccion se dibuja con un prefijo y un color: no hace falta un objeto
// "selector" aparte, aunque se puede asignar uno si se quiere una flecha.
public class DialogueView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Text speakerNameText;
    [SerializeField] private Image speakerPortraitImage;
    [SerializeField] private Text dialogueLineText;

    [Header("Opciones (Si/No)")]
    [Tooltip("Contenedor de las dos opciones. Se prende solo cuando hay pregunta.")]
    [SerializeField] private GameObject choicePanel;
    [Tooltip("Un Text por opcion. Alcanza con dos.")]
    [SerializeField] private Text[] choiceTexts = new Text[2];

    [Header("Estilo de la seleccion")]
    [SerializeField] private string prefijoSeleccionado = "> ";
    [SerializeField] private Color colorSeleccionado = Color.white;
    [SerializeField] private Color colorNormal = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("Pista de avance")]
    [Tooltip("Cartelito tipo 'X para seguir'. Opcional.")]
    [SerializeField] private GameObject continueHint;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void DisplayLine(string speakerName, Sprite portrait, string line)
    {
        if (speakerNameText != null)
        {
            speakerNameText.text = speakerName;
            // Sin nombre no dejamos el hueco: queda un cartel de sistema.
            speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(speakerName));
        }

        if (dialogueLineText != null) dialogueLineText.text = line;

        if (speakerPortraitImage != null)
        {
            if (portrait != null)
            {
                speakerPortraitImage.sprite = portrait;
                speakerPortraitImage.gameObject.SetActive(true);
            }
            else
            {
                speakerPortraitImage.gameObject.SetActive(false);
            }
        }
    }

    // Prende el panel de opciones y marca cual esta elegida.
    public void ShowChoices(string[] opciones, int seleccion)
    {
        if (choicePanel != null) choicePanel.SetActive(true);
        if (continueHint != null) continueHint.SetActive(false);

        for (int i = 0; i < choiceTexts.Length; i++)
        {
            if (choiceTexts[i] == null) continue;

            bool hay = opciones != null && i < opciones.Length;
            choiceTexts[i].gameObject.SetActive(hay);
            if (!hay) continue;

            bool elegida = i == seleccion;
            choiceTexts[i].text = (elegida ? prefijoSeleccionado : "") + opciones[i];
            choiceTexts[i].color = elegida ? colorSeleccionado : colorNormal;
        }
    }

    public void HideChoices()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        if (continueHint != null) continueHint.SetActive(true);
    }
}
