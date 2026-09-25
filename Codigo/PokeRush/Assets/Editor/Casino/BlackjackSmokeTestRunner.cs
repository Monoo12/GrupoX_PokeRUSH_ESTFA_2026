using UnityEditor;
using UnityEngine;

// Lanza el banco de pruebas del blackjack: mete el componente en la escena
// abierta y entra en Play Mode. El objeto NO se guarda en la escena; al salir
// del Play Mode se borra solo. Mismo esquema que RouletteSmokeTestRunner.
public static class BlackjackSmokeTestRunner
{
    private const string ObjectName = "[SmokeBlackjack]";
    private const string PendingKey = "PokeRush.SmokeBlackjack.Pendiente";

    [MenuItem("PokeRush/Casino/Probar el blackjack")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SmokeBlackjack] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Limpiar();

        var go = new GameObject(ObjectName);
        var test = go.AddComponent<BlackjackSmokeTest>();

        // Las capturas van a Capturas/Blackjack, al lado de las que ya sacan los
        // otros bancos. Sirven para revisar la mesa sin tener que jugarla.
        test.carpetaDeCapturas = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName, "Capturas", "Blackjack");

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
        foreach (var t in Object.FindObjectsByType<BlackjackSmokeTest>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }
}
