using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// La vida propia del PJ, abajo a la izquierda: del lado del jugador, arriba de
// la barra de mensajes y a la izquierda de su Pokemon. Arriba al centro vive
// el panel de la Prediccion, y arriba a los costados los datos del rival y el
// retrato de Nestor.
public class VidaDelPJView : MonoBehaviour
{
    private RectTransform _relleno;
    private Image _rellenoImg;
    private Text _nombre;
    private Text _numeros;
    private RectTransform _raiz;
    private Vector2 _posicionBase;
    private float _mostrado = -1f;
    private int _maxima = 1;

    public bool Visible => gameObject.activeSelf;

    public static VidaDelPJView Armar(Transform padre, Font fuente, Color colorDeJulieta)
    {
        RectTransform raiz = LienzoDePeleaJulieta.Rect(padre, "VidaDelPJ",
            new Vector2(0f, 0.27f), new Vector2(0f, 0.27f), new Vector2(0f, 0f),
            new Vector2(18f, 0f), new Vector2(420f, 58f));
        LienzoDePeleaJulieta.Imagen(raiz, new Color(0.07f, 0.05f, 0.1f, 0.88f));

        RectTransform borde = LienzoDePeleaJulieta.Rect(raiz, "Borde", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        LienzoDePeleaJulieta.Imagen(borde, new Color(colorDeJulieta.r, colorDeJulieta.g, colorDeJulieta.b, 0.55f));
        borde.SetAsFirstSibling();

        var vista = raiz.gameObject.AddComponent<VidaDelPJView>();
        vista._raiz = raiz;
        vista._posicionBase = raiz.anchoredPosition;

        RectTransform nombre = LienzoDePeleaJulieta.Rect(raiz, "Nombre", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(12f, -4f), new Vector2(260f, 26f));
        vista._nombre = LienzoDePeleaJulieta.Texto(nombre, fuente, 20, TextAnchor.UpperLeft, Color.white);

        RectTransform numeros = LienzoDePeleaJulieta.Rect(raiz, "Numeros", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-12f, -4f), new Vector2(140f, 26f));
        vista._numeros = LienzoDePeleaJulieta.Texto(numeros, fuente, 20, TextAnchor.UpperRight, Color.white);

        RectTransform fondo = LienzoDePeleaJulieta.Rect(raiz, "Barra", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(-24f, 14f));
        LienzoDePeleaJulieta.Imagen(fondo, new Color(0.18f, 0.16f, 0.2f, 1f));

        vista._relleno = LienzoDePeleaJulieta.Rect(fondo, "Relleno", Vector2.zero, Vector2.one,
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        vista._rellenoImg = LienzoDePeleaJulieta.Imagen(vista._relleno, new Color(0.35f, 0.9f, 0.45f));

        raiz.gameObject.SetActive(false);
        return vista;
    }

    public void Mostrar(bool si) => gameObject.SetActive(si);

    // Refresco instantaneo.
    public void Poner(string nombreDelPJ, int actual, int maxima)
    {
        _maxima = Mathf.Max(1, maxima);
        _nombre.text = $"{nombreDelPJ} · VIDA";
        Dibujar(actual);
    }

    // La barra drena de 'desde' a 'hasta' y el recuadro se sacude.
    public IEnumerator Golpe(int desde, int hasta, float segundos = 0.6f)
    {
        if (!gameObject.activeInHierarchy) yield break;

        float t = 0f;
        while (t < segundos)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / segundos);
            Dibujar(Mathf.Lerp(desde, hasta, k));
            float temblor = (1f - k) * 9f;
            _raiz.anchoredPosition = _posicionBase + new Vector2(Random.Range(-temblor, temblor), Random.Range(-temblor, temblor) * 0.5f);
            yield return null;
        }
        _raiz.anchoredPosition = _posicionBase;
        Dibujar(hasta);
    }

    private void Dibujar(float valor)
    {
        _mostrado = Mathf.Clamp(valor, 0f, _maxima);
        float k = _mostrado / _maxima;
        _relleno.anchorMax = new Vector2(k, 1f);
        _relleno.sizeDelta = Vector2.zero;
        _rellenoImg.color = k > 0.5f ? new Color(0.35f, 0.9f, 0.45f)
                          : k > 0.25f ? new Color(0.98f, 0.8f, 0.25f)
                          : new Color(0.95f, 0.3f, 0.3f);
        _numeros.text = $"{Mathf.CeilToInt(_mostrado)}/{_maxima}";
    }
}
