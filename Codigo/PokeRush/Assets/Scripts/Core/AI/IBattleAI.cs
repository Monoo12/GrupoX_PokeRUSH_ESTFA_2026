// Interfaz comun de todas las IAs de combate. La factory (BattleAIFactory)
// elige la implementacion segun el AIProfileDefinition del enemigo.
public interface IBattleAI
{
    // Elige la accion del turno (movimiento o cambio de Poken).
    BattleAction ChooseAction(AiContext context, IBattleRng rng);

    // Elige el reemplazo cuando el Poken propio se debilita.
    // Devuelve el indice en el equipo, o -1 si no queda ninguno con vida.
    int ChooseReplacement(AiContext context);
}
