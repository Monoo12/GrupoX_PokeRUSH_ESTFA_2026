using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lanza el playtest del balance (PlaytestDeBalance) sobre la escena de cada
// pelea y entra en Play. El banco se crea solo al arrancar el Play, no se
// agrega a la escena. Resultados en el Editor.log con la etiqueta [PlayBalance].
public static class PlaytestDeBalanceRunner
{
    [MenuItem("PokeRush/Balance/Playtest en Play/Tutorial (Rebeca)")]
    public static void Tutorial() => Lanzar("tutorial", "Assets/Scenes/habitacion.unity", 2);

    [MenuItem("PokeRush/Balance/Playtest en Play/Nestor (con prediccion y liberacion)")]
    public static void Nestor() => Lanzar("nestor", "Assets/Scenes/NestorGOD2.unity", 1);

    [MenuItem("PokeRush/Balance/Playtest en Play/Nestor (solo con subsistemas cortados)")]
    public static void NestorConCortes() => Lanzar("nestor-cortado", "Assets/Scenes/NestorGOD2.unity", 1);

    [MenuItem("PokeRush/Balance/Playtest en Play/Casino piso 1 (NPCs, ruletas y guardia)")]
    public static void Casino() => Lanzar("casino", "Assets/Scenes/Casino Nivel 1.unity", 1);

    [MenuItem("PokeRush/Balance/Playtest en Play/Reymar (ruleta y blackjack)")]
    public static void Reymar() => Lanzar("reymar", "Assets/Scenes/Casino piso FINAL.unity", 2);

    [MenuItem("PokeRush/Balance/Playtest en Play/Julieta (guion entero)")]
    public static void Julieta() => Lanzar("julieta", "Assets/Scenes/BossFinal.unity", 2);

    public static void Lanzar(string pelea, string escena, int veces)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlayBalance] Ya estás en Play Mode. Salí y volvé a intentar.");
            return;
        }
        if (SceneManager.GetActiveScene().path != escena)
        {
            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);
        }

        SessionState.SetString(PlaytestDeBalance.PedidoKey, pelea);
        SessionState.SetInt(PlaytestDeBalance.VecesKey, veces);
        EditorApplication.EnterPlaymode();
    }
}
