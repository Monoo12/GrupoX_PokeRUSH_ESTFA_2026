using UnityEngine;
using UnityEngine.UI;

public class DialogueView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Text speakerNameText;
    [SerializeField] private Image speakerPortraitImage;
    [SerializeField] private Text dialogueLineText;

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
        speakerNameText.text = speakerName;
        dialogueLineText.text = line;

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