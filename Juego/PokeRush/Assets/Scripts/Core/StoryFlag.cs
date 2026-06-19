public enum StoryFlag
{

    //Los valores de este enum los va a ir completando el equipo a medida que diseñen los eventos del juego. Lo importante ahora es tener la estructura. Cada flag representa algo que pasó en el mundo y no vuelve atrás.

    None,

    // Progresión principal
    GameStarted,
    ReceivedStarterPoken,

    // Bosses — agregar uno por cada boss del juego
    BossDefeated_01,
    BossDefeated_02,
    BossDefeated_03,

    // Eventos del mundo — agregar según diseño
    MetFirstNPC,
    UnlockedShop,
    UnlockedGacha
}