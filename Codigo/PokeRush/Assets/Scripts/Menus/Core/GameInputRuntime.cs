using UnityEngine;

// Tickea el servicio una vez por frame. Se crea solo; no hay que ponerlo en
// ninguna escena.
public class GameInputRuntime : MonoBehaviour
{
    private static GameInputRuntime _instance;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("[GameInput]");
        _instance = go.AddComponent<GameInputRuntime>();
        go.hideFlags = HideFlags.DontSave;
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        // unscaledDeltaTime: los menus tienen que responder aunque alguien
        // ponga Time.timeScale en 0.
        GameInputService.Instance.Tick(Time.unscaledDeltaTime);
    }
}
