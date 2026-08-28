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
    Torre1_NestorLiberado,

    // Objetos del piso: uno por pokeball, para que no reaparezcan al recargar
    // la partida. El InteractableTrigger los usa como 'otorgaFlag'.
    Torre1_PocionesTomadas,
    Torre1_SuperPocionesTomadas,

    // Casino (nivel 2): un flag por objetivo de piso. Se guardan solos con el
    // resto de los flags, asi el jugador no tiene que volver a cumplir una
    // condicion que ya cumplio, ni siquiera despues de cerrar el juego.
    Casino_Piso1_Superado,
    // El apostador del piso 1: se pelea una sola vez y paga 100.
    Casino_ApostadorVencido,
    Casino_Ruleta_ObjetivoCumplido,
    Casino_Piso2_Superado,
    Casino_Blackjack_Ganado,
    Casino_Piso3_Superado
}