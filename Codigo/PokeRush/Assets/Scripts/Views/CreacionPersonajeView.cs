using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Pantalla de creacion de personaje: genero -> nombre -> inicial -> a la torre.
//
// Es un asistente de pasos, no tres escenas: se prende y apaga un panel por
// paso, asi el jugador puede volver atras sin recargar nada.
//
// Al confirmar arma la partida (GameManager.NewGame), la GUARDA de una — para
// que exista un save aunque el jugador cierre el juego enseguida — y carga la
// escena del mapa.
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

    [Header("Paneles (uno por paso)")]
    [SerializeField] private GameObject panelGenero;
    [SerializeField] private GameObject panelNombre;
    [SerializeField] private GameObject panelInicial;
    [SerializeField] private GameObject panelConfirmar;

    [Header("Genero")]
    [SerializeField] private Button botonVaron;
    [SerializeField] private Button botonMujer;
    [SerializeField] private Image retratoVaron;
    [SerializeField] private Image retratoMujer;
    [SerializeField] private Color colorElegido = Color.white;
    [SerializeField] private Color colorNormal = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Header("Nombre")]
    [SerializeField] private InputField campoNombre;
    [SerializeField] private Button botonNombreOk;
    [SerializeField] private Text avisoNombre;
    [SerializeField] private int largoMaximo = 10;

    [Header("Inicial")]
    [SerializeField] private OpcionInicial[] iniciales = new OpcionInicial[3];
    [SerializeField] private Button[] botonesInicial = new Button[3];
    [SerializeField] private Image[] retratosInicial = new Image[3];
    [SerializeField] private Text[] nombresInicial = new Text[3];
    [SerializeField] private Text descripcionInicial;
    [SerializeField] private int nivelInicial = 15;

    [Header("Confirmacion")]
    [SerializeField] private Text textoResumen;
    [SerializeField] private Button botonEmpezar;
    [SerializeField] private Button botonVolver;

    [Header("Destino")]
    [SerializeField] private string escenaDestino = "NestorGOD2";

    private Genero _genero = Genero.Varon;
    private int _inicialElegido = -1;

    private void Awake()
    {
        if (botonVaron != null) botonVaron.onClick.AddListener(ElegirVaron);
        if (botonMujer != null) botonMujer.onClick.AddListener(ElegirMujer);
        if (botonNombreOk != null) botonNombreOk.onClick.AddListener(ConfirmarNombre);
        if (botonEmpezar != null) botonEmpezar.onClick.AddListener(Empezar);
        if (botonVolver != null) botonVolver.onClick.AddListener(VolverAInicial);

        for (int i = 0; i < botonesInicial.Length; i++)
        {
            int idx = i; // copia local: sin esto todos los botones eligen el ultimo
            if (botonesInicial[i] != null)
                botonesInicial[i].onClick.AddListener(delegate { ElegirInicial(idx); });
        }
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
            {
                nombresInicial[i].text = string.IsNullOrEmpty(op.nombre)
                    ? (op.definicion != null ? op.definicion.pokenName : "?")
                    : op.nombre;
            }
        }

        AplicarGenero(Genero.Varon);
        Paso(panelGenero);
    }

    // ---- pasos ----

    private void Paso(GameObject panel)
    {
        if (panelGenero != null) panelGenero.SetActive(panel == panelGenero);
        if (panelNombre != null) panelNombre.SetActive(panel == panelNombre);
        if (panelInicial != null) panelInicial.SetActive(panel == panelInicial);
        if (panelConfirmar != null) panelConfirmar.SetActive(panel == panelConfirmar);
    }

    private void ElegirVaron() { AplicarGenero(Genero.Varon); IrANombre(); }
    private void ElegirMujer() { AplicarGenero(Genero.Mujer); IrANombre(); }
    private void VolverAInicial() { Paso(panelInicial); }

    private void AplicarGenero(Genero g)
    {
        _genero = g;
        if (retratoVaron != null) retratoVaron.color = g == Genero.Varon ? colorElegido : colorNormal;
        if (retratoMujer != null) retratoMujer.color = g == Genero.Mujer ? colorElegido : colorNormal;
    }

    private void IrANombre()
    {
        Paso(panelNombre);
        if (campoNombre != null)
        {
            campoNombre.characterLimit = largoMaximo;
            campoNombre.Select();
            campoNombre.ActivateInputField();
        }
    }

    private void ConfirmarNombre()
    {
        string nombre = campoNombre == null ? "" : campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            if (avisoNombre != null) avisoNombre.text = "Escribi un nombre para seguir.";
            return;
        }
        if (avisoNombre != null) avisoNombre.text = "";
        Paso(panelInicial);
    }

    private void ElegirInicial(int i)
    {
        if (i < 0 || i >= iniciales.Length || iniciales[i] == null) return;
        _inicialElegido = i;

        if (descripcionInicial != null) descripcionInicial.text = iniciales[i].descripcion;

        for (int k = 0; k < retratosInicial.Length; k++)
            if (retratosInicial[k] != null)
                retratosInicial[k].color = k == i ? colorElegido : colorNormal;

        MostrarResumen();
        Paso(panelConfirmar);
    }

    private void MostrarResumen()
    {
        if (textoResumen == null || _inicialElegido < 0) return;

        var op = iniciales[_inicialElegido];
        string nombre = campoNombre == null ? "" : campoNombre.text.Trim();
        string especie = op.definicion != null ? op.definicion.pokenName : op.nombre;
        string gen = _genero == Genero.Varon ? "Varon" : "Mujer";

        textoResumen.text =
            nombre + "   (" + gen + ")\n" +
            "Companiero: " + especie + "  Nv" + nivelInicial + "\n\n" +
            "Podes guardar la partida cuando quieras\n" +
            "apretando F2.";
    }

    // ---- arranque ----

    private void Empezar()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[Creacion] No hay GameManager. Entra por 'Menu Principal' " +
                           "o agregale uno a esta escena.");
            return;
        }
        if (_inicialElegido < 0)
        {
            Paso(panelInicial);
            return;
        }

        string nombre = campoNombre == null ? "JUGADOR" : campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre)) nombre = "JUGADOR";

        var elegido = iniciales[_inicialElegido];
        gm.NewGame(nombre, _genero, elegido.definicion, nivelInicial);

        // Guardar YA: si el jugador cierra el juego sin llegar a apretar F2,
        // igual queda una partida para continuar desde el menu.
        gm.SaveGame();
        Debug.Log("[Creacion] Partida creada y guardada: " + nombre + " (" + _genero + ") con " +
                  (elegido.definicion != null ? elegido.definicion.pokenName : "?") + ".");

        SceneManager.LoadScene(escenaDestino);
    }
}
