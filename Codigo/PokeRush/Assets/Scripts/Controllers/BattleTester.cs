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

    [Header("Disparo")]
    [SerializeField] private bool startOnPlay = true;

    private void Start()
    {
        if (startOnPlay) Launch();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame) Launch();
    }

    private void Launch()
    {
        if (battleController == null)
        {
            Debug.LogWarning("BattleTester: falta asignar el BattleController.");
            return;
        }

        PlayerModel player = new PlayerModel("Tester");
        for (int i = 0; i < playerSpecies.Length; i++)
        {
            if (playerSpecies[i] == null) continue;
            int level = (playerLevels != null && i < playerLevels.Length) ? playerLevels[i] : 5;
            player.Team.Add(new PokenModel(playerSpecies[i], level));
        }

        if (player.Team.Count == 0 || enemyDefinition == null)
        {
            Debug.LogWarning("BattleTester: asigná al menos un Poken del jugador y un rival.");
            return;
        }

        battleController.StartBattle(player, enemyDefinition);
    }
}
