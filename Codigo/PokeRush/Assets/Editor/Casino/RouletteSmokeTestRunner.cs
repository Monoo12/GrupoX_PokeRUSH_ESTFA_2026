using UnityEditor;
using UnityEngine;

// Lanza el banco de pruebas de la ruleta: mete el componente en la escena
// abierta y entra en Play Mode. El objeto NO se guarda en la escena; al salir
// del Play Mode se borra solo. Mismo esquema que CasinoSmokeTestRunner.
public static class RouletteSmokeTestRunner
{
    private const string ObjectName = "[SmokeRuleta]";
    private const string PendingKey = "PokeRush.SmokeRuleta.Pendiente";

    [MenuItem("PokeRush/Casino/Probar la ruleta")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SmokeRuleta] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Limpiar();

        var go = new GameObject(ObjectName);
        var test = go.AddComponent<RouletteSmokeTest>();

        // Las capturas van a Capturas/Ruleta, al lado de las que ya saca el
        // banco de menus. Sirven para revisar la mesa sin tener que jugarla.
        test.carpetaDeCapturas = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName, "Capturas", "Ruleta");

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
        foreach (var t in Object.FindObjectsByType<RouletteSmokeTest>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }
}
