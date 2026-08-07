using System;
using UnityEngine;

// Un Poken vivo dentro de una ruta: el jugador o un salvaje.
//
// Es el puente entre la escena y el motor de combate. Guarda el PokenModel y el
// BattlePokenState que ya usa el sistema por turnos, asi el daño de la ruta pasa
// por el MISMO DamageCalculator: efectividad de tipos, STAB, criticos y stat
// stages salen igual que en un combate clasico, sin duplicar formulas.
public class RunCombatant : MonoBehaviour
{
    [Header("Identidad")]
    [SerializeField] private PokenDefinition especie;
    [SerializeField] private int nivel = 25;
    [Tooltip("Marca al Poken del jugador. Los golpes solo alcanzan al bando contrario.")]
    [SerializeField] private bool esDelJugador;

    [Header("Control")]
    [Tooltip("Altura a la que apuntan los golpes, medida desde los pies. Sirve " +
             "para que los impactos revienten en el cuerpo y no en el piso.")]
    [SerializeField] private float alturaDelPecho = 0.7f;

    private static readonly IBattleRng Azar = new UnityRng();

    private ControlKind _control = ControlKind.Ninguno;
    private float _controlHasta;
    private float _fuerzaControl = 1f;

    public PokenModel Poken { get; private set; }
    public BattlePokenState Estado { get; private set; }
    public bool EsDelJugador => esDelJugador;
    public bool EstaDerrotado => Poken == null || Poken.IsFainted;
    public Vector3 PuntoDeImpacto => transform.position + Vector3.up * alturaDelPecho;

    /// Multiplicador de velocidad que impone el control activo: 0 si esta
    /// aturdido o inmovilizado, menos de 1 si esta ralentizado.
    public float MultiplicadorDeVelocidad
    {
        get
        {
            if (!ControlActivo) return 1f;
            if (_control == ControlKind.Ralentizar) return _fuerzaControl;
            return _control == ControlKind.Aturdir || _control == ControlKind.Inmovilizar ? 0f : 1f;
        }
    }

    /// Aturdido: ademas de no moverse, tampoco puede atacar.
    public bool PuedeActuar => !(ControlActivo && _control == ControlKind.Aturdir);

    private bool ControlActivo => _control != ControlKind.Ninguno && Time.time < _controlHasta;

    public event Action<int, int> VidaCambiada;      // (actual, maximo)
    public event Action<DamageResult> Golpeado;      // para el feedback visual
    public event Action Derrotado;

    private void Awake()
    {
        if (especie == null)
        {
            Debug.LogWarning($"[Ruta] {name} no tiene especie asignada.");
            return;
        }

        Poken = new PokenModel(especie, nivel);
        Estado = new BattlePokenState(Poken, esDelJugador);
    }

    /// Configura el combatiente por codigo. Lo usa el builder de la escena.
    public void Configurar(PokenDefinition nuevaEspecie, int nuevoNivel, bool delJugador)
    {
        especie = nuevaEspecie;
        nivel = nuevoNivel;
        esDelJugador = delJugador;
        Poken = new PokenModel(especie, nivel);
        Estado = new BattlePokenState(Poken, delJugador);
        VidaCambiada?.Invoke(Poken.CurrentHP, Poken.MaxHP);
    }

    /// Vuelve a la vida con los PS llenos. En la escena de prueba lo usan los
    /// muñecos para poder seguir pegandoles sin rearmar la escena.
    public void Revivir()
    {
        if (Poken == null) return;

        Poken.Heal(Poken.MaxHP);
        Estado = new BattlePokenState(Poken, esDelJugador);
        _control = ControlKind.Ninguno;
        VidaCambiada?.Invoke(Poken.CurrentHP, Poken.MaxHP);
    }

    public void RecibirGolpe(RunCombatant atacante, AbilityDefinition habilidad, Vector3 desde)
    {
        if (EstaDerrotado || Estado == null || atacante == null || atacante.Estado == null) return;

        MoveDefinition movimiento = habilidad.MovimientoParaDaño();
        DamageResult resultado = DamageCalculator.Calculate(atacante.Estado, Estado, movimiento, Azar);

        if (resultado.Damage > 0)
        {
            Poken.TakeDamage(resultado.Damage);
            VidaCambiada?.Invoke(Poken.CurrentHP, Poken.MaxHP);
        }

        Golpeado?.Invoke(resultado);
        AplicarControl(habilidad.control, (transform.position - desde).normalized);

        if (Poken.IsFainted) Derrotado?.Invoke();
    }

    public void AplicarControl(ControlEffect efecto, Vector3 direccion)
    {
        if (!efecto.EsAlguno) return;

        // El empuje es instantaneo, no un estado que dure: se resuelve moviendo
        // al Poken y listo.
        if (efecto.tipo == ControlKind.Empujar)
        {
            var controlador = GetComponent<CharacterController>();
            Vector3 desplazamiento = Vector3.ProjectOnPlane(direccion, Vector3.up).normalized * efecto.fuerza;

            if (controlador != null) controlador.Move(desplazamiento);
            else transform.position += desplazamiento;
            return;
        }

        // Con dos controles encima gana el que termine mas tarde, para que un
        // aturdido corto no corte uno largo que ya estaba corriendo.
        float hasta = Time.time + efecto.duracion;
        if (hasta <= _controlHasta) return;

        _control = efecto.tipo;
        _controlHasta = hasta;
        _fuerzaControl = efecto.fuerza <= 0f ? 0.5f : efecto.fuerza;
    }
}
