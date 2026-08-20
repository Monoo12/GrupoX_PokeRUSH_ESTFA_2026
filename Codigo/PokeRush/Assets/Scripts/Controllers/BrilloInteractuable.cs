using UnityEngine;

// Un brillito sobre las cosas con las que se puede interactuar y todavia no se
// usaron: los paneles que cortan los subsistemas de la torre.
//
// POR QUE EXISTE
// Esos puntos NO tienen sprite propio — son colliders invisibles puestos sobre
// los tiles del mapa. Sin una marca, el jugador no tiene forma de saber que ahi
// se puede apretar X, y el mapa es grande. El brillo aparece mientras el
// subsistema sigue en pie y se apaga solo al cortarlo, asi que ademas sirve de
// checklist visual de lo que falta.
//
// USO
//   1. Poner el componente en el mismo objeto que el InteractableTrigger.
//   2. Nada mas: el flag lo toma del trigger, y los sprites tienen valores por
//      defecto. Todo es ajustable desde el Inspector si hace falta.
//
// El visual se arma en runtime como hijos: asi no ensucia la escena con dos
// GameObjects por cada panel, y agregar un panel nuevo es un solo componente.
public class BrilloInteractuable : MonoBehaviour
{
    [Header("Cuando deja de brillar")]
    [Tooltip("None = lo toma del InteractableTrigger que este en este objeto.")]
    [SerializeField] private StoryFlag flagQueLoApaga = StoryFlag.None;

    [Header("Aspecto")]
    [SerializeField] private Sprite spriteHalo;
    [SerializeField] private Sprite spriteEstrella;
    [SerializeField] private Color colorHalo = new Color(1f, 0.86f, 0.45f, 0.5f);
    [SerializeField] private Color colorEstrella = new Color(1f, 0.97f, 0.8f, 0.95f);
    [Tooltip("Alto del halo en tiles.")]
    [SerializeField] private float tamanioHalo = 1.15f;
    [Tooltip("Alto de la estrellita en tiles.")]
    [SerializeField] private float tamanioEstrella = 0.4f;
    [Tooltip("Encima de Colisiones (10) y debajo del jugador (15).")]
    [SerializeField] private int sortingOrder = 12;
    [SerializeField] private Vector2 desplazamiento = new Vector2(0f, 0.1f);

    [Header("Ritmo")]
    [Tooltip("Segundos que tarda un latido completo.")]
    [SerializeField] private float periodo = 1.7f;
    [Tooltip("Cuanto crece y se achica el halo, en porcentaje.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float amplitud = 0.18f;
    [SerializeField] private float girosPorMinuto = 12f;

    private StoryModel _story;
    private SpriteRenderer _halo;
    private SpriteRenderer _estrella;
    private float _escalaHaloBase;
    private float _fase;   // arranca distinto en cada panel para que no latan al unisono
    private bool _apagado;

    private void Awake()
    {
        // El flag sale del propio trigger: escribirlo dos veces es una forma
        // segura de que algun dia queden distintos.
        if (flagQueLoApaga == StoryFlag.None)
        {
            var trigger = GetComponent<InteractableTrigger>();
            if (trigger != null) flagQueLoApaga = trigger.OtorgaFlag;
        }

        if (flagQueLoApaga == StoryFlag.None)
            Debug.LogWarning($"[Brillo] '{name}' no tiene flag: va a brillar para siempre.");

        // Desfase estable por objeto (no aleatorio): asi se ve organico pero
        // siempre igual entre partidas.
        _fase = Mathf.Abs(name.GetHashCode() % 100) / 100f * Mathf.PI * 2f;

        Armar();
    }

    private void Armar()
    {
        if (spriteHalo != null)
        {
            _halo = NuevoSprite("BrilloHalo", spriteHalo, colorHalo, tamanioHalo);
            _escalaHaloBase = _halo.transform.localScale.x;
        }
        if (spriteEstrella != null)
            _estrella = NuevoSprite("BrilloEstrella", spriteEstrella, colorEstrella, tamanioEstrella);
    }

    private SpriteRenderer NuevoSprite(string nombre, Sprite sprite, Color color, float altoEnTiles)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = desplazamiento;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        float alto = sprite.bounds.size.y;
        float escala = alto > 0f ? altoEnTiles / alto : 1f;
        go.transform.localScale = new Vector3(escala, escala, 1f);
        return sr;
    }

    // Se resuelve el StoryModel en Update y no en Start: lo crea
    // SceneBootstrapper en SU Start y el orden entre dos Start no esta
    // garantizado. Es el mismo motivo que en BarreraPorFlag.
    private void Update()
    {
        if (_apagado) return;

        if (_story == null)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Story != null)
            {
                _story = gm.Story;
                _story.FlagActivated += OnFlagActivada;
                if (_story.HasFlag(flagQueLoApaga)) { Apagar(); return; }
            }
        }

        Latir();
    }

    private void OnDestroy()
    {
        if (_story != null) _story.FlagActivated -= OnFlagActivada;
    }

    private void OnFlagActivada(StoryFlag flag)
    {
        if (flag == flagQueLoApaga) Apagar();
    }

    private void Latir()
    {
        float t = Time.time / Mathf.Max(0.05f, periodo) * Mathf.PI * 2f + _fase;
        float onda = (Mathf.Sin(t) + 1f) * 0.5f;   // 0..1

        if (_halo != null)
        {
            float escala = _escalaHaloBase * (1f + amplitud * (onda - 0.5f) * 2f);
            _halo.transform.localScale = new Vector3(escala, escala, 1f);

            var c = colorHalo;
            c.a = colorHalo.a * Mathf.Lerp(0.55f, 1f, onda);
            _halo.color = c;
        }

        if (_estrella != null)
        {
            // La estrella va en contrafase: cuando el halo baja, ella destella.
            float destello = (Mathf.Sin(t + Mathf.PI) + 1f) * 0.5f;
            var c = colorEstrella;
            c.a = colorEstrella.a * Mathf.Lerp(0.35f, 1f, destello);
            _estrella.color = c;

            _estrella.transform.localRotation =
                Quaternion.Euler(0f, 0f, Time.time * girosPorMinuto * 6f);
        }
    }

    private void Apagar()
    {
        _apagado = true;
        if (_halo != null) _halo.gameObject.SetActive(false);
        if (_estrella != null) _estrella.gameObject.SetActive(false);
    }
}
