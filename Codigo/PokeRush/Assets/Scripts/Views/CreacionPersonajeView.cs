using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de creacion de personaje: genero -> nombre -> inicial -> confirmar.
//
// Es un asistente de pasos, no cuatro escenas: se prende y apaga un panel por
// paso, asi el jugador puede volver atras sin recargar nada. Cada cambio de
// paso se anima (el panel que se va sale, el que llega entra con
// EntradaAnimada) y mientras dura no se puede tocar nada: un doble clic
// apurado no salta dos pasos ni elige dos veces.
//
// Elegir es en dos tiempos, para que nada se decida por accidente: tocar una
// ficha la MARCA (y habilita "Continuar"); tocarla otra vez, o "Continuar",
// sigue. Los botones que todavia no se pueden usar se ven deshabilitados.
//
// Se maneja con mouse, teclado o joystick. Las flechas mueven la seleccion del
// EventSystem; aceptar es X / Enter / A y volver es Z / Escape / B (las teclas
// de GameInputService, remapeables desde Opciones). Mientras se escribe el
// nombre, las teclas son del campo de texto.
//
// Al confirmar arma la partida (GameManager.NewGame), la GUARDA de una — para
// que exista un save aunque el jugador cierre el juego enseguida — y funde a
// negro hacia la escena del juego.
public class CreacionPersonajeView : MonoBehaviour
{
    [System.Serializable]
    public class OpcionInicial
    {
        public string nombre;
        public PokenDefinition definicion;
        [Tooltip("Sprite grande para la ficha de eleccion.")]
        public Sprite retrato;
        [TextArea(2, 4)] public string descripcion;
    }

    private enum Paso { Genero, Nombre, Inicial, Confirmar }

    [Header("Paneles (uno por paso)")]
    [SerializeField] private GameObject panelGenero;
    [SerializeField] private GameObject panelNombre;
    [SerializeField] private GameObject panelInicial;
    [SerializeField] private GameObject panelConfirmar;

    [Header("Indicador de pasos")]
    [Tooltip("Una etiqueta por paso, en orden.")]
    [SerializeField] private Text[] etiquetasDePaso = new Text[4];
    [Tooltip("Una marca (barrita) por paso, en orden.")]
    [SerializeField] private Image[] marcasDePaso = new Image[4];
    [SerializeField] private Color colorPasoActual = new Color(0.35f, 0.72f, 0.95f, 1f);
    [SerializeField] private Color colorPasoHecho = new Color(0.85f, 0.9f, 0.95f, 1f);
    [SerializeField] private Color colorPasoPendiente = new Color(0.45f, 0.5f, 0.58f, 1f);

    [Header("Genero")]
    [SerializeField] private Button botonVaron;
    [SerializeField] private Button botonMujer;
    [SerializeField] private Image retratoVaron;
    [SerializeField] private Image retratoMujer;
    [SerializeField] private Button continuarGenero;
    [Tooltip("Vuelve al menu principal.")]
    [SerializeField] private Button volverGenero;
    [SerializeField] private Color colorElegido = Color.white;
    [SerializeField] private Color colorNormal = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Header("Nombre")]
    [SerializeField] private InputField campoNombre;
    [SerializeField] private Button botonNombreOk;
    [SerializeField] private Button volverNombre;
    [SerializeField] private Text avisoNombre;
    [Tooltip("\"3/10\": cuanto lleva escrito.")]
    [SerializeField] private Text contadorNombre;
    [SerializeField] private int largoMaximo = 10;

    [Header("Inicial")]
    [SerializeField] private OpcionInicial[] iniciales = new OpcionInicial[3];
    [SerializeField] private Button[] botonesInicial = new Button[3];
    [SerializeField] private Image[] retratosInicial = new Image[3];
    [SerializeField] private Text[] nombresInicial = new Text[3];
    [SerializeField] private Text descripcionInicial;
    [SerializeField] private Button continuarInicial;
    [SerializeField] private Button volverInicial;
    [SerializeField] private int nivelInicial = 15;
    [Tooltip("Cuadros por segundo del Pokemon elegido (usa sus battleFrontFrames).")]
    [SerializeField] private float cuadrosPorSegundo = 24f;

    [Header("Confirmacion")]
    [SerializeField] private Text textoResumen;
    [SerializeField] private Image retratoResumenJugador;
    [SerializeField] private Image retratoResumenInicial;
    [SerializeField] private Button botonEmpezar;
    [SerializeField] private Button botonVolver;

    [Header("Destino")]
    [SerializeField] private string escenaDestino = "NestorGOD2";
    [Tooltip("A donde vuelve 'Volver' desde el primer paso.")]
    [SerializeField] private string escenaAnterior = "Menu Principal";
    [SerializeField, Min(0.05f)] private float fundido = 0.45f;

    private Genero _genero = Genero.Varon;
    private bool _generoElegido;
    private int _inicialElegido = -1;
    private Paso _paso = Paso.Genero;

    // Mientras cambia de paso (o ya se va de la escena) no se acepta nada.
    private bool _ocupada;
    private bool _saliendo;

    // Animacion del Pokemon elegido.
    private Sprite[] _cuadros;
    private float _reloj;

    private void Awake()
    {
        if (botonVaron != null) botonVaron.onClick.AddListener(() => TocarGenero(Genero.Varon));
        if (botonMujer != null) botonMujer.onClick.AddListener(() => TocarGenero(Genero.Mujer));
        if (continuarGenero != null) continuarGenero.onClick.AddListener(() => Ir(Paso.Nombre));
        if (volverGenero != null) volverGenero.onClick.AddListener(VolverAlMenu);

        if (botonNombreOk != null) botonNombreOk.onClick.AddListener(ConfirmarNombre);
        if (volverNombre != null) volverNombre.onClick.AddListener(() => Ir(Paso.Genero));
        if (campoNombre != null)
        {
            campoNombre.onValueChanged.AddListener(_ => ActualizarNombre());
            // Enter dentro del campo: si el nombre sirve, sigue.
            campoNombre.onEndEdit.AddListener(_ =>
            {
                if (Keyboard.current != null &&
                    (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                    ConfirmarNombre();
            });
        }

        for (int i = 0; i < botonesInicial.Length; i++)
        {
            int idx = i; // copia local: sin esto todos los botones eligen el ultimo
            if (botonesInicial[i] == null) continue;
            botonesInicial[i].onClick.AddListener(() => TocarInicial(idx));
            // Pasar por encima (o llegar con las flechas) muestra de quien se trata.
            AgregarEvento(botonesInicial[i].gameObject, EventTriggerType.PointerEnter, () => MostrarDescripcion(idx));
            AgregarEvento(botonesInicial[i].gameObject, EventTriggerType.Select, () => MostrarDescripcion(idx));
            AgregarEvento(botonesInicial[i].gameObject, EventTriggerType.PointerExit, () => MostrarDescripcion(_inicialElegido));
        }
        if (continuarInicial != null) continuarInicial.onClick.AddListener(() => Ir(Paso.Confirmar));
        if (volverInicial != null) volverInicial.onClick.AddListener(() => Ir(Paso.Nombre));

        if (botonEmpezar != null) botonEmpezar.onClick.AddListener(Empezar);
        if (botonVolver != null) botonVolver.onClick.AddListener(() => Ir(Paso.Inicial));
    }

    private void Start()
    {
        // Las fichas se llenan solas desde el array: agregar o cambiar un
        // inicial es tocar el Inspector, no esta clase.
        for (int i = 0; i < iniciales.Length && i < retratosInicial.Length; i++)
        {
            var op = iniciales[i];
            if (op == null) continue;

            if (retratosInicial[i] != null && op.retrato != null)
            {
                retratosInicial[i].sprite = op.retrato;
                retratosInicial[i].preserveAspect = true;
            }
            if (i < nombresInicial.Length && nombresInicial[i] != null)
                nombresInicial[i].text = NombreDe(op);
        }

        if (campoNombre != null) campoNombre.characterLimit = largoMaximo;
        MarcarGenero();
        MarcarInicial();
        ActualizarNombre();
        MostrarDescripcion(-1);

        _paso = Paso.Genero;
        if (panelNombre != null) panelNombre.SetActive(false);
        if (panelInicial != null) panelInicial.SetActive(false);
        if (panelConfirmar != null) panelConfirmar.SetActive(false);
        if (panelGenero != null) panelGenero.SetActive(true);
        PintarPasos();
        StartCoroutine(SeleccionarAlEntrar(botonVaron));
    }

    private void Update()
    {
        AnimarInicial();

        if (_ocupada || _saliendo) return;
        bool escribiendo = campoNombre != null && campoNombre.isFocused;
        if (escribiendo) return;

        GameInputService input = GameInputService.Instance;

        // Volver: Z / Backspace / B (y Escape, que en esta pantalla siempre es volver).
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (escape || input.WasPressed(GameAction.Cancel))
        {
            input.Consume(GameAction.Cancel, GameAction.Menu);
            Atras();
            return;
        }

        // Aceptar con X (o la tecla que este en Opciones). Enter, Espacio y el
        // boton A ya los entrega el EventSystem como "submit": si se aceptaran
        // tambien aca, el boton se apretaria dos veces.
        if (input.WasPressed(GameAction.Confirm) && !LoManejaElEventSystem())
        {
            input.Consume(GameAction.Confirm);
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Button boton = sel != null ? sel.GetComponent<Button>() : null;
            if (boton != null && boton.IsActive() && boton.IsInteractable()) boton.onClick.Invoke();
        }

        // Un clic en el fondo deja sin seleccion: las flechas tienen que seguir andando.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            Seleccionar(BotonPorDefecto());
    }

    private static bool LoManejaElEventSystem()
    {
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
               || (g != null && g.buttonSouth.wasPressedThisFrame);
    }

    // ---- pasos ----

    private void Atras()
    {
        switch (_paso)
        {
            case Paso.Genero: VolverAlMenu(); break;
            case Paso.Nombre: Ir(Paso.Genero); break;
            case Paso.Inicial: Ir(Paso.Nombre); break;
            case Paso.Confirmar: Ir(Paso.Inicial); break;
        }
    }

    private void Ir(Paso destino)
    {
        if (_ocupada || _saliendo || destino == _paso) return;

        // No se avanza a un paso cuyo requisito falta (un boton deshabilitado
        // no deberia poder llegar aca, pero el teclado y los tests si).
        if (destino == Paso.Nombre && !_generoElegido) return;
        if (destino == Paso.Inicial && !NombreValido()) return;
        if (destino == Paso.Confirmar && _inicialElegido < 0) return;

        StartCoroutine(CambiarDePaso(destino));
    }

    private IEnumerator CambiarDePaso(Paso destino)
    {
        _ocupada = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        GameObject saliente = Panel(_paso);
        GameObject entrante = Panel(destino);

        var anim = saliente != null ? saliente.GetComponent<EntradaAnimada>() : null;
        if (anim != null) yield return anim.Salir();
        if (saliente != null) saliente.SetActive(false);

        _paso = destino;
        if (destino == Paso.Confirmar) MostrarResumen();
        if (destino == Paso.Nombre) ActualizarNombre();
        PintarPasos();

        if (entrante != null) entrante.SetActive(true);
        var animEntrante = entrante != null ? entrante.GetComponent<EntradaAnimada>() : null;
        float espera = animEntrante != null ? animEntrante.Duracion : 0f;
        for (float t = 0f; t < espera; t += Time.unscaledDeltaTime) yield return null;

        _ocupada = false;

        if (destino == Paso.Nombre && campoNombre != null)
        {
            campoNombre.Select();
            campoNombre.ActivateInputField();
        }
        else Seleccionar(BotonPorDefecto());
    }

    private GameObject Panel(Paso p)
    {
        switch (p)
        {
            case Paso.Genero: return panelGenero;
            case Paso.Nombre: return panelNombre;
            case Paso.Inicial: return panelInicial;
            default: return panelConfirmar;
        }
    }

    // Donde queda el cursor al llegar a cada paso.
    private Selectable BotonPorDefecto()
    {
        switch (_paso)
        {
            case Paso.Genero:
                return _generoElegido ? (Selectable)continuarGenero : (_genero == Genero.Mujer ? botonMujer : botonVaron);
            case Paso.Nombre:
                return NombreValido() ? (Selectable)botonNombreOk : campoNombre;
            case Paso.Inicial:
                if (_inicialElegido >= 0) return continuarInicial;
                return botonesInicial.Length > 0 ? botonesInicial[0] : null;
            default:
                return botonEmpezar;
        }
    }

    private void PintarPasos()
    {
        int actual = (int)_paso;
        for (int i = 0; i < 4; i++)
        {
            Color c = i == actual ? colorPasoActual : (i < actual ? colorPasoHecho : colorPasoPendiente);
            if (i < etiquetasDePaso.Length && etiquetasDePaso[i] != null) etiquetasDePaso[i].color = c;
            if (i < marcasDePaso.Length && marcasDePaso[i] != null) marcasDePaso[i].color = c;
        }
    }

    // ---- genero ----

    private void TocarGenero(Genero g)
    {
        if (_ocupada || _saliendo) return;

        // Segundo toque sobre la misma ficha: es "si, ese". Sigue.
        if (_generoElegido && _genero == g) { Ir(Paso.Nombre); return; }

        _genero = g;
        _generoElegido = true;
        MarcarGenero();
        Seleccionar(continuarGenero);
    }

    private void MarcarGenero()
    {
        bool varon = _generoElegido && _genero == Genero.Varon;
        bool mujer = _generoElegido && _genero == Genero.Mujer;

        // Sin elegir, los dos a color: no hay uno "apagado" que parezca
        // descartado antes de que el jugador decida.
        if (retratoVaron != null) retratoVaron.color = !_generoElegido || varon ? colorElegido : colorNormal;
        if (retratoMujer != null) retratoMujer.color = !_generoElegido || mujer ? colorElegido : colorNormal;
        MarcarElegido(botonVaron, varon);
        MarcarElegido(botonMujer, mujer);
        if (continuarGenero != null) continuarGenero.interactable = _generoElegido;
    }

    // ---- nombre ----

    private string NombreEscrito() => campoNombre == null ? "" : campoNombre.text.Trim();

    private bool NombreValido() => NombreEscrito().Length > 0;

    private void ActualizarNombre()
    {
        int largo = campoNombre != null ? campoNombre.text.Length : 0;
        if (contadorNombre != null) contadorNombre.text = $"{largo}/{largoMaximo}";
        if (botonNombreOk != null) botonNombreOk.interactable = NombreValido();
        if (avisoNombre != null && NombreValido()) avisoNombre.text = "";
    }

    private void ConfirmarNombre()
    {
        if (_ocupada || _saliendo || _paso != Paso.Nombre) return;
        if (!NombreValido())
        {
            if (avisoNombre != null) avisoNombre.text = "Escribí un nombre para seguir.";
            return;
        }
        if (avisoNombre != null) avisoNombre.text = "";
        Ir(Paso.Inicial);
    }

    // ---- inicial ----

    private void TocarInicial(int i)
    {
        if (_ocupada || _saliendo) return;
        if (i < 0 || i >= iniciales.Length || iniciales[i] == null) return;

        if (_inicialElegido == i) { Ir(Paso.Confirmar); return; }

        _inicialElegido = i;
        MarcarInicial();
        MostrarDescripcion(i);
        Seleccionar(continuarInicial);
    }

    private void MarcarInicial()
    {
        for (int k = 0; k < botonesInicial.Length; k++)
        {
            bool elegido = k == _inicialElegido;
            if (k < retratosInicial.Length && retratosInicial[k] != null)
            {
                retratosInicial[k].color = _inicialElegido < 0 || elegido ? colorElegido : colorNormal;
                // Al dejar de estar elegido vuelve a su primer cuadro.
                if (!elegido && k < iniciales.Length && iniciales[k] != null && iniciales[k].retrato != null)
                    retratosInicial[k].sprite = iniciales[k].retrato;
            }
            MarcarElegido(botonesInicial[k], elegido);
        }

        _cuadros = _inicialElegido >= 0 && iniciales[_inicialElegido].definicion != null
            ? iniciales[_inicialElegido].definicion.battleFrontFrames
            : null;
        _reloj = 0f;
        if (continuarInicial != null) continuarInicial.interactable = _inicialElegido >= 0;
    }

    private void MostrarDescripcion(int i)
    {
        if (descripcionInicial == null) return;
        if (i < 0 || i >= iniciales.Length || iniciales[i] == null)
        {
            descripcionInicial.text = "Pasá por una ficha para conocerlo. Tocala para elegirlo.";
            return;
        }
        descripcionInicial.text = $"{NombreDe(iniciales[i]).ToUpperInvariant()}: {iniciales[i].descripcion}";
    }

    // El elegido se mueve (los cuadros de su animacion de combate): es el
    // feedback de "este es el tuyo".
    private void AnimarInicial()
    {
        if (_cuadros == null || _cuadros.Length < 2 || _inicialElegido < 0) return;
        if (_inicialElegido >= retratosInicial.Length || retratosInicial[_inicialElegido] == null) return;

        _reloj += Time.unscaledDeltaTime;
        int cuadro = Mathf.FloorToInt(_reloj * cuadrosPorSegundo) % _cuadros.Length;
        if (_cuadros[cuadro] != null) retratosInicial[_inicialElegido].sprite = _cuadros[cuadro];
        if (retratoResumenInicial != null && _paso == Paso.Confirmar && _cuadros[cuadro] != null)
            retratoResumenInicial.sprite = _cuadros[cuadro];
    }

    private static string NombreDe(OpcionInicial op) => string.IsNullOrEmpty(op.nombre)
        ? (op.definicion != null ? op.definicion.pokenName : "?")
        : op.nombre;

    // ---- confirmacion ----

    private void MostrarResumen()
    {
        if (_inicialElegido < 0) return;

        var op = iniciales[_inicialElegido];
        string especie = op.definicion != null ? op.definicion.pokenName : op.nombre;
        string gen = _genero == Genero.Varon ? "Varón" : "Mujer";

        if (textoResumen != null)
            textoResumen.text =
                $"<b>{NombreEscrito()}</b>  ({gen})\n" +
                $"Compañero: {especie}  Nv. {nivelInicial}\n\n" +
                "Podés guardar la partida cuando quieras con F2.";

        if (retratoResumenJugador != null)
        {
            Image fuente = _genero == Genero.Varon ? retratoVaron : retratoMujer;
            if (fuente != null) retratoResumenJugador.sprite = fuente.sprite;
            retratoResumenJugador.preserveAspect = true;
        }
        if (retratoResumenInicial != null)
        {
            retratoResumenInicial.sprite = op.retrato;
            retratoResumenInicial.preserveAspect = true;
        }
    }

    // ---- salir de la escena ----

    private void Empezar()
    {
        if (_ocupada || _saliendo) return;

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[Creacion] No hay GameManager. Entra por 'Menu Principal' " +
                           "o agregale uno a esta escena.");
            return;
        }
        if (_inicialElegido < 0 || !_generoElegido || !NombreValido())
        {
            Ir(_inicialElegido < 0 ? Paso.Inicial : (!NombreValido() ? Paso.Nombre : Paso.Genero));
            return;
        }

        _saliendo = true;
        BloquearTodo();

        string nombre = NombreEscrito();
        var elegido = iniciales[_inicialElegido];
        gm.NewGame(nombre, _genero, elegido.definicion, nivelInicial);

        // Guardar YA: si el jugador cierra el juego sin llegar a apretar F2,
        // igual queda una partida para continuar desde el menu.
        gm.SaveGame();
        Debug.Log("[Creacion] Partida creada y guardada: " + nombre + " (" + _genero + ") con " +
                  (elegido.definicion != null ? elegido.definicion.pokenName : "?") + ".");

        FundidoAEscena.Ir(escenaDestino, fundido);
    }

    private void VolverAlMenu()
    {
        if (_ocupada || _saliendo || string.IsNullOrEmpty(escenaAnterior)) return;
        _saliendo = true;
        BloquearTodo();
        FundidoAEscena.Ir(escenaAnterior, fundido);
    }

    // Mientras funde a negro no se toca nada: ni un segundo "Empezar".
    private void BloquearTodo()
    {
        var grupo = GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
    }

    // ---- utilidades ----

    private static void MarcarElegido(Button boton, bool elegido)
    {
        if (boton == null) return;
        var anim = boton.GetComponent<BotonAnimado>();
        if (anim != null) anim.Elegido = elegido;
    }

    private static void Seleccionar(Selectable s)
    {
        if (s == null || EventSystem.current == null || !s.gameObject.activeInHierarchy) return;
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // El primer frame el EventSystem todavia no esta listo para seleccionar.
    private IEnumerator SeleccionarAlEntrar(Selectable s)
    {
        yield return null;
        Seleccionar(s);
    }

    private static void AgregarEvento(GameObject go, EventTriggerType tipo, System.Action accion)
    {
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        var entrada = new EventTrigger.Entry { eventID = tipo };
        entrada.callback.AddListener(_ => accion());
        trigger.triggers.Add(entrada);
    }
}
