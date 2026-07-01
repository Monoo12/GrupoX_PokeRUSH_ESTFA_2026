using UnityEngine;

// Cambia el sprite del jugador segun hacia donde mira (Facing), si se mueve
// (IsMoving) y si corre (IsRunning). Lee el estado del PlayerController, asi la
// logica de movimiento queda separada de lo visual (MVC).
// Va en el mismo GameObject que el SpriteRenderer del jugador.
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerView : MonoBehaviour
{
    // Un set de cuadros por cada una de las 4 direcciones. El primer cuadro de
    // cada direccion se usa como pose quieta.
    [System.Serializable]
    public class DirectionalAnimation
    {
        public Sprite[] down;
        public Sprite[] up;
        public Sprite[] left;
        public Sprite[] right;

        public Sprite[] FramesFor(Vector2 facing)
        {
            // 4 direcciones: el eje dominante decide a donde mira.
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
                return facing.x > 0f ? right : left;
            return facing.y > 0f ? up : down;
        }
    }

    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animaciones (4 cuadros por direccion)")]
    [SerializeField] private DirectionalAnimation walk;
    [SerializeField] private DirectionalAnimation run;

    [Header("Velocidad de animacion")]
    [Tooltip("Cuadros por segundo al caminar.")]
    [SerializeField] private float walkFps = 6f;
    [Tooltip("Cuadros por segundo al correr.")]
    [SerializeField] private float runFps = 10f;

    private float _frameTimer;
    private int _frameIndex;

    // Autollenar referencias al agregar el componente en el editor.
    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerController = GetComponentInParent<PlayerController>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        bool moving = playerController.IsMoving;
        bool running = moving && playerController.IsRunning;

        DirectionalAnimation set = running ? run : walk;
        Sprite[] frames = set.FramesFor(playerController.Facing);

        // Si todavia no cargaste los sprites de correr, uso los de caminar.
        if ((frames == null || frames.Length == 0) && running)
            frames = walk.FramesFor(playerController.Facing);

        // Sin sprites asignados para esa direccion: no piso lo que haya puesto.
        if (frames == null || frames.Length == 0)
            return;

        if (moving)
            Animate(frames, running ? runFps : walkFps);
        else
            ShowIdle(frames);
    }

    private void Animate(Sprite[] frames, float fps)
    {
        if (frames.Length == 1)
        {
            spriteRenderer.sprite = frames[0];
            return;
        }

        _frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(0.01f, fps);
        if (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % frames.Length;
        }

        // Por si cambie a un set con menos cuadros (walk <-> run).
        _frameIndex = Mathf.Clamp(_frameIndex, 0, frames.Length - 1);
        spriteRenderer.sprite = frames[_frameIndex];
    }

    private void ShowIdle(Sprite[] frames)
    {
        _frameTimer = 0f;
        _frameIndex = 0;
        spriteRenderer.sprite = frames[0];
    }
}
