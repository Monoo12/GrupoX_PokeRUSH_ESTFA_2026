using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de inicio ("Press Start"). Espera que el jugador toque el boton
// (o cualquier tecla/click) y decide a que escena ir segun exista o no una
// partida guardada.
//
// La pantalla entra con un fundido desde negro, el PRESS START "respira"
// (PulsoSuave sobre una copia exacta de ese pedazo del arte) y al aceptar
// destella y funde a negro hacia la escena siguiente (FundidoAEscena), en vez
// de cortar de golpe.
public class StartScreenView : MonoBehaviour
{
    [Header("Presentacion (opcionales)")]
    [Tooltip("Grupo de toda la pantalla: entra desde transparente.")]
    [SerializeField] private CanvasGroup pantalla;
    [SerializeField, Min(0f)] private float fundidoDeEntrada = 0.7f;
    [Tooltip("El PRESS START que respira y destella al aceptar.")]
    [SerializeField] private PulsoSuave pressStart;
    [Tooltip("Cuanto destella antes de fundir a negro.")]
    [SerializeField, Min(0f)] private float destello = 0.45f;
    [SerializeField, Min(0.05f)] private float fundidoDeSalida = 0.45f;

    [Header("Boton")]
    // Boton "Press Start". Se puede dejar null si solo se usa "cualquier tecla".
    [SerializeField] private Button startButton;

    [Header("Escenas destino")]
    // Escena a la que ir si YA existe una partida guardada (menu de elegir save).
    [SerializeField] private string sceneWithSave = "MenuSaves";
    // Escena a la que ir si NO hay ninguna partida (arranque del juego).
    [SerializeField] private string sceneNewGame = "IntroJuego";

    [Header("Opciones")]
    // Si esta activo, ademas del boton, cualquier tecla o click avanza.
    [SerializeField] private bool anyKeyToStart = true;

    // Slot de guardado a consultar. Por ahora siempre 1 (igual que SaveService).
    [SerializeField] private int playerId = 1;

    // Evita que un doble toque dispare dos cargas de escena seguidas.
    private bool _started;

    private void Awake()
    {
        // Enganchamos el boton por codigo para no depender del Inspector.
        if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartPressed);
    }

    private void Start()
    {
        if (pantalla != null) StartCoroutine(Aparecer());
    }

    private IEnumerator Aparecer()
    {
        pantalla.alpha = 0f;
        for (float t = 0f; t < fundidoDeEntrada; t += Time.unscaledDeltaTime)
        {
            pantalla.alpha = t / fundidoDeEntrada;
            yield return null;
        }
        pantalla.alpha = 1f;
    }

    // Los primeros instantes no se acepta nada: el clic con el que se llego
    // (o un toque apurado) no tiene que saltear la pantalla de titulo.
    private bool Lista => Time.timeSinceLevelLoad > Mathf.Min(0.35f, fundidoDeEntrada);

    private void Update()
    {
        if (!anyKeyToStart || _started || !Lista) return;

        // "Press Start" clasico: cualquier tecla, click o toque tambien arranca.
        // El proyecto usa el Input System nuevo (activeInputHandler = 1), por eso
        // se consulta Keyboard/Mouse/Touchscreen.current en vez de Input.anyKeyDown.
        //
        // OJO: mouse y touch se detectan AL SOLTAR (wasReleased), no al apretar.
        // Si cambiamos de escena con el boton todavia apretado, el EventSystem de
        // la escena nueva interpreta la soltada como un click sobre sus botones
        // (click-through) y puede "tocar" algo sin querer (ej: Borrar partida).
        bool tecla = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        bool toque = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;

        if (tecla || click || toque)
        {
            OnStartPressed();
        }
    }

    // Handler unico: decide el destino y carga la escena.
    private void OnStartPressed()
    {
        if (_started || !Lista) return; // ya estamos cargando, ignorar toques extra

        // HasSaveData necesita la conexion SQLite abierta. GameManager la abre en
        // su Awake (DatabaseService.Instance). Si esta pantalla es la primera del
        // juego, asegurate de que haya un GameManager en la escena (ver instrucciones).
        bool tieneSave = SaveService.Instance.HasSaveData(playerId);

        string destino = tieneSave ? sceneWithSave : sceneNewGame;
        Debug.Log($"[StartScreen] Save encontrado: {tieneSave}. Cargando escena '{destino}'...");

        // Si la escena no esta en Build Settings (o no existe), LoadScene falla.
        // Avisamos claro y NO marcamos _started, asi se puede reintentar.
        if (!Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogError($"[StartScreen] La escena '{destino}' no se puede cargar. " +
                "Verifica que exista y este agregada en File > Build Settings (Add Open Scenes).");
            return;
        }

        _started = true;
        Sonidos.Confirmar();
        StartCoroutine(Salir(destino));
    }

    // El "aceptado" clasico: el PRESS START destella y recien ahi funde a negro.
    private IEnumerator Salir(string destino)
    {
        if (startButton != null) startButton.interactable = false;
        if (pressStart != null)
        {
            pressStart.Destellar(destello);
            for (float t = 0f; t < destello; t += Time.unscaledDeltaTime) yield return null;
        }
        FundidoAEscena.Ir(destino, fundidoDeSalida);
    }
}
