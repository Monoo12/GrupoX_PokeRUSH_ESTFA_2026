using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lanza el banco de pruebas de la historia del Data Center
// (DataCenterHistoriaPlayTest): abre NestorGOD2 y entra en Play. El banco se
// crea solo al arrancar el Play, no se agrega a la escena. Resultados en el
// Editor.log con la etiqueta [PlayHistoria] y capturas en Capturas/Historia/.
public static class DataCenterHistoriaPlayTestRunner
{
    private const string Escena = "Assets/Scenes/NestorGOD2.unity";

    [MenuItem("PokeRush/Historia/Probar el Data Center en Play")]
    public static void Probar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayHistoria] Ya estás en Play Mode. Salí y volvé a intentar.");
            return;
        }

        if (SceneManager.GetActiveScene().path != Escena)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError($"[PlayHistoria] '{SceneManager.GetSceneAt(i).name}' tiene cambios sin guardar.");
                    return;
                }
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        }

        SessionState.SetBool(DataCenterHistoriaPlayTest.PedidoKey, true);
        EditorApplication.EnterPlaymode();
    }
}
