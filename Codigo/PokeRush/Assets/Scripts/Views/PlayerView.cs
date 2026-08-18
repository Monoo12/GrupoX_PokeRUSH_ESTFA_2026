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

    [Header("Skin de mujer (opcional)")]
    [Tooltip("Si estan cargadas y la partida es de una jugadora, se usan estas " +
             "en lugar de las de arriba. Vacio = siempre la skin por defecto.")]
    [SerializeField] private DirectionalAnimation walkMujer;
    [SerializeField] private DirectionalAnimation runMujer;

    [Header("Velocidad de animacion")]
    [Tooltip("Cuadros por segundo al caminar.")]
    [SerializeField] private float walkFps = 6f;
    [Tooltip("Cuadros por segundo al correr.")]
    [SerializeField] private float runFps = 10f;

    private float _frameTimer;
    private int _frameIndex;
    private bool _skinResuelta;

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

    // El genero se resuelve en Update y no en Start porque el PlayerModel lo
    // inyecta SceneBootstrapper en SU Start, y el orden entre dos Start no esta
    // garantizado: en Start todavia puede no haber partida.
    private void ResolverSkin()
    {
        if (_skinResuelta) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentPlayer == null) return;

        _skinResuelta = true;
        if (gm.CurrentPlayer.Genero != Genero.Mujer) return;

        if (walkMujer != null && walkMujer.FramesFor(Vector2.down) != null
            && walkMujer.FramesFor(Vector2.down).Length > 0)
        {
            walk = walkMujer;
            if (runMujer != null) run = runMujer;
            Debug.Log("[PlayerView] Partida de mujer: uso la skin femenina.");
        }
    }

    private void Update()
    {
        ResolverSkin();

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
