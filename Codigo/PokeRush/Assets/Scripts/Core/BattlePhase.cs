public enum BattlePhase
{
    //Estos son los diferentes estados o fases que pueden ocurrir durante una batalla, se pueden agregar m�s fases seg�n sea necesario
    PlayerTurn,
    BossTurn,
    CalculatingResult,

    // Fase de liberacion: el boss no puede caer y el chip se quema solo mientras
    // el jugador aguanta SIN atacar. Se resuelve con LiberationEngine, no con
    // TurnEngine, porque ahi se invierten todas las reglas a la vez.
    // (BossTurn quedo sin usar de antes; no se reutiliza el nombre a proposito,
    //  significa otra cosa y confundiria.)
    Liberation,

    // El Poken activo del jugador se debilito y el combate esta DETENIDO hasta
    // que elija con cual sigue. Es una fase propia y no un sub-estado del turno
    // porque mientras dura no se resuelve nada: ni el rival actua ni avanza la
    // Prediccion.
    PlayerReplacement,

    End
}