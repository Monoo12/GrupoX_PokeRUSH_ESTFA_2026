using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private BattleController battleController;

    private bool _inBattle = false;

    // Acciones del nuevo Input System, definidas en codigo: no requieren un
    // asset .inputactions ni configuracion en el inspector. Soportan teclado
    // y gamepad, y se pueden re-mapear mas adelante.
    private InputAction _moveAction;
    private InputAction _runAction;
    private InputAction _interactAction;
    private InputAction _menuAction;

    private void Awake()
    {
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

        // Interact: confirmar / hablar (Z, Enter o boton sur del gamepad).
        _interactAction = new InputAction("Interact", InputActionType.Button);
        _interactAction.AddBinding("<Keyboard>/z");
        _interactAction.AddBinding("<Keyboard>/enter");
        _interactAction.AddBinding("<Gamepad>/buttonSouth");

        // Menu: abrir menu (Escape o Start del gamepad).
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

    private void HandleBattleInput()
    {
        // seleccion de movimientos y acciones en batalla - se implementa despues
    }
}
