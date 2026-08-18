using UnityEngine;

// Maneja la caja de texto del mapa: lineas seguidas y preguntas de Si/No.
//
// No lee input por su cuenta. El unico que lee teclado es PlayerInputController,
// que mientras 'IsOpen' este en true le manda todo aca en vez de mover al
// jugador. Asi no hay dos componentes peleandose por la misma tecla.
public class DialogueController : MonoBehaviour
{
    private enum Modo { Cerrado, Lineas, Pregunta }

    [Header("Referencias")]
    [SerializeField] private DialogueView dialogueView;

    // Mientras esto sea true, el jugador no se mueve.
    public bool IsOpen => _modo != Modo.Cerrado;

    private Modo _modo = Modo.Cerrado;

    private StoryModel _storyModel;
    private StoryFlag _flagAlTerminar = StoryFlag.None;

    private string _speaker;
    private Sprite _portrait;
    private string[] _lineas;
    private int _lineaActual;
    private System.Action _onComplete;

    private string[] _opciones;
    private int _seleccion;
    private System.Action<bool> _onResultado;

    private void Awake()
    {
        if (dialogueView == null) dialogueView = GetComponentInChildren<DialogueView>(true);
        // La caja arranca escondida pase lo que pase.
        if (dialogueView != null) dialogueView.Hide();
    }

    // ---- API ----

    // Lineas sueltas, sin necesidad de un DialogueDefinition.
    public void StartMessage(string speaker, Sprite portrait, string[] lineas,
        System.Action onComplete = null)
    {
        if (lineas == null || lineas.Length == 0) { onComplete?.Invoke(); return; }

        _speaker = speaker;
        _portrait = portrait;
        _lineas = lineas;
        _lineaActual = 0;
        _onComplete = onComplete;
        _flagAlTerminar = StoryFlag.None;
        _modo = Modo.Lineas;

        dialogueView.Show();
        dialogueView.HideChoices();
        MostrarLineaActual();
    }

    public void StartDialogue(DialogueDefinition dialogue, StoryModel storyModel,
        System.Action onComplete = null)
    {
        if (dialogue == null) { onComplete?.Invoke(); return; }

        _storyModel = storyModel;
        StartMessage(dialogue.speakerName, dialogue.speakerPortrait, dialogue.lines, onComplete);
        _flagAlTerminar = dialogue.grantsFlag;
    }

    // Pregunta de dos opciones. El callback recibe true si eligio la primera.
    public void StartConfirm(string speaker, Sprite portrait, string pregunta,
        string opcionSi, string opcionNo, System.Action<bool> onResultado)
    {
        _speaker = speaker;
        _portrait = portrait;
        _opciones = new[] { opcionSi, opcionNo };
        _seleccion = 0;
        _onResultado = onResultado;
        _modo = Modo.Pregunta;

        dialogueView.Show();
        dialogueView.DisplayLine(speaker, portrait, pregunta);
        dialogueView.ShowChoices(_opciones, _seleccion);
    }

    // ---- input, lo llama PlayerInputController ----

    public void OnNavigate(int delta)
    {
        if (_modo != Modo.Pregunta || delta == 0) return;

        _seleccion = Mathf.Clamp(_seleccion + delta, 0, _opciones.Length - 1);
        dialogueView.ShowChoices(_opciones, _seleccion);
    }

    public void OnConfirm()
    {
        if (_modo == Modo.Lineas) AvanzarLinea();
        else if (_modo == Modo.Pregunta) ResolverPregunta();
    }

    // Cancelar equivale a elegir la segunda opcion (el "No").
    public void OnCancel()
    {
        if (_modo != Modo.Pregunta) return;
        _seleccion = _opciones.Length - 1;
        ResolverPregunta();
    }

    // ---- interno ----

    private void MostrarLineaActual()
    {
        dialogueView.DisplayLine(_speaker, _portrait, _lineas[_lineaActual]);
    }

    private void AvanzarLinea()
    {
        _lineaActual++;
        if (_lineaActual < _lineas.Length) { MostrarLineaActual(); return; }

        if (_flagAlTerminar != StoryFlag.None && _storyModel != null)
            _storyModel.ActivateFlag(_flagAlTerminar);

        Cerrar();
        // El callback se llama DESPUES de cerrar: si abre otro dialogo (tipico
        // del "se unio al equipo"), no se lo come el Cerrar de este.
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    private void ResolverPregunta()
    {
        bool eligioSi = _seleccion == 0;
        Cerrar();

        var cb = _onResultado;
        _onResultado = null;
        cb?.Invoke(eligioSi);
    }

    private void Cerrar()
    {
        _modo = Modo.Cerrado;
        dialogueView.HideChoices();
        dialogueView.Hide();
    }
}
