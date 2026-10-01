using UnityEngine;

// Un "respirar" suave: la escala (y opcionalmente la transparencia) va y viene.
// Es lo que hace que un PRESS START o un "Continuar" se lean como algo vivo que
// espera al jugador, y no como un dibujo quieto.
//
// Tambien sabe destellar: Destellar() lo hace parpadear rapido unas veces, el
// clasico "aceptado" de las pantallas de titulo.
public class PulsoSuave : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float periodo = 1.8f;
    [SerializeField] private float escalaMinima = 1f;
    [SerializeField] private float escalaMaxima = 1.035f;
    [Tooltip("Opcional: tambien la transparencia (necesita un CanvasGroup).")]
    [SerializeField, Range(0f, 1f)] private float alfaMinimo = 1f;

    private Vector3 _escalaBase = Vector3.one;
    private CanvasGroup _grupo;
    private float _destelloHasta = -1f;

    private void Awake()
    {
        _escalaBase = transform.localScale;
        _grupo = GetComponent<CanvasGroup>();
    }

    private void OnDisable() => transform.localScale = _escalaBase;

    private void Update()
    {
        float t = Time.unscaledTime;

        if (t < _destelloHasta)
        {
            // Parpadeo seco: prendido / apagado cada 70 ms, un poco mas grande.
            bool prendido = Mathf.FloorToInt(t / 0.07f) % 2 == 0;
            transform.localScale = _escalaBase * escalaMaxima * 1.03f;
            if (_grupo != null) _grupo.alpha = prendido ? 1f : 0.15f;
            return;
        }

        float k = 0.5f - 0.5f * Mathf.Cos(t / periodo * Mathf.PI * 2f);
        transform.localScale = _escalaBase * Mathf.Lerp(escalaMinima, escalaMaxima, k);
        if (_grupo != null) _grupo.alpha = Mathf.Lerp(alfaMinimo, 1f, k);
    }

    public void Destellar(float segundos = 0.5f) => _destelloHasta = Time.unscaledTime + segundos;
}
