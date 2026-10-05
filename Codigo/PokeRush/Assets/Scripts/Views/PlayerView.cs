using UnityEngine;

// Cambia el sprite del jugador segun hacia donde mira (Facing), si se mueve
// (IsMoving) y si corre (IsRunning). Lee el estado del PlayerController, asi la
// logica de movimiento queda separada de lo visual (MVC).
// Va en el mismo GameObject que el SpriteRenderer del jugador.
//
// La animacion va ATADA A LOS TILES, no al reloj (playtest del 5-oct: "parece
// cojo"). Antes avanzaba un cuadro cada 1/fps segundos y volvia al cuadro 0
// cada vez que el personaje quedaba quieto, y entre un tile y el siguiente
// siempre queda quieto un instante: a 4 tiles por segundo y 8 cuadros por
// segundo, cada tile mostraba "parado -> paso con el pie derecho" y el pie
// izquierdo no se veia nunca. Ahora cada tile es medio ciclo: la primera mitad
// con un pie adelante y la segunda parado, y el tile siguiente con el otro
// pie, igual que en los juegos.
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

    [Header("Ritmo")]
    [Tooltip("Parte del paso (0-1) que se ve con el pie adelante; el resto, parado.")]
    [SerializeField, Range(0.2f, 0.9f)] private float mitadConElPie = 0.55f;

    private int _ultimoPaso = -1;
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
            Animate(frames);
        else
            ShowIdle(frames);
    }

    // Los cuatro cuadros de cada direccion son: 0 parado, 1 un pie, 2 parado
    // (en algunas hojas, otra pose de parado), 3 el otro pie. Los tiles pares
    // van 1 -> 2 y los impares 3 -> 0, asi dos tiles seguidos recorren el ciclo
    // entero y los pies se alternan.
    private void Animate(Sprite[] frames)
    {
        int paso = playerController.PasosDados;
        if (paso != _ultimoPaso)
        {
            _ultimoPaso = paso;
            Pasos.DelJugador(playerController.IsRunning);
        }

        if (frames.Length < 4)
        {
            spriteRenderer.sprite = frames[Mathf.Min(1, frames.Length - 1)];
            return;
        }

        bool pieDerecho = (paso & 1) == 0;
        bool conElPieAdelante = playerController.ProgresoDelPaso < mitadConElPie;
        int cuadro = pieDerecho ? (conElPieAdelante ? 1 : 2) : (conElPieAdelante ? 3 : 0);
        spriteRenderer.sprite = frames[cuadro];
    }

    private void ShowIdle(Sprite[] frames)
    {
        spriteRenderer.sprite = frames[0];
    }
}
