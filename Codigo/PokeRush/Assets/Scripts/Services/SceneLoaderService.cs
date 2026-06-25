using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderService
{
    // Singleton: una sola instancia compartida por todo el juego
    private static SceneLoaderService _instance;
    public static SceneLoaderService Instance => _instance ??= new SceneLoaderService();

    private SceneLoaderService() { }

    // Carga una escena de forma inmediata, reemplazando la actual
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Carga una escena de forma asincronica (en segundo plano, sin bloquear el juego)
    // Util para pantallas de carga con barra de progreso
    public void LoadSceneAsync(string sceneName, MonoBehaviour runner, System.Action onComplete = null)
    {
        runner.StartCoroutine(LoadSceneCoroutine(sceneName, onComplete));
    }

    // Corutina: se ejecuta de a poco entre frames hasta que la escena termina de cargar
    private IEnumerator LoadSceneCoroutine(string sceneName, System.Action onComplete)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // yield return null = "esperar al siguiente frame y volver aca"
        // se repite hasta que operation.isDone sea true
        while (!operation.isDone)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }
}

/*
SceneManager.LoadScene es la forma simple — congela un frame mientras carga, aceptable para escenas chicas. LoadSceneAsync con corutina permite mostrar una pantalla de carga mientras Unity carga en segundo plano. runner es necesario porque las corutinas (IEnumerator) solo pueden ejecutarse desde un MonoBehaviour — como SceneLoaderService es una clase pura sin MonoBehaviour,
necesita "prestarle" la capacidad de correr la corutina a algún componente de la escena (por ejemplo, el mismo MapController que llama este método).
*/