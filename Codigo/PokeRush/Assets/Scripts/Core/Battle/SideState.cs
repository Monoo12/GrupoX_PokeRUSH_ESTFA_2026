// Efectos persistentes de UN lado del campo: pantallas (turnos restantes)
// y trampas de entrada (capas). Vive en BattleModel, uno por lado.
public class SideState
{
    // Pantallas: turnos restantes (0 = inactiva)
    public int ReflectTurns;
    public int LightScreenTurns;
    public int AuroraVeilTurns;

    // Trampas que sufre ESTE lado al enviar un Poken
    public int SpikesLayers;       // 0-3
    public int ToxicSpikesLayers;  // 0-2
    public bool StealthRock;
    public bool StickyWeb;

    public bool HasReflect => ReflectTurns > 0;
    public bool HasLightScreen => LightScreenTurns > 0;
    public bool HasAuroraVeil => AuroraVeilTurns > 0;
}
