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

    // Escudo del nucleo (torre 1): absorbe daño hasta agotarse. Va del lado y
    // no del Poken porque es un escudo del EDIFICIO: sobrevive los cambios de
    // Poken del boss. Lo arma BossBattleConfig segun los nodos de firewall que
    // hayan quedado activos.
    public int ShieldHp;

    // Con cuanto arranco. Hace falta para dibujar la barra: sin el maximo solo
    // se puede mostrar un numero, que es lo que habia y casi no se leia.
    public int ShieldMaxHp;

    // Distinto de HasShield: eso dice si QUEDA escudo, esto si esta pelea
    // TIENE escudo. La barra se muestra aunque llegue a cero (rota), porque
    // que se rompa es informacion.
    public bool UsaEscudo => ShieldMaxHp > 0;

    public bool HasReflect => ReflectTurns > 0;
    public bool HasLightScreen => LightScreenTurns > 0;
    public bool HasAuroraVeil => AuroraVeilTurns > 0;
    public bool HasShield => ShieldHp > 0;
}
