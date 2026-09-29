using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// La puerta de la habitacion tutorial: no deja salir hasta terminar el
// tutorial, salvo que el jugador elija omitirlo ahi mismo.
//
// Se arma de una de dos formas, segun como este dibujada la puerta:
//
//   Hueco en la pared (se pisa): zona para PISAR, como la salida de la Quarcho
//     Cueva. El Collider2D va en 'Is Trigger' y en la capa Default: en
//     Obstacles el jugador frenaria en el borde y el OnTriggerEnter2D no
//     llegaria nunca. Del otro lado va el 'Tope' (hijo, en Obstacles) para no
//     salir caminando al vacio despues de decir que no. Es la del esqueleto.
//   Dibujada como pared (no se pisa): se usa con X, como un mueble. El
//     Collider2D va SIN 'Is Trigger' y en Obstacles, que es la capa que mira el
//     interactableMask del jugador. No lleva tope. Es la de 'habitacion'.
//
//   Sin terminar -> "Todavia te falta ..." y elegir: seguir u omitir.
//   Terminado    -> "¿Salis hacia la Quarcho Cueva?"
//
// Al salir guarda la partida y carga 'escenaSiguiente'. Como SaveService no
// guarda la escena, "Continuar" arranca en la cueva de todas formas.
//
// Si tiene 'camino', antes de cargar funde a negro y cuenta el viaje (el guion:
// el camino a la Quarcho Cueva, Cruz, el viejo Ameghino y la cerradura NFC).
// Suena una vez: su grantsFlag lo marca.
[RequireComponent(typeof(Collider2D))]
public class PuertaDelTutorial : MonoBehaviour, IInteractable
{
    [Header("Adonde lleva")]
    [Tooltip("Tiene que estar en Build Settings.")]
    [SerializeField] private string escenaSiguiente = RecorridoDeLaHistoria.EscenaDelLobby;
    [Tooltip("Como se nombra en la pregunta de salir.")]
    [SerializeField] private string nombreDelDestino = "la Quarcho Cueva";

    [Header("Sin terminar")]
    [Tooltip("{0} = lo que falta.")]
    // Es la decision del guion: investigar mas el cuarto o salir ya (en pijama).
    [SerializeField, TextArea(2, 3)] private string mensajeSinTerminar =
        "Todavía te falta {0}. ¿Investigás un poco más o salís ya para la Quarcho Cueva?";
    // Cortas: con dos opciones la caja usa la ventanita del Si/No, de ancho
    // fijo, y una opcion larga se parte en dos renglones que se pisan.
    [SerializeField] private string opcionSeguir = "Investigar";
    [SerializeField] private string opcionOmitir = "Salir ya";
    [Tooltip("Despues de omitir, antes de irse. {0} = lo que se llevo. Vacio = no dice nada.")]
    [SerializeField, TextArea(2, 3)] private string mensajeOmitido =
        "Agarrás lo que tenés a mano ({0}) y salís tal cual estás: en pijama.";

    [Header("Terminado")]
    [Tooltip("{0} = el destino.")]
    [SerializeField, TextArea(2, 3)] private string preguntaSalir = "¿Salís hacia {0}?";
    [SerializeField] private string opcionSalir = "Salir";
    [SerializeField] private string opcionQuedarse = "Todavía no";
    [Tooltip("{0} = la escena.")]
    [SerializeField, TextArea(2, 3)] private string mensajeSinDestino =
        "La puerta no abre: la escena '{0}' no está en Build Settings.";

    [Header("El camino (opcional)")]
    [Tooltip("Lo que pasa entre la puerta y el destino, sobre negro. Su grantsFlag " +
             "marca que ya se conto. Vacio = se carga el destino de una.")]
    [SerializeField] private DialogueDefinition camino;
    [Min(0f)] [SerializeField] private float duracionDelFundido = 0.8f;

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    public string EscenaSiguiente => escenaSiguiente;
    public DialogueDefinition Camino => camino;

    // Se desarma al pisarla y se rearma al salir de la zona: si dijo que no,
    // tiene que volver a pisarla para que pregunte de nuevo.
    private bool _armado = true;
    private bool _saliendo;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_armado || _saliendo || other.GetComponentInParent<PlayerController>() == null) return;
        if (MenuManager.AnyOpen || (dialogueController != null && dialogueController.IsOpen)) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Story == null) return;
        _armado = false;

        Abrir(gm.CurrentPlayer, gm.Story);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _armado = true;
    }

    // La puerta dibujada como pared: apretando X de al lado.
    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (_saliendo || story == null) return false;
        Abrir(player, story);
        return true;
    }

    private void Abrir(PlayerModel player, StoryModel story)
    {
        if (TutorialDeLaHabitacion.PuedeSalir(story)) PreguntarSalir();
        else OfrecerOmitir(player, story);
    }

    private void OfrecerOmitir(PlayerModel player, StoryModel story)
    {
        string texto = string.Format(mensajeSinTerminar, TutorialDeLaHabitacion.Faltan(story));

        // Seguir va PRIMERO y es tambien lo que elige Escape: machacar aceptar o
        // volver nunca saltea el tutorial por accidente.
        if (dialogueController != null)
        {
            dialogueController.StartChoice("", null, texto, new[] { opcionSeguir, opcionOmitir },
                eleccion => { if (eleccion == 1) Omitir(player, story); }, opcionAlCancelar: 0);
            return;
        }

        // Sin caja de dialogo: el cartel del menu, que arranca en NO (aca, seguir).
        CasinoDialogo.Preguntar(null, "", null, texto, opcionOmitir, opcionSeguir,
            omitir => { if (omitir) Omitir(player, story); });
    }

    private void Omitir(PlayerModel player, StoryModel story)
    {
        string llevo = TutorialDeLaHabitacion.Omitir(player, story);
        if (string.IsNullOrEmpty(llevo) || string.IsNullOrEmpty(mensajeOmitido)) { Salir(); return; }

        CasinoDialogo.Decir(dialogueController, "", null, new[] { string.Format(mensajeOmitido, llevo) }, Salir);
    }

    private void PreguntarSalir()
    {
        CasinoDialogo.Preguntar(dialogueController, "", null, string.Format(preguntaSalir, nombreDelDestino),
            opcionSalir, opcionQuedarse, salir => { if (salir) Salir(); });
    }

    private void Salir()
    {
        // Que la escena este en Build Settings: si no, LoadScene falla recien en
        // runtime y el jugador queda en la puerta sin saber por que.
        if (!Application.CanStreamedLevelBeLoaded(escenaSiguiente))
        {
            CasinoDialogo.Decir(dialogueController, "", null,
                new[] { string.Format(mensajeSinDestino, escenaSiguiente) });
            return;
        }

        _saliendo = true;
        Debug.Log($"[Tutorial] Salida de la habitacion hacia {escenaSiguiente}.");
        CasinoDialogo.Guardar();

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (TocaElCamino(story))
        {
            StartCoroutine(ContarElCamino(story));
            return;
        }

        SceneManager.LoadScene(escenaSiguiente);
    }

    // Publico para los tests.
    public bool TocaElCamino(StoryModel story)
    {
        if (camino == null || dialogueController == null || story == null) return false;
        StoryFlag marca = camino.grantsFlag;
        return marca == StoryFlag.None || !story.HasFlag(marca);
    }

    private IEnumerator ContarElCamino(StoryModel story)
    {
        // Un Canvas propio sobre el mapa y debajo de la caja de dialogo (100),
        // como el fundido del despertar. Se va con la escena.
        var capa = new GameObject("[FundidoDelCamino]");
        var canvas = capa.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var negro = new GameObject("Negro").AddComponent<Image>();
        negro.transform.SetParent(capa.transform, false);
        negro.raycastTarget = false;
        negro.rectTransform.anchorMin = Vector2.zero;
        negro.rectTransform.anchorMax = Vector2.one;
        negro.rectTransform.offsetMin = Vector2.zero;
        negro.rectTransform.offsetMax = Vector2.zero;

        float t = 0f, d = Mathf.Max(0.01f, duracionDelFundido);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / d));
            yield return null;
        }
        negro.color = Color.black;

        dialogueController.StartDialogue(camino, story, () =>
        {
            CasinoDialogo.Guardar();
            SceneManager.LoadScene(escenaSiguiente);
        });
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : default;
        if (b.size.sqrMagnitude < 0.01f) b = new Bounds(transform.position, new Vector3(0.9f, 0.9f, 0f));

        Gizmos.color = new Color(0.35f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.01f));
        UnityEditor.Handles.Label(new Vector3(b.min.x, b.max.y + 0.4f, 0f), $"Puerta -> {escenaSiguiente}");
    }
#endif
}
