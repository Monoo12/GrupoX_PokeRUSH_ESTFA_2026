using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lanza el banco de pruebas de la Quarcho Cueva (LobbyPlayTest): abre la cueva
// y entra en Play. El banco se crea solo al arrancar el Play, no se agrega a la
// escena. Resultados en el Editor.log con la etiqueta [PlayLobby] y capturas en
// Capturas/Lobby/.
public static class LobbyPlayTestRunner
{
    [MenuItem("PokeRush/Quarcho Cueva/Probar la cueva en Play")]
    public static void Probar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayLobby] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Scene activa = SceneManager.GetActiveScene();
        if (activa.path != QuarchoCuevaBuilder.Escena)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(QuarchoCuevaBuilder.Escena, OpenSceneMode.Single);
        }

        SessionState.SetBool(LobbyPlayTest.PedidoKey, true);
        EditorApplication.EnterPlaymode();
    }
}
