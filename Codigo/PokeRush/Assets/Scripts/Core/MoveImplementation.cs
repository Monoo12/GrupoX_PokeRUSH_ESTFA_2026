// Que tan implementado esta el efecto de un movimiento importado de PokeAPI.
// El importador lo asigna; el combate y la IA lo consultan.
public enum MoveImplementation
{
    // El efecto completo del movimiento funciona en el combate.
    Full,

    // El movimiento hace su daño normal pero su efecto secundario complejo
    // (two-turn, trampa, counter...) no esta implementado: se ignora.
    DamageOnly,

    // Movimiento de estado cuyo efecto no esta soportado: no se puede usar.
    // El importador lo excluye de los learnsets pero el asset existe para el futuro.
    NotImplemented
}
