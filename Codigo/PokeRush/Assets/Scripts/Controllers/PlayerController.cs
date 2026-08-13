using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerInputController inputController;
    [SerializeField] private PauseMenuView pauseMenuView;

    [Header("Movimiento por grilla")]
    [Tooltip("Medida de cada celda del Tilemap en unidades de mundo (normalmente 1).")]
    [SerializeField] private float tileSize = 1f;
    [Tooltip("Velocidad al caminar, en unidades por segundo.")]
    [SerializeField] private float walkSpeed = 4f;
    [Tooltip("Velocidad al correr (Shift), en unidades por segundo.")]
    [SerializeField] private float runSpeed = 7f;
    [Tooltip("Capas que bloquean el paso (paredes, objetos solidos, NPCs).")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Radio del chequeo de colision en el tile destino. Menor a medio tile.")]
    [SerializeField] private float collisionCheckRadius = 0.3f;

    [Header("Interaccion")]
    [Tooltip("Capas donde buscar cosas con las que interactuar (paneles, NPCs, puertas).")]
    [SerializeField] private LayerMask interactableMask;
    [Tooltip("Radio del chequeo en el tile de adelante.")]
    [SerializeField] private float interactionRadius = 0.4f;

    private PlayerModel _playerModel;
    private StoryModel _storyModel;

    // Estado del movimiento por grilla
    private Vector2 _targetPosition;
    private bool _isMoving;
    private bool _isRunning;
    private Vector2 _facing = Vector2.down; // ultima direccion mirada (para animaciones futuras)

    // Lectura publica para otros sistemas (interaccion, animaciones)
    public bool IsMoving => _isMoving;
    public bool IsRunning => _isRunning;
    public Vector2 Facing => _facing;

    private void Awake()
    {
        // El personaje arranca quieto, alineado a su tile actual.
        _targetPosition = transform.position;
    }

    public void Initialize(PlayerModel playerModel, StoryModel storyModel)
    {
        _playerModel = playerModel;
        _storyModel = storyModel;
    }

    // Busca algo con que interactuar en el tile de ADELANTE (no en el propio),
    // asi que hay que estar mirando la cosa. Lo llama PlayerInputController.
    public void Interact()
    {
        if (_playerModel == null || _storyModel == null) return;
        if (_isMoving) return; // a mitad de un paso no se interactua

        Vector2 frente = (Vector2)transform.position + _facing * tileSize;

        // OverlapCircleAll y no OverlapCircle: un panel puede tener el collider
        // en un hijo, o haber dos cosas cerca. Nos quedamos con la primera que
        // consuma la interaccion.
        Collider2D[] cerca = Physics2D.OverlapCircleAll(frente, interactionRadius, interactableMask);
        foreach (Collider2D c in cerca)
        {
            // GetComponentInParent para que ande aunque el collider este en un hijo
            var interactuable = c.GetComponentInParent<IInteractable>();
            if (interactuable == null) continue;
            if (interactuable.Interact(_playerModel, _storyModel)) return;
        }
    }

    // Termina el paso en curso al instante y deja al jugador alineado a la
    // grilla. Lo llama MapBattleBridge antes de esconder el mapa: si el objeto
    // se desactiva a mitad de una corrutina de movimiento, esa corrutina muere
    // y el jugador queda entre dos tiles para siempre.
    public void SnapToGrid()
    {
        StopAllCoroutines();
        _isMoving = false;
        float z = transform.position.z;
        transform.position = new Vector3(_targetPosition.x, _targetPosition.y, z);
    }

    // Mueve al jugador de golpe, cortando cualquier paso en curso. Lo usa
    // DoorTrigger para pasar de sala.
    public void TeleportTo(Vector3 posicion, Vector2 mirandoHacia)
    {
        StopAllCoroutines();
        _isMoving = false;

        transform.position = new Vector3(posicion.x, posicion.y, transform.position.z);
        _targetPosition = transform.position;

        if (mirandoHacia != Vector2.zero) _facing = mirandoHacia;
    }

    public void OpenMenu()
    {
        // La misma tecla abre y cierra el menu de pausa (toggle).
        if (pauseMenuView == null || _playerModel == null) return;

        if (pauseMenuView.IsOpen)
            pauseMenuView.Hide();
        else
            pauseMenuView.Show(_playerModel);
    }

    public void SaveGame()
    {
        // delegar a SaveService - se implementa despues
    }

    // Movimiento por grilla: avanza de a un tile. Mientras el personaje se desliza
    // hacia el tile destino ignora nuevos pedidos de movimiento. Lo llama cada frame
    // PlayerInputController mientras se mantenga una direccion.
    public void Move(Vector2 direction, bool isRunning)
    {
        if (_isMoving) return; // ya estoy yendo a un tile: ignoro hasta llegar

        // 4 direcciones (sin diagonales): priorizo el eje horizontal sobre el vertical
        Vector2 step;
        if (Mathf.Abs(direction.x) > 0.01f)
            step = new Vector2(Mathf.Sign(direction.x), 0f);
        else if (Mathf.Abs(direction.y) > 0.01f)
            step = new Vector2(0f, Mathf.Sign(direction.y));
        else
            return;

        _facing = step; // recuerdo hacia donde mira aunque no pueda avanzar

        Vector2 destination = (Vector2)transform.position + step * tileSize;

        // Si hay un obstaculo en el tile destino, no me muevo (choco contra la pared).
        if (Physics2D.OverlapCircle(destination, collisionCheckRadius, obstacleMask) != null)
            return;

        _isRunning = isRunning;
        _targetPosition = destination;
        StartCoroutine(MoveRoutine(isRunning ? runSpeed : walkSpeed));
    }

    private IEnumerator MoveRoutine(float speed)
    {
        _isMoving = true;

        float z = transform.position.z; // conservo la Z (orden de dibujo / camara)

        // Deslizo suave desde el tile actual hasta el destino.
        while ((Vector2)transform.position != _targetPosition)
        {
            Vector2 next = Vector2.MoveTowards(
                transform.position, _targetPosition, speed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, z);
            yield return null;
        }

        // Alineo exacto a la grilla para que no se acumule error.
        transform.position = new Vector3(_targetPosition.x, _targetPosition.y, z);
        _isMoving = false;
    }

    public void ChangePokenInBattle(PokenModel poken)
    {
        // cambiar pokemon activo durante batalla - se implementa despues
    }
}