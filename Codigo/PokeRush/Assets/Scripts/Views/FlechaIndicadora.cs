using UnityEngine;

// Una flecha que va y viene señalando algo del mapa: la salida de la
// habitacion tutorial (playtest del 4-oct: no se encontraba la puerta).
//
// Anima los cuadros que tenga (la hoja de Graphics/UI/right_arrow.png trae
// ocho) y se mueve unos pixeles hacia donde apunta y vuelve. Se puede atar a
// los flags de la partida: aparece con 'apareceCon' y se va con 'seVaCon'
// (None = no depende de nada).
[RequireComponent(typeof(SpriteRenderer))]
public class FlechaIndicadora : MonoBehaviour
{
    [SerializeField] private Sprite[] cuadros = new Sprite[0];
    [SerializeField, Min(1f)] private float cuadrosPorSegundo = 10f;

    [Tooltip("Hacia donde apunta: se mueve en esa direccion y vuelve.")]
    [SerializeField] private Vector2 direccion = Vector2.right;
    [SerializeField, Min(0f)] private float recorrido = 0.25f;
    [SerializeField, Min(0.1f)] private float periodo = 0.9f;

    [Header("Cuando se ve")]
    [SerializeField] private StoryFlag apareceCon = StoryFlag.None;
    [SerializeField] private StoryFlag seVaCon = StoryFlag.None;

    private SpriteRenderer _sr;
    private Vector3 _base;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _base = transform.localPosition;
    }

    private void Update()
    {
        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        bool visible = (apareceCon == StoryFlag.None || (story != null && story.HasFlag(apareceCon)))
                       && (seVaCon == StoryFlag.None || story == null || !story.HasFlag(seVaCon));
        _sr.enabled = visible;
        if (!visible) return;

        float t = Time.time;
        if (cuadros != null && cuadros.Length > 0)
            _sr.sprite = cuadros[(int)(t * cuadrosPorSegundo) % cuadros.Length];

        float ida = (Mathf.Sin(t * Mathf.PI * 2f / periodo) + 1f) * 0.5f;
        transform.localPosition = _base + (Vector3)(direccion.normalized * (recorrido * ida));
    }
}
