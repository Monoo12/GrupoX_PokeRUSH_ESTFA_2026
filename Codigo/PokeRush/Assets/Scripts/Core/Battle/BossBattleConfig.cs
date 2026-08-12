// Como los subsistemas cortados en la torre modifican el combate del boss.
//
// Es la traduccion de "lo que hiciste caminando" a "lo que pasa en la pelea".
// Se arma una vez al empezar el combate y de ahi en mas solo se lee.
//
// Clase pura (sin Unity) a proposito: asi se puede armar a mano desde
// BattleTester y probar las 16 combinaciones de subsistemas en segundos, en vez
// de caminar la torre entera cada vez que se toca un numero de balance.
public class BossBattleConfig
{
    // Camaras cortadas: la Prediccion se anuncia un turno ANTES, asi se puede
    // planificar. Activas: te enteras recien cuando te toca actuar. Misma
    // informacion, timing distinto — ahi esta el valor de cortarlas.
    public bool PrediccionConAviso;

    // Escudo del nucleo, en PS. Se achica proporcional a los nodos de firewall
    // cortados: es el unico subsistema PARCIAL, para ensenar que esto no es
    // binario.
    public int EscudoInicial;

    // Rastreo activo: no podes cambiar de Poken en toda la pelea.
    public bool PermiteCambio;

    // Turnos que hay que aguantar en la fase de liberacion. La energia auxiliar
    // no hace la pelea mas facil, hace la LIBERACION mas facil.
    public int TurnosLiberacion;

    // Con los cuatro subsistemas cortados, Melmetal no aparece (sale un Unown
    // en su lugar). Todo o nada: dos de tres nodos achican el escudo pero no
    // alcanzan, para obligar a terminar lo que se empieza.
    public bool SinMelmetal;

    // --- numeros de balance, todos en un solo lugar ---

    public const int EscudoPorNodoActivo = 40;   // 3 nodos activos = 120 PS de escudo
    public const int TurnosLiberacionBase = 6;
    public const int MultiplicadorSinEnergia = 2; // 12 turnos si no cortaste la energia

    // Config por defecto: la del combate sin ningun subsistema cortado. Es la
    // que se usa si alguien pelea contra el boss sin pasar por la torre (por
    // ejemplo desde CombatePrueba).
    public static BossBattleConfig PorDefecto()
    {
        return new BossBattleConfig
        {
            PrediccionConAviso = false,
            EscudoInicial = EscudoPorNodoActivo * TowerProgressModel.FirewallNodosTotales,
            PermiteCambio = false,
            TurnosLiberacion = TurnosLiberacionBase * MultiplicadorSinEnergia,
            SinMelmetal = false
        };
    }

    public static BossBattleConfig DesdeTorre(TowerProgressModel torre)
    {
        if (torre == null) return PorDefecto();

        int nodosActivos = TowerProgressModel.FirewallNodosTotales - torre.FirewallNodos;

        return new BossBattleConfig
        {
            PrediccionConAviso = torre.CamarasCortadas,
            EscudoInicial = EscudoPorNodoActivo * nodosActivos,
            PermiteCambio = torre.RastreoCortado,
            TurnosLiberacion = torre.EnergiaAuxiliarCortada
                ? TurnosLiberacionBase
                : TurnosLiberacionBase * MultiplicadorSinEnergia,
            SinMelmetal = torre.TodosCortados
        };
    }

    // Para los logs de balance: una linea que dice como quedo la pelea.
    public string Resumen()
    {
        return $"escudo={EscudoInicial} aviso={(PrediccionConAviso ? "anticipado" : "tardio")} " +
               $"cambios={(PermiteCambio ? "si" : "NO")} liberacion={TurnosLiberacion}t " +
               $"melmetal={(SinMelmetal ? "no" : "si")}";
    }
}
