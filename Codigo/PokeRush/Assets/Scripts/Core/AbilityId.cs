// Habilidades con efecto implementado en combate (subconjunto basico).
// Las especies cuya habilidad no este aca quedan en None: el nombre se muestra
// en la UI (PokenDefinition.ability) pero no tiene efecto mecanico todavia.
// Para agregar una habilidad nueva: sumarla aca y darle efecto en AbilityEffects.
public enum AbilityId
{
    None,
    Intimidate,     // Al entrar: baja 1 stage el Ataque del rival
    Levitate,       // Inmune a movimientos de tipo Ground
    VoltAbsorb,     // Inmune a Electric: cura 25% del MaxHP
    WaterAbsorb,    // Inmune a Water: cura 25% del MaxHP
    FlashFire,      // Inmune a Fire: potencia sus propios movimientos Fire x1.5
    Static,         // 30% de paralizar al atacante si el golpe hace contacto
    PoisonPoint,    // 30% de envenenar al atacante si el golpe hace contacto
    FlameBody,      // 30% de quemar al atacante si el golpe hace contacto
    Sturdy,         // Con HP lleno, aguanta cualquier golpe con 1 HP
    Guts,           // Con estado alterado: Ataque x1.5 y no sufre la reduccion de quemadura
    SpeedBoost,     // Al final de cada turno: +1 stage de Velocidad
    Blaze,          // Con HP < 1/3: movimientos Fire x1.5
    Torrent,        // Con HP < 1/3: movimientos Water x1.5
    Overgrow,       // Con HP < 1/3: movimientos Grass x1.5
    Swarm,          // Con HP < 1/3: movimientos Bug x1.5
    RoughSkin       // El atacante pierde 1/8 de su MaxHP si el golpe hace contacto
}
