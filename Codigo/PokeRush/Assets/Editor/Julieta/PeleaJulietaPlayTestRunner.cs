using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lanza el banco de pruebas de la pelea (PeleaJulietaPlayTest) y entra en Play:
// sobre la escena abierta si tiene la pelea, y si no sobre la escena de prueba.
// El banco se crea solo al arrancar el Play, no se agrega a la escena.
// Resultados en el Editor.log con la etiqueta [PlayJulieta] y capturas en
// Capturas/Julieta/.
public static class PeleaJulietaPlayTestRunner
{
    [MenuItem("PokeRush/Jefe final (Julieta)/Probar la pelea en Play")]
    public static void Probar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayJulieta] Ya estás en Play Mode. Salí y volvé a intentar.");
            return;
        }

        if (PeleaJulietaBuilder.Buscar<PeleaJulieta>(SceneManager.GetActiveScene()) == null)
        {
            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(PeleaJulietaBuilder.EscenaDePrueba, OpenSceneMode.Single);
        }

        SessionState.SetBool(PeleaJulietaPlayTest.PedidoKey, true);
        EditorApplication.EnterPlaymode();
    }
}
