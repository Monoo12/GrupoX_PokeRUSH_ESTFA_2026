using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Recorre la historia del Data Center de Nestor (NestorGOD2) como un jugador:
// habla con cada persona y cosa, pisa las zonas, escucha a Nestor antes de la
// pelea (y dice "Todavia no"), simula la victoria, escucha lo de despues (el
// apagon y el Protocolo Nestor), vuelve a hablar con todos (dicen otra cosa),
// sale caminando por la recepcion y llega a la cueva, donde Tixi cuenta la
// vuelta. La pelea en si no se juega: la prueban los tests del combate.
//
// Deja capturas en Capturas/Historia/ y los resultados en el Editor.log con la
// etiqueta [PlayHistoria]. Se lanza desde PokeRush > Historia > Probar el Data
// Center en Play. Juega con la partida DEMO y NO guarda.
public class DataCenterHistoriaPlayTest : MonoBehaviour
{
    public const string PedidoKey = "PokeRush.PlayHistoria.Pendiente";

    public string carpeta = "Capturas/Historia";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearSiLoPidieron()
    {
        if (!UnityEditor.SessionState.GetBool(PedidoKey, false)) return;
        UnityEditor.SessionState.SetBool(PedidoKey, false);
        var go = new GameObject("[PlayHistoria]");
        DontDestroyOnLoad(go);
        go.AddComponent<DataCenterHistoriaPlayTest>();
    }
#endif

    private int _ok;
    private int _fallos;
    private DialogueController _dialogo;
    private DialogueView _vista;

    private void Awake()
    {
        Application.runInBackground = true;
        CasinoDialogo.GuardadoAutomatico = false;
    }

    private void OnDestroy() => CasinoDialogo.GuardadoAutomatico = true;

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        System.IO.Directory.CreateDirectory(carpeta);
        Debug.Log("[PlayHistoria] Arranca la prueba del Data Center.");

        yield return EsperarHasta(() => GameManager.Instance != null && GameManager.Instance.Story != null &&
                                        GameManager.Instance.CurrentPlayer != null, 10f);
        GameManager gm = GameManager.Instance;
        StoryModel story = gm?.Story;
        PlayerModel player = gm?.CurrentPlayer;
        Check("Hay partida (DEMO)", player != null && story != null);
        if (player == null || story == null) { Terminar(); yield break; }

        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _vista = FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
        PlayerController jugador = FindAnyObjectByType<PlayerController>();
        GameObject raiz = GameObject.Find("HistoriaDataCenter");
        Check("Esta el contenedor HistoriaDataCenter", raiz != null);
        if (raiz == null || _dialogo == null || jugador == null) { Terminar(); yield break; }

        NpcQueHabla[] npcs = raiz.GetComponentsInChildren<NpcQueHabla>(true);
        ZonaDeHistoria[] zonas = raiz.GetComponentsInChildren<ZonaDeHistoria>(true);
        SalidaDelNivel salida = raiz.GetComponentInChildren<SalidaDelNivel>(true);
        ApagonDelNivel apagon = raiz.GetComponentInChildren<ApagonDelNivel>(true);
        InteractableTrigger nestor = FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.name.StartsWith("NPC_Nestor"));
        Check("12 cosas para hablar, 2 zonas, salida, apagon y Nestor",
              npcs.Length == 12 && zonas.Length == 2 && salida && apagon && nestor);

        // ---- 1. Todos hablan (antes de Nestor) ----
        var antes = new System.Collections.Generic.Dictionary<NpcQueHabla, string>();
        foreach (NpcQueHabla npc in npcs)
        {
            npc.Interact(player, story);
            yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
            antes[npc] = TextoDeLaCaja();
            Check($"'{npc.name}' habla", _dialogo.IsOpen && antes[npc].Length > 0);
            yield return Foto($"01-{npc.name}");
            yield return CerrarDialogos(20f);
        }
        Check("Hablar con el tecnico deja la pista para el casino", story.HasFlag(StoryFlag.Historia_DC_TecnicoHablo));
        Check("Leer el memo de J. queda anotado", story.HasFlag(StoryFlag.Historia_DC_MemoLeido));

        // ---- 2. Las zonas ----
        foreach (ZonaDeHistoria zona in zonas)
        {
            yield return Pisar(jugador, zona.transform.position);
            yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
            Check($"Pisar '{zona.name}' cuenta su momento", _dialogo.IsOpen);
            yield return Foto($"02-{zona.name}");
            yield return CerrarDialogos(10f);
            Check($"'{zona.name}' queda contada", zona.Dialogo != null && story.HasFlag(zona.Dialogo.grantsFlag));
            yield return Pisar(jugador, zona.transform.position + Vector3.down * 2f);
            yield return Pisar(jugador, zona.transform.position);
            yield return new WaitForSecondsRealtime(0.4f);
            Check($"'{zona.name}' no se repite", !_dialogo.IsOpen);
        }

        // ---- 3. Nestor antes de la pelea: se escucha y se dice "Todavia no" ----
        BattleController combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        nestor.Interact(player, story);
        yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
        yield return Foto("03-nestor-antes");
        float limite = Time.realtimeSinceStartup + 20f;
        while (_dialogo.IsOpen && !HayOpciones() && Time.realtimeSinceStartup < limite)
        {
            _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.15f);
        }
        Check("Despues de la charla, el sistema pregunta si pelear", HayOpciones() && TextoElegido().Contains("Pelear"));
        yield return Foto("04-nestor-protocolo");
        _dialogo.OnCancel();                                    // "Todavia no"
        yield return CerrarDialogos(5f);
        Check("'Todavia no' no arranca la pelea", combate == null || !combate.gameObject.activeInHierarchy);
        Check("La charla de antes queda contada", story.HasFlag(StoryFlag.Historia_NestorPresentado));

        nestor.Interact(player, story);
        yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
        Check("La segunda vez Nestor va al grano", TextoDeLaCaja().Contains("Otra vez"));
        yield return CerrarDialogos(5f);

        // ---- 4. La victoria (simulada) y lo de despues ----
        story.ActivateFlag(StoryFlag.Torre1_NestorLiberado);
        yield return new WaitForSecondsRealtime(3f);
        Check("Con Nestor liberado, la salida del nivel NO te saca sola", SceneManager.GetActiveScene().name == "NestorGOD2");

        DialogueDefinition despues = Campo<DialogueDefinition>(nestor, "dialogoAlGanar");
        Check("Nestor tiene su charla de despues", despues != null);
        if (despues != null)
        {
            _dialogo.StartDialogue(despues, story);
            yield return Foto("05-nestor-despues");
            bool fotoApagon = false;
            limite = Time.realtimeSinceStartup + 40f;
            while (_dialogo.IsOpen && Time.realtimeSinceStartup < limite)
            {
                if (!fotoApagon && apagon.Apagado)
                {
                    yield return new WaitForSecondsRealtime(2f);
                    yield return Foto("06-apagon");
                    fotoApagon = true;
                }
                if (TextoDeLaCaja().Contains("PROTOCOLO NÉSTOR")) yield return Foto("07-protocolo");
                _dialogo.OnConfirm();
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }
        Check("A mitad de la charla el servidor se desconecta", story.HasFlag(StoryFlag.Historia_Servidor01Desconectado));
        Check("Y las luces se apagan", apagon.Apagado);
        Check("Nestor da el Protocolo Nestor (objeto clave)",
              player.Inventory.SpecialItems.Any(i => i.Definition != null && i.Definition.itemName == "Protocolo Néstor"));
        Check("La charla de despues queda contada", story.HasFlag(StoryFlag.Historia_NestorDesperto));

        // ---- 5. Todos reaccionan ----
        int cambiaron = 0;
        foreach (NpcQueHabla npc in npcs)
        {
            npc.Interact(player, story);
            yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
            if (TextoDeLaCaja() != antes[npc]) cambiaron++;
            if (npc.name == "Recepcionista" || npc.name == "GuardiaDeLaLiga") yield return Foto($"08-despues-{npc.name}");
            yield return CerrarDialogos(20f);
        }
        Check($"Despues de la caida, dicen otra cosa ({cambiaron} de {npcs.Length})", cambiaron >= npcs.Length - 1);

        nestor.Interact(player, story);
        yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
        Check("Ya vencido, Nestor no vuelve a ofrecer la pelea", !HayOpciones() && TextoDeLaCaja().Contains("acordando"));
        yield return Foto("09-nestor-ya-hecho");
        yield return CerrarDialogos(5f);

        // ---- 6. La salida, caminando ----
        story.ActivateFlag(StoryFlag.Lobby_TixiPresentado);       // para ver la vuelta de Tixi
        yield return Pisar(jugador, salida.transform.position + Vector3.up);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Foto("10-salida-brilla");
        yield return Pisar(jugador, salida.transform.position);
        yield return EsperarHasta(() => _dialogo.IsOpen && HayOpciones(), 3f);
        Check("Pisar la salida pregunta si volver a la cueva", HayOpciones() && TextoDeLaCaja().Contains("Quarcho"));
        _dialogo.OnConfirm();                                   // Volver
        yield return EsperarHasta(() => SceneManager.GetActiveScene().name == RecorridoDeLaHistoria.EscenaDelLobby, 10f);
        Check("La salida lleva a la cueva", SceneManager.GetActiveScene().name == RecorridoDeLaHistoria.EscenaDelLobby);

        // ---- 7. En la cueva, Tixi cuenta la vuelta ----
        yield return new WaitForSecondsRealtime(0.5f);
        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _vista = FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
        yield return EsperarHasta(() => _dialogo != null && _dialogo.IsOpen, 6f);
        Check("Tixi cuenta la vuelta del Data Center", _dialogo != null && _dialogo.IsOpen);
        yield return Foto("11-tixi-vuelta");
        bool fotoMemo = false, fotoCasino = false;
        limite = Time.realtimeSinceStartup + 40f;
        while (_dialogo.IsOpen && Time.realtimeSinceStartup < limite)
        {
            if (!fotoMemo && TextoDeLaCaja().Contains("firmado con una J")) { yield return Foto("12-tixi-memo"); fotoMemo = true; }
            if (!fotoCasino && TextoDeLaCaja().Contains("¿Eso es un casino?")) { yield return Foto("13-tixi-casino"); fotoCasino = true; }
            _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.12f);
        }
        Check("Tixi se acuerda del memo de J.", fotoMemo);
        Check("Tixi presenta el casino", fotoCasino);
        Check("La vuelta queda contada", story.HasFlag(StoryFlag.Historia_TixiVueltaDelDataCenter));

        Terminar();
    }

    // ---------- utilidades ----------

    private void Terminar()
    {
        MenuManager.Instance?.CloseAll();
        Debug.Log($"[PlayHistoria] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[PlayHistoria] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[PlayHistoria] FALLO: {nombre}"); }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < limite) yield return null;
    }

    private IEnumerator CerrarDialogos(float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while ((_dialogo.IsOpen || MenuManager.AnyOpen) && Time.realtimeSinceStartup < limite)
        {
            if (MenuManager.AnyOpen) MenuManager.Instance.CloseAll();
            else if (HayOpciones()) _dialogo.OnCancel();
            else _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private static IEnumerator Pisar(PlayerController jugador, Vector3 donde)
    {
        var celda = new Vector3(Mathf.Floor(donde.x) + 0.5f, Mathf.Floor(donde.y) + 0.5f, 0f);
        jugador.TeleportTo(celda, Vector2.down);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    private bool HayOpciones()
    {
        var panel = Campo<GameObject>(_vista, "choicePanel");
        return panel != null && panel.activeInHierarchy;
    }

    private string TextoDeLaCaja()
    {
        var texto = Campo<Text>(_vista, "dialogueLineText");
        return texto != null ? texto.text : "";
    }

    private string TextoElegido()
    {
        var panel = Campo<GameObject>(_vista, "choicePanel");
        if (panel == null) return "";
        Text t = panel.GetComponentsInChildren<Text>(false).FirstOrDefault(x => x.text.StartsWith(">"));
        return t != null ? t.text : "";
    }

    private static T Campo<T>(object objeto, string nombre) where T : class
    {
        if (objeto == null) return null;
        FieldInfo f = objeto.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f?.GetValue(objeto) as T;
    }

    private IEnumerator Foto(string nombre)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot($"{carpeta}/{nombre}.png");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
    }
}
