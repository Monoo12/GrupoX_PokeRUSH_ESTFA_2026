// IA de los bosses finales. Por ahora hereda todo el comportamiento de
// GymLeaderAI (con un perfil mas agresivo: menos randomness, mas peso de KO
// y cambios mas frecuentes).
//
// GANCHO PARA LOS DOS BOSSES FINALES: cuando se diseñen, esta clase va a
// sumar evaluacion estrategica adicional sin tocar el resto del sistema:
//  - Prediccion parcial de la respuesta del jugador (si mi Poken esta en
//    desventaja de tipo, asumir que el jugador ataca con su STAB y evaluar
//    si conviene cambiar ANTES de recibir el golpe).
//  - Lookahead de 2 turnos con daño esperado en ambas direcciones.
//  - Secuencias de setup planificadas (ej: Danza Espada x2 -> remate) cuando
//    el matchup defensivo lo permite.
// Sobrescribir ChooseAction, evaluar esas capas y delegar en base.ChooseAction
// cuando ninguna aplique.
public class FinalBossAI : GymLeaderAI
{
    public override BattleAction ChooseAction(AiContext context, IBattleRng rng)
    {
        // Por ahora: comportamiento de lider con perfil FinalBoss
        return base.ChooseAction(context, rng);
    }
}
