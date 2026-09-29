using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Recorre la Quarcho Cueva como un jugador, con las mismas acciones del
// teclado: la presentacion de Tixi, la tienda, el menu de Tixi, el tutor de
// Nestor, la ruleta de mejoras de Martin y Tacconi. Deja capturas en
// Capturas/Lobby/ y los resultados en el Editor.log con la etiqueta
// [PlayLobby] (la consola del MCP no es confiable para esto).
//
// Se lanza desde PokeRush > Quarcho Cueva > Probar la cueva en Play. Juega con
// una partida DEMO y NO guarda: apaga el guardado automatico para no pisar la
// partida de verdad.
public class LobbyPlayTest : MonoBehaviour
{
    public const string PedidoKey = "PokeRush.PlayLobby.Pendiente";

    public string carpeta = "Capturas/Lobby";

#if UNITY_EDITOR
    // El banco se crea al entrar en Play y no se pone en la escena: asi la cueva
    // no queda con cambios sin guardar por la prueba.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearSiLoPidieron()
    {
        if (!UnityEditor.SessionState.GetBool(PedidoKey, false)) return;
        UnityEditor.SessionState.SetBool(PedidoKey, false);
        new GameObject("[PlayLobby]").AddComponent<LobbyPlayTest>();
    }
#endif

    private int _ok;
    private int _fallos;
    private DialogueController _dialogo;
    private DialogueView _vista;

    private void Awake()
    {
        // Con la ventana de Unity en segundo plano el player loop se congela.
        Application.runInBackground = true;
        CasinoDialogo.GuardadoAutomatico = false;
    }

    private void OnDestroy() => CasinoDialogo.GuardadoAutomatico = true;

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        System.IO.Directory.CreateDirectory(carpeta);
        Debug.Log("[PlayLobby] Arranca la prueba de la Quarcho Cueva.");

        yield return EsperarHasta(() => GameManager.Instance != null && GameManager.Instance.Story != null &&
                                        GameManager.Instance.CurrentPlayer != null, 10f);
        GameManager gm = GameManager.Instance;
        StoryModel story = gm?.Story;
        PlayerModel player = gm?.CurrentPlayer;
        Check("Hay partida (DEMO)", player != null && story != null);
        if (player == null) { Terminar(); yield break; }

        // El equipo de respaldo de la cueva (GameManager de la escena). Si no
        // hubiera, la prueba sigue con un Lucario para poder probar el tutor.
        Check("La partida DEMO de la cueva trae equipo", player.Team.Count > 0);
        if (player.Team.Count == 0)
        {
            PokenDefinition lucario = DefinitionRepository.Instance.GetPoken("Lucario");
            if (lucario != null) player.Team.Add(new PokenModel(lucario, 20));
        }

        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        _vista = FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
        MenuManager menus = MenuManager.Instance;
        var tixi = FindAnyObjectByType<QuarchoTixiNpc>(FindObjectsInactive.Include);
        var nestor = FindAnyObjectByType<QuarchoNestorNpc>(FindObjectsInactive.Include);
        var martin = FindAnyObjectByType<QuarchoMartinNpc>(FindObjectsInactive.Include);
        var tacconi = FindObjectsByType<CasinoTacconiNpc>(FindObjectsInactive.Include).FirstOrDefault();
        Check("Estan Tixi, Nestor, Martin y Tacconi en la escena", tixi && nestor && martin && tacconi);
        Check("Nestor, Martin y Tacconi arrancan escondidos",
              !nestor.gameObject.activeInHierarchy && !martin.gameObject.activeInHierarchy && !tacconi.gameObject.activeInHierarchy);

        // ---- 1. Tixi habla sola ----
        yield return EsperarHasta(() => _dialogo.IsOpen, 5f);
        Check("Tixi habla sola al llegar", _dialogo.IsOpen);
        yield return Foto("01-tixi-lore");
        int plataAntes = player.Money;

        // Avanzar el lore (con su pregunta: va la primera opcion) y el regalo
        // hasta la pregunta de la tienda. El lore es el del guion: es largo.
        bool fotoPregunta = false;
        float limite = Time.realtimeSinceStartup + 60f;
        while (!(menus.Top is ShopScreen) && Time.realtimeSinceStartup < limite)
        {
            if (_dialogo.IsOpen && HayOpciones() && !fotoPregunta && TextoDeLaCaja().Contains("ver lo que tengo"))
            {
                yield return Foto("02-tixi-pregunta-tienda");
                fotoPregunta = true;
            }
            if (_dialogo.IsOpen) _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Check("Despues del lore, Tixi ofrece la tienda y se abre", menus.Top is ShopScreen);
        Check("La presentacion abre la salida (Lobby_TixiPresentado)", story.HasFlag(StoryFlag.Lobby_TixiPresentado));
        Check("Tixi da el regalo de bienvenida", player.Money == plataAntes + tixi.RegaloDeBienvenida);

        // ---- 2. La tienda ----
        if (menus.Top is ShopScreen tienda)
        {
            yield return Foto("03-tienda");
            HealItemDefinition primero = tixi.Tienda.healItemsForSale[0];
            int antes = player.Inventory.CountHealItem(primero);
            int plata = player.Money;

            tienda.HandleAction(GameAction.Confirm);          // elige el primero
            tienda.HandleAction(GameAction.Up);                // 2
            tienda.HandleAction(GameAction.Up);                // 3
            yield return Foto("04-tienda-cantidad");
            tienda.HandleAction(GameAction.Confirm);           // pregunta el total
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Foto("05-tienda-confirmar");
            ResponderSi(menus, tienda);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Foto("06-tienda-gracias");
            if (menus.Top != tienda) menus.Top.HandleAction(GameAction.Confirm);   // cierra el cartel
            yield return new WaitForSecondsRealtime(0.3f);

            Check("La tienda cobra 3 x el precio", player.Money == plata - 3 * primero.buyPrice);
            Check("Y mete 3 en la mochila", player.Inventory.CountHealItem(primero) == antes + 3);

            tienda.HandleAction(GameAction.Cancel);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Volver cierra la tienda", !(menus.Top is ShopScreen));
        }

        // Tixi cierra con el consejo de a donde ir.
        yield return EsperarHasta(() => _dialogo.IsOpen, 3f);
        yield return Foto("07-tixi-consejo");
        yield return CerrarDialogos();

        // ---- 3. El menu de Tixi (lista de cuatro opciones) ----
        tixi.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        Check("El menu de Tixi abre con su lista", OpcionesVisibles() >= 2);
        Check("La primera opcion de Tixi es Comprar", TextoElegido().Contains("Comprar"));
        yield return Foto("08-tixi-menu");
        for (int i = 0; i < 3; i++) _dialogo.OnNavigate(1);
        Check("Bajando se llega a 'Nada' aunque no entren todas", TextoElegido().Contains("Nada"));
        yield return Foto("08b-tixi-menu-abajo");
        _dialogo.OnCancel();                                   // Escape = Nada
        yield return new WaitForSecondsRealtime(0.2f);
        Check("Escape elige 'Nada' y cierra", !_dialogo.IsOpen);

        // ---- 4. La vuelta del Data Center ----
        // En el guion Nestor se queda en su Data Center recuperando la memoria:
        // a la cueva llega recien despues del casino.
        story.ActivateFlag(StoryFlag.Torre1_NestorLiberado);
        yield return null;
        Check("Con Nestor rescatado, Nestor todavia no esta en la cueva", !nestor.gameObject.activeInHierarchy);
        yield return EsperarHasta(() => _dialogo.IsOpen, 5f);
        Check("Al volver del Data Center, Tixi cuenta la vuelta sola", _dialogo.IsOpen);
        yield return Foto("09a-tixi-vuelta-data-center");
        yield return CerrarDialogos(45f);
        Check("La vuelta del Data Center queda contada", story.HasFlag(StoryFlag.Historia_TixiVueltaDelDataCenter));

        // ---- 5. La vuelta del casino: llegan Nestor, Martin y Tacconi ----
        story.ActivateFlag(StoryFlag.Casino_Completado);
        yield return null;
        Check("Con el casino completo aparecen Nestor, Martin y Tacconi",
              nestor.gameObject.activeInHierarchy && martin.gameObject.activeInHierarchy && tacconi.gameObject.activeInHierarchy);
        yield return EsperarHasta(() => _dialogo.IsOpen, 5f);
        Check("Al volver del casino, Tixi cuenta la vuelta sola", _dialogo.IsOpen);
        yield return Foto("09b-tixi-vuelta-casino");
        yield return CerrarDialogos(30f);
        Check("La vuelta del casino queda contada", story.HasFlag(StoryFlag.Historia_TixiVueltaDelCasino));

        // ---- 6. Nestor, el tutor ----
        player.AddMoney(5000);

        PokenModel alumno = player.Team[0];
        nestor.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Foto("09-nestor-menu");
        _dialogo.OnConfirm();                                  // Enseñar una MT
        yield return EsperarHasta(() => menus.Top is PartyScreen, 3f);
        if (menus.Top is PartyScreen) menus.Top.HandleAction(GameAction.Confirm);   // el primero
        yield return EsperarHasta(() => menus.Top is MoveTutorScreen, 3f);
        Check("Elegir un Pokemon abre el tutor", menus.Top is MoveTutorScreen);

        if (menus.Top is MoveTutorScreen tutor)
        {
            yield return Foto("10-tutor");
            tutor.HandleAction(GameAction.Down);
            yield return Foto("11-tutor-segundo");
            var aprendibles = TutorDeMovimientos.Ensenables(alumno);
            MoveDefinition elegido = aprendibles.Count > 1 ? aprendibles[1] : null;
            int plata = player.Money;

            tutor.HandleAction(GameAction.Confirm);
            yield return new WaitForSecondsRealtime(0.3f);
            if (menus.Top == tutor)
            {
                // Sabia cuatro: paso "¿que olvida?". Olvida el primero.
                yield return Foto("12-tutor-olvidar");
                tutor.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            yield return Foto("13-tutor-confirmar");
            ResponderSi(menus, tutor);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Foto("14-tutor-aprendio");
            if (menus.Top != null && menus.Top != tutor) menus.Top.HandleAction(GameAction.Confirm);   // cierra el cartel
            yield return new WaitForSecondsRealtime(0.3f);

            Check($"El Pokemon aprendio {elegido?.moveName}", elegido != null && alumno.KnowsMove(elegido));
            Check("Nestor cobro la MT", elegido != null && player.Money == plata - TutorDeMovimientos.Precio(elegido, nestor.Precios));
            Check("El tutor se cierra solo al aprender", !(menus.Top is MoveTutorScreen));
        }
        yield return CerrarDialogos();

        // ---- 7. Martin y Tacconi ----
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Foto("15-cueva-completa");

        martin.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        Check("El menu de Martin abre con su lista", OpcionesVisibles() >= 2);
        yield return Foto("16-martin-menu");
        _dialogo.OnNavigate(1);
        _dialogo.OnNavigate(1);                                // Ruleta de mejoras
        Check("La tercera opcion de Martin es la ruleta de mejoras", TextoElegido().Contains("mejoras"));
        yield return Foto("16b-martin-menu-mejoras");
        int plataMartin = player.Money;
        _dialogo.OnConfirm();
        yield return EsperarHasta(() => menus.Top is PrizeWheelController, 3f);
        Check("La ruleta de mejoras se abre", menus.Top is PrizeWheelController);
        Check("Martin cobra el tiro", player.Money == plataMartin - martin.Ruletas[2].precio);

        if (menus.Top is PrizeWheelController rueda)
        {
            yield return Foto("17-ruleta-mejoras");
            rueda.HandleAction(GameAction.Confirm);            // girar
            yield return EsperarHasta(() => menus.Top != rueda, 25f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Foto("18-ruleta-premio");
            float fin = Time.realtimeSinceStartup + 10f;
            while (MenuManager.AnyOpen && Time.realtimeSinceStartup < fin)
            {
                menus.Top?.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Check("La ruleta de mejoras deja una mejora para la proxima batalla", player.MejorasPendientes.Count == 1);
            Debug.Log($"[PlayLobby] Mejora ganada: {MejorasDeBatalla.Describir(player.MejorasPendientes)}");
        }
        yield return CerrarDialogos();

        tacconi.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Foto("19-tacconi");
        _dialogo.OnCancel();
        yield return CerrarDialogos();

        // ---- 8. La salida ----
        RecorridoDeLaHistoria.Nivel siguiente = RecorridoDeLaHistoria.SiguienteNivel(story);
        Check("Despues del casino la salida apunta al jefe final", siguiente?.EscenaDeEntrada == "BossFinal");
        Check("Como la escena del jefe todavia no existe, la salida no la carga", !ViajeEntreNiveles.SePuedeCargar(siguiente));
        Check("El casino si esta en Build Settings", Application.CanStreamedLevelBeLoaded("Casino Nivel 1"));

        Terminar();
    }

    // ---------- utilidades ----------

    // La pregunta de la caja de mensajes arranca en NO (a proposito: machacar
    // Aceptar no contesta que si). Como el jugador: mover a SI y aceptar.
    private static void ResponderSi(MenuManager menus, MenuScreen debajo)
    {
        if (menus.Top == null || menus.Top == debajo) return;
        menus.Top.HandleAction(GameAction.Up);
        menus.Top.HandleAction(GameAction.Confirm);
    }

    private void Terminar()
    {
        MenuManager.Instance?.CloseAll();
        Debug.Log($"[PlayLobby] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[PlayLobby] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[PlayLobby] FALLO: {nombre}"); }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < limite) yield return null;
    }

    private IEnumerator CerrarDialogos(float segundos = 8f)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while ((_dialogo.IsOpen || MenuManager.AnyOpen) && Time.realtimeSinceStartup < limite)
        {
            if (MenuManager.AnyOpen) MenuManager.Instance.Top?.HandleAction(GameAction.Confirm);
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

    private int OpcionesVisibles()
    {
        var panel = Campo<GameObject>(_vista, "choicePanel");
        return panel == null || !panel.activeInHierarchy
            ? 0
            : panel.GetComponentsInChildren<Text>(false).Count(t => t.gameObject.activeInHierarchy);
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
