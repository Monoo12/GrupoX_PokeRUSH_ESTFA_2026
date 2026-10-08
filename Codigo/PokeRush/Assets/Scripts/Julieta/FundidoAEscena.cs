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
        Image img = CrearNegro(out GameObject go);
        go.AddComponent<FundidoAEscena>().StartCoroutine(Correr(go, img, escena, Mathf.Max(0.05f, segundos)));
    }

    // Funde a negro y cierra el juego (en el editor, sale de Play). La usan
    // las cruces de salir del titulo y del menu de partidas.
    public static void SalirDelJuego(float segundos)
    {
        if (EnCurso) return;
        Image img = CrearNegro(out GameObject go);
        go.AddComponent<FundidoAEscena>().StartCoroutine(Cerrar(img, Mathf.Max(0.05f, segundos)));
    }

    private static Image CrearNegro(out GameObject go)
    {
        go = new GameObject("[FundidoAEscena]");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        RectTransform negro = LienzoDePeleaJulieta.Rect(go.transform, "Negro", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image img = LienzoDePeleaJulieta.Imagen(negro, new Color(0f, 0f, 0f, 0f));
        img.raycastTarget = true;   // mientras funde, no se toca nada
        return img;
    }

    private static IEnumerator Cerrar(Image negro, float segundos)
    {
        EnCurso = true;
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            negro.color = new Color(0f, 0f, 0f, t / segundos);
            yield return null;
        }
        negro.color = Color.black;

        // El proyecto no recarga el dominio al entrar a Play: si EnCurso quedara
        // en true, el proximo Play no podria fundir a ninguna escena.
        EnCurso = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
