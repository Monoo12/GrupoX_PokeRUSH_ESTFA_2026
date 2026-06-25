using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private BattleController battleController;

    private bool _inBattle = false;

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
        Vector2 direction = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (direction != Vector2.zero)
            playerController.Move(direction, isRunning);

        if (Input.GetKeyDown(KeyCode.Z))
            playerController.Interact();

        if (Input.GetKeyDown(KeyCode.Escape))
            playerController.OpenMenu();
    }

    private void HandleBattleInput()
    {
        // seleccion de movimientos y acciones en batalla — se implementa despues
    }
}