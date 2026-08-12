using UnityEngine;
using UnityEngine.InputSystem;

// Arranca un combate de prueba sin depender del flujo de exploracion todavia.
// TEMPORAL: borralo cuando exista el disparador real (encuentro con NPC/Boss).
// Asigna todo en el Inspector y apreta Play (o la tecla B para reiniciar).
public class BattleTester : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleController battleController;

    [Header("Equipo del jugador (prueba)")]
    [SerializeField] private PokenDefinition[] playerSpecies;
    [SerializeField] private int[] playerLevels;

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

    // Arma el equipo de prueba del jugador desde el Inspector (null si esta vacio)
    private PlayerModel BuildPlayerTeam()
    {
        PlayerModel player = new PlayerModel("Tester");
        for (int i = 0; i < playerSpecies.Length; i++)
        {
            if (playerSpecies[i] == null) continue;
            int level = (playerLevels != null && i < playerLevels.Length) ? playerLevels[i] : 5;
            player.Team.Add(new PokenModel(playerSpecies[i], level));
        }

        // Sin objetos no se puede probar CURAR en la fase de liberacion, que es
        // una de las tres herramientas de esa fase.
        if (testHealItem != null && testHealItemQuantity > 0)
            player.Inventory.AddHealItem(testHealItem, testHealItemQuantity);

        return player.Team.Count > 0 ? player : null;
    }
}
