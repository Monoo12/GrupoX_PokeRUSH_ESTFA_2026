using UnityEngine;

// Como se apunta la habilidad. Es lo que decide que dibuja el indicador en el
// suelo y como se resuelve el impacto.
public enum AimMode
{
    // Linea recta desde el Poken hacia el cursor. Golpea a todo lo que cruce.
    SkillshotLineal,
    // Circulo puesto en el suelo, con un alcance maximo desde el Poken.
    AreaEnSuelo,
    // Circulo centrado en el propio Poken.
    AreaPropia,
    // Se desplaza hacia el cursor y golpea lo que atropella.
    Dash,
    // No golpea: se aplica sobre uno mismo (escudos, buffos).
    Propia,
}

// Una habilidad del modelo de UNITE: se apunta, tiene area, y tiene enfriamiento.
//
// La parte de COMBATE (tipo, potencia, categoria) sale de un MoveDefinition, que
// es lo que ya come DamageCalculator: asi el daño de la ruta usa exactamente la
// misma formula que el combate por turnos, con efectividad de tipos y STAB. Si
// no se le asigna ninguno, se arma uno en runtime con los valores sueltos de
// abajo, para poder probar sin tener que elegir movimiento a mano.
//
// La parte de ACCION (apuntado, rango, enfriamiento, ventana de daño) no existe
// en los datos de PokeAPI: son los campos que hay que medir mirando el clip.
[CreateAssetMenu(fileName = "NuevaHabilidad", menuName = "PokeRush/Habilidad")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string nombre = "Habilidad";
    [Tooltip("Icono para el HUD. Opcional.")]
    public Sprite icono;

    [Header("Apuntado")]
    public AimMode apuntado = AimMode.SkillshotLineal;
    [Tooltip("Alcance en unidades. En SkillshotLineal es el largo; en AreaEnSuelo, " +
             "hasta donde se puede alejar el circulo; en Dash, cuanto se desplaza.")]
    public float rango = 5f;
    [Tooltip("Radio del area, en unidades. En SkillshotLineal es el ancho del haz.")]
    public float radio = 1.2f;

    [Header("Tiempos")]
    [Tooltip("Segundos hasta poder volver a usarla.")]
    public float enfriamiento = 6f;
    [Tooltip("Animacion que se reproduce. De aca sale la duracion total.")]
    public AnimationClip clip;
    [Tooltip("Segundo del clip en que el golpe empieza a hacer daño. Es la " +
             "ventana de anticipacion: cuanto mas larga, mas telegrafiada queda.")]
    public float ventanaInicio = 0.35f;
    [Tooltip("Segundo del clip en que el golpe deja de hacer daño.")]
    public float ventanaFin = 0.55f;
    [Tooltip("Segundos durante los cuales no se puede mover ni encadenar otra " +
             "cosa. 0 = puede cancelar apenas termina la ventana de daño.")]
    public float bloqueo = 0.6f;

    [Header("Daño")]
    [Tooltip("Movimiento del que salen tipo, potencia y categoria. Vacio = se " +
             "usan los valores sueltos de abajo.")]
    public MoveDefinition movimiento;
    public PokenType tipo = PokenType.Normal;
    public AttackType categoria = AttackType.Physical;
    public int potencia = 60;

    [Header("Efecto de control")]
    public ControlEffect control;

    private MoveDefinition _movimientoImprovisado;

    /// El MoveDefinition que se le pasa a DamageCalculator. Si no hay uno
    /// asignado se arma en memoria (no se guarda como asset) con los valores
    /// sueltos, para poder iterar sin crear un asset por habilidad.
    public MoveDefinition MovimientoParaDaño()
    {
        if (movimiento != null) return movimiento;

        if (_movimientoImprovisado == null)
        {
            _movimientoImprovisado = CreateInstance<MoveDefinition>();
            _movimientoImprovisado.name = nombre;
            _movimientoImprovisado.moveName = nombre;
            _movimientoImprovisado.alwaysHits = true;
        }

        // Se refrescan cada vez: asi tocar los valores en el inspector se ve al
        // instante, sin recrear el objeto.
        _movimientoImprovisado.type = tipo;
        _movimientoImprovisado.attackType = categoria;
        _movimientoImprovisado.power = potencia;
        return _movimientoImprovisado;
    }

    /// Duracion total de la accion. El clip manda; si no hay, la ventana.
    public float Duracion => clip != null ? clip.length : Mathf.Max(ventanaFin, bloqueo);

    private void OnValidate()
    {
        ventanaInicio = Mathf.Max(0f, ventanaInicio);
        ventanaFin = Mathf.Max(ventanaInicio, ventanaFin);
        radio = Mathf.Max(0.05f, radio);
        rango = Mathf.Max(0.1f, rango);
    }
}
