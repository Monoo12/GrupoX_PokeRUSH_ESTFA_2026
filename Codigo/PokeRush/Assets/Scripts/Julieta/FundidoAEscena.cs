using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fundido a negro y cambio de escena. Vive solo durante el fundido: se crea,
// sobrevive la carga, aclara la escena nueva y se destruye.
public class FundidoAEscena : MonoBehaviour
{
    public static bool EnCurso { get; private set; }

    public static void Ir(string escena, float segundos)
    {
        if (EnCurso) return;
        var go = new GameObject("[FundidoAEscena]");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        RectTransform negro = LienzoDePeleaJulieta.Rect(go.transform, "Negro", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image img = LienzoDePeleaJulieta.Imagen(negro, new Color(0f, 0f, 0f, 0f));
        img.raycastTarget = true;   // mientras funde, no se toca nada
        go.AddComponent<FundidoAEscena>().StartCoroutine(Correr(go, img, escena, Mathf.Max(0.05f, segundos)));
    }

    private static IEnumerator Correr(GameObject go, Image negro, string escena, float segundos)
    {
        EnCurso = true;
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            negro.color = new Color(0f, 0f, 0f, t / segundos);
            yield return null;
        }
        negro.color = Color.black;

        AsyncOperation carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(escena);
        while (carga != null && !carga.isDone) yield return null;

        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            negro.color = new Color(0f, 0f, 0f, 1f - t / segundos);
            yield return null;
        }
        EnCurso = false;
        Destroy(go);
    }
}
