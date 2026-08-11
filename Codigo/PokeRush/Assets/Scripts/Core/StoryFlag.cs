public enum StoryFlag
{

    //Los valores de este enum los va a ir completando el equipo a medida que dise�en los eventos del juego. Lo importante ahora es tener la estructura. Cada flag representa algo que pas� en el mundo y no vuelve atr�s.

    None,

    // Progresi�n principal
    GameStarted,
    ReceivedStarterPoken,

    // Bosses � agregar uno por cada boss del juego
    BossDefeated_01,
    BossDefeated_02,
    BossDefeated_03,

    // Eventos del mundo � agregar seg�n dise�o
    MetFirstNPC,
    UnlockedShop,
    UnlockedGacha,

    // Torre 1 - Ciberseguridad: un flag por subsistema cortado.
    // Van aca y no en un modelo aparte porque SaveService ya persiste los flags
    // como TEXTO (flag.ToString() -> active_flags.FlagName) y los relee con
    // Enum.TryParse: agregar miembros es seguro y no hay que tocar el guardado.
    // Leerlos ordenados es tarea de TowerProgressModel.
    Torre1_CamarasCortadas,
    Torre1_FirewallNodoA,
    Torre1_FirewallNodoB,
    Torre1_FirewallNodoC,
    Torre1_RastreoCortado,
    Torre1_EnergiaAuxiliarCortada,
    Torre1_NestorLiberado
}