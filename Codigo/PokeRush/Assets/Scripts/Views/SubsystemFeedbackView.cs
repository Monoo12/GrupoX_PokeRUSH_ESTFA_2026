using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// El cartel que confirma que cortaste un subsistema.
//
//     FIREWALL - nodo 1 de 3 caido. Escudo del nucleo reducido.
//
// Parece decoracion y es lo contrario: sin esto el jugador nunca conecta lo que
// hizo en la torre con lo que pasa en el combate, y toda la estructura del
// nivel se vuelve invisible. Es un Text y un fade, cuesta nada.
//
// Uso: un GameObject con este componente en la escena de la torre. Los
// InteractableTrigger lo encuentran solos; no hace falta arrastrarlo a cada uno.
public class SubsystemFeedbackView : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Panel que se prende y apaga. Si esta vacio se usa este GameObject.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [Tooltip("Opcional: para el fundido. Sin esto el cartel aparece y desaparece de golpe.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tiempos")]
    [SerializeField] private float fadeIn = 0.2f;
    [SerializeField] private float hold = 2.4f;
    [SerializeField] private float fadeOut = 0.5f;

    private Coroutine _running;

    private void Awake()
    {
        if (panel == null) panel = gameObject;
        panel.SetActive(false);
    }

    // Lo llaman los InteractableTrigger. Si ya habia un cartel en pantalla lo
    // corta y muestra el nuevo: es preferible a encolarlos, porque el jugador
    // que corta dos cosas seguidas quiere ver la ultima.
    public void Show(string titulo, string cuerpo)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(ShowRoutine(titulo, cuerpo));
    }

    private IEnumerator ShowRoutine(string titulo, string cuerpo)
    {
        if (titleText != null) titleText.text = titulo;
        if (bodyText != null) bodyText.text = cuerpo;
        panel.SetActive(true);

        yield return Fade(0f, 1f, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return Fade(1f, 0f, fadeOut);

        panel.SetActive(false);
        _running = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = to;
            yield break;
        }

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
