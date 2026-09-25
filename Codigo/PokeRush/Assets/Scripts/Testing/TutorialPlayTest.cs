using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Recorre el esqueleto del tutorial como un jugador: el despertar, la puerta
// cerrada, la pelea antes de tiempo, las cinco partes de menu, la pelea, la
// puerta abierta y, al final, omitir el tutorial y salir a la cueva. Deja
// capturas en Capturas/Tutorial/ y los resultados en el Editor.log con la
// etiqueta [PlayTutorial] (la consola del MCP no es confiable para esto).
//
// Se lanza desde PokeRush > Tutorial > Probar el esqueleto en Play. Juega con
// una partida DEMO y NO guarda: apaga el guardado automatico para no pisar la
// partida de verdad.
public class TutorialPlayTest : MonoBehaviour
{
    public const string PedidoKey = "PokeRush.PlayTutorial.Pendiente";

    public string carpeta = "Capturas/Tutorial";

#if UNITY_EDITOR
    // El banco se crea al entrar en Play y no se pone en la escena: asi el
    // esqueleto no queda con cambios sin guardar por la prueba.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearSiLoPidieron()
    {
        if (!UnityEditor.SessionState.GetBool(PedidoKey, false)) return;
        UnityEditor.SessionState.SetBool(PedidoKey, false);
        new GameObject("[PlayTutorial]").AddComponent<TutorialPlayTest>();
    }
#endif

    private int _ok;
    private int _fallos;
    private DialogueController _dialogo;
    private DialogueView _vista;
    private bool _habiaNegro;

    private void Awake()
    {
        // Con la ventana de Unity en segundo plano el player loop se congela.
        Application.runInBackground = true;
        CasinoDialogo.GuardadoAutomatico = false;
        // Tiene que sobrevivir a la carga de la cueva del final.
        DontDestroyOnLoad(gameObject);
        // El fundido se arma en el Awake del tutorial, antes que esto.
        _habiaNegro = GameObject.Find("[FundidoDelDespertar]") != null;
    }

    private void OnDestroy() => CasinoDialogo.GuardadoAutomatico = true;

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        System.IO.Directory.CreateDirectory(carpeta);
        Debug.Log("[PlayTutorial] Arranca la prueba del tutorial.");
        string escena = SceneManager.GetActiveScene().name;
        Check("Arranca en negro (el fundido del despertar)", _habiaNegro);

        yield return EsperarHasta(() => GameManager.Instance != null && GameManager.Instance.Story != null &&
                                        GameManager.Instance.CurrentPlayer != null, 10f);
        GameManager gm = GameManager.Instance;
        StoryModel story = gm?.Story;
        PlayerModel player = gm?.CurrentPlayer;
        Check("Hay partida (DEMO) con un Pokemon", player != null && player.Team.Count > 0);
        if (player == null || story == null) { Terminar(); yield break; }

        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _vista = FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
        MenuManager menus = MenuManager.Instance;
        TutorialDeLaHabitacion tutorial = TutorialDeLaHabitacion.Buscar();
        List<TutorialParte> partes = TutorialParte.TodasLasDeLaEscena();
        TutorialParte pelea = partes.FirstOrDefault(p => p.QueHace == TutorialParte.Tipo.Pelea);
        PuertaDelTutorial puerta = FindAnyObjectByType<PuertaDelTutorial>();
        PlayerController jugador = FindAnyObjectByType<PlayerController>();
        BattleController combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        Check("Estan el tutorial, las partes, la pelea, la puerta y el combate",
              tutorial && partes.Count == 6 && pelea && puerta && jugador && combate);
        if (!tutorial || !pelea || !puerta || !jugador || !combate) { Terminar(); yield break; }

        Vector3 cama = TutorialDeLaHabitacion.CentroDeCelda(tutorial.PuntoDeDespertar.position);
        Check("El jugador despierta al lado de la cama", Vector2.Distance(jugador.transform.position, cama) < 0.01f);

        // ---- 1. Lo basico al despertar ----
        yield return EsperarHasta(() => _dialogo.IsOpen, 6f);
        Check("Al despertar cuenta lo basico sin que nadie lo pida", _dialogo.IsOpen);
        Check("El fundido ya termino cuando habla", GameObject.Find("[FundidoDelDespertar]") == null);
        yield return Foto("01-despertar");
        yield return CerrarDialogos();
        Check("Queda Tutorial_Despertar", story.HasFlag(StoryFlag.Tutorial_Despertar));
        yield return new WaitForSecondsRealtime(0.3f);
        Check("Todas las partes brillan al principio", partes.All(Brilla));
        yield return Foto("02-habitacion");

        // ---- 2. La puerta sin terminar ----
        yield return Pisar(jugador, puerta.transform.position);
        yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
        Check("La puerta no deja salir: pregunta si seguir u omitir", _dialogo.IsOpen && HayOpciones());
        Check("La puerta dice lo que falta", TextoDeLaCaja().Contains("falta"));
        Check("La opcion marcada es seguir", TextoElegido().Contains("Seguir"));
        yield return Foto("03-puerta-sin-terminar");
        _dialogo.OnCancel();                                     // Escape
        yield return new WaitForSecondsRealtime(0.2f);
        Check("Escape sigue con el tutorial: no lo omite", !_dialogo.IsOpen && !story.HasFlag(StoryFlag.Tutorial_Omitido));
        yield return Pisar(jugador, cama);

        // ---- 3. La pelea antes de tiempo ----
        pelea.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        Check("La compañera pide terminar lo demas primero", _dialogo.IsOpen && TextoDeLaCaja().Contains("Primero"));
        Check("...y la pelea no arranca", !combate.gameObject.activeInHierarchy);
        yield return Foto("04-pelea-antes-de-tiempo");
        yield return CerrarDialogos();

        // ---- 4. Las partes de menu ----
        HealItemDefinition pocion = DefinitionRepository.Instance.GetHealItem("Poción");
        int pociones = pocion != null ? player.Inventory.CountHealItem(pocion) : 0;
        int n = 0;
        foreach (TutorialParte parte in partes.Where(p => p.QueHace != TutorialParte.Tipo.Pelea))
        {
            n++;
            parte.Interact(player, story);
            bool fotoCharla = false;
            float limite = Time.realtimeSinceStartup + 12f;
            while (!MenuManager.AnyOpen && Time.realtimeSinceStartup < limite)
            {
                if (_dialogo.IsOpen)
                {
                    if (!fotoCharla) { yield return Foto($"05-{n}-{parte.name}-charla"); fotoCharla = true; }
                    _dialogo.OnConfirm();
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }

            Check($"{parte.name}: abre su pantalla ({parte.QueHace})", PantallaEsperada(parte.QueHace, menus.Top));
            Check($"{parte.name}: queda hecha", ReglasDelTutorial.Hecha(parte, story));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Foto($"05-{n}-{parte.name}-menu");

            menus.CloseAll();
            yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
            Check($"{parte.name}: al cerrar dice lo que falta", TextoDeLaCaja().Contains("falta"));
            if (n == 1) yield return Foto("05-progreso");
            yield return CerrarDialogos();
            yield return null;
            Check($"{parte.name}: deja de brillar", !Brilla(parte));
            Check($"{parte.name}: la parte queda libre", !parte.Ocupada);
        }

        if (pocion != null)
            Check("La mochila dio dos Pociones", player.Inventory.CountHealItem(pocion) == pociones + 2);
        Check("Despues de los menus solo falta la pelea", TutorialDeLaHabitacion.Faltan(story) == pelea.Nombre);

        // Volver a usar una parte hecha: la mochila abre la mochila de nuevo.
        TutorialParte mochila = partes.First(p => p.QueHace == TutorialParte.Tipo.Mochila);
        mochila.Interact(player, story);
        yield return EsperarHasta(() => MenuManager.AnyOpen, 3f);
        Check("La mochila ya usada es un atajo a la Bolsa", menus.Top is BagScreen);
        menus.CloseAll();
        yield return CerrarDialogos();

        // ---- 5. La pelea ----
        pelea.Interact(player, story);
        bool fotoPelea = false;
        float limitePelea = Time.realtimeSinceStartup + 15f;
        while (!combate.gameObject.activeInHierarchy && Time.realtimeSinceStartup < limitePelea)
        {
            if (_dialogo.IsOpen)
            {
                if (!fotoPelea) { yield return Foto("06-pelea-charla"); fotoPelea = true; }
                _dialogo.OnConfirm();
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Check("Con todo lo demas hecho, la pelea arranca", combate.gameObject.activeInHierarchy);

        yield return new WaitForSecondsRealtime(4f);
        yield return Foto("07-pelea");
        // El que mas pega: el primero de la lista puede ser uno de estado y la
        // pelea no terminaria nunca.
        int ataque = MejorAtaque(player.Team[0]);
        float finPelea = Time.realtimeSinceStartup + 120f;
        while (combate.gameObject.activeInHierarchy && Time.realtimeSinceStartup < finPelea)
        {
            combate.SelectMove(ataque);
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("La pelea termina", !combate.gameObject.activeInHierarchy);

        yield return EsperarHasta(() => _dialogo.IsOpen, 5f);
        Check("Ganada: queda Tutorial_Pelea", story.HasFlag(StoryFlag.Tutorial_Pelea));
        Check("El equipo sale curado", player.Team.All(p => p.CurrentHP == p.MaxHP && p.CurrentStatus == StatusEffect.None));
        yield return Foto("08-pelea-ganada");
        _dialogo.OnConfirm();                                    // cierra "¡Bien peleado!"
        yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
        Check("Con la ultima parte llega el cierre", _dialogo.IsOpen);
        yield return Foto("09-cierre");
        yield return CerrarDialogos();
        Check("Queda Tutorial_Completado", story.HasFlag(StoryFlag.Tutorial_Completado));
        Check("La compañera deja de brillar", !Brilla(pelea));

        // ---- 6. La puerta terminada ----
        yield return Pisar(jugador, puerta.transform.position);
        yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
        Check("Terminado, la puerta ofrece salir a la cueva", _dialogo.IsOpen && TextoDeLaCaja().Contains("Quarcho"));
        yield return Foto("10-puerta-terminado");
        _dialogo.OnCancel();                                     // "Todavía no"
        yield return new WaitForSecondsRealtime(0.3f);
        Check("'Todavia no' se queda en la habitacion", SceneManager.GetActiveScene().name == escena);
        yield return Pisar(jugador, cama);

        // ---- 7. Omitir (partida como recien empezada) y salir a la cueva ----
        story.ActiveFlags.RemoveWhere(f => f.ToString().StartsWith("Tutorial_"));
        int antesDeOmitir = pocion != null ? player.Inventory.CountHealItem(pocion) : 0;
        yield return Pisar(jugador, puerta.transform.position);
        yield return EsperarHasta(() => _dialogo.IsOpen && HayOpciones(), 3f);
        _dialogo.OnNavigate(1);
        Check("La segunda opcion es omitir", TextoElegido().Contains("Omitir"));
        yield return Foto("11-omitir");
        _dialogo.OnConfirm();
        yield return EsperarHasta(() => _dialogo.IsOpen, 2f);
        Check("Omitido: queda Tutorial_Omitido", story.HasFlag(StoryFlag.Tutorial_Omitido));
        if (pocion != null)
            Check("Omitir da las Pociones de la mochila igual", player.Inventory.CountHealItem(pocion) == antesDeOmitir + 2);
        yield return Foto("12-omitido");
        if (_dialogo.IsOpen) _dialogo.OnConfirm();               // "Te llevas..." -> sale
        yield return EsperarHasta(() => SceneManager.GetActiveScene().name != escena, 10f);
        Check($"La puerta lleva a {RecorridoDeLaHistoria.EscenaDelLobby}",
              SceneManager.GetActiveScene().name == RecorridoDeLaHistoria.EscenaDelLobby);
        yield return new WaitForSecondsRealtime(1f);
        yield return Foto("13-cueva");

        Terminar();
    }

    // ---------- utilidades ----------

    private static int MejorAtaque(PokenModel poken)
    {
        int mejor = 0;
        for (int i = 0; i < poken.Moves.Count; i++)
            if (poken.Moves[i].HasPP && poken.Moves[i].Definition.power > poken.Moves[mejor].Definition.power)
                mejor = i;
        return mejor;
    }

    private static bool PantallaEsperada(TutorialParte.Tipo tipo, MenuScreen top)
    {
        switch (tipo)
        {
            case TutorialParte.Tipo.Mochila: return top is BagScreen;
            case TutorialParte.Tipo.Equipo: return top is PartyScreen;
            case TutorialParte.Tipo.Resumen: return top is SummaryScreen;
            case TutorialParte.Tipo.Pc: return top is PcStorageScreen;
            case TutorialParte.Tipo.Guardar: return top is PauseMenuScreen;
            case TutorialParte.Tipo.Opciones: return top is OptionsScreen;
            default: return false;
        }
    }

    // El brillo arma sus hijos (ocultos en la jerarquia) mientras la parte no
    // este hecha, y los borra al apagarse.
    private static bool Brilla(TutorialParte parte)
        => parte != null && parte.transform.Find("BrilloHalo") != null;

    // Poner al jugador en una celda y dejar correr la fisica: asi se disparan
    // los OnTrigger de la puerta como al caminar.
    private static IEnumerator Pisar(PlayerController jugador, Vector3 donde)
    {
        jugador.TeleportTo(TutorialDeLaHabitacion.CentroDeCelda(donde), Vector2.down);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    private void Terminar()
    {
        MenuManager.Instance?.CloseAll();
        Debug.Log($"[PlayTutorial] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[PlayTutorial] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[PlayTutorial] FALLO: {nombre}"); }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < limite) yield return null;
    }

    private IEnumerator CerrarDialogos()
    {
        float limite = Time.realtimeSinceStartup + 10f;
        while ((_dialogo.IsOpen || MenuManager.AnyOpen) && Time.realtimeSinceStartup < limite)
        {
            if (MenuManager.AnyOpen) MenuManager.Instance.CloseAll();
            else if (HayOpciones()) _dialogo.OnCancel();
            else _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.25f);
        }
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

    // El renglon marcado con el prefijo de seleccion ("> ").
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
