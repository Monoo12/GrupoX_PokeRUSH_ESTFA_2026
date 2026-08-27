using UnityEditor;
using UnityEngine;

// Lanza el banco de pruebas de los menus: mete el componente en la escena
// abierta y entra en Play Mode. El objeto NO se guarda en la escena; al salir
// del Play Mode se borra solo.
public static class MenuSmokeTestRunner
{
    private const string ObjectName = "[SmokeMenus]";
    private const string PendingKey = "PokeRush.SmokeMenus.Pendiente";

    [MenuItem("PokeRush/UI/Probar sistema de menus")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SmokeMenus] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Limpiar();

        var go = new GameObject(ObjectName);
        go.AddComponent<MenuSystemSmokeTest>();

        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("PokeRush/UI/Sacar capturas de los menus")]
    public static void Capturas()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SmokeMenus] Ya estas en Play Mode. Sali y volve a intentar.");
            return;
        }

        Limpiar();

        var go = new GameObject(ObjectName);
        go.AddComponent<MenuScreenshotTaker>();

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
        // Al volver al editor se saca el objeto de prueba, asi la escena queda
        // como estaba y nadie lo commitea sin querer.
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool(PendingKey, false)) return;

        SessionState.SetBool(PendingKey, false);
        Limpiar();
    }

    private static void Limpiar()
    {
        foreach (var t in Object.FindObjectsByType<MenuSystemSmokeTest>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        foreach (var c in Object.FindObjectsByType<MenuScreenshotTaker>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
