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
        var camina = tixi.GetComponent<NpcQueCamina>();
        var jugador = FindAnyObjectByType<PlayerController>();
        Check("Tixi camina hasta el jugador antes de hablar",
              camina != null && jugador != null && !camina.EnSuLugar &&
              Vector2.Distance(tixi.transform.position, jugador.transform.position) < 1.1f);
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

        // Cerrada la charla, vuelve caminando a su lugar y recien ahi suelta al jugador.
        Check("Mientras vuelve, el jugador queda frenado", !tixi.Libre && PlayerInputController.Frenado);
        yield return EsperarHasta(() => tixi.Libre, 8f);
        Check("Despues de la charla Tixi vuelve a su lugar", camina != null && camina.EnSuLugar);
        Check("Y el jugador se puede mover de nuevo", !PlayerInputController.Frenado);

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

        // ---- 7b. El entrenamiento intenso del chip (playtest del 5-oct) ----
        yield return EntrenamientoDelChip(tixi, player, story);

        // ---- 7c. Aprender al subir de nivel (el tutor en modo "subir de nivel") ----
        yield return AprenderAlSubirDeNivel(player, menus);

        // ---- 8. La salida ----
        RecorridoDeLaHistoria.Nivel siguiente = RecorridoDeLaHistoria.SiguienteNivel(story);
        Check("Despues del casino la salida apunta al jefe final", siguiente?.EscenaDeEntrada == "BossFinal");
        // Desde el 4-oct BossFinal existe (la arena de Santiago con Julieta).
        Check("La escena del jefe final esta en Build Settings y la salida la carga", ViajeEntreNiveles.SePuedeCargar(siguiente));
        Check("El casino si esta en Build Settings", Application.CanStreamedLevelBeLoaded("Casino Nivel 1"));

        Terminar();
    }

    // Tixi -> "Entrenamiento intenso del chip" -> conectarse -> ganar el
    // primer combate (el rival a 1 PS) -> retirarse. Tiene que pagar lo del
    // primer combate y devolver el equipo curado.
    private IEnumerator EntrenamientoDelChip(QuarchoTixiNpc tixi, PlayerModel player, StoryModel story)
    {
        var chip = FindAnyObjectByType<EntrenamientoDelChip>(FindObjectsInactive.Include);
        var combate = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        Check("La cueva tiene el entrenamiento del chip y su combate", chip != null && combate != null);
        if (chip == null || combate == null) yield break;
        Check("Despues del casino el chip esta disponible", chip.Disponible(story));

        // Con un solo Pokemon no hay a quien cambiar: se suma uno para probar el
        // aviso del rival (modo "Cambio"). La partida DEMO no se guarda.
        if (player.Team.Count < 2)
        {
            PokenDefinition lucario = DefinitionRepository.Instance.GetPoken("Lucario");
            if (lucario != null) player.Team.Add(new PokenModel(lucario, 20));
        }

        yield return EsperarHasta(() => tixi.Libre, 8f);
        tixi.Interact(player, story);
        yield return new WaitForSecondsRealtime(0.3f);
        _dialogo.OnNavigate(1);
        Check("La segunda opcion de Tixi es el entrenamiento del chip", TextoElegido().Contains("chip"));
        yield return Foto("20-tixi-menu-chip");

        int plata = player.Money;
        int premio = chip.PremioDe(1);
        _dialogo.OnConfirm();
        // La explicacion y la pregunta ("Conectarme" es la primera opcion).
        float limite = Time.realtimeSinceStartup + 6f;
        while (!HayOpciones() && Time.realtimeSinceStartup < limite)
        {
            _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Check("El chip explica y pregunta si se conecta", HayOpciones() && TextoDeLaCaja().Contains(premio.ToString()));
        yield return Foto("21-chip-pregunta");
        _dialogo.OnConfirm();

        // "Simulacion 1..." y arranca la pelea.
        limite = Time.realtimeSinceStartup + 8f;
        while (!combate.EnCurso && Time.realtimeSinceStartup < limite)
        {
            if (_dialogo.IsOpen) _dialogo.OnConfirm();
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Check("El chip arranca un combate", combate.EnCurso && chip.EnCurso);
        if (!combate.EnCurso) yield break;
        Check("El rival virtual trae tres Pokemon", combate.Modelo.Enemy.Team.Count == 3);
        yield return new WaitForSecondsRealtime(3f);
        yield return Foto("22-chip-combate");

        _probarElCambio = player.Team.Count(p => !p.IsFainted) > 1;
        _cambioPreguntado = false;
        _relevoElegido = -1;
        _cambioHecho = false;
        yield return GanarElCombate(combate, 90f);
        Check("El primer combate del chip se gana", !combate.EnCurso);
        if (_probarElCambio)
        {
            Check("Al caer un rival, avisa a quien manda y pregunta si cambia", _cambioPreguntado);
            Check("Diciendo que si, el elegido entra antes que el rival", _cambioHecho);
        }

        // "¡Racha de 1! ... ¿Seguís?" -> Retirarme (la segunda).
        yield return EsperarHasta(() => HayOpciones(), 8f);
        Check("Despues de ganar pregunta si sigue", HayOpciones() && chip.Racha == 1);
        yield return Foto("23-chip-seguir");
        _dialogo.OnCancel();
        yield return EsperarHasta(() => !chip.EnCurso, 3f);
        yield return Foto("24-chip-fin");
        yield return CerrarDialogos();

        Check("Retirarse termina el entrenamiento", !chip.EnCurso);
        Check($"El primer combate paga {premio}", player.Money == plata + premio);
        Check("El equipo sale curado", !player.EquipoNecesitaCuracion);
    }

    // La pantalla que abre el combate cuando un Pokemon que ya sabe cuatro
    // quiere aprender otro. Primero "no aprenderlo" y despues olvidar el primero.
    private IEnumerator AprenderAlSubirDeNivel(PlayerModel player, MenuManager menus)
    {
        PokenModel alumno = player.Team.FirstOrDefault(p => p.Moves.Count == 4);
        MoveDefinition nuevo = DefinitionRepository.Instance.GetMove("Lanzallamas");
        Check("Hay un Pokemon con cuatro movimientos y un movimiento para aprender", alumno != null && nuevo != null);
        if (alumno == null || nuevo == null || alumno.KnowsMove(nuevo)) yield break;

        var antes = alumno.Moves.Select(mv => mv.Definition).ToList();
        menus.SetContext(MenuContextKind.Battle);
        menus.Open(MenuScreenId.MoveTutor, new MoveTutorRequest { Poken = alumno, AprenderEste = nuevo, Maestro = "" });
        yield return EsperarHasta(() => menus.Top is MoveTutorScreen, 3f);
        Check("Subir de nivel abre el tutor en modo 'que olvida'", menus.Top is MoveTutorScreen);
        yield return Foto("25-subir-de-nivel");

        // Volver = "¿dejar de aprender?" -> SI.
        menus.Top.HandleAction(GameAction.Cancel);
        yield return EsperarHasta(() => menus.Top is MessageBoxScreen, 2f);
        yield return Foto("25b-dejar-de-aprender");
        menus.Top.HandleAction(GameAction.Up);
        menus.Top.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return CerrarCarteles(menus, 4f);
        Check("No aprenderlo deja los cuatro de antes", alumno.Moves.Select(mv => mv.Definition).SequenceEqual(antes));

        // Olvidar el primero -> SI -> lo aprende en su lugar.
        menus.Open(MenuScreenId.MoveTutor, new MoveTutorRequest { Poken = alumno, AprenderEste = nuevo, Maestro = "" });
        yield return EsperarHasta(() => menus.Top is MoveTutorScreen, 3f);
        menus.Top.HandleAction(GameAction.Confirm);
        yield return EsperarHasta(() => menus.Top is MessageBoxScreen, 2f);
        yield return Foto("25c-olvidar-el-primero");
        menus.Top.HandleAction(GameAction.Up);
        menus.Top.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Foto("25d-aprendio");
        yield return CerrarCarteles(menus, 4f);
        Check("Olvidando el primero, el nuevo queda en su lugar",
              alumno.Moves[0].Definition == nuevo && alumno.Moves.Skip(1).Select(mv => mv.Definition).SequenceEqual(antes.Skip(1)));
        Check("El tutor se cierra solo despues", !MenuManager.AnyOpen);
        menus.SetContext(MenuContextKind.Overworld);
    }

    private static IEnumerator CerrarCarteles(MenuManager menus, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        while (menus.Top is MessageBoxScreen && Time.realtimeSinceStartup < limite)
        {
            menus.Top.HandleAction(GameAction.Confirm);
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }

    // Para probar el aviso del rival una vez (ver EntrenamientoDelChip).
    private bool _probarElCambio;
    private bool _cambioPreguntado;
    private int _relevoElegido = -1;
    private bool _cambioHecho;

    // Pelea con el rival a 1 PS: el ataque que mas pega y listo. El primer
    // aviso del rival ("va a enviar a...") se contesta que SI y se manda al
    // otro, si _probarElCambio; los demas carteles, con NO. Si sube de nivel
    // sabiendo cuatro, no aprende.
    private IEnumerator GanarElCombate(BattleController combate, float segundos)
    {
        float limite = Time.realtimeSinceStartup + segundos;
        float turnoListoDesde = -1f;
        // Lo que va diciendo la barra de mensajes, para el log.
        Text mensajes = Campo<Text>(FindAnyObjectByType<BattleHudView>(FindObjectsInactive.Include), "messageText");
        string ultimo = null;
        while (combate.EnCurso && Time.realtimeSinceStartup < limite)
        {
            if (mensajes != null && mensajes.text != ultimo)
            {
                ultimo = mensajes.text;
                if (!string.IsNullOrEmpty(ultimo)) Debug.Log($"[PlayLobby] Combate: {ultimo}");
            }
            MenuScreen arriba = MenuManager.HasInstance ? MenuManager.Instance.Top : null;
            if (arriba is MoveTutorScreen) { arriba.HandleAction(GameAction.Cancel); yield return new WaitForSecondsRealtime(0.15f); continue; }
            if (arriba is MessageBoxScreen && MenuManager.Instance.BelowTop is MoveTutorScreen)
            {
                arriba.HandleAction(GameAction.Up);
                arriba.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is MessageBoxScreen aviso && _probarElCambio && !_cambioPreguntado
                && (Campo<Text>(aviso, "messageText")?.text ?? "").Contains("va a enviar"))
            {
                _cambioPreguntado = true;
                yield return Foto("22b-chip-aviso-del-rival");
                aviso.HandleAction(GameAction.Up);             // la caja arranca en NO
                aviso.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.3f);
                continue;
            }
            if (arriba is PartyScreen equipo && _cambioPreguntado && _relevoElegido < 0 && combate.Modelo != null)
            {
                BattleModel bm = combate.Modelo;
                for (int i = 0; i < bm.Player.Team.Count; i++)
                    if (i != bm.ActivePlayerIndex && !bm.Player.Team[i].IsFainted) { _relevoElegido = i; break; }
                yield return Foto("22c-chip-elegir-relevo");
                for (int n = 0; n < 12 && equipo.CursorIndex != _relevoElegido; n++) equipo.HandleAction(GameAction.Right);
                equipo.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                // Si abrio el submenu del Pokemon, la primera opcion es mandarlo.
                if (MenuManager.Instance.Top == equipo) equipo.HandleAction(GameAction.Confirm);
                yield return new WaitForSecondsRealtime(0.15f);
                continue;
            }
            if (arriba is MessageBoxScreen) { arriba.HandleAction(GameAction.Confirm); yield return new WaitForSecondsRealtime(0.15f); continue; }
            if (arriba is PartyScreen) { arriba.HandleAction(GameAction.Cancel); yield return new WaitForSecondsRealtime(0.15f); continue; }

            BattleModel m = combate.Modelo;
            if (m != null && _relevoElegido >= 0 && m.ActivePlayerIndex == _relevoElegido) _cambioHecho = true;
            if (m != null && m.CurrentPhase == BattlePhase.PlayerTurn)
            {
                if (turnoListoDesde < 0f) turnoListoDesde = Time.realtimeSinceStartup;
                if (BattleHudView.IsNavigating || Time.realtimeSinceStartup - turnoListoDesde > 2.5f)
                {
                    m.ActiveEnemyPoken.SetCurrentHP(1);
                    PokenModel activo = m.ActivePlayerPoken;
                    int mejor = Enumerable.Range(0, activo.Moves.Count)
                        .Where(i => activo.Moves[i].HasPP && PokenModel.SirveParaPegar(activo.Moves[i].Definition))
                        .OrderByDescending(i => activo.Moves[i].Definition.power)
                        .DefaultIfEmpty(0).First();
                    Debug.Log($"[PlayLobby] Turno: {activo.Definition.pokenName} ({activo.CurrentHP} PS) usa " +
                              $"{activo.Moves[mejor].Definition.moveName} contra {m.ActiveEnemyPoken.Definition.pokenName}.");
                    combate.SelectMove(mejor);
                    turnoListoDesde = -1f;
                }
            }
            else turnoListoDesde = -1f;
            yield return null;
        }
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
