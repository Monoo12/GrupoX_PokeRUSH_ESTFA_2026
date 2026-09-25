// Como usa los turnos un movimiento. Se serializa por numero en los assets de
// movimientos: agregar valores nuevos AL FINAL.
public enum MoveTurnRule
{
    // Pega el turno en que se elige.
    Normal,

    // Carga un turno y pega el siguiente (Rayo Solar, Cabezazo, Ataque Aereo).
    Charge,

    // Igual que Charge, pero mientras carga casi nada lo alcanza
    // (Vuelo, Excavar, Buceo, Bote, Golpe Fantasma).
    ChargeInvulnerable,

    // Pega y el turno siguiente tiene que descansar (Hiperrayo, Gigaimpacto).
    Recharge
}
