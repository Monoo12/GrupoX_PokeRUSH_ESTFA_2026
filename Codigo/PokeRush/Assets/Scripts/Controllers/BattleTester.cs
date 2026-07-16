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
        return player.Team.Count > 0 ? player : null;
    }
}
