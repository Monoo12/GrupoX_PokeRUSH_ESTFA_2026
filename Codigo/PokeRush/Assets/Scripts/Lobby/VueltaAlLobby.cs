using UnityEngine;
using UnityEngine.SceneManagement;

// Al terminar un nivel de la historia, lleva al jugador de vuelta a la Quarcho
// Cueva.
//
// Se crea solo al arrancar el juego y vive entre escenas, asi ni NestorGOD2 ni
// los pisos del casino necesitan un componente nuevo: mira el flag de
// "completado" del nivel en el que esta el jugador (RecorridoDeLaHistoria) y,
// si se enciende mientras esta adentro, espera a que termine lo que este
// pasando en pantalla (el combate, la mano de blackjack, el cartel de victoria)
// y lo lleva a la cueva.
//
// Consulta el flag por frame en vez de escuchar StoryModel.FlagActivated:
// NewGame y ContinueGame crean un StoryModel nuevo y quien se suscribio antes no
// se entera nunca mas (la misma trampa que documenta BrilloInteractuable).
public class VueltaAlLobby : MonoBehaviour
{
    // Cuanto tiene que quedar la pantalla quieta (sin combate, cartel ni menu)
    // antes de volver. Tapa el hueco entre que termina el combate y aparece el
    // cartel de victoria, que llega unos frames despues.
    private const float SegundosQuieto = 1.2f;

    private const string Mensaje = "¡Nivel superado! Volvés a la Quarcho Cueva.";

    // Los bancos de prueba lo apagan para poder simular un nivel completo sin
    // que la escena cambie debajo de ellos.
    public static bool Activa = true;

    private static VueltaAlLobby _instancia;

    private string _escena;
    private RecorridoDeLaHistoria.Nivel _nivel;
    private StoryModel _story;
    private bool _yaEstabaCompletado;
    private bool _volviendo;
    private float _quietoDesde = -1f;

    private DialogueController _dialogo;
    private BattleController _combate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (_instancia != null) return;
        var go = new GameObject("[VueltaAlLobby]");
        _instancia = go.AddComponent<VueltaAlLobby>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        string escena = SceneManager.GetActiveScene().name;
        if (escena != _escena) EntrarA(escena);
        if (!Activa || _nivel == null || _volviendo) return;

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (story == null) return;

        // La primera vez que hay partida en esta escena (o si la partida
        // cambio: el arranque suelto crea una al entrar por Play) se anota si
        // el nivel YA estaba completo. Volver a un nivel terminado no tiene
        // que expulsar a nadie.
        if (story != _story)
        {
            _story = story;
            _yaEstabaCompletado = story.HasFlag(_nivel.FlagCompletado);
        }

        if (_yaEstabaCompletado || !story.HasFlag(_nivel.FlagCompletado))
        {
            _quietoDesde = -1f;
            return;
        }

        if (HayAlgoEnPantalla())
        {
            _quietoDesde = -1f;
            return;
        }

        if (_quietoDesde < 0f) { _quietoDesde = Time.unscaledTime; return; }
        if (Time.unscaledTime - _quietoDesde < SegundosQuieto) return;

        Volver();
    }

    private void EntrarA(string escena)
    {
        _escena = escena;
        _nivel = RecorridoDeLaHistoria.NivelDeLaEscena(escena);
        if (_nivel != null && _nivel.FlagCompletado == StoryFlag.None) _nivel = null;
        _story = null;
        _volviendo = false;
        _quietoDesde = -1f;
        _dialogo = null;
        _combate = null;

        if (_nivel == null) return;
        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
    }

    private bool HayAlgoEnPantalla()
    {
        if (MenuManager.AnyOpen) return true;
        if (_dialogo != null && _dialogo.IsOpen) return true;
        // El prefab de combate vive apagado y lo prende el puente al pelear.
        return _combate != null && _combate.gameObject.activeInHierarchy;
    }

    private void Volver()
    {
        _volviendo = true;
        Debug.Log($"[Recorrido] Nivel completo ({_nivel.Nombre}): vuelta a {RecorridoDeLaHistoria.EscenaDelLobby}.");

        // El nivel ya guarda al terminar; se vuelve a guardar por si algo del
        // cierre (la mano del jefe, un premio) quedo sin guardar.
        CasinoDialogo.Guardar();

        if (_dialogo == null)
        {
            SceneManager.LoadScene(RecorridoDeLaHistoria.EscenaDelLobby);
            return;
        }

        _dialogo.StartMessage("", null, new[] { Mensaje },
            () => SceneManager.LoadScene(RecorridoDeLaHistoria.EscenaDelLobby));
    }
}
