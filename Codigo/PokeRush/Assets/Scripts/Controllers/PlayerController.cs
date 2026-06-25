using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerInputController inputController;

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

    private PlayerModel _playerModel;
    private StoryModel _storyModel;

    // Estado del movimiento por grilla
    private Vector2 _targetPosition;
    private bool _isMoving;
    private Vector2 _facing = Vector2.down; // ultima direccion mirada (para animaciones futuras)

    // Lectura publica para otros sistemas (interaccion, animaciones)
    public bool IsMoving => _isMoving;
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

    public void Interact()
    {
        // detectar NPC cercano e iniciar dialogo o batalla - se implementa despues
    }

    public void OpenMenu()
    {
        // abrir menu de inventario o equipo - se implementa despues
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