using UnityEngine;

// La cadena de ataques basicos: atk1 -> atk2 -> atk3, y vuelta a empezar.
//
// No es una AbilityDefinition mas, y por eso vive aparte: no tiene enfriamiento
// sino un CONTADOR y una ventana de encadenado. Si volves a atacar antes de que
// esa ventana cierre, avanzas al siguiente golpe; si dejas pasar el tiempo, la
// cadena se reinicia desde el primero.
//
// El tercer golpe es el POTENCIADO. Es el rasgo del basico de UNITE: premia
// quedarse a rematar la cadena en vez de golpear y salir corriendo.
[RequireComponent(typeof(AbilityRunner))]
public class BasicAttackChain : MonoBehaviour
{
    [Header("Golpes")]
    [Tooltip("En orden. El ULTIMO es el potenciado. Con los clips de UNITE son " +
             "ani_atk1, ani_atk2 y ani_atk3.")]
    [SerializeField] private AbilityDefinition[] golpes = new AbilityDefinition[3];

    [Header("Encadenado")]
    [Tooltip("Segundos que quedan para encadenar el siguiente golpe despues de " +
             "terminar uno. Si se vence, la cadena vuelve al primero.")]
    [SerializeField] private float ventanaDeEncadenado = 1.1f;

    private AbilityRunner _ejecutor;
    private int _siguiente;
    private float _venceEn;

    /// Indice del proximo golpe de la cadena, para que el HUD lo muestre.
    public int Siguiente => _siguiente;
    public int Largo => golpes != null ? golpes.Length : 0;

    /// El proximo golpe es el potenciado. El HUD lo resalta: sin ese aviso, el
    /// remate de la cadena pasa desapercibido.
    public bool SiguienteEsPotenciado => Largo > 0 && _siguiente == Largo - 1;

    private void Awake()
    {
        _ejecutor = GetComponent<AbilityRunner>();
    }

    private void OnEnable() => _ejecutor.Impacto += AlResolverGolpe;
    private void OnDisable() => _ejecutor.Impacto -= AlResolverGolpe;

    private void Update()
    {
        // Vencida la ventana, la cadena se reinicia.
        if (_siguiente != 0 && Time.time > _venceEn) _siguiente = 0;
    }

    public void Atacar(Vector3 puntoApuntado)
    {
        if (Largo == 0 || _ejecutor.Ocupado) return;

        AbilityDefinition golpe = golpes[Mathf.Clamp(_siguiente, 0, Largo - 1)];
        if (golpe == null) return;

        _ejecutor.EjecutarSuelta(golpe, puntoApuntado);
    }

    // La cadena avanza al RESOLVER el golpe, no al lanzarlo, y solo si conecto.
    // Encadenar al aire no deberia acercarte al remate: si avanzara igual,
    // convendria machacar el boton lejos del rival para llegar potenciado.
    private void AlResolverGolpe(AbilityDefinition habilidad, int alcanzados)
    {
        if (System.Array.IndexOf(golpes, habilidad) < 0) return;

        if (alcanzados <= 0)
        {
            _siguiente = 0;
            return;
        }

        _siguiente = (_siguiente + 1) % Largo;
        _venceEn = Time.time + ventanaDeEncadenado;
    }
}
