using UnityEngine;
using UnityEngine.InputSystem;

// Traduce el input del jugador en habilidades.
//
// Basico en click izquierdo, habilidades en 1 y 2. Las habilidades se apuntan
// MANTENIENDO la tecla: mientras esta apretada se dibuja el indicador y se puede
// mover el cursor, y se lanza al soltarla. Dispararlas de golpe al presionar
// seria mas rapido pero saca la decision de donde cae el area, que es justamente
// lo que hay que evaluar en esta fase.
//
// Las acciones se declaran en codigo, como en el resto del proyecto.
[RequireComponent(typeof(AbilityRunner))]
public class RunPlayerAbilities : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RunCameraRig camaraRig;
    [SerializeField] private AbilityIndicator indicador;
    [SerializeField] private BasicAttackChain basico;

    private AbilityRunner _ejecutor;
    private InputAction _atacar;
    private InputAction _habilidad1;
    private InputAction _habilidad2;
    private int _apuntando = -1;

    /// Slot que se esta apuntando, o -1. Lo lee el HUD para resaltar el icono.
    public int Apuntando => _apuntando;

    private void Awake()
    {
        _ejecutor = GetComponent<AbilityRunner>();
        if (basico == null) basico = GetComponent<BasicAttackChain>();
        if (camaraRig == null && Camera.main != null) camaraRig = Camera.main.GetComponent<RunCameraRig>();

        _atacar = new InputAction("Atacar", InputActionType.Button);
        _atacar.AddBinding("<Mouse>/leftButton");
        _atacar.AddBinding("<Gamepad>/buttonWest");

        _habilidad1 = new InputAction("Habilidad1", InputActionType.Button);
        _habilidad1.AddBinding("<Keyboard>/1");
        _habilidad1.AddBinding("<Gamepad>/buttonNorth");

        _habilidad2 = new InputAction("Habilidad2", InputActionType.Button);
        _habilidad2.AddBinding("<Keyboard>/2");
        _habilidad2.AddBinding("<Gamepad>/buttonEast");
    }

    private void OnEnable()
    {
        _atacar.Enable();
        _habilidad1.Enable();
        _habilidad2.Enable();
    }

    private void OnDisable()
    {
        _atacar.Disable();
        _habilidad1.Disable();
        _habilidad2.Disable();
    }

    private void OnDestroy()
    {
        _atacar?.Dispose();
        _habilidad1?.Dispose();
        _habilidad2?.Dispose();
    }

    private void Update()
    {
        Vector3 puntoApuntado = camaraRig != null ? camaraRig.PuntoApuntado : transform.position + transform.forward * 5f;

        Apuntar(0, _habilidad1, puntoApuntado);
        Apuntar(1, _habilidad2, puntoApuntado);

        // El basico no se apunta: va hacia el cursor y listo. Encadenar tres
        // golpes seguidos tiene que ser fluido, y una fase de apuntado en el
        // medio lo cortaria.
        if (_apuntando < 0 && _atacar.WasPressedThisFrame() && basico != null)
            basico.Atacar(puntoApuntado);

        if (_apuntando < 0 && indicador != null && _ejecutor.EnEjecucion == null)
            indicador.Ocultar();
    }

    private void Apuntar(int slot, InputAction tecla, Vector3 puntoApuntado)
    {
        if (tecla.WasPressedThisFrame() && _ejecutor.Disponible(slot))
            _apuntando = slot;

        if (_apuntando != slot) return;

        if (tecla.IsPressed())
        {
            if (indicador != null)
                indicador.Mostrar(transform, _ejecutor.Habilidad(slot), puntoApuntado);
            return;
        }

        // Soltó: lanza.
        _apuntando = -1;
        if (indicador != null) indicador.Ocultar();
        _ejecutor.Usar(slot, puntoApuntado);
    }
}
