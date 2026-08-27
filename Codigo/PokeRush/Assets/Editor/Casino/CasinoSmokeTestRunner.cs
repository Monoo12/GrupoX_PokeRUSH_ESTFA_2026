using UnityEditor;
using UnityEngine;

// Lanza el banco de pruebas del casino: mete el componente en la escena abierta
// y entra en Play Mode. El objeto NO se guarda en la escena; al salir del Play
// Mode se borra solo. Mismo esquema que MenuSmokeTestRunner.
public static class CasinoSmokeTestRunner
{
    private const string ObjectName = "[SmokeCasino]";
    private const string PendingKey = "PokeRush.SmokeCasino.Pendiente";

    [MenuItem("PokeRush/Casino/Probar el casino")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SmokeCasino] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Limpiar();

        var go = new GameObject(ObjectName);
        go.AddComponent<CasinoSmokeTest>();

        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Enganchar()
    {
        EditorApplication.playModeStateChanged -= AlCambiarPlayMode;
        EditorApplication.playModeStateChanged += AlCambiarPlayMode;
    }

    private static void AlCambiarPlayMode(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool(PendingKey, false)) return;

        SessionState.SetBool(PendingKey, false);
        Limpiar();
    }

    private static void Limpiar()
    {
        foreach (var t in Object.FindObjectsByType<CasinoSmokeTest>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }
}
