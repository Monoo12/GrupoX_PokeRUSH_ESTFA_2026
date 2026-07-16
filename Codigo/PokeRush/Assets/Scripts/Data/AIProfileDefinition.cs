using UnityEngine;

// Que familia de IA usa un enemigo. La factory (BattleAIFactory) mapea
// cada valor a su clase concreta.
public enum BattleAIType
{
    Wild,       // Poken salvaje: casi aleatorio, leve sesgo a lo efectivo
    Trainer,    // entrenador comun: scoring de movimientos, sin cambios
    GymLeader,  // lider/elite: scoring + cambios estrategicos + setup + remate
    FinalBoss   // bosses finales: version avanzada (por ahora hereda de GymLeader)
}

// Perfil de dificultad de la IA: pesos del scoring y comportamientos habilitados.
// Un asset por nivel de inteligencia (Wild, Trainer, GymLeader, Elite, FinalBoss).
// Ajustar estos numeros re-balancea la IA sin tocar codigo.
[CreateAssetMenu(fileName = "NewAIProfile", menuName = "PokeRush/AIProfileDefinition")]
public class AIProfileDefinition : ScriptableObject
{
    [Header("Tipo de IA")]
    public BattleAIType aiType = BattleAIType.Trainer;

    [Header("Aleatoriedad")]
    [Tooltip("Probabilidad (0-1) de elegir un movimiento al azar en vez del mejor. Mas alto = mas torpe.")]
    [Range(0f, 1f)] public float randomness = 0.2f;
    [Tooltip("Si esta activo, nunca elige movimientos sin efecto (efectividad 0). Los salvajes lo tienen apagado.")]
    public bool avoidsIneffective = true;

    [Header("Pesos del scoring")]
    [Tooltip("Peso del daño esperado (relativo al HP del rival).")]
    public float damageWeight = 1f;
    [Tooltip("Bonus por poder debilitar al rival este turno.")]
    public float koWeight = 0.6f;
    [Tooltip("Peso de aplicar estados alterados (paralisis, quemadura...).")]
    public float statusWeight = 0.35f;
    [Tooltip("Peso de los movimientos de subir stats propias (setup).")]
    public float setupWeight = 0f;
    [Tooltip("Peso de los movimientos de curacion.")]
    public float healWeight = 0f;

    [Header("Cambio de Poken")]
    [Tooltip("Si puede cambiar de Poken voluntariamente durante el combate.")]
    public bool canSwitch = false;
    [Tooltip("Cuanta ventaja de matchup necesita el suplente para justificar el cambio. Mas bajo = cambia mas seguido.")]
    public float switchGainThreshold = 0.5f;

    [Header("Setup")]
    [Tooltip("HP propio minimo (0-1) para considerar movimientos de setup.")]
    [Range(0f, 1f)] public float minHpPctForSetup = 0.7f;
    [Tooltip("No sigue subiendo una stat propia mas alla de este stage.")]
    [Range(1, 6)] public int maxSetupStages = 2;
}
