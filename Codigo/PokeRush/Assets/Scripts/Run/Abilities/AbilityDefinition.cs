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
    // Abanico delante del Poken, hacia donde mira. Es la forma que tiene de
    // verdad un golpe fisico -una patada, un cabezazo, un zarpazo-: nace del
    // cuerpo, barre un arco y llega hasta donde llega el miembro.
    //
    // Es el modo que corresponde a los basicos. Antes usaban AreaEnSuelo, que
    // pone el circulo DONDE ESTA EL MOUSE: el golpe no salia del Poken sino del
    // cursor, y por eso pegaba en lugares donde la animacion no tocaba nada.
    ConoFrontal,
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
    [Tooltip("Radio del area, en unidades. En SkillshotLineal es el ancho del haz. " +
             "En ConoFrontal no se usa: el ancho lo da el angulo.")]
    public float radio = 1.2f;

    [Tooltip("Solo en ConoFrontal: cuantos grados abre el abanico, contando de " +
             "punta a punta. 60 es un golpe dirigido; 120 barre casi todo el frente.")]
    [Range(15f, 360f)]
    public float anguloCono = 90f;

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

    [Header("Efectos visuales")]
    [Tooltip("Estela del golpe. Vacio = la que le toca al tipo en el catalogo.")]
    public GameObject fxArco;
    [Tooltip("Efecto propio de esta habilidad, si le queres uno distinto al que " +
             "le toca a su tipo. Vacio = sale del catalogo de FX del ejecutor.")]
    public GameObject fxImpacto;
    [Tooltip("Efecto que sale del Poken al arrancar la animacion. Vacio = el del " +
             "catalogo, que casi siempre es ninguno.")]
    public GameObject fxLanzamiento;
    [Tooltip("Cuanto tiene que MEDIR el efecto en unidades de mundo. Se deriva " +
             "del area de la habilidad, asi que normalmente no hace falta " +
             "tocarlo: 0 = automatico.")]
    public float tamañoFx;

    /// Tamaño del efecto en unidades. Por defecto acompaña al area del golpe: un
    /// ataque de radio 2,4 tiene que verse del ancho de su area, no de un tamaño
    /// fijo que quede chico en unas habilidades y enorme en otras.
    ///
    /// El cono es el caso aparte: ahi el area no la da el radio sino el alcance,
    /// asi que el efecto tiene que medir lo que mide el abanico.
    public float TamañoDeFx
    {
        get
        {
            if (tamañoFx > 0.01f) return tamañoFx;

            float medida = apuntado == AimMode.ConoFrontal ? rango : radio * 2f;
            return Mathf.Clamp(medida, 1f, 8f);
        }
    }

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
