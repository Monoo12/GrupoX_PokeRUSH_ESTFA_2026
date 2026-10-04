using UnityEngine;
using UnityEngine.SceneManagement;

// El cierre del juego, despues de ganarle a Julieta (historia.pdf: "Epílogo" y
// "QuarchoCueva"):
//
//   arena (GuionDeJulieta.finalDelJuego)
//     -> fundido a la Quarcho Cueva, que arranca en negro:
//        EPILOGO   las lineas de GuionDeJulieta.epilogo, sobre negro;
//        LA CUEVA  aparece la cueva y Tixi charla con el PJ (enLaCueva);
//        CIERRE    la camara se aleja y en una pantalla aparece la
//                  ultimaConexion, linea por linea; la pantalla se apaga.
//     -> fundido a CreditosFinales -> pantalla de inicio.
//
// La cueva no se toca: al cargarla con el final pendiente se le agrega
// EscenaFinalEnLaCueva, que hace todo. Mientras dura, Tixi no habla sola y la
// musica de la arena (el tema de despues de vencerla) no se corta al cambiar
// de escena.
//
// Nada de esto guarda la partida: el final no es un estado del juego, es un
// recorrido (ver PeleaJulieta).
public static class FinalDelJuego
{
    public const string RutaDelGuion = "Definitions/Julieta/GuionDeJulieta";

    // Desde el fundido de la arena hasta que arrancan los creditos.
    public static bool EnCurso { get; private set; }

    private static string _escenaDeCreditos = "CreditosFinales";

    public static string EscenaDeLaCueva => RecorridoDeLaHistoria.EscenaDelLobby;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar()
    {
        EnCurso = false;
        SceneManager.sceneLoaded -= AlCargar;
    }

    // Lo llama PeleaJulieta al terminar la charla del final.
    public static void Empezar(string escenaDeCreditos, float fundido)
    {
        if (EnCurso) return;
        _escenaDeCreditos = escenaDeCreditos;

        if (!Application.CanStreamedLevelBeLoaded(EscenaDeLaCueva))
        {
            Debug.LogWarning($"[Final] '{EscenaDeLaCueva}' no esta en Build Settings: directo a los creditos.");
            FundidoAEscena.Ir(escenaDeCreditos, fundido);
            return;
        }

        EnCurso = true;
        MusicaDelJuego.Sostener = true;
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        Debug.Log("[Final] A la Quarcho Cueva: epilogo y charla con Tixi.");
        FundidoAEscena.Ir(EscenaDeLaCueva, fundido);
    }

    // Para probar el cierre sin pelear: con la cueva ya abierta en Play.
    public static void EmpezarEnEstaEscena(string escenaDeCreditos)
    {
        _escenaDeCreditos = escenaDeCreditos;
        EnCurso = true;
        MusicaDelJuego.Sostener = true;
        new GameObject("[FinalEnLaCueva]").AddComponent<EscenaFinalEnLaCueva>();
    }

    // Se engancha en sceneLoaded, antes del primer frame de la cueva: asi el
    // negro del epilogo ya esta puesto cuando el fundido se aclara.
    private static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single || escena.name != EscenaDeLaCueva) return;
        SceneManager.sceneLoaded -= AlCargar;
        var go = new GameObject("[FinalEnLaCueva]");
        SceneManager.MoveGameObjectToScene(go, escena);
        go.AddComponent<EscenaFinalEnLaCueva>();
    }

    // Lo llama EscenaFinalEnLaCueva cuando la pantalla se apaga.
    public static void IrALosCreditos(float fundido)
    {
        EnCurso = false;
        MusicaDelJuego.Sostener = false;

        if (!string.IsNullOrEmpty(_escenaDeCreditos) && Application.CanStreamedLevelBeLoaded(_escenaDeCreditos))
        {
            FundidoAEscena.Ir(_escenaDeCreditos, fundido);
            return;
        }
        Debug.LogError($"[Final] La escena de creditos '{_escenaDeCreditos}' no esta en Build Settings.");
    }
}
