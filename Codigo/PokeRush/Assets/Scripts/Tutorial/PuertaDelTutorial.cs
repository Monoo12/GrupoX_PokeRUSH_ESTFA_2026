using UnityEngine;
using UnityEngine.SceneManagement;

// La puerta de la habitacion tutorial: no deja salir hasta terminar el
// tutorial, salvo que el jugador elija omitirlo ahi mismo.
//
// Es una zona para PISAR, como la salida de la Quarcho Cueva: el Collider2D va
// en 'Is Trigger' y en la capa Default. En Obstacles el jugador frenaria en el
// borde y el OnTriggerEnter2D no llegaria nunca. Del otro lado va el 'Tope'
// (hijo, en Obstacles) para no salir caminando al vacio despues de decir que no.
//
//   Sin terminar -> "Todavia te falta ..." y elegir: seguir u omitir.
//   Terminado    -> "¿Salis hacia la Quarcho Cueva?"
//
// Al salir guarda la partida y carga 'escenaSiguiente'. Como SaveService no
// guarda la escena, "Continuar" arranca en la cueva de todas formas.
[RequireComponent(typeof(Collider2D))]
public class PuertaDelTutorial : MonoBehaviour
{
    [Header("Adonde lleva")]
    [Tooltip("Tiene que estar en Build Settings.")]
    [SerializeField] private string escenaSiguiente = RecorridoDeLaHistoria.EscenaDelLobby;
    [Tooltip("Como se nombra en la pregunta de salir.")]
    [SerializeField] private string nombreDelDestino = "la Quarcho Cueva";

    [Header("Sin terminar")]
    [Tooltip("{0} = lo que falta.")]
    [SerializeField, TextArea(2, 3)] private string mensajeSinTerminar =
        "Todavía te falta {0}. ¿Seguís con el tutorial o lo omitís?";
    // Cortas: con dos opciones la caja usa la ventanita del Si/No, de ancho
    // fijo, y una opcion larga se parte en dos renglones que se pisan.
    [SerializeField] private string opcionSeguir = "Seguir";
    [SerializeField] private string opcionOmitir = "Omitir";
    [Tooltip("Despues de omitir, antes de irse. {0} = lo que se llevo. Vacio = no dice nada.")]
    [SerializeField, TextArea(2, 3)] private string mensajeOmitido = "Te llevás lo que ibas a juntar: {0}.";

    [Header("Terminado")]
    [Tooltip("{0} = el destino.")]
    [SerializeField, TextArea(2, 3)] private string preguntaSalir = "¿Salís hacia {0}?";
    [SerializeField] private string opcionSalir = "Salir";
    [SerializeField] private string opcionQuedarse = "Todavía no";
    [Tooltip("{0} = la escena.")]
    [SerializeField, TextArea(2, 3)] private string mensajeSinDestino =
        "La puerta no abre: la escena '{0}' no está en Build Settings.";

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    public string EscenaSiguiente => escenaSiguiente;

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

        if (TutorialDeLaHabitacion.PuedeSalir(gm.Story)) PreguntarSalir();
        else OfrecerOmitir(gm.CurrentPlayer, gm.Story);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _armado = true;
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
        SceneManager.LoadScene(escenaSiguiente);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.35f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.9f, 0.9f, 0.01f));
        UnityEditor.Handles.Label(transform.position + new Vector3(-0.45f, 0.8f, 0f), $"Puerta -> {escenaSiguiente}");
    }
#endif
}
