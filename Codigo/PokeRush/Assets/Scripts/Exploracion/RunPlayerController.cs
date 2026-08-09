using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento en tercera persona del Poken que maneja el jugador.
//
// Viene del experimento de combate de accion (rama Ruta-Accion), que se
// descarto. El movimiento fue lo unico de ese experimento que quedo bien, asi
// que se conserva: sirve igual para una zona de exploracion en 3D desde la que
// se entra a los combates por turnos. Lo unico que se saco son los enganches al
// sistema de habilidades, que ya no existe; en su lugar quedaron dos
// propiedades publicas que puede tocar cualquiera.
//
// Usa CharacterController y no Rigidbody: el movimiento es directo y
// determinista, sin el temblequeo que aparece cuando la fisica empuja a un
// personaje contra una pared.
//
// El desplazamiento lo mueve este script, no la animacion: los clips del pack de
// UNITE vienen sin root motion (verificado sobre los 25 clips de Cinderace), asi
// que corren en el lugar y el controlador los desplaza.
//
// Las acciones de input se declaran en codigo, igual que en PlayerInputController:
// no hace falta el asset .inputactions ni cablearlas en el inspector.
[RequireComponent(typeof(CharacterController))]
public class RunPlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Camara que define hacia donde es 'adelante'. Si esta vacio se usa la principal.")]
    [SerializeField] private Transform camaraReferencia;

    [Header("Velocidad")]
    [Tooltip("Unidades por segundo caminando.")]
    [SerializeField] private float velocidad = 4.5f;
    [Tooltip("Unidades por segundo con Shift.")]
    [SerializeField] private float velocidadCorriendo = 7f;

    [Header("Respuesta")]
    [Tooltip("Que tan rapido alcanza la velocidad objetivo. Mas alto = mas seco.")]
    [SerializeField] private float aceleracion = 18f;
    [Tooltip("Que tan rapido frena al soltar. Mas alto = frena en seco.")]
    [SerializeField] private float frenado = 30f;
    [Tooltip("Grados por segundo al girar hacia donde se mueve.")]
    [SerializeField] private float velocidadGiro = 900f;
    [Tooltip("Cuanta velocidad conserva mientras todavia no encara hacia donde " +
             "quiere ir, de 0 a 1. Bajo = pivotea casi en el lugar y despues " +
             "arranca; alto = sigue avanzando mientras gira y se lee como que patina.")]
    [SerializeField, Range(0f, 1f)] private float velocidadEnGiro = 0.15f;

    [Header("Apuntado")]
    [Tooltip("Si esta activo, el Poken encara siempre al cursor en vez de a su " +
             "direccion de movimiento.")]
    [SerializeField] private bool encararAlCursor;

    [Header("Suelo")]
    [Tooltip("Gravedad aplicada mientras no toca el piso.")]
    [SerializeField] private float gravedad = -25f;

    private CharacterController _controlador;
    private InputAction _mover;
    private InputAction _correr;

    // La rapidez es un ESCALAR y el avance va siempre sobre transform.forward.
    // Guardarla como vector permitia que, al invertir la direccion, el vector
    // interpolara de la vieja a la nueva y el personaje describiera una curva
    // mientras giraba: eso es el resbalon lateral. Siendo escalar sobre el
    // frente, el desplazamiento de costado es imposible por construccion.
    private float _rapidez;
    private float _velocidadVertical;

    /// Deja al Poken clavado. Lo pone en true quien necesite quitarle el control:
    /// una cinematica, un dialogo, o la transicion al combate por turnos.
    public bool Bloqueado { get; set; }

    /// Multiplica la velocidad. 0 = inmovilizado, 0,5 = ralentizado a la mitad.
    /// Queda expuesto para que un efecto de estado lo module desde afuera.
    public float MultiplicadorDeVelocidad { get; set; } = 1f;

    // Estado que leen la animacion y el HUD.
    public bool EstaEnMovimiento => _rapidez > 0.05f;

    /// Rapidez actual normalizada contra la velocidad de correr: 0 quieto, 1 a
    /// full. Es lo que mezcla las animaciones de espera y carrera.
    public float Rapidez01 => Mathf.Clamp01(_rapidez / Mathf.Max(0.01f, velocidadCorriendo));

    public Vector3 Mirada => transform.forward;

    private void Awake()
    {
        _controlador = GetComponent<CharacterController>();

        if (camaraReferencia == null && Camera.main != null)
            camaraReferencia = Camera.main.transform;

        _mover = new InputAction("Mover", InputActionType.Value, expectedControlType: "Vector2");
        _mover.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _mover.AddBinding("<Gamepad>/leftStick");

        _correr = new InputAction("Correr", InputActionType.Button);
        _correr.AddBinding("<Keyboard>/leftShift");
        _correr.AddBinding("<Gamepad>/leftStickPress");
    }

    private void OnEnable()
    {
        _mover.Enable();
        _correr.Enable();
    }

    private void OnDisable()
    {
        _mover.Disable();
        _correr.Disable();
    }

    private void OnDestroy()
    {
        // Las acciones creadas en codigo hay que liberarlas a mano.
        _mover?.Dispose();
        _correr?.Dispose();
    }

    private void Update()
    {
        Vector3 deseada = DireccionDeseada();

        // Girar ANTES de avanzar: asi el paso de este cuadro ya sale hacia donde
        // el personaje quedo mirando, y no hacia donde miraba el cuadro anterior.
        Girar(deseada);
        AplicarRapidez(deseada);
        AplicarGravedad();

        // Encarando al cursor el avance va por el input y no por el frente: es el
        // modo en que se mira hacia un lado mientras te movés hacia otro. Ahi el
        // desplazamiento lateral es intencional.
        Vector3 avance = encararAlCursor && deseada.sqrMagnitude > 0.01f
            ? deseada.normalized
            : transform.forward;

        Vector3 total = avance * _rapidez + Vector3.up * _velocidadVertical;
        _controlador.Move(total * Time.deltaTime);
    }

    // El input es relativo a la camara: "W" es siempre hacia el fondo de la
    // pantalla, no hacia el eje Z del mundo. Con una camara cenital inclinada,
    // moverse por el eje del mundo se siente torcido.
    private Vector3 DireccionDeseada()
    {
        if (Bloqueado) return Vector3.zero;

        Vector2 entrada = _mover.ReadValue<Vector2>();
        if (entrada.sqrMagnitude < 0.01f) return Vector3.zero;

        Vector3 adelante = Vector3.forward;
        Vector3 derecha = Vector3.right;

        if (camaraReferencia != null)
        {
            // Solo el giro horizontal: la inclinacion de la camara no debe
            // hundir al personaje contra el piso.
            adelante = Vector3.ProjectOnPlane(camaraReferencia.forward, Vector3.up).normalized;
            derecha = Vector3.ProjectOnPlane(camaraReferencia.right, Vector3.up).normalized;
        }

        Vector3 direccion = adelante * entrada.y + derecha * entrada.x;
        return Vector3.ClampMagnitude(direccion, 1f);
    }

    private void AplicarRapidez(Vector3 deseada)
    {
        if (deseada.sqrMagnitude < 0.01f)
        {
            _rapidez = Mathf.MoveTowards(_rapidez, 0f, frenado * Time.deltaTime);
            return;
        }

        float objetivo = (_correr.IsPressed() ? velocidadCorriendo : velocidad)
                         * Mathf.Max(0f, MultiplicadorDeVelocidad);

        // Freno mientras todavia no encara hacia donde quiere ir. Girar sin
        // frenar deja al personaje avanzando en una direccion con la animacion
        // mirando a otra; frenando, pivotea y despues arranca.
        float alineacion = Vector3.Dot(transform.forward, deseada.normalized);
        objetivo *= Mathf.Lerp(velocidadEnGiro, 1f, Mathf.Clamp01(alineacion));

        // Frenar es mas rapido que acelerar: arrancar de golpe se siente
        // artificial, pero frenar despacio se siente resbaladizo.
        float ritmo = objetivo < _rapidez ? frenado : aceleracion;
        _rapidez = Mathf.MoveTowards(_rapidez, objetivo, ritmo * Time.deltaTime);
    }

    private void AplicarGravedad()
    {
        if (_controlador.isGrounded && _velocidadVertical < 0f)
        {
            // Un valor chico y negativo, no cero: mantiene el contacto con el
            // piso para que isGrounded no parpadee al bajar una pendiente.
            _velocidadVertical = -2f;
            return;
        }
        _velocidadVertical += gravedad * Time.deltaTime;
    }

    private void Girar(Vector3 deseada)
    {
        Vector3 hacia = encararAlCursor ? DireccionAlCursor() : deseada;
        if (hacia.sqrMagnitude < 0.01f) return;

        Quaternion objetivo = Quaternion.LookRotation(hacia, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, objetivo, velocidadGiro * Time.deltaTime);
    }

    // Proyecta el cursor sobre el plano del suelo a la altura del personaje.
    private Vector3 DireccionAlCursor()
    {
        if (Camera.main == null || Mouse.current == null) return Vector3.zero;

        Ray rayo = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        var suelo = new Plane(Vector3.up, transform.position);

        if (!suelo.Raycast(rayo, out float distancia)) return Vector3.zero;

        Vector3 punto = rayo.GetPoint(distancia);
        return Vector3.ProjectOnPlane(punto - transform.position, Vector3.up);
    }
}
