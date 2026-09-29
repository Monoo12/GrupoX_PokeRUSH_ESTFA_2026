using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// La señal de que Julieta tiene el control: un marco violeta que late
// alrededor de la pantalla y un cartel a la izquierda. Mas el golpe de "glitch" de los
// momentos fuertes (un destello violeta y el cartel temblando).
public class SenalDeControlView : MonoBehaviour
{
    private Image[] _bordes;
    private Image _destello;
    private Text _cartel;
    private RectTransform _cartelRect;
    private Color _color;
    private bool _encendida;

    public bool Encendida => _encendida;
    public string TextoDelCartel => _cartel != null ? _cartel.text : null;

    public static SenalDeControlView Armar(Transform padre, Font fuente, Color colorDeJulieta)
    {
        RectTransform raiz = LienzoDePeleaJulieta.Rect(padre, "SenalDeControl", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var vista = raiz.gameObject.AddComponent<SenalDeControlView>();
        vista._color = colorDeJulieta;

        vista._destello = LienzoDePeleaJulieta.Imagen(
            LienzoDePeleaJulieta.Rect(raiz, "Destello", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                                      Vector2.zero, Vector2.zero),
            new Color(colorDeJulieta.r, colorDeJulieta.g, colorDeJulieta.b, 0f));

        const float grosor = 14f;
        vista._bordes = new[]
        {
            Borde(raiz, "Arriba", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, grosor)),
            Borde(raiz, "Abajo", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, grosor)),
            Borde(raiz, "Izquierda", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(grosor, 0f)),
            Borde(raiz, "Derecha", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(grosor, 0f)),
        };
        foreach (Image b in vista._bordes) b.color = colorDeJulieta;

        // A la izquierda, debajo de los datos del rival: arriba al centro esta
        // el panel de la Prediccion, que en esta fase tambien esta prendido.
        vista._cartelRect = LienzoDePeleaJulieta.Rect(raiz, "Cartel", new Vector2(0f, 0.72f), new Vector2(0f, 0.72f),
            new Vector2(0f, 1f), new Vector2(18f, 0f), new Vector2(620f, 36f));
        LienzoDePeleaJulieta.Imagen(vista._cartelRect, new Color(0.05f, 0.02f, 0.08f, 0.85f));
        RectTransform texto = LienzoDePeleaJulieta.Rect(vista._cartelRect, "Texto", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        vista._cartel = LienzoDePeleaJulieta.Texto(texto, fuente, 20, TextAnchor.MiddleCenter,
            Color.Lerp(colorDeJulieta, Color.white, 0.45f));

        vista.Apagar();
        return vista;
    }

    private static Image Borde(RectTransform padre, string nombre, Vector2 min, Vector2 max, Vector2 pivote, Vector2 tamanio)
        => LienzoDePeleaJulieta.Imagen(LienzoDePeleaJulieta.Rect(padre, nombre, min, max, pivote, Vector2.zero, tamanio),
                                       Color.clear);

    public void Encender(string cartel)
    {
        _encendida = true;
        _cartel.text = cartel;
        _cartelRect.gameObject.SetActive(true);
        foreach (Image b in _bordes) b.gameObject.SetActive(true);
    }

    public void Apagar()
    {
        _encendida = false;
        if (_cartelRect != null) _cartelRect.gameObject.SetActive(false);
        if (_bordes != null) foreach (Image b in _bordes) b.gameObject.SetActive(false);
        if (_destello != null) _destello.color = new Color(_color.r, _color.g, _color.b, 0f);
    }

    private void Update()
    {
        if (!_encendida) return;

        // Late lento: tiene que notarse sin tapar la pelea.
        float a = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f);
        foreach (Image b in _bordes) b.color = new Color(_color.r, _color.g, _color.b, a);
    }

    // El golpe de los momentos fuertes: destello violeta y el cartel temblando.
    public IEnumerator Destello(float segundos = 0.7f)
    {
        if (!gameObject.activeInHierarchy) yield break;

        Vector2 base_ = _cartelRect.anchoredPosition;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / segundos);
            // Tres picos que se apagan: se lee como una interferencia, no como un fundido.
            float pico = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k);
            _destello.color = new Color(_color.r, _color.g, _color.b, 0.55f * pico);
            _cartelRect.anchoredPosition = base_ + new Vector2(Random.Range(-14f, 14f) * (1f - k), 0f);
            yield return null;
        }
        _destello.color = new Color(_color.r, _color.g, _color.b, 0f);
        _cartelRect.anchoredPosition = base_;
    }
}
