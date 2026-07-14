using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de inicio ("Press Start"). Espera que el jugador toque el boton
// (o cualquier tecla/click) y decide a que escena ir segun exista o no una
// partida guardada.
public class StartScreenView : MonoBehaviour
{
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

    private void Update()
    {
        if (!anyKeyToStart || _started) return;

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
        if (_started) return; // ya estamos cargando, ignorar toques extra

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
        SceneLoaderService.Instance.LoadScene(destino);
    }
}
