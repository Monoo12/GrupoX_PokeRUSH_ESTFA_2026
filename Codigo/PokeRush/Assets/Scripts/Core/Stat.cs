// Las 7 estadisticas que pueden subir o bajar por stages durante el combate.
// El orden importa: se usa como indice del array Stages en BattlePokenState.
// HP no esta porque nunca tiene stages.
public enum Stat
{
    Attack = 0,
    Defense = 1,
    SpecialAttack = 2,
    SpecialDefense = 3,
    Speed = 4,
    Accuracy = 5,
    Evasion = 6
}
