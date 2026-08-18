using UnityEngine;

// Un Poken encerrado que se puede liberar para que se sume al equipo.
//
// Pregunta "queres liberarlo?" con dos opciones y, si el jugador acepta, crea
// el PokenModel y lo mete en el equipo. Sirve para los Pokemon cautivos de la
// torre: son el premio de explorar, no de pelear.
//
// Dos formas de dispararlo, no excluyentes:
//   - Apretando la tecla de accion mirandolo de frente (IInteractable). Para
//     esto el collider tiene que estar en una capa del 'interactableMask' del
//     PlayerController.
//   - Caminando al lado, si 'tambienPorTrigger' esta activo.
//
// OJO: la cercania se mide por DISTANCIA, no con un Collider2D en trigger.
// Con un trigger no anda: el movimiento del jugador decide si puede avanzar con
// Physics2D.OverlapCircle contra su obstacleMask, y esa consulta tambien
// engancha los triggers de esa capa. Un trigger de radio 0.9 en 'Obstacles'
// bloqueaba TODOS los tiles de alrededor del NPC y no se podia ni llegar.
//
// Queda desarmado hasta que el jugador se aleja, si no volveria a preguntar
// apenas se cierra la caja de texto.
public class LiberarPokenTrigger : MonoBehaviour, IInteractable
{
    [Header("A quien se libera")]
    [SerializeField] private PokenDefinition poken;
    [Tooltip("Nivel con el que se suma al equipo.")]
    [SerializeField] private int nivel = 15;

    [Header("Textos")]
    [Tooltip("Nombre que aparece arriba de la caja. Vacio = cartel de sistema.")]
    [SerializeField] private string nombreQueHabla = "";
    [SerializeField] private Sprite retrato;
    [SerializeField, TextArea(2, 4)] private string pregunta = "Esta encerrado y te mira. Lo liberas?";
    [SerializeField] private string opcionSi = "Si";
    [SerializeField] private string opcionNo = "No";
    [Tooltip("{0} se reemplaza por el nombre del Poken.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSi = "{0} se solto y te sigue. Se unio al equipo!";
    [SerializeField, TextArea(2, 4)] private string mensajeNo = "Lo dejaste donde estaba.";
    [SerializeField, TextArea(2, 4)] private string mensajeEquipoLleno = "Tu equipo ya esta completo. No podes llevarlo.";
    [SerializeField, TextArea(2, 4)] private string mensajeYaLiberado = "Ya no hay nadie aca.";

    [Header("Comportamiento")]
    [Tooltip("Ademas de la tecla de accion, se dispara al caminarle al lado.")]
    [SerializeField] private bool tambienPorTrigger = true;
    [Tooltip("A que distancia se dispara solo. 1.2 = el tile de al lado.")]
    [SerializeField] private float radioDeteccion = 1.2f;
    [Tooltip("Esconde el objeto al liberarlo. Apagalo si el sprite tiene que quedarse.")]
    [SerializeField] private bool desaparecerAlLiberar = true;
    [Tooltip("Flag que se activa al liberarlo. None = ninguno.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;

    [Header("Referencias (opcional: se buscan solas)")]
    [SerializeField] private DialogueController dialogueController;

    // Tope clasico de 6: es lo que dibuja el HUD de combate (teamNameTexts).
    private const int EquipoMaximo = 6;

    private bool _liberado;
    private bool _dialogoAbierto;
    private bool _armado = true;
    private Transform _jugador;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);

        if (poken == null)
            Debug.LogError($"[Liberar] '{name}' no tiene PokenDefinition asignado: no se puede liberar a nadie.");
    }

    // ---- tecla de accion ----

    public bool Interact(PlayerModel player, StoryModel story)
    {
        return Abrir(player, story);
    }

    // ---- caminarle al lado ----

    private void Update()
    {
        if (!tambienPorTrigger || _liberado || _dialogoAbierto) return;

        if (_jugador == null)
        {
            var pcs = FindAnyObjectByType<PlayerController>();
            if (pcs == null) return;
            _jugador = pcs.transform;
        }

        float d = Vector2.Distance(_jugador.position, transform.position);

        // Histeresis: se re-arma un poco mas lejos de lo que dispara, asi no
        // parpadea si el jugador queda justo en el borde.
        if (!_armado)
        {
            if (d > radioDeteccion + 0.4f) _armado = true;
            return;
        }

        if (d > radioDeteccion) return;

        _armado = false;
        var gm = GameManager.Instance;
        if (gm == null) return;
        Abrir(gm.CurrentPlayer, gm.Story);
    }

    // ---- flujo ----

    private bool Abrir(PlayerModel player, StoryModel story)
    {
        if (_dialogoAbierto) return false;
        if (player == null || dialogueController == null) return false;

        if (_liberado)
        {
            Mensaje(mensajeYaLiberado);
            return true;
        }

        if (poken == null) return false;

        if (player.Team.Count >= EquipoMaximo)
        {
            Mensaje(mensajeEquipoLleno);
            return true;
        }

        _dialogoAbierto = true;
        dialogueController.StartConfirm(nombreQueHabla, retrato, pregunta, opcionSi, opcionNo,
            eligioSi =>
            {
                _dialogoAbierto = false;
                if (eligioSi) Liberar(player, story);
                else Mensaje(mensajeNo);
            });

        return true;
    }

    private void Liberar(PlayerModel player, StoryModel story)
    {
        player.Team.Add(new PokenModel(poken, nivel));
        _liberado = true;

        if (otorgaFlag != StoryFlag.None && story != null)
            story.ActivateFlag(otorgaFlag);

        string nombre = string.IsNullOrEmpty(poken.pokenName) ? poken.name : poken.pokenName;
        Debug.Log($"[Liberar] '{nombre}' Nv{nivel} se sumo al equipo ({player.Team.Count}/{EquipoMaximo}).");

        // El objeto se esconde recien cuando el jugador cierra el cartel: si se
        // apaga antes, muere el callback y la caja queda abierta para siempre.
        Mensaje(string.Format(mensajeSi, nombre), () =>
        {
            if (desaparecerAlLiberar) gameObject.SetActive(false);
        });
    }

    private void Mensaje(string texto, System.Action alCerrar = null)
    {
        _dialogoAbierto = true;
        dialogueController.StartMessage(nombreQueHabla, retrato, new[] { texto }, () =>
        {
            _dialogoAbierto = false;
            alCerrar?.Invoke();
        });
    }
}
