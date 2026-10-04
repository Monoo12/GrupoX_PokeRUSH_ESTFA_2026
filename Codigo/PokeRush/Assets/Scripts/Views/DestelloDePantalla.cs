using System.Collections;
using UnityEngine;

// Pulsos de color sobre todo el mapa: la alarma de la sede de la Liga
// ("Las alarmas comienzan a sonar").
//
// Como ApagonDelNivel, es un rectangulo pegado a la camara en el MUNDO y no un
// Canvas: tiñe el mapa y los personajes pero no la caja de dialogo, que sigue
// leyendose. Se crea, late las veces pedidas y se destruye solo.
public class DestelloDePantalla : MonoBehaviour
{
    // Por encima de todo lo del mapa (el jugador va en 15) y del apagon (500).
    private const int Orden = 510;

    private SpriteRenderer _velo;

    public static void Pulsar(Color color, int veces, float segundosPorPulso = 0.7f)
    {
        if (veces <= 0) return;
        var go = new GameObject("[DestelloDePantalla]");
        var d = go.AddComponent<DestelloDePantalla>();
        d.StartCoroutine(d.Correr(color, veces, Mathf.Max(0.1f, segundosPorPulso)));
    }

    private IEnumerator Correr(Color color, int veces, float segundos)
    {
        _velo = gameObject.AddComponent<SpriteRenderer>();
        _velo.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        _velo.sortingOrder = Orden;

        float total = veces * segundos;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            Seguir();
            // Sube rapido y baja lento, como una luz giratoria.
            float k = (t % segundos) / segundos;
            float a = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f;
            _velo.color = new Color(color.r, color.g, color.b, color.a * 0.45f * a);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void Seguir()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float alto = cam.orthographic ? cam.orthographicSize * 2f + 2f : 40f;
        float ancho = alto * Mathf.Max(1f, cam.aspect) + 2f;
        Vector3 p = cam.transform.position;
        transform.position = new Vector3(p.x, p.y, 0f);
        transform.localScale = new Vector3(ancho, alto, 1f);
    }
}
