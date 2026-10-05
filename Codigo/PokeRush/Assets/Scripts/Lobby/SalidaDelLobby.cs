using UnityEngine;
using UnityEngine.SceneManagement;

// La salida de la Quarcho Cueva: lleva al siguiente nivel de la historia.
//
// Una sola puerta para todos los niveles, porque el recorrido es lineal: a
// donde lleva lo deciden los flags de la partida (RecorridoDeLaHistoria), no la
// puerta. Al principio va al Data Center de Nestor, con Nestor rescatado va al
// casino, y despues del casino al jefe final.
//
// Es una zona para PISAR: el Collider2D va en 'Is Trigger' y en la capa
// Default, NO en Obstacles (con Obstacles el jugador frena en el borde y el
// OnTriggerEnter2D no llega nunca; ver el doc de la cueva). Al entrar pregunta;
// si dice que no, queda parado en la puerta y tiene que salir para que vuelva
// a preguntar.
[RequireComponent(typeof(Collider2D))]
public class SalidaDelLobby : MonoBehaviour
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Textos")]
    [Tooltip("Antes de hablar con Tixi la salida no abre.")]
    [SerializeField, TextArea(2, 3)] private string mensajeSinTixi =
        "Todavía no. Tixi te está esperando: hablá primero con Tixi.";
    [Tooltip("{0} = el nivel.")]
    [SerializeField, TextArea(2, 3)] private string pregunta = "¿Salís hacia {0}?";
    [SerializeField] private string opcionIr = "Ir";
    [SerializeField] private string opcionNo = "Todavía no";

    [Header("Antes de salir (opcional)")]
    [Tooltip("Lo que pasa al ir a salir, si tiene algo para decir en esta partida: " +
             "Tixi te frena y te da el transmisor despues del casino. Suena una vez " +
             "(lo marca su flag) y recien despues pregunta adonde vas.")]
    [SerializeField] private DialogueDefinition antesDeSalir;

    private bool _armado = true;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_armado || other.GetComponentInParent<PlayerController>() == null) return;
        if (dialogueController != null && dialogueController.IsOpen) return;
        _armado = false;

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (story != null && !story.HasFlag(StoryFlag.Lobby_TixiPresentado))
        {
            CasinoDialogo.Decir(dialogueController, "", null, new[] { mensajeSinTixi });
            return;
        }

        ViajeEntreNiveles.Preguntar(dialogueController, "", null, story, pregunta, opcionIr, opcionNo, antesDeSalir);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _armado = true;
    }
}

// Salir de la cueva al siguiente nivel. Lo usan la puerta y Tixi, asi los dos
// dicen lo mismo y cargan lo mismo.
public static class ViajeEntreNiveles
{
    public const string MensajeNivelCerrado =
        "El camino hacia {0} todavía no está abierto. Por ahora, la historia llega hasta acá.";
    public const string MensajeSinNiveles = "No queda ningún nivel por delante.";

    // Pregunta "¿Salís hacia X?" y, si acepta, carga el nivel. 'antes', si
    // tiene algo para decir en esta partida, suena primero (una vez: lo marca
    // su flag) y la pregunta viene despues.
    public static void Preguntar(DialogueController dialogo, string nombre, Sprite retrato,
                                 StoryModel story, string pregunta, string si, string no,
                                 DialogueDefinition antes = null)
    {
        if (antes != null && dialogo != null && antes.TieneQueDecir(story)
            && (antes.FlagPara(story) == StoryFlag.None || story == null || !story.HasFlag(antes.FlagPara(story))))
        {
            dialogo.StartDialogue(antes, story, () =>
            {
                CasinoDialogo.Guardar();
                Preguntar(dialogo, nombre, retrato, story, pregunta, si, no);
            });
            return;
        }

        RecorridoDeLaHistoria.Nivel nivel = RecorridoDeLaHistoria.SiguienteNivel(story);
        if (nivel == null)
        {
            CasinoDialogo.Decir(dialogo, nombre, retrato, new[] { MensajeSinNiveles });
            return;
        }

        if (!SePuedeCargar(nivel))
        {
            CasinoDialogo.Decir(dialogo, nombre, retrato, new[] { string.Format(MensajeNivelCerrado, nivel.Nombre) });
            return;
        }

        CasinoDialogo.Preguntar(dialogo, nombre, retrato, string.Format(pregunta, nivel.Nombre), si, no,
            ir => { if (ir) Ir(nivel); });
    }

    // Que la escena exista Y este en Build Settings: si no, LoadScene falla
    // recien en runtime y el jugador queda en la puerta sin saber por que.
    public static bool SePuedeCargar(RecorridoDeLaHistoria.Nivel nivel)
        => nivel != null && Application.CanStreamedLevelBeLoaded(nivel.EscenaDeEntrada);

    public static void Ir(RecorridoDeLaHistoria.Nivel nivel)
    {
        if (nivel == null) return;
        Debug.Log($"[Recorrido] Salida de la cueva hacia {nivel.Nombre} ({nivel.EscenaDeEntrada}).");
        CasinoDialogo.Guardar();
        Sonidos.Teletransporte();
        SceneManager.LoadScene(nivel.EscenaDeEntrada);
    }
}
