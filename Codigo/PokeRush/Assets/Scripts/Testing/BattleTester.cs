using UnityEngine;
using UnityEngine.InputSystem;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Vive en Scripts/Testing/ y NO esta en el prefab de combate: el flujo real es
// BattleZoneTrigger / InteractableTrigger -> BattleController, sin pasar por
// aca. Solo lo usa la escena CombatePrueba.
//
// Se conserva a proposito en vez de borrarlo: es la unica forma de probar las
// 16 combinaciones de subsistemas de Nestor sin caminar la torre entera cada
// vez que se toca un numero de balance (tecla N), y de probar un movimiento
// concreto sin pelear con el learnset.
//
// Uso: asignar todo en el Inspector y apretar Play.
//   B = reiniciar contra el entrenador   W = salvaje   N = Nestor
public class BattleTester : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleController battleController;

    [Header("Equipo del jugador (prueba)")]
    [SerializeField] private PokenDefinition[] playerSpecies;
    [SerializeField] private int[] playerLevels;

    // Movimientos a mano, uno por Poken. Vacio = los que le tocan por learnset.
    //
    // Por que hace falta: PokenModel.LearnInitialMoves se queda con los 4
    // movimientos de MAYOR NIVEL que la especie ya puede conocer, asi que
    // cambiar el nivel cambia el set entero. Esta bien para el juego, pero
    // hace imposible probar un movimiento concreto.
    [System.Serializable]
    public class MovimientosDePrueba
    {
        [Tooltip("Hasta 4. Se ignoran los vacios. Si queda vacio, usa el learnset.")]
        public MoveDefinition[] movimientos = new MoveDefinition[4];
    }

    [Tooltip("Uno por Poken, en el mismo orden que 'Player Species'. " +
             "Dejalo vacio para que use los movimientos automaticos.")]
    [SerializeField] private MovimientosDePrueba[] playerMoves;

    [Header("Rival")]
    [Tooltip("Un EnemyDefinition o BossDefinition con su pokenTeam cargado.")]
    [SerializeField] private NPCDefinition enemyDefinition;

    [Header("Encuentro salvaje (prueba)")]
    [Tooltip("Opcional: un WildEncounterDefinition para probar combates salvajes con la tecla W.")]
    [SerializeField] private WildEncounterDefinition wildEncounter;

    [Header("Objetos (para probar CURAR en la fase de liberacion)")]
    [Tooltip("Sin esto el boton CURAR de la liberacion no tiene con que curar.")]
    [SerializeField] private HealItemDefinition testHealItem;
    [SerializeField] private int testHealItemQuantity = 5;

    // --- combate contra Nestor con subsistemas, tecla N ---
    // Esto es lo que evita tener que caminar la torre para probar el balance:
    // se marcan los subsistemas aca y se pelea directo. Son 16 combinaciones.
    [Header("Nestor con subsistemas (tecla N)")]
    [SerializeField] private BossDefinition nestorConMelmetal;
    [Tooltip("Variante sin Melmetal (con Unown). Se usa sola si cortas los cuatro.")]
    [SerializeField] private BossDefinition nestorSinMelmetal;
    [SerializeField] private bool camarasCortadas;
    [Range(0, 3)] [SerializeField] private int firewallNodosCortados;
    [SerializeField] private bool rastreoCortado;
    [SerializeField] private bool energiaAuxiliarCortada;

    [Header("Disparo")]
    [SerializeField] private bool startOnPlay = true;

    private void Start()
    {
        // En la escena de prueba no hay GameManager: cargamos las definiciones
        // y la tabla de XP a mano para que la experiencia funcione igual.
        if (PokenModel.DefaultXpTable == null)
        {
            DefinitionRepository.Instance.LoadAll();
            PokenModel.DefaultXpTable = DefinitionRepository.Instance.ExperienceTable;
        }

        if (startOnPlay) Launch();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.bKey.wasPressedThisFrame) Launch();
        else if (kb.wKey.wasPressedThisFrame) LaunchWild();
        else if (kb.nKey.wasPressedThisFrame) LaunchNestor();
    }

    // Combate contra Nestor con los subsistemas marcados en el Inspector.
    // No hace falta caminar la torre ni tener partida: se arma la config a mano.
    private void LaunchNestor()
    {
        if (battleController == null) return;

        BossDefinition boss = ElegirVarianteDeNestor(out bool todosCortados);
        if (boss == null)
        {
            Debug.LogWarning("BattleTester: asigná nestorConMelmetal (y ojala tambien la variante sin Melmetal).");
            return;
        }

        PlayerModel player = BuildPlayerTeam();
        if (player == null) return;

        int nodosActivos = TowerProgressModel.FirewallNodosTotales - firewallNodosCortados;
        var config = new BossBattleConfig
        {
            PrediccionConAviso = camarasCortadas,
            EscudoInicial = BossBattleConfig.EscudoPorNodoActivo * nodosActivos,
            PermiteCambio = rastreoCortado,
            TurnosLiberacion = energiaAuxiliarCortada
                ? BossBattleConfig.TurnosLiberacionBase
                : BossBattleConfig.TurnosLiberacionBase * BossBattleConfig.MultiplicadorSinEnergia,
            SinMelmetal = todosCortados
        };

        Debug.Log($"[Tester] Nestor con {Cortados()}/4 subsistemas -> {config.Resumen()}");
        battleController.StartBattle(player, boss, config);
    }

    private int Cortados() =>
        (camarasCortadas ? 1 : 0) +
        (firewallNodosCortados >= TowerProgressModel.FirewallNodosTotales ? 1 : 0) +
        (rastreoCortado ? 1 : 0) +
        (energiaAuxiliarCortada ? 1 : 0);

    // Melmetal solo desaparece con los CUATRO cortados (el firewall cuenta solo
    // si estan los tres nodos). Todo o nada, como pide el GDD.
    private BossDefinition ElegirVarianteDeNestor(out bool todosCortados)
    {
        todosCortados = Cortados() >= TowerProgressModel.SubsistemasTotales;
        if (todosCortados && nestorSinMelmetal != null) return nestorSinMelmetal;
        return nestorConMelmetal;
    }

    private void LaunchWild()
    {
        if (battleController == null || wildEncounter == null)
        {
            Debug.LogWarning("BattleTester: falta el BattleController o el WildEncounterDefinition.");
            return;
        }

        PlayerModel player = BuildPlayerTeam();
        if (player == null) return;
        battleController.StartWildBattle(player, wildEncounter);
    }

    private void Launch()
    {
        if (battleController == null)
        {
            Debug.LogWarning("BattleTester: falta asignar el BattleController.");
            return;
        }

        PlayerModel player = BuildPlayerTeam();
        if (player == null || enemyDefinition == null)
        {
            Debug.LogWarning("BattleTester: asigná al menos un Poken del jugador y un rival.");
            return;
        }

        battleController.StartBattle(player, enemyDefinition);
    }

    // Pisa el set automatico por el elegido en el Inspector, si hay uno.
    // Los vacios se ignoran, asi se puede dar un solo movimiento para probar un
    // caso puntual (por ejemplo: que pasa si el jugador NO puede hacer daño).
    private void AplicarMovimientosManuales(PokenModel poken, int indice)
    {
        if (playerMoves == null || indice >= playerMoves.Length) return;
        MovimientosDePrueba set = playerMoves[indice];
        if (set == null || set.movimientos == null) return;

        var elegidos = new System.Collections.Generic.List<MoveDefinition>();
        foreach (MoveDefinition m in set.movimientos)
            if (m != null && !elegidos.Contains(m)) elegidos.Add(m);

        if (elegidos.Count == 0) return; // sin nada elegido: deja el automatico

        poken.Moves.Clear();
        foreach (MoveDefinition m in elegidos)
            poken.Moves.Add(new MoveModel(m));

        Debug.Log($"[Tester] {poken.Definition.pokenName}: movimientos manuales -> " +
                  string.Join(", ", elegidos.ConvertAll(m => m.moveName)));
    }

    // Arma el equipo de prueba del jugador desde el Inspector (null si esta vacio)
    private PlayerModel BuildPlayerTeam()
    {
        PlayerModel player = new PlayerModel("Tester");
        for (int i = 0; i < playerSpecies.Length; i++)
        {
            if (playerSpecies[i] == null) continue;
            int level = (playerLevels != null && i < playerLevels.Length) ? playerLevels[i] : 5;
            var poken = new PokenModel(playerSpecies[i], level);
            AplicarMovimientosManuales(poken, i);
            player.Team.Add(poken);
        }

        // Sin objetos no se puede probar CURAR en la fase de liberacion, que es
        // una de las tres herramientas de esa fase.
        if (testHealItem != null && testHealItemQuantity > 0)
            player.Inventory.AddHealItem(testHealItem, testHealItemQuantity);

        return player.Team.Count > 0 ? player : null;
    }
}
