using UnityEngine;

// Ajustes que solo valen en la build final (no en el editor ni en una
// Development Build).
//
// El juego tiene cientos de Debug.Log de diagnostico ("[Combate] ...",
// "[Recorrido] ..."). En la build final van al Player.log del jugador: se
// filtran para que ahi queden solo los avisos y los errores, que son los que
// sirven si alguien manda un reporte. En el editor no cambia nada.
public static class ConfiguracionDeProduccion
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Aplicar()
    {
        if (Application.isEditor || Debug.isDebugBuild) return;
        Debug.unityLogger.filterLogType = LogType.Warning;
    }
}
