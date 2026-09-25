using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// El tutorial de la habitacion donde despierta el personaje: va despues de
// crear la partida y antes de la Quarcho Cueva.
//
// Va en el objeto 'Tutorial', que es lo que se copia de la escena esqueleto y
// se pega en la habitacion decorada. Adentro tiene:
//   Despertar   donde aparece el jugador (al lado de la cama)
//   Partes      lo que brilla: una TutorialParte por cosa que se ensenia
//   Puerta      la salida (PuertaDelTutorial): no abre hasta terminar u omitir
//
// Este componente se ocupa de lo que es de todo el tutorial:
//   1. Al llegar pone al jugador en 'Despertar', funde desde negro y cuenta lo
//      basico (el asset 'despertar': moverse, interactuar, el menu).
//   2. Despues de cada parte dice lo que falta; al terminar la ultima
//      obligatoria activa Tutorial_Completado y dice el 'cierre'.
//   3. Omitir (lo pide la puerta): da lo que las partes pendientes iban a dar y
//      activa Tutorial_Omitido.
//
// Todo lo que usa se busca solo al arrancar: se pega en otra escena y anda.
public class TutorialDeLaHabitacion : MonoBehaviour
{
    [Header("Al despertar")]
    [Tooltip("Donde aparece el jugador, al lado de la cama. Se ajusta al centro de su celda.")]
    [SerializeField] private Transform puntoDeDespertar;
    [SerializeField] private Vector2 mirandoHacia = Vector2.down;
    [Tooltip("Lo que se cuenta al despertar: moverse, interactuar y el menu. Lo escribe el equipo.")]
    [SerializeField] private DialogueDefinition despertar;
    [SerializeField] private bool fundidoDesdeNegro = true;
    [Min(0f)] [SerializeField] private float duracionDelFundido = 1.2f;
    [Min(0f)] [SerializeField] private float esperaAntesDeHablar = 0.4f;

    [Header("Progreso")]
    [Tooltip("Despues de cada parte. {0} = lo que falta. Vacio = no dice nada.")]
    [SerializeField, TextArea(2, 3)] private string mensajeProgreso = "Te falta {0}.";
    [Tooltip("Al terminar la ultima parte obligatoria. Lo escribe el equipo.")]
    [SerializeField] private DialogueDefinition cierre;

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    public Transform PuntoDeDespertar => puntoDeDespertar;
    public DialogueDefinition Despertar => despertar;
    public DialogueDefinition Cierre => cierre;

    private GameObject _capaNegra;
    private Image _negro;

    public static TutorialDeLaHabitacion Buscar()
        => FindAnyObjectByType<TutorialDeLaHabitacion>(FindObjectsInactive.Include);

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);

        // El negro desde el primer frame: si esperara a Start, la habitacion se
        // veria un instante antes de fundir.
        if (fundidoDesdeNegro && TocaElDespertar()) CrearNegro();
    }

    private IEnumerator Start()
    {
        PonerAlJugadorEnLaCama();

        // La partida la trae el GameManager desde la creacion de personaje, o la
        // arma SceneBootstrapper en SU Start al entrar directo por Play. El orden
        // entre dos Start no esta garantizado: se espera.
        while (!HayPartida()) yield return null;
        StoryModel story = GameManager.Instance.Story;
        RevisarPartes();

        if (_negro != null) yield return Fundir();
        if (!TocaElDespertar()) yield break;

        if (esperaAntesDeHablar > 0f) yield return new WaitForSeconds(esperaAntesDeHablar);
        while (MenuManager.AnyOpen || (dialogueController != null && dialogueController.IsOpen)) yield return null;

        ContarLoBasico(story);
    }

    private void OnDestroy() => BorrarNegro();

    // ---------- al despertar ----------

    private static bool HayPartida()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.Story != null && gm.CurrentPlayer != null;
    }

    // Sin partida todavia (entrando por Play, la crea el bootstrap) se asume
    // una nueva: es lo que va a pasar.
    private static bool TocaElDespertar()
    {
        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (story == null) return true;
        return !story.HasFlag(StoryFlag.Tutorial_Despertar) &&
               !story.HasFlag(StoryFlag.Tutorial_Completado) &&
               !story.HasFlag(StoryFlag.Tutorial_Omitido);
    }

    private void ContarLoBasico(StoryModel story)
    {
        System.Action listo = () => story.ActivateFlag(StoryFlag.Tutorial_Despertar);

        if (despertar == null || despertar.lines == null || despertar.lines.Length == 0) { listo(); return; }
        if (dialogueController != null) dialogueController.StartDialogue(despertar, story, listo);
        else CasinoDialogo.Decir(null, despertar.speakerName, despertar.speakerPortrait, despertar.lines, listo);
    }

    private void PonerAlJugadorEnLaCama()
    {
        if (puntoDeDespertar == null) return;
        PlayerController jugador = FindAnyObjectByType<PlayerController>();
        if (jugador == null) return;

        Vector3 celda = CentroDeCelda(puntoDeDespertar.position);
        jugador.TeleportTo(celda, mirandoHacia);

        // La camara pegada al jugador: CameraFollow sigue desde donde la pongan,
        // asi no se la ve viajar desde donde quedo guardada en la escena.
        Camera cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(celda.x, celda.y, cam.transform.position.z);
    }

    // El centro de la celda de la grilla del mapa (medios enteros con el Grid de
    // siempre). El jugador tiene que arrancar ahi: si arranca corrido, todos sus
    // pasos quedan corridos y los choques contra las paredes fallan.
    public static Vector3 CentroDeCelda(Vector3 posicion)
    {
        Grid grid = FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            Vector3 c = grid.GetCellCenterWorld(grid.WorldToCell(posicion));
            return new Vector3(c.x, c.y, 0f);
        }
        return new Vector3(Mathf.Floor(posicion.x) + 0.5f, Mathf.Floor(posicion.y) + 0.5f, 0f);
    }

    // El fundido es un Canvas propio que se arma en runtime y se borra al
    // terminar: la escena no necesita nada para tenerlo.
    private void CrearNegro()
    {
        _capaNegra = new GameObject("[FundidoDelDespertar]");
        var canvas = _capaNegra.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Arriba del mapa y debajo de la caja de dialogo (100).
        canvas.sortingOrder = 90;

        _negro = new GameObject("Negro").AddComponent<Image>();
        _negro.transform.SetParent(_capaNegra.transform, false);
        _negro.color = Color.black;
        _negro.raycastTarget = false;
        RectTransform rt = _negro.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator Fundir()
    {
        float t = 0f;
        float duracion = Mathf.Max(0.01f, duracionDelFundido);
        while (_negro != null && t < duracion)
        {
            t += Time.unscaledDeltaTime;
            _negro.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / duracion));
            yield return null;
        }
        BorrarNegro();
    }

    private void BorrarNegro()
    {
        if (_capaNegra != null) Destroy(_capaNegra);
        _capaNegra = null;
        _negro = null;
    }

    // ---------- progreso ----------

    // Lo llama cada parte al terminar, con su menu ya cerrado. Si era la ultima
    // obligatoria cierra el tutorial; si no, dice lo que falta.
    public void AvisarProgreso(StoryModel story, System.Action alCerrar = null)
    {
        if (story == null) { alCerrar?.Invoke(); return; }

        List<TutorialParte> partes = TutorialParte.TodasLasDeLaEscena();
        if (ReglasDelTutorial.Completo(partes, story))
        {
            // Una sola vez: volver a usar una parte ya hecha no repite el cierre.
            if (story.HasFlag(StoryFlag.Tutorial_Completado)) { alCerrar?.Invoke(); return; }

            story.ActivateFlag(StoryFlag.Tutorial_Completado);
            Debug.Log("[Tutorial] Completo: la puerta ya abre.");
            if (cierre != null && cierre.lines != null && cierre.lines.Length > 0 && dialogueController != null)
                dialogueController.StartDialogue(cierre, story, alCerrar);
            else
                Decir("¡Terminaste el tutorial! Ya podés salir por la puerta.", alCerrar);
            return;
        }

        if (string.IsNullOrEmpty(mensajeProgreso)) { alCerrar?.Invoke(); return; }
        Decir(string.Format(mensajeProgreso, Faltan(story)), alCerrar);
    }

    private void Decir(string linea, System.Action alCerrar)
        => CasinoDialogo.Decir(dialogueController, "", null, new[] { linea }, alCerrar);

    // ---------- lo que pregunta la puerta ----------

    public static bool PuedeSalir(StoryModel story)
        => ReglasDelTutorial.PuedeSalir(TutorialParte.TodasLasDeLaEscena(), story);

    // "la mochila, el PC y la pelea"
    public static string Faltan(StoryModel story)
        => ReglasDelTutorial.Enumerar(ReglasDelTutorial.Faltantes(TutorialParte.TodasLasDeLaEscena(), story));

    // Omitir el tutorial: da lo que las partes pendientes iban a dar (las
    // Pociones de la mochila), asi saltearlo no deja en desventaja, y lo marca
    // omitido. No carga ninguna escena: eso lo hace la puerta. Devuelve lo que
    // dio ("Poción x2"), o null si no dio nada.
    public static string Omitir(PlayerModel player, StoryModel story)
    {
        if (story == null) return null;

        var dados = new List<string>();
        foreach (TutorialParte parte in TutorialParte.TodasLasDeLaEscena())
        {
            if (ReglasDelTutorial.Hecha(parte, story)) continue;
            string dio = parte.Entregar(player);
            if (!string.IsNullOrEmpty(dio)) dados.Add(dio);
        }

        story.ActivateFlag(StoryFlag.Tutorial_Omitido);
        Debug.Log($"[Tutorial] Omitido. Se entrego: {(dados.Count > 0 ? string.Join(", ", dados) : "nada")}.");
        return dados.Count == 0 ? null : ReglasDelTutorial.Enumerar(dados);
    }

    // Avisos para quien arma la escena: una parte sin flag no cuenta, dos con el
    // mismo flag se completan juntas y una pelea sin rival no arranca.
    private static void RevisarPartes()
    {
        var vistos = new Dictionary<StoryFlag, TutorialParte>();
        foreach (TutorialParte p in TutorialParte.TodasLasDeLaEscena())
        {
            if (p.QueHace == TutorialParte.Tipo.Pelea && p.Rival == null)
                Debug.LogWarning($"[Tutorial] '{p.name}' es la pelea y no tiene rival: no va a arrancar.");

            if (p.Flag == StoryFlag.None)
            {
                Debug.LogWarning($"[Tutorial] La parte '{p.name}' no tiene flag: no cuenta para la puerta.");
                continue;
            }

            if (vistos.TryGetValue(p.Flag, out TutorialParte otra))
                Debug.LogWarning($"[Tutorial] '{p.name}' y '{otra.name}' usan el mismo flag ({p.Flag}): se completan juntas.");
            else
                vistos[p.Flag] = p;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (puntoDeDespertar == null) return;
        Vector3 p = puntoDeDespertar.position;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(p, 0.35f);
        UnityEditor.Handles.Label(p + new Vector3(-0.45f, 0.8f, 0f), "Despierta acá");
    }
#endif
}
