using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "PokeRush/MoveDefinition")]
public class MoveDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string moveName;
    [Tooltip("Id del movimiento en PokeAPI. Clave estable del importador: permite actualizar el asset sin duplicarlo.")]
    public int pokeApiId;
    [Tooltip("Nombre oficial en ingles (referencia para el importador y mapeos).")]
    public string englishName;
    public PokenType type;
    public AttackType attackType;

    [Header("Stats")]
    public int power;
    public int accuracy;
    public int maxPP;
    [Tooltip("Prioridad del movimiento. >0 actua antes sin importar la velocidad (ej. Ataque Rapido = 1). Normal = 0.")]
    public int priority;
    [Tooltip("El movimiento nunca falla (accuracy null en PokeAPI, ej. Golpe Certero).")]
    public bool alwaysHits;
    [Tooltip("Ratio de critico: 0 = normal (1/16), 1 = alto (1/8, ej. Cuchillada), 2 = muy alto (1/2).")]
    public int critRateStage;

    [Header("Efecto de estado")]
    public StatusEffect effect;
    [Range(0, 100)]
    public int effectChance;
    public string effectDescription;

    [Header("Cambios de stats")]
    [Tooltip("Stages que sube/baja este movimiento (ej. Danza Espada = +2 Ataque propio; Gruñido = -1 Ataque rival).")]
    public StatChangeEntry[] statChanges;
    [Tooltip("Probabilidad (0-100) de aplicar los cambios de stats. 0 = siempre (movimientos de estado puros).")]
    [Range(0, 100)]
    public int statChangeChance;

    [Header("Efectos de daño")]
    [Tooltip("Probabilidad (0-100) de hacer retroceder al rival (pierde su accion este turno).")]
    [Range(0, 100)]
    public int flinchChance;
    [Tooltip("Golpes multiples: min/max (ej. 2-5). Dejar 0,0 para golpe unico.")]
    public int minHits;
    public int maxHits;
    [Tooltip("Retroceso: % del daño infligido que sufre el usuario (ej. Doble Filo = 33).")]
    public int recoilPercent;
    [Tooltip("Drenaje: % del daño infligido que se cura el usuario (ej. Absorber = 50).")]
    public int drainPercent;
    [Tooltip("Curacion: % del MaxHP propio que recupera el usuario (ej. Recuperacion = 50).")]
    public int healPercent;
    [Tooltip("Si el movimiento hace contacto fisico (activa Static, Rough Skin, etc.).")]
    public bool makesContact;

    [Header("Implementacion")]
    [Tooltip("Full: efecto completo. DamageOnly: hace daño pero su efecto complejo se ignora. NotImplemented: no usable.")]
    public MoveImplementation implementation = MoveImplementation.Full;

    [Header("Efectos de campo")]
    [Tooltip("Clima que instala este movimiento (Danza Lluvia, Dia Soleado...).")]
    public WeatherKind weatherEffect = WeatherKind.None;
    [Tooltip("Efecto de campo que genera (pantallas, trampas de entrada, terrenos).")]
    public FieldEffectKind fieldEffect = FieldEffectKind.None;

    [Header("Turnos (carga y recarga)")]
    [Tooltip("Normal, carga un turno (Rayo Solar), carga fuera de alcance (Vuelo) o descansa el turno siguiente (Hiperrayo).")]
    public MoveTurnRule turnRule = MoveTurnRule.Normal;
    [Tooltip("Mensaje del turno de carga. {0} = el Poken. Vacio = mensaje generico.")]
    public string chargeMessage;
    [Tooltip("Stats que sube el usuario en el turno de carga (Cabezazo: Defensa +1). Se aplican siempre al usuario.")]
    public StatChangeEntry[] chargeStatChanges;
    [Tooltip("Con este clima no carga: pega en el acto (Rayo Solar con sol).")]
    public WeatherKind skipChargeInWeather = WeatherKind.None;

    [Header("Animacion")]
    [Tooltip("Animacion propia de este movimiento (opcional). Vacio = animacion generica segun categoria y tipo. El importador de PokeAPI nunca toca este campo.")]
    public MoveAnimationDefinition animationOverride;
}
