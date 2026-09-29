using System.Linq;
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
//   1. Poner el componente en el mismo objeto que el InteractableTrigger (o que
//      la TutorialParte, en la habitacion tutorial).
//   2. Nada mas: el flag lo toma de ese componente y los sprites tienen valores
//      por defecto. 'tamanioHalo' y 'tamanioEstrella' regulan que tan grande es.
//
// SE VE MIENTRAS LO EDITAS
// [ExecuteAlways] hace que el brillo se arme tambien fuera de Play, y los
// tamanios se re-aplican en cada frame: se mueve el slider en el Inspector y se
// ve el cambio en la Scene view, sin entrar y salir de Play para calibrar.
//
// Los hijos se crean con HideAndDontSave: no ensucian la jerarquia ni se
// guardan en la escena, asi que no quedan dos GameObjects por panel en el .unity
// ni se duplican al recompilar.
[ExecuteAlways]
public class BrilloInteractuable : MonoBehaviour
{
    [Header("Cuando deja de brillar")]
    [Tooltip("None = lo toma del InteractableTrigger o de la TutorialParte que este en este objeto.")]
    [SerializeField] private StoryFlag flagQueLoApaga = StoryFlag.None;

    [Header("Cuando empieza a brillar (opcional)")]
    [Tooltip("Al reves: no brilla hasta que se activa este flag. Es la salida de un " +
             "nivel terminado. None = lo toma de la SalidaDelNivel de al lado, y si no " +
             "hay, brilla desde el principio.")]
    [SerializeField] private StoryFlag flagQueLoPrende = StoryFlag.None;

    [Header("Tamanio (en tiles)")]
    [Tooltip("Alto del halo. 1 = un tile.")]
    [Range(0.1f, 4f)]
    [SerializeField] private float tamanioHalo = 1.15f;
    [Tooltip("Alto de la estrellita. 0 = sin estrella.")]
    [Range(0f, 2f)]
    [SerializeField] private float tamanioEstrella = 0.4f;
    [Tooltip("Escala todo junto, para agrandar o achicar sin tocar los dos de arriba.")]
    [Range(0.1f, 4f)]
    [SerializeField] private float escalaGeneral = 1f;

    [Header("Aspecto")]
    [SerializeField] private Sprite spriteHalo;
    [SerializeField] private Sprite spriteEstrella;
    [SerializeField] private Color colorHalo = new Color(1f, 0.86f, 0.45f, 0.5f);
    [SerializeField] private Color colorEstrella = new Color(1f, 0.97f, 0.8f, 0.95f);
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

    [Header("Editor")]
    [Tooltip("Mostrarlo tambien fuera de Play, para poder calibrar el tamanio.")]
    [SerializeField] private bool verEnElEditor = true;

    private SpriteRenderer _halo;
    private SpriteRenderer _estrella;
    private float _fase;      // desfase por objeto, para que no laten al unisono
    private bool _apagado;

    // El flag que se consulta de verdad: el del Inspector o, si esta en None, el
    // del componente de al lado. Se resuelve aca y NO se escribe en el campo:
    // escribiendolo quedaba fijo al guardar la escena, y cambiar despues el flag
    // del trigger ya no le llegaba al brillo.
    private StoryFlag _flag;
    private StoryFlag _prende;
    // Esperando el flag que lo prende: no se dibuja nada todavia.
    private bool _esperando;

    private void OnEnable()
    {
        // Desfase estable por objeto (no aleatorio): se ve organico pero es
        // siempre igual entre partidas, asi un bug visual se reproduce.
        _fase = Mathf.Abs(name.GetHashCode() % 100) / 100f * Mathf.PI * 2f;
        _apagado = false;

        _flag = flagQueLoApaga != StoryFlag.None ? flagQueLoApaga : FlagDelComponenteDeAlLado();
        _prende = flagQueLoPrende != StoryFlag.None ? flagQueLoPrende : FlagQueLoPrendeDeAlLado();
        _esperando = false;

        // Uno que se prende con un flag y no se apaga nunca es lo esperado.
        if (Application.isPlaying && _flag == StoryFlag.None && _prende == StoryFlag.None)
            Debug.LogWarning($"[Brillo] '{name}' no tiene flag: va a brillar para siempre.");

        // Arrancar siempre de cero. Los hijos llevan HideFlags.DontSave, y esos
        // objetos NO se destruyen al cambiar de escena ni al entrar a Play: si
        // no se limpian, los que armo el editor se cuelan en la partida y el
        // componente ya no los conoce (no los puede apagar).
        LimpiarHijos();
    }

    private void OnDisable()
    {
        LimpiarHijos();
    }

    private void Update()
    {
        // Fuera de Play solo se muestra si lo pidieron, para poder calibrarlo.
        if (!Application.isPlaying && !verEnElEditor) { LimpiarHijos(); return; }

        if (Application.isPlaying)
        {
            if (_apagado) return;

            // Se consulta el flag en cada frame en vez de escuchar
            // StoryModel.FlagActivated. Es un HashSet.Contains, no cuesta nada,
            // y evita el problema de quedarse suscripto a un StoryModel viejo:
            // NewGame crea uno nuevo, y quien se suscribio antes no se entera
            // nunca mas. Asi tambien cubre el caso de cargar una partida que ya
            // tenia el subsistema cortado.
            var gm = GameManager.Instance;
            if (gm != null && gm.Story != null && gm.Story.HasFlag(_flag))
            {
                Apagar();
                return;
            }

            // Todavia no le toca: nada en pantalla. Se limpia una sola vez, al
            // pasar a esperar, y no en cada frame.
            bool falta = _prende != StoryFlag.None && (gm == null || gm.Story == null || !gm.Story.HasFlag(_prende));
            if (falta)
            {
                if (!_esperando) { LimpiarHijos(); _esperando = true; }
                return;
            }
            _esperando = false;
        }

        Armar();
        Latir();
    }

    private StoryFlag FlagQueLoPrendeDeAlLado()
    {
        var salida = GetComponentInParent<SalidaDelNivel>();
        return salida != null ? salida.FlagQueAbre : StoryFlag.None;
    }

    private StoryFlag FlagDelComponenteDeAlLado()
    {
        var trigger = GetComponent<InteractableTrigger>();
        if (trigger != null) return trigger.OtorgaFlag;

        var parte = GetComponent<TutorialParte>();
        if (parte != null) return parte.Flag;

        return StoryFlag.None;
    }

    // ---- armado ----

    private void Armar()
    {
        if (spriteHalo != null && _halo == null)
            _halo = NuevoSprite("BrilloHalo", spriteHalo);
        if (spriteEstrella != null && _estrella == null && tamanioEstrella > 0f)
            _estrella = NuevoSprite("BrilloEstrella", spriteEstrella);

        // si le sacaron el sprite desde el Inspector, se va el hijo
        if (spriteHalo == null && _halo != null) { Borrar(_halo); _halo = null; }
        if ((spriteEstrella == null || tamanioEstrella <= 0f) && _estrella != null)
        { Borrar(_estrella); _estrella = null; }
    }

    private SpriteRenderer NuevoSprite(string nombre, Sprite sprite)
    {
        var go = new GameObject(nombre);
        // Ni se guarda en la escena ni aparece en la jerarquia: lo maneja este
        // componente y se rearma solo.
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    // Borra TODO hijo de brillo, no solo los que este componente tiene
    // referenciados: los que sobreviven de una sesion de editor o de un
    // recompilado no estan en _halo/_estrella y quedarian encendidos para
    // siempre.
    private void LimpiarHijos()
    {
        _halo = null;
        _estrella = null;

        var previos = GetComponentsInChildren<Transform>(true)
            .Where(t => t != transform && t.name.StartsWith("Brillo"))
            .Select(t => t.gameObject)
            .ToList();

        foreach (var go in previos)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private void Borrar(SpriteRenderer sr)
    {
        if (sr == null) return;
        if (Application.isPlaying) Destroy(sr.gameObject);
        else DestroyImmediate(sr.gameObject);
    }

    // ---- animacion ----

    // El tamanio se recalcula en CADA frame a proposito: asi mover el slider en
    // el Inspector se ve al instante, sin reiniciar ni volver a entrar a Play.
    private float EscalaPara(Sprite sprite, float altoEnTiles)
    {
        float alto = sprite == null ? 0f : sprite.bounds.size.y;
        if (alto <= 0f) return 1f;
        return altoEnTiles * Mathf.Max(0.01f, escalaGeneral) / alto;
    }

    // Unity llama a esto apenas se toca un valor en el Inspector, tenga foco o
    // no y sin depender de que corra Update. Es lo que hace que mover el slider
    // de tamanio se vea al toque en la Scene view.
    //
    // OJO: aca NO se pueden crear ni destruir objetos (Unity lo prohibe durante
    // OnValidate). Solo se ajusta lo que ya existe; de armar y desarmar se
    // encarga Update.
    private void OnValidate()
    {
        AplicarTamanios();
    }

    private void AplicarTamanios()
    {
        if (_halo != null)
        {
            float e = EscalaPara(spriteHalo, tamanioHalo);
            _halo.transform.localPosition = desplazamiento;
            _halo.transform.localScale = new Vector3(e, e, 1f);
            _halo.sortingOrder = sortingOrder;
            _halo.color = colorHalo;
        }

        if (_estrella != null)
        {
            float e = EscalaPara(spriteEstrella, tamanioEstrella);
            _estrella.transform.localPosition = desplazamiento;
            _estrella.transform.localScale = new Vector3(e, e, 1f);
            _estrella.sortingOrder = sortingOrder + 1;
            _estrella.color = colorEstrella;
        }
    }

    private void Latir()
    {
        // realtimeSinceStartup y no Time.time: en el editor, fuera de Play,
        // Time.time no avanza y el brillo quedaria congelado.
        float ahora = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float t = ahora / Mathf.Max(0.05f, periodo) * Mathf.PI * 2f + _fase;
        float onda = (Mathf.Sin(t) + 1f) * 0.5f;   // 0..1

        if (_halo != null)
        {
            float baseEscala = EscalaPara(spriteHalo, tamanioHalo);
            float escala = baseEscala * (1f + amplitud * (onda - 0.5f) * 2f);
            _halo.transform.localPosition = desplazamiento;
            _halo.transform.localScale = new Vector3(escala, escala, 1f);
            _halo.sortingOrder = sortingOrder;

            var c = colorHalo;
            c.a = colorHalo.a * Mathf.Lerp(0.55f, 1f, onda);
            _halo.color = c;
        }

        if (_estrella != null)
        {
            float escala = EscalaPara(spriteEstrella, tamanioEstrella);
            _estrella.transform.localPosition = desplazamiento;
            _estrella.transform.localScale = new Vector3(escala, escala, 1f);
            _estrella.sortingOrder = sortingOrder + 1;

            // En contrafase con el halo: cuando el halo baja, ella destella.
            float destello = (Mathf.Sin(t + Mathf.PI) + 1f) * 0.5f;
            var c = colorEstrella;
            c.a = colorEstrella.a * Mathf.Lerp(0.35f, 1f, destello);
            _estrella.color = c;

            _estrella.transform.localRotation =
                Quaternion.Euler(0f, 0f, ahora * girosPorMinuto * 6f);
        }
    }

    private void Apagar()
    {
        // El flag primero: Destroy es diferido, y sin esto el Update del mismo
        // frame volveria a armar el brillo antes de que se borren los hijos.
        _apagado = true;
        LimpiarHijos();
    }
}
