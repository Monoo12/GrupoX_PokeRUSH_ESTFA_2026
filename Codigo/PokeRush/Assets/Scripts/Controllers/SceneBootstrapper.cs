using UnityEngine;

// El eslabon que faltaba entre el menu y el juego.
//
// GameManager sobrevive los cambios de escena con el PlayerModel adentro, pero
// los controladores de la escena tienen su Initialize() y NADIE los llamaba:
// el PlayerModel nunca llegaba a la escena, y por eso PlayerController.OpenMenu
// hacia early-return para siempre. Esto lo conecta.
//
// Va en un GameObject suelto de cada escena jugable, con las referencias
// arrastradas en el Inspector.
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Controladores de la escena")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private MapController mapController;
    [SerializeField] private EventController eventController;

    [Header("Arranque suelto (desarrollo)")]
    [Tooltip("Si no hay partida cargada, crea una de prueba en vez de romper. " +
             "Permite apretar Play directo en esta escena sin pasar por el menu.")]
    [SerializeField] private bool crearPartidaSiFalta = true;
    [SerializeField] private string nombrePartidaDePrueba = "DEMO";

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[Bootstrap] No hay GameManager en la escena ni viniendo del menu. " +
                           "Agregalo, o entra por 'Menu Principal'.");
            return;
        }

        // Entrar directo a esta escena desde el editor es lo normal mientras se
        // desarrolla; sin esto habria que pasar por el menu en cada Play.
        if (gm.CurrentPlayer == null)
        {
            if (!crearPartidaSiFalta)
            {
                Debug.LogError("[Bootstrap] No hay partida cargada y crearPartidaSiFalta esta apagado.");
                return;
            }

            Debug.LogWarning($"[Bootstrap] Sin partida cargada: arranco una de prueba " +
                             $"('{nombrePartidaDePrueba}'). Esto es normal al entrar directo por Play.");
            gm.NewGame(nombrePartidaDePrueba);
        }

        // El orden importa poco, pero el jugador primero deja la escena usable
        // aunque falte cablear los otros dos.
        if (playerController != null)
            playerController.Initialize(gm.CurrentPlayer, gm.Story);
        else
            Debug.LogWarning("[Bootstrap] Falta el PlayerController: no vas a poder moverte.");

        if (eventController != null)
            eventController.Initialize(gm.Story, gm.CurrentPlayer);

        if (mapController != null)
            mapController.Initialize(gm.Story);

        Debug.Log($"[Bootstrap] Listo. Jugador '{gm.CurrentPlayer.PlayerName}' con " +
                  $"{gm.CurrentPlayer.Team.Count} Pokens. Subsistemas cortados: " +
                  $"{gm.Tower?.SubsistemasCortados ?? 0}/{TowerProgressModel.SubsistemasTotales}");
    }
}
