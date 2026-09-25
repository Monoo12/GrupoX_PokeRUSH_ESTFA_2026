using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lanza el banco de pruebas del tutorial (TutorialPlayTest): abre el esqueleto
// y entra en Play. El banco se crea solo al arrancar el Play, no se agrega a la
// escena. Resultados en el Editor.log con la etiqueta [PlayTutorial] y capturas
// en Capturas/Tutorial/.
public static class TutorialPlayTestRunner
{
    [MenuItem("PokeRush/Tutorial/Probar el esqueleto en Play")]
    public static void Probar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayTutorial] Ya estás en Play Mode. Salí y volvé a intentar.");
            return;
        }

        if (SceneManager.GetActiveScene().path != TutorialBuilder.EscenaEsqueleto)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TutorialBuilder.EscenaEsqueleto, OpenSceneMode.Single);
        }

        SessionState.SetBool(TutorialPlayTest.PedidoKey, true);
        EditorApplication.EnterPlaymode();
    }
}
