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
    Casino_Piso3_Superado,

    // Casino, piso 1: el guardia del ascensor. El pase sale pagando o
    // ganandole; ganarle ademas da su ruleta de Pokemon, una sola vez.
    Casino_Piso1_PaseAscensor,
    Casino_GuardiaVencido,

    // Casino: los que pelean una sola vez, uno por NPC.
    Casino_MalabaristaVencido,
    Casino_JovenRicaVencida,
    Casino_ModeloVencida,
    Casino_MacarraVencido,
    Casino_GuayVencido,

    // Ruleta de premios por ganarle a un NPC: un flag por uso, hasta el maximo
    // por partida (ver CasinoRuletaDeNpcs). El 3 y el 4 estan al final.
    Casino_RuletaNpc_1,
    Casino_RuletaNpc_2,

    // Casino: la segunda tanda de NPCs que pelean una vez (dos por piso). Van al
    // final porque las escenas guardan el flag de cada NPC como NUMERO: meterlos
    // arriba correria a todos los que ya estan puestos.
    Casino_MotoristaVencido,
    Casino_PokemaniacoVencido,
    Casino_MediumVencido,
    Casino_LadronVencido,
    Casino_VeteranoVencido,
    Casino_KaratekaVencido,

    // Ruleta de los NPCs: los usos 3 y 4, de cuando el tope paso de dos a
    // cuatro. Al final por lo mismo que la tanda de arriba.
    Casino_RuletaNpc_3,
    Casino_RuletaNpc_4,

    // Casino, piso 2: el guardia VIP del ascensor (se le muestra la Tarjeta VIP
    // o se le gana), el magnate que da la tarjeta al ganarle y los dos que
    // pelean una vez que se sumaron. Al final por lo mismo que las tandas de
    // arriba: las escenas guardan el flag como numero.
    Casino_Piso2_PaseAscensor,
    Casino_GuardiaVipVencido,
    Casino_MagnateVencido,
    Casino_TahurVencido,
    Casino_BailarinaVencida,

    // Casino, piso 3: Tacconi (se le gana su Cosmog shiny al blackjack) y los
    // dos que pelean una vez que se sumaron.
    Casino_TacconiVencido,
    Casino_GangsterVencido,
    Casino_CrupieraVencida,

    // Casino, ultimo piso: el pasillo de guardias y el jefe (Martin Reymar).
    // Al final por lo mismo que las tandas de arriba: las escenas guardan el
    // flag como numero, asi que meter uno en el medio le cambia el valor a
    // todos los de abajo.
    Casino_JefeFinalVencido,
    Casino_Completado,

    // Quarcho Cueva, el lobby entre niveles. Al final por lo mismo que las
    // tandas de arriba: las escenas guardan el flag como numero.
    //
    // Tixi ya conto su parte del lore: desde ahi la salida de la cueva abre.
    Lobby_TixiPresentado,
    // Tacconi en la cueva: tres chances para ganarle el Mew, una por mano
    // perdida (el empate no gasta), y el Mew ya ganado.
    Lobby_TacconiChance1,
    Lobby_TacconiChance2,
    Lobby_TacconiChance3,
    Lobby_TacconiMewGanado,

    // Habitacion tutorial, donde despierta el personaje. Al final por lo mismo
    // que las tandas de arriba: las escenas guardan el flag como numero.
    //
    // Ya se conto lo basico al despertar (moverse, interactuar, el menu).
    Tutorial_Despertar,
    // Una por parte: al activarse se apaga su brillo y cuenta para la puerta.
    Tutorial_Mochila,
    Tutorial_Equipo,
    Tutorial_Pc,
    Tutorial_Guardar,
    Tutorial_Opciones,
    Tutorial_Pelea,
    // Hizo todas las partes obligatorias, o eligio omitirlo en la puerta.
    Tutorial_Completado,
    Tutorial_Omitido,

    // El jefe final. Al final por lo mismo que las tandas de arriba: las
    // escenas guardan el flag como numero.
    //
    // Se enciende al ganarle la pelea a Julieta, antes del epilogo y los
    // creditos. Ver PeleaJulieta.
    Julieta_Vencida,

    // La historia repartida por los niveles (ver Historia_En_Los_Niveles.md).
    // Al final por lo mismo que las tandas de arriba: las escenas guardan el
    // flag como numero. Casi todos los enciende un DialogueDefinition al
    // terminar (su grantsFlag o el de una variante o respuesta), y los leen
    // las variantes de otros dialogos: asi un NPC se acuerda de lo que paso
    // antes, en este nivel o en otro.
    //
    // El camino de la habitacion a la Quarcho Cueva, y lo que se hizo con Cruz.
    Historia_CaminoContado,
    Historia_CruzEnganiado,
    Historia_CruzHuida,
    Historia_CruzPateado,

    // Tixi: lo que cuenta al volver de cada nivel (una sola vez).
    Historia_TixiVueltaDelDataCenter,
    Historia_TixiVueltaDelCasino,

    // Data Center de Nestor.
    Historia_DC_PantallasParpadearon,
    Historia_DC_GuardianEnEspera,
    Historia_DC_TecnicoHablo,
    Historia_DC_GuardiaRecibioOrden,
    Historia_DC_MemoLeido,
    Historia_NestorPresentado,
    // Mitad de la charla de despues de la pelea: aca se apagan las luces.
    Historia_Servidor01Desconectado,
    Historia_NestorDesperto,

    // Casino.
    Historia_Casino_MujerHablo,
    Historia_Casino_CroupierHablo,
    Historia_Casino_JugadorHablo,
    Historia_TacconiPresentado,
    Historia_TacconiRevelo,
    Historia_ReymarPresentado,
    Historia_Servidor02Desconectado,

    // Quarcho Cueva: Tacconi ya conto lo suyo antes de ofrecer el Mew.
    Historia_TacconiEnLaCueva,

    // Data Center: la terminal del salon y la foto de la mesa de Nestor ya se
    // vieron (apagan su brillo).
    Historia_DC_TerminalLeida,
    Historia_DC_FotoVista
}