using System.Collections;
using UnityEngine;

// Un NPC que te ve cuando entras a su sala: se le prende el "!" arriba de la
// cabeza, se da vuelta hacia vos y arranca a hablar solo, como un entrenador
// de la saga que te descubre.
//
// Es el guardia de la Liga de la sala de control del Data Center (la del panel
// de la energia auxiliar): el guion lo tiene recibiendo por radio la orden de
// buscar a alguien sin chip... y se la pregunta a vos.
//
// Lo que dice es el dialogo del NpcQueHabla del mismo objeto: un solo texto
// para las dos formas de hablarle (que te vea o que le hables con X).
//
// Por que la sala es un rectangulo y no un collider en 'Is Trigger':
//   - En la capa Obstacles frenaria al jugador en la puerta (el obstacleMask
//     ve los triggers: ver trigger-en-obstacles).
//   - El combate apaga el mapa entero (MapBattleBridge) y al volver Unity
//     re-dispara los OnTriggerEnter con el jugador adentro.
// Se pregunta cada frame si el jugador esta adentro, como LiberarPokenTrigger
// con su radio. Son cuatro comparaciones.
//
// Suena UNA vez: lo marca 'yaPasoConFlag', que enciende el propio dialogo al
// terminar (en el guardia lo da DC_Guardia_Cruz, al final de la cadena). Es un
// flag de la partida: tampoco se repite al cargar. Mientras corre no se puede
// disparar de nuevo, y si al entrar habia un cartel o un menu abierto espera a
// que se cierre (con el jugador todavia adentro).
[RequireComponent(typeof(NpcQueHabla))]
public class NpcQueTeDetecta : MonoBehaviour
{
    [Header("La sala que vigila (en el mundo)")]
    [SerializeField] private Vector2 centroDeLaSala;
    [SerializeField] private Vector2 tamanioDeLaSala = new Vector2(10f, 5f);

    [Header("Cuando")]
    [Tooltip("Ya paso: lo enciende el dialogo (o el ultimo de su cadena) al terminar. " +
             "None = una vez por sesion, sin recordarlo en la partida.")]
    [SerializeField] private StoryFlag yaPasoConFlag = StoryFlag.None;
    [Tooltip("Solo con este flag. None = siempre.")]
    [SerializeField] private StoryFlag requiere = StoryFlag.None;
    [Tooltip("Nunca con este flag (ej: despues de que cae el servidor ya no vigila).")]
    [SerializeField] private StoryFlag excluye = StoryFlag.None;

    [Header("El signo de exclamacion")]
    [SerializeField] private Sprite signo;
    [Tooltip("Altura sobre los pies del NPC.")]
    [SerializeField] private float alturaDelSigno = 2.25f;
    [Tooltip("Escala del sprite. Con la del pack (PPU 100), 4 = el mismo tamanio de pixel que los personajes.")]
    [SerializeField] private float escalaDelSigno = 4f;
    [Tooltip("Cuanto queda a la vista antes de irse.")]
    [SerializeField, Min(0.2f)] private float duracionDelSigno = 1.1f;
    [Tooltip("Por encima de Techos (20) y de los personajes (15).")]
    [SerializeField] private int ordenDelSigno = 40;

    [Header("Hacia donde mira: abajo, izquierda, derecha, arriba")]
    [Tooltip("Las cuatro poses quietas de su hoja, en el orden de CasinoNpcBuilder.")]
    [SerializeField] private Sprite[] miradas = new Sprite[4];
    [SerializeField] private SpriteRenderer visual;

    [Header("Referencias (opcional: se buscan solas)")]
    [SerializeField] private DialogueController dialogueController;

    private const int Abajo = 0, Izquierda = 1, Derecha = 2, Arriba = 3;

    private NpcQueHabla _habla;
    private PlayerController _jugador;
    private SpriteRenderer _signo;
    private bool _enCurso;
    private float _desde;
    // Ya paso en esta sesion (ademas del flag, que es lo que se guarda).
    private bool _hecho;

    public bool EnCurso => _enCurso;
    public StoryFlag YaPasoConFlag => yaPasoConFlag;
    public Rect Sala => new Rect(centroDeLaSala - tamanioDeLaSala * 0.5f, tamanioDeLaSala);

    private void Awake()
    {
        _habla = GetComponent<NpcQueHabla>();
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (visual == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visual = v.GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        // Red de seguridad: DialogueController.Cancelar cierra la caja SIN
        // llamar al callback. Sin esto quedaria "en curso" para siempre.
        if (_enCurso && dialogueController != null && !dialogueController.IsOpen && Time.time - _desde > 0.5f)
            _enCurso = false;

        if (_enCurso || _hecho) return;

        if (_jugador == null)
        {
            _jugador = FindAnyObjectByType<PlayerController>();
            if (_jugador == null) return;
        }
        if (!EstaEnLaSala(_jugador.transform.position)) return;

        // Algo abierto (un cartel, el menu, el combate): se espera. Como no se
        // marca nada, el proximo frame con todo cerrado vuelve a probar.
        if (MenuManager.AnyOpen || BattleHudView.IsNavigating) return;
        if (dialogueController == null || dialogueController.IsOpen) return;

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (!TocaDetectar(story))
        {
            // Ya paso en la partida: no hace falta seguir mirando.
            if (story != null && yaPasoConFlag != StoryFlag.None && story.HasFlag(yaPasoConFlag)) _hecho = true;
            return;
        }

        StartCoroutine(Detectar(story));
    }

    public bool EstaEnLaSala(Vector3 posicion) => Sala.Contains(new Vector2(posicion.x, posicion.y));

    // Publico para los tests: si en esta partida le toca verte.
    public bool TocaDetectar(StoryModel story)
    {
        if (story == null || _habla == null || _habla.Dialogo == null) return false;
        if (yaPasoConFlag != StoryFlag.None && story.HasFlag(yaPasoConFlag)) return false;
        if (requiere != StoryFlag.None && !story.HasFlag(requiere)) return false;
        if (excluye != StoryFlag.None && story.HasFlag(excluye)) return false;
        return _habla.Dialogo.TieneQueDecir(story);
    }

    private IEnumerator Detectar(StoryModel story)
    {
        _enCurso = true;
        _desde = Time.time;

        Mirar(_jugador.transform.position - transform.position);
        StartCoroutine(MostrarSigno());

        // La caja se abre en el acto: desde ahi el jugador ya no camina (el
        // input va a la caja), asi que no hace falta frenarlo a mano.
        dialogueController.StartDialogue(_habla.Dialogo, story, () =>
        {
            _enCurso = false;
            _hecho = true;
            Debug.Log($"[Detecta] '{name}' termino de hablar" +
                      (yaPasoConFlag != StoryFlag.None ? $" ({yaPasoConFlag}: {story.HasFlag(yaPasoConFlag)})." : "."));
        });

        // Y el jugador lo mira a el, recien cuando termina el paso que venia
        // dando (cortarlo a mitad se veria como un salto).
        float limite = Time.time + 1f;
        while (_jugador != null && _jugador.IsMoving && Time.time < limite) yield return null;
        if (_jugador != null && !_jugador.IsMoving)
            _jugador.TeleportTo(_jugador.transform.position, Cardinal(transform.position - _jugador.transform.position));
    }

    // ---------- mirar ----------

    private void Mirar(Vector2 hacia)
    {
        if (visual == null || miradas == null || miradas.Length < 4) return;
        Vector2 d = Cardinal(hacia);
        int pose = d == Vector2.left ? Izquierda : d == Vector2.right ? Derecha : d == Vector2.up ? Arriba : Abajo;
        if (miradas[pose] != null) visual.sprite = miradas[pose];
    }

    // La direccion de las cuatro que mas se parece (el eje mas largo).
    private static Vector2 Cardinal(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? Vector2.right : Vector2.left;
        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    // ---------- el "!" ----------

    private IEnumerator MostrarSigno()
    {
        Sonidos.Exclamacion();
        if (signo == null) yield break;

        if (_signo == null)
        {
            var go = new GameObject("Signo");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, alturaDelSigno, 0f);
            _signo = go.AddComponent<SpriteRenderer>();
            _signo.sprite = signo;
            _signo.sortingOrder = ordenDelSigno;
        }
        _signo.gameObject.SetActive(true);
        _signo.color = Color.white;

        // Aparece con un saltito (crece de mas y se acomoda), queda y se va.
        Transform t = _signo.transform;
        const float aparicion = 0.18f;
        for (float s = 0f; s < aparicion; s += Time.deltaTime)
        {
            float k = s / aparicion;
            float rebote = k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
            t.localScale = Vector3.one * escalaDelSigno * rebote;
            yield return null;
        }
        t.localScale = Vector3.one * escalaDelSigno;

        yield return new WaitForSeconds(duracionDelSigno);

        const float salida = 0.2f;
        for (float s = 0f; s < salida; s += Time.deltaTime)
        {
            _signo.color = new Color(1f, 1f, 1f, 1f - s / salida);
            yield return null;
        }
        _signo.gameObject.SetActive(false);
    }

    // Si el mapa se apaga a mitad del aviso (no deberia: la caja frena todo),
    // el signo no puede quedar prendido para la proxima.
    private void OnDisable()
    {
        if (_signo != null) _signo.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Rect r = Sala;
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y, 0f), new Vector3(r.width, r.height, 0.01f));
        UnityEditor.Handles.Label(new Vector3(r.xMin, r.yMax + 0.3f, 0f), "Vigila: " + name);
    }
#endif
}
