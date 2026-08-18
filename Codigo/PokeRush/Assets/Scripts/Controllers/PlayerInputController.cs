using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private BattleController battleController;
    [Tooltip("Opcional. Si esta, mientras haya un dialogo abierto el input va " +
             "a la caja de texto y el jugador no se mueve.")]
    [SerializeField] private DialogueController dialogueController;

    private bool _inBattle = false;

    // Acciones del nuevo Input System, definidas en codigo: no requieren un
    // asset .inputactions ni configuracion en el inspector. Soportan teclado
    // y gamepad, y se pueden re-mapear mas adelante.
    private InputAction _moveAction;
    private InputAction _runAction;
    private InputAction _interactAction;
    private InputAction _menuAction;

    // Ultima direccion vertical/horizontal usada para navegar el dialogo. Sirve
    // para que mantener la tecla no barra todas las opciones de golpe.
    private int _navPrevio;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);

        // Move: vector 2D. WASD + flechas en teclado, stick izquierdo en gamepad.
        _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        _moveAction.AddBinding("<Gamepad>/leftStick");

        // Run: mantener presionado para correr (Shift o boton este del gamepad).
        _runAction = new InputAction("Run", InputActionType.Button);
        _runAction.AddBinding("<Keyboard>/leftShift");
        _runAction.AddBinding("<Gamepad>/buttonEast");

        // Interact: la tecla de accion del juego es la X. Se dejan Z y Enter
        // como alias porque ya estaban y no molestan.
        _interactAction = new InputAction("Interact", InputActionType.Button);
        _interactAction.AddBinding("<Keyboard>/x");
        _interactAction.AddBinding("<Keyboard>/z");
        _interactAction.AddBinding("<Keyboard>/enter");
        _interactAction.AddBinding("<Gamepad>/buttonSouth");

        // Menu: abrir menu (Escape o Start del gamepad). Dentro de un dialogo
        // cancela, que equivale a elegir la ultima opcion.
        _menuAction = new InputAction("Menu", InputActionType.Button);
        _menuAction.AddBinding("<Keyboard>/escape");
        _menuAction.AddBinding("<Gamepad>/start");
    }

    private void OnEnable()
    {
        _moveAction.Enable();
        _runAction.Enable();
        _interactAction.Enable();
        _menuAction.Enable();
    }

    private void OnDisable()
    {
        _moveAction.Disable();
        _runAction.Disable();
        _interactAction.Disable();
        _menuAction.Disable();
    }

    private void OnDestroy()
    {
        // Liberar las acciones creadas en codigo para no filtrar memoria nativa.
        _moveAction?.Dispose();
        _runAction?.Dispose();
        _interactAction?.Dispose();
        _menuAction?.Dispose();
    }

    public void SetBattleMode(bool inBattle)
    {
        _inBattle = inBattle;
    }

    private void Update()
    {
        // El dialogo se queda con el teclado: si no, apretar X para avanzar la
        // linea tambien volveria a interactuar, y las flechas moverian al
        // jugador por atras de la caja de texto.
        if (dialogueController != null && dialogueController.IsOpen)
        {
            HandleDialogueInput();
            return;
        }

        _navPrevio = 0;

        if (_inBattle)
            HandleBattleInput();
        else
            HandleExplorationInput();
    }

    private void HandleExplorationInput()
    {
        Vector2 direction = _moveAction.ReadValue<Vector2>();
        bool isRunning = _runAction.IsPressed();

        if (direction != Vector2.zero)
            playerController.Move(direction, isRunning);

        if (_interactAction.WasPressedThisFrame())
            playerController.Interact();

        if (_menuAction.WasPressedThisFrame())
            playerController.OpenMenu();
    }

    private void HandleDialogueInput()
    {
        Vector2 dir = _moveAction.ReadValue<Vector2>();

        // Arriba/izquierda = opcion anterior; abajo/derecha = siguiente.
        int nav = 0;
        if (dir.y > 0.5f) nav = -1;
        else if (dir.y < -0.5f) nav = 1;
        else if (dir.x < -0.5f) nav = -1;
        else if (dir.x > 0.5f) nav = 1;

        if (nav != 0 && nav != _navPrevio)
            dialogueController.OnNavigate(nav);
        _navPrevio = nav;

        if (_interactAction.WasPressedThisFrame())
            dialogueController.OnConfirm();
        else if (_menuAction.WasPressedThisFrame())
            dialogueController.OnCancel();
    }

    private void HandleBattleInput()
    {
        // seleccion de movimientos y acciones en batalla - se implementa despues
    }
}
