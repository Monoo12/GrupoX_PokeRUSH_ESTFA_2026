using UnityEngine;

// Traduce el input del mundo a ordenes para el PlayerController.
//
// Ya NO arma sus propias InputAction: todas las teclas salen de
// GameInputService, que es el mismo mapa que usan los menus y el que el jugador
// puede remapear desde Opciones. Antes las teclas estaban escritas aca a mano,
// asi que cambiarlas desde el juego era imposible y "la tecla de accion" vivia
// en dos lugares distintos.
public class PlayerInputController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private BattleController battleController;
    [Tooltip("Opcional. Si esta, mientras haya un dialogo abierto el input va " +
             "a la caja de texto y el jugador no se mueve.")]
    [SerializeField] private DialogueController dialogueController;

    private bool _inBattle = false;

    // Ultima direccion usada para navegar el dialogo. Sirve para que mantener
    // la tecla no barra todas las opciones de golpe.
    private int _navPrevio;

    private static GameInputService Input => GameInputService.Instance;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public void SetBattleMode(bool inBattle)
    {
        _inBattle = inBattle;
    }

    private void Update()
    {
        // Con un menu (o el HUD del combate) abierto, el mundo no escucha NADA.
        // Es lo que evita que abrir la mochila camine al jugador por atras del
        // panel, y que elegir un movimiento cuente como interactuar.
        if (MenuManager.AnyOpen || BattleHudView.IsNavigating) { _navPrevio = 0; return; }

        // El dialogo se queda con el teclado: si no, apretar Aceptar para
        // avanzar la linea tambien volveria a interactuar, y las flechas
        // moverian al jugador por atras de la caja de texto.
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
        if (playerController == null) return;

        Vector2 direction = Input.ReadDirection();
        bool isRunning = Input.IsPressed(GameAction.Run);

        if (direction != Vector2.zero)
            playerController.Move(direction, isRunning);

        if (Input.WasPressed(GameAction.Confirm))
        {
            Input.Consume(GameAction.Confirm);
            playerController.Interact();
        }

        if (Input.WasPressed(GameAction.Menu))
        {
            // Se consumen las dos porque comparten tecla: sin esto el menu que
            // acaba de abrirse veria la misma Z como "volver" y se cerraria.
            Input.Consume(GameAction.Menu, GameAction.Cancel);
            playerController.OpenMenu();
        }
    }

    private void HandleDialogueInput()
    {
        Vector2 dir = Input.ReadDirection();

        // Arriba/izquierda = opcion anterior; abajo/derecha = siguiente.
        int nav = 0;
        if (dir.y > 0.5f) nav = -1;
        else if (dir.y < -0.5f) nav = 1;
        else if (dir.x < -0.5f) nav = -1;
        else if (dir.x > 0.5f) nav = 1;

        if (nav != 0 && nav != _navPrevio)
            dialogueController.OnNavigate(nav);
        _navPrevio = nav;

        if (Input.WasPressed(GameAction.Confirm))
            dialogueController.OnConfirm();
        else if (Input.WasPressed(GameAction.Cancel))
            dialogueController.OnCancel();
    }

    private void HandleBattleInput()
    {
        // La seleccion de acciones en combate la maneja el BattleHudView por
        // botones; aca no hay atajos de teclado.
    }
}
