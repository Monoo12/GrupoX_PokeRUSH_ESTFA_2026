// Estados del campo de batalla: clima, terreno y efectos por lado.

// Clima activo (afecta a AMBOS lados)
public enum WeatherKind
{
    None,
    Rain,       // Lluvia: Agua x1.5, Fuego x0.5
    Sun,        // Sol: Fuego x1.5, Agua x0.5
    Sandstorm,  // Tormenta de arena: daño residual 1/16 (salvo Roca/Tierra/Acero), DefEsp x1.5 a Roca
    Hail        // Granizo: daño residual 1/16 (salvo Hielo)
}

// Terreno activo (afecta a los Pokens EN EL SUELO de ambos lados)
public enum FieldTerrain
{
    None,
    Electric,   // Electrico x1.3, nadie se duerme
    Psychic,    // Psiquico x1.3
    Grassy,     // Planta x1.3, cura 1/16 por turno
    Misty       // Dragon x0.5 contra afectados, sin estados nuevos
}

// Efecto de campo que genera un movimiento (pantallas, trampas, terrenos)
public enum FieldEffectKind
{
    None,
    Reflect,         // fisico x0.5 al lado protegido, 5 turnos
    LightScreen,     // especial x0.5, 5 turnos
    AuroraVeil,      // ambos x0.5, 5 turnos (solo con granizo)
    StealthRock,     // daño al entrar segun efectividad Roca
    Spikes,          // hasta 3 capas: 1/8, 1/6, 1/4 al entrar (solo en el suelo)
    ToxicSpikes,     // hasta 2 capas: envenena al entrar (solo en el suelo)
    StickyWeb,       // -1 Velocidad al entrar (solo en el suelo)
    ElectricTerrain,
    PsychicTerrain,
    GrassyTerrain,
    MistyTerrain
}
