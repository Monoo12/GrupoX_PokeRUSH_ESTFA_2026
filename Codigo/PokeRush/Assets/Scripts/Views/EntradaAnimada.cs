using System.Collections;
using UnityEngine;

// Hace que un panel (o una ficha) entre a pantalla en vez de aparecer de golpe:
// se desliza un poco desde 'desde' y se vuelve visible. Mientras entra no se
// puede tocar, asi un clic apurado no cae en un boton que todavia se esta
// acomodando.
//
// Entra solo cada vez que se prende (OnEnable). Con 'retraso' distintos en
// varias fichas se consigue la entrada escalonada. Salir() es la inversa, para
// cuando la pantalla quiere animar la salida antes de apagarlo.
[RequireComponent(typeof(RectTransform))]
public class EntradaAnimada : MonoBehaviour
{
    [SerializeField, Min(0f)] private float retraso = 0f;
    [SerializeField, Min(0.01f)] private float duracion = 0.28f;
    [Tooltip("Desde donde llega, relativo a su lugar (en unidades del Canvas).")]
    [SerializeField] private Vector2 desde = new Vector2(0f, -28f);

    private RectTransform _rt;
    private CanvasGroup _grupo;
    private Vector2 _lugar;
    private bool _lugarGuardado;
    private Coroutine _animacion;

    public float Duracion => retraso + duracion;

    private void Awake() => Preparar();

    private void Preparar()
    {
        if (_rt != null) return;
        _rt = (RectTransform)transform;
        _grupo = GetComponent<CanvasGroup>();
        if (_grupo == null) _grupo = gameObject.AddComponent<CanvasGroup>();
    }

    private void GuardarLugar()
    {
        if (_lugarGuardado) return;
        _lugar = _rt.anchoredPosition;
        _lugarGuardado = true;
    }

    private void OnEnable()
    {
        Preparar();
        GuardarLugar();
        if (_animacion != null) StopCoroutine(_animacion);
        _animacion = StartCoroutine(Entrar());
    }

    private void OnDisable()
    {
        // Apagado a mitad de la entrada: la proxima vez arranca de su lugar.
        if (_lugarGuardado) _rt.anchoredPosition = _lugar;
        if (_grupo != null) { _grupo.alpha = 1f; _grupo.interactable = true; _grupo.blocksRaycasts = true; }
    }

    private IEnumerator Entrar()
    {
        _grupo.alpha = 0f;
        _grupo.interactable = false;
        _grupo.blocksRaycasts = false;
        _rt.anchoredPosition = _lugar + desde;

        for (float t = 0f; t < retraso; t += Time.unscaledDeltaTime) yield return null;

        for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
        {
            float k = SalidaCubica(t / duracion);
            _grupo.alpha = k;
            _rt.anchoredPosition = Vector2.LerpUnclamped(_lugar + desde, _lugar, k);
            yield return null;
        }

        _grupo.alpha = 1f;
        _rt.anchoredPosition = _lugar;
        _grupo.interactable = true;
        _grupo.blocksRaycasts = true;
        _animacion = null;
    }

    // La salida: se va un poco para el otro lado y se desvanece. No lo apaga:
    // eso lo decide quien llamo, cuando termina.
    public IEnumerator Salir(float segundos = 0.16f)
    {
        Preparar();
        GuardarLugar();
        if (_animacion != null) { StopCoroutine(_animacion); _animacion = null; }

        _grupo.interactable = false;
        _grupo.blocksRaycasts = false;
        float alfaInicial = _grupo.alpha;
        Vector2 inicio = _rt.anchoredPosition;
        Vector2 fin = _lugar - desde * 0.5f;

        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            float k = t / segundos;
            _grupo.alpha = Mathf.Lerp(alfaInicial, 0f, k);
            _rt.anchoredPosition = Vector2.Lerp(inicio, fin, k * k);
            yield return null;
        }
        _grupo.alpha = 0f;
    }

    private static float SalidaCubica(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }
}
