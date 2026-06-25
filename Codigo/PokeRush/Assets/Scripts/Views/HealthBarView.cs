using UnityEngine;
using UnityEngine.UI;

public class HealthBarView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Text hpText;

    public void SetHealth(int currentHP, int maxHP)
    {
        float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
        fillImage.fillAmount = Mathf.Clamp01(ratio);

        if (hpText != null)
            hpText.text = $"{currentHP}/{maxHP}";
    }
}