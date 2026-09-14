using UnityEngine;
using UnityEngine.UI;

// Un panel del equipo: sprite, nombre, nivel, barra de PS, estado.
//
// Es el MISMO widget para el menu Pokemon, para elegir objetivo de un objeto y
// para elegir relevo en combate. Lo unico que cambia es que le pide la pantalla.
public class PartyPanelWidget : MenuWidget
{
    [Header("Datos del Poken")]
    [SerializeField] private Image pokenSprite;
    [SerializeField] private Text nameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text hpText;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image statusIcon;
    [Tooltip("Se prende cuando este Poken es el que sale primero (slot 1).")]
    [SerializeField] private GameObject leaderMark;
    [Tooltip("Se prende cuando este Poken esta agarrado para intercambiar.")]
    [SerializeField] private GameObject swapMark;

    [Header("Colores de la barra de PS")]
    [SerializeField] private Color hpHigh = new Color(0.11f, 0.79f, 0.29f);
    [SerializeField] private Color hpMid = new Color(0.97f, 0.78f, 0.17f);
    [SerializeField] private Color hpLow = new Color(0.94f, 0.22f, 0.16f);

    [Header("Formatos")]
    [SerializeField] private string levelFormat = "Nv.{0}";
    [SerializeField] private string hpFormat = "{0}/ {1}";

    public PokenModel Poken { get; private set; }

    public void Bind(PokenModel poken)
    {
        Poken = poken;
        gameObject.SetActive(poken != null);
        if (poken == null) return;

        PokenDefinition def = poken.Definition;

        if (pokenSprite != null)
        {
            Sprite sprite = def != null ? poken.IconSprite : null;
            pokenSprite.sprite = sprite;
            pokenSprite.enabled = sprite != null;
            pokenSprite.preserveAspect = true;
        }

        if (nameText != null) nameText.text = def != null ? def.pokenName : "???";
        if (levelText != null) levelText.text = string.Format(levelFormat, poken.Level);
        if (hpText != null) hpText.text = string.Format(hpFormat, poken.CurrentHP, poken.MaxHP);

        if (hpFill != null)
        {
            float ratio = poken.MaxHP > 0 ? (float)poken.CurrentHP / poken.MaxHP : 0f;
            hpFill.fillAmount = Mathf.Clamp01(ratio);
            hpFill.color = ratio > 0.5f ? hpHigh : (ratio > 0.2f ? hpMid : hpLow);
        }

        MenuIconLibrary.PaintStatus(statusIcon, poken.CurrentStatus);

        SetLeader(false);
        SetSwapping(false);
    }

    public void SetLeader(bool isLeader)
    {
        if (leaderMark != null) leaderMark.SetActive(isLeader);
    }

    public void SetSwapping(bool swapping)
    {
        if (swapMark != null) swapMark.SetActive(swapping);
    }

    public void Clear()
    {
        Poken = null;
        gameObject.SetActive(false);
    }
}
