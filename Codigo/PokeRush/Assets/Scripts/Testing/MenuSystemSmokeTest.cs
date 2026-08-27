using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// BANCO DE PRUEBAS del sistema de menus. No es parte del juego.
//
// Recorre el flujo completo (pausa -> party -> summary -> bolsa -> objetivo ->
// opciones -> guardar -> recargar) llamando a las MISMAS entradas de input que
// usa el MenuManager, y verifica que los cambios lleguen al modelo y al save.
// Se ejecuta desde PokeRush/UI/Probar sistema de menus.
public class MenuSystemSmokeTest : MonoBehaviour
{
    [Tooltip("Frenar el Play Mode al terminar.")]
    public bool salirAlTerminar = true;

    private int _ok;
    private int _fallos;

    private MenuManager Menus => MenuManager.Instance;

    private void Awake()
    {
        // Sin esto, con la ventana de Unity en segundo plano el player loop se
        // congela y la prueba queda a medias sin decir nada. Es una prueba: que
        // corra aunque nadie este mirando la ventana es justamente el punto.
        Application.runInBackground = true;
    }

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        // Un frame para que SceneBootstrapper cree/cargue la partida.
        yield return null;
        yield return null;

        PlayerModel player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (player == null)
        {
            Debug.LogError("[SmokeMenus] No hay partida cargada. Abortado.");
            yield break;
        }

        PrepararDatos(player);

        Check("MenuManager se auto-instancia desde Resources", Menus != null);
        if (Menus == null) yield break;

        Menus.SetContext(MenuContextKind.Overworld);

        yield return ProbarAbrirYCerrarConLaTecla();
        yield return ProbarPausa();
        yield return ProbarParty(player);
        yield return ProbarSummary(player);
        yield return ProbarBolsa(player);
        yield return ProbarOpciones();
        yield return ProbarCombate(player);
        yield return ProbarHudDeCombate(player);
        yield return ProbarCambioEnCombate(player);
        yield return ProbarGuardado(player);
        yield return ProbarCierre();

        Debug.Log($"[SmokeMenus] TERMINADO. OK={_ok} FALLOS={_fallos}");

#if UNITY_EDITOR
        if (salirAlTerminar)
        {
            yield return null;
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif
    }

    // ---------- preparacion ----------

    private void PrepararDatos(PlayerModel player)
    {
        // Equipo de al menos 3 para poder reordenar.
        if (player.Team.Count < 3)
        {
            var especies = Resources.LoadAll<PokenDefinition>("Definitions/Pokens");
            for (int i = 0; player.Team.Count < 3 && i < especies.Length; i++)
                player.Team.Add(new PokenModel(especies[i], 12 + i));
        }

        // Un objeto de curacion con stock y alguien a quien curar.
        var objetos = Resources.LoadAll<HealItemDefinition>("Definitions/Items");
        if (objetos.Length > 0) player.Inventory.AddHealItem(objetos[0], 5);

        foreach (PokenModel p in player.Team) p.SetCurrentHP(p.MaxHP);
        if (player.Team.Count > 1) player.Team[1].TakeDamage(player.Team[1].MaxHP / 2);
    }

    // ---------- pruebas ----------

    // REGRESION: el menu de pausa se abria una sola vez.
    //
    // La Z es Menu (abrir) y Cancel (volver) a la vez. El MenuManager cerraba
    // el menu con esa Z y, en el mismo frame, el PlayerInputController veia la
    // misma tecla sin ningun menu abierto y lo reabria. Se prueba con teclas de
    // verdad (no llamando a HandleAction) porque el bug estaba justamente en
    // como se reparte una tecla entre dos Update.
    private IEnumerator ProbarAbrirYCerrarConLaTecla()
    {
        if (Keyboard.current == null)
        {
            Debug.LogWarning("[SmokeMenus] No hay teclado: se saltea la prueba de la tecla.");
            yield break;
        }

        yield return ApretarYSoltar(Key.Z);
        Check("La tecla de menu abre la pausa", MenuManager.AnyOpen);

        yield return ApretarYSoltar(Key.Z);
        Check("La misma tecla la cierra", !MenuManager.AnyOpen);

        yield return ApretarYSoltar(Key.Z);
        Check("Y la vuelve a abrir (no se abre una sola vez)", MenuManager.AnyOpen);

        yield return ApretarYSoltar(Key.Z);
        Check("Y la vuelve a cerrar", !MenuManager.AnyOpen);
    }

    private IEnumerator ApretarYSoltar(Key tecla)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(tecla));
        yield return null;
        yield return null;

        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return null;
        yield return null;
    }

    private IEnumerator ProbarPausa()
    {
        MenuScreen pausa = Menus.Open(MenuScreenId.Pause);
        yield return null;

        Check("Se abre el menu de pausa", pausa != null && pausa.IsOpen);
        Check("MenuManager.AnyOpen es true con el menu abierto", MenuManager.AnyOpen);
        Check("La pausa queda arriba de la pila", Menus.Top == pausa);

        // Navegar no debe romper nada ni cambiar de pantalla.
        pausa.HandleAction(GameAction.Down);
        pausa.HandleAction(GameAction.Down);
        pausa.HandleAction(GameAction.Up);
        yield return null;
        Check("Navegar no cierra la pausa", Menus.Top == pausa);

        int seleccionados = 0;
        foreach (MenuOptionWidget w in pausa.GetComponentsInChildren<MenuOptionWidget>(true))
            if (w.IsSelected) seleccionados++;
        Check("Hay exactamente una opcion resaltada", seleccionados == 1);
    }

    private IEnumerator ProbarParty(PlayerModel player)
    {
        var request = new PartyScreenRequest { Mode = PartyScreenMode.Browse };
        MenuScreen party = Menus.Open(MenuScreenId.Party, request);
        yield return null;

        Check("Se abre la party sobre la pausa", party != null && Menus.Top == party);

        var paneles = new List<PartyPanelWidget>();
        foreach (PartyPanelWidget w in party.GetComponentsInChildren<PartyPanelWidget>(true))
            if (w.gameObject.activeSelf) paneles.Add(w);
        Check($"Se dibuja un panel por Poken ({paneles.Count} vs {player.Team.Count})",
            paneles.Count == player.Team.Count);

        // Reordenar: agarrar el slot 0 y cambiarlo con el 1.
        string antes0 = player.Team[0].Definition.pokenName;
        string antes1 = player.Team[1].Definition.pokenName;

        party.HandleAction(GameAction.Confirm);            // abre el submenu
        party.HandleAction(GameAction.Down);               // CAMBIAR
        party.HandleAction(GameAction.Confirm);            // agarra el slot 0
        party.HandleAction(GameAction.Right);              // se mueve al slot 1
        party.HandleAction(GameAction.Confirm);            // confirma el intercambio
        yield return null;

        Check($"El orden del equipo cambio de verdad ({antes0}/{antes1} -> " +
              $"{player.Team[0].Definition.pokenName}/{player.Team[1].Definition.pokenName})",
            player.Team[0].Definition.pokenName == antes1 &&
            player.Team[1].Definition.pokenName == antes0);

        // El cursor quedo en el slot 1 despues del intercambio. Se vuelve al 0
        // para que el resumen (y la prueba de persistencia) miren al mismo Poken.
        party.HandleAction(GameAction.Left);
        yield return null;
    }

    private IEnumerator ProbarSummary(PlayerModel player)
    {
        MenuScreen party = Menus.Top;
        party.HandleAction(GameAction.Confirm);   // submenu
        party.HandleAction(GameAction.Confirm);   // DATOS -> abre el resumen
        yield return null;

        MenuScreen summary = Menus.Top;
        Check("Se abre el resumen desde la party", summary is SummaryScreen);
        if (!(summary is SummaryScreen)) yield break;

        // Ir a la pagina de movimientos (la 4a) y reordenar.
        summary.HandleAction(GameAction.Right);
        summary.HandleAction(GameAction.Right);
        summary.HandleAction(GameAction.Right);
        yield return null;

        // El resumen se abre sobre el Poken donde quedo el cursor de la party,
        // que no tiene por que ser el slot 0: se le pregunta a la pantalla.
        PokenModel poken = ((SummaryScreen)summary).CurrentPoken;
        Check("El resumen sabe que Poken esta mostrando", poken != null);
        if (poken == null) yield break;

        if (poken.Moves.Count >= 2)
        {
            string m0 = poken.Moves[0].Definition.moveName;
            string m1 = poken.Moves[1].Definition.moveName;

            summary.HandleAction(GameAction.Confirm);  // entra al modo reordenar
            summary.HandleAction(GameAction.Confirm);  // agarra el movimiento 0
            summary.HandleAction(GameAction.Down);     // baja al 1
            summary.HandleAction(GameAction.Confirm);  // intercambia
            yield return null;

            Check($"El orden de movimientos cambio de verdad ({m0}/{m1} -> " +
                  $"{poken.Moves[0].Definition.moveName}/{poken.Moves[1].Definition.moveName})",
                poken.Moves[0].Definition.moveName == m1 && poken.Moves[1].Definition.moveName == m0);
        }
        else
        {
            Debug.LogWarning("[SmokeMenus] El primer Poken tiene menos de 2 movimientos: " +
                             "no se puede probar el reordenamiento.");
        }

        // Volver: modo reordenar -> resumen -> party -> pausa
        summary.HandleAction(GameAction.Cancel);
        summary.HandleAction(GameAction.Cancel);
        yield return null;
        Check("Cerrar el resumen devuelve a la party", Menus.Top is PartyScreen);

        Menus.Top.HandleAction(GameAction.Cancel);
        yield return null;
        Check("Cerrar la party devuelve a la pausa", Menus.Top is PauseMenuScreen);
    }

    private IEnumerator ProbarBolsa(PlayerModel player)
    {
        MenuScreen bolsa = Menus.Open(MenuScreenId.Bag,
            new BagScreenRequest { Context = MenuContextKind.Overworld, Pocket = ItemPocket.Medicine });
        yield return null;

        Check("Se abre la mochila", bolsa is BagScreen);

        List<BagEntry> entradas = BagModel.Build(player.Inventory, ItemPocket.Medicine);
        Check("La mochila lee el inventario real", entradas.Count > 0);
        if (entradas.Count == 0) yield break;

        HealItemDefinition item = entradas[0].Heal;
        int stockAntes = player.Inventory.CountHealItem(item);

        // Buscar un Poken herido y dejarlo en una posicion conocida.
        int heridoIndex = player.Team.FindIndex(p => p.CurrentHP < p.MaxHP);
        if (heridoIndex < 0)
        {
            player.Team[0].TakeDamage(player.Team[0].MaxHP / 2);
            heridoIndex = 0;
        }
        int psAntes = player.Team[heridoIndex].CurrentHP;

        bolsa.HandleAction(GameAction.Confirm);   // submenu del objeto
        bolsa.HandleAction(GameAction.Confirm);   // USAR -> abre la party
        yield return null;

        Check("Usar un objeto de curacion abre la seleccion de Poken", Menus.Top is PartyScreen);
        if (!(Menus.Top is PartyScreen)) yield break;

        MenuScreen objetivo = Menus.Top;
        // El cursor arranca sobre el primer Poken elegible, que no tiene por que
        // ser el slot 0: se navega hasta el que queremos en vez de asumirlo.
        yield return LlevarCursorA(objetivo as PartyScreen, heridoIndex);
        objetivo.HandleAction(GameAction.Confirm);
        yield return null;

        int psDespues = player.Team[heridoIndex].CurrentHP;
        int stockDespues = player.Inventory.CountHealItem(item);

        Check($"El objeto curo de verdad ({psAntes} -> {psDespues})", psDespues > psAntes);
        Check($"El objeto se descuento del inventario ({stockAntes} -> {stockDespues})",
            stockDespues == stockAntes - 1);

        // Cerrar el cartel de resultado, la party y la bolsa.
        for (int i = 0; i < 4 && Menus.Top != null && !(Menus.Top is PauseMenuScreen); i++)
        {
            Menus.Top.HandleAction(GameAction.Confirm);
            yield return null;
            if (Menus.Top is PauseMenuScreen) break;
            Menus.Top.HandleAction(GameAction.Cancel);
            yield return null;
        }
        Check("Se vuelve a la pausa despues de usar el objeto", Menus.Top is PauseMenuScreen);
    }

    private IEnumerator ProbarOpciones()
    {
        MenuScreen opciones = Menus.Open(MenuScreenId.Options);
        yield return null;
        Check("Se abre el menu de opciones", opciones is OptionsScreen);

        // Al abrir, el cursor ya esta en la primera fila seleccionable (Musica):
        // los titulos de seccion se saltean solos.
        float volumenAntes = GameSettings.MusicVolume;
        opciones.HandleAction(GameAction.Left);
        opciones.HandleAction(GameAction.Left);
        yield return null;

        Check($"El volumen de musica cambio ({volumenAntes:0.00} -> {GameSettings.MusicVolume:0.00})",
            !Mathf.Approximately(volumenAntes, GameSettings.MusicVolume));

        // El remapeo se prueba por la API: simular la tecla fisica no es posible
        // desde aca, pero el guardado y la lectura si.
        GameInputService.Instance.SetPrimaryBinding(GameAction.Confirm, "<Keyboard>/k");
        Check("El binding nuevo queda activo",
            GameInputService.Instance.GetBindings(GameAction.Confirm)[0] == "<Keyboard>/k");
        Check("El binding nuevo se guardo",
            SettingsService.Instance.GetString("input.Confirm", "").StartsWith("<Keyboard>/k"));

        GameInputService.Instance.SetPrimaryBinding(GameAction.Confirm, "<Keyboard>/x");

        opciones.HandleAction(GameAction.Cancel);
        yield return null;
        Check("Cerrar opciones devuelve a la pausa", Menus.Top is PauseMenuScreen);
    }

    // El caso que pide el enunciado: en combate la mochila NO aplica el objeto,
    // solo dice cual y sobre quien. Lo aplica el TurnEngine como accion del turno.
    private IEnumerator ProbarCombate(PlayerModel player)
    {
        Menus.SetContext(MenuContextKind.Battle);

        // Un Poken del BANQUILLO herido: si el objeto se aplicara al activo
        // (como antes), esta prueba lo detecta.
        int banquillo = player.Team.Count - 1;
        player.Team[banquillo].SetCurrentHP(Mathf.Max(1, player.Team[banquillo].MaxHP / 4));
        int psAntes = player.Team[banquillo].CurrentHP;

        BagScreenResult resultado = null;
        Menus.Open(MenuScreenId.Bag,
            new BagScreenRequest { Context = MenuContextKind.Battle, Pocket = ItemPocket.Medicine },
            raw => resultado = raw as BagScreenResult);
        yield return null;

        MenuScreen bolsa = Menus.Top;
        Check("La mochila se abre en contexto de combate",
            bolsa is BagScreen && bolsa.Context == MenuContextKind.Battle);

        HealItemDefinition item = BagModel.Build(player.Inventory, ItemPocket.Medicine)[0].Heal;
        int stockAntes = player.Inventory.CountHealItem(item);

        bolsa.HandleAction(GameAction.Confirm);   // submenu
        bolsa.HandleAction(GameAction.Confirm);   // USAR -> party
        yield return null;

        MenuScreen objetivo = Menus.Top;
        Check("En combate tambien se elige objetivo", objetivo is PartyScreen);
        if (!(objetivo is PartyScreen)) { Menus.CloseAll(); yield break; }

        yield return LlevarCursorA(objetivo as PartyScreen, banquillo);
        objetivo.HandleAction(GameAction.Confirm);
        yield return null;

        Check("La mochila devuelve el objeto elegido",
            resultado != null && !resultado.Cancelled && resultado.UsedHealItem == item);
        Check($"La mochila devuelve el objetivo elegido ({banquillo})",
            resultado != null && resultado.TargetIndex == banquillo);
        Check("El uso consume el turno de combate",
            resultado != null && resultado.ConsumesBattleTurn);

        // Lo importante: todavia NO paso nada. Si la mochila hubiera aplicado
        // el objeto aca, el TurnEngine lo aplicaria de nuevo y curaria doble.
        Check("En combate la mochila NO cura por su cuenta",
            player.Team[banquillo].CurrentHP == psAntes);
        Check("En combate la mochila NO descuenta el objeto",
            player.Inventory.CountHealItem(item) == stockAntes);

        // Ahora si: el turno resuelve la accion, como haria BattleController.
        var especies = Resources.LoadAll<PokenDefinition>("Definitions/Pokens");
        var enemigo = new EnemyModel(especies[0], 10);
        var modelo = new BattleModel(player, enemigo);
        modelo.SwitchPlayerPoken(0);
        modelo.SwitchEnemyPoken(0);

        var motor = new TurnEngine(modelo, new UnityRng());
        motor.ResolveTurn(BattleAction.UseItemOn(item, banquillo), BattleAction.Protect());
        yield return null;

        Check($"El TurnEngine cura al Poken elegido, no al activo " +
              $"({psAntes} -> {player.Team[banquillo].CurrentHP})",
            player.Team[banquillo].CurrentHP > psAntes);
        Check("El TurnEngine descuenta el objeto",
            player.Inventory.CountHealItem(item) == stockAntes - 1);

        Menus.CloseAll();
        Menus.SetContext(MenuContextKind.Overworld);
        yield return null;
        Check("Al salir del combate no queda ningun menu abierto", !MenuManager.AnyOpen);
    }

    // El HUD del combate se navega con las flechas, no solo con el mouse.
    //
    // Se prueba con teclas de verdad y mirando a quien selecciona el
    // EventSystem, que es el "cursor" del HUD.
    private IEnumerator ProbarHudDeCombate(PlayerModel player)
    {
        if (Keyboard.current == null || EventSystem.current == null)
        {
            Debug.LogWarning("[SmokeMenus] Sin teclado o sin EventSystem: se saltea el HUD.");
            yield break;
        }

        // El sistema de combate arranca APAGADO, asi que GameObject.Find no lo
        // ve: hay que buscar incluyendo los inactivos.
        BattleHudView hud = FindAnyObjectByType<BattleHudView>(FindObjectsInactive.Include);
        if (hud == null)
        {
            Debug.LogWarning("[SmokeMenus] No hay BattleHudView en la escena: se saltea el HUD.");
            yield break;
        }

        GameObject raiz = hud.transform.root.gameObject;
        bool estabaPrendido = raiz.activeSelf;
        raiz.SetActive(true);

        var especies = Resources.LoadAll<PokenDefinition>("Definitions/Pokens");
        var modelo = new BattleModel(player, new EnemyModel(especies[0], 10));
        modelo.SwitchPlayerPoken(0);
        modelo.SwitchEnemyPoken(0);

        hud.Show();
        hud.ShowCommandMenu(modelo);
        yield return null;

        GameObject primero = EventSystem.current.currentSelectedGameObject;
        Check("El menu de comandos arranca con una opcion marcada", primero != null);
        Check("El EventSystem deja de navegar solo (si no, cada flecha salta dos)",
            !EventSystem.current.sendNavigationEvents);

        yield return ApretarYSoltar(Key.DownArrow);
        GameObject segundo = EventSystem.current.currentSelectedGameObject;
        Check($"Las flechas mueven la seleccion ({Nombre(primero)} -> {Nombre(segundo)})",
            segundo != null && segundo != primero);

        // Volver al primero y entrar a LUCHAR.
        yield return ApretarYSoltar(Key.UpArrow);
        Check("Y vuelven a la anterior",
            EventSystem.current.currentSelectedGameObject == primero);

        yield return ApretarYSoltar(Key.X);
        GameObject enLucha = EventSystem.current.currentSelectedGameObject;
        Check($"Aceptar entra al panel de movimientos ({Nombre(enLucha)})",
            enLucha != null && enLucha != primero);

        yield return ApretarYSoltar(Key.Z);
        Check("Cancelar vuelve al menu de comandos",
            EventSystem.current.currentSelectedGameObject == primero);

        hud.HideMenus();
        yield return null;
        Check("Al cerrar los menus no queda nada seleccionado",
            EventSystem.current.currentSelectedGameObject == null);
        Check("Y el EventSystem vuelve a navegar por su cuenta",
            EventSystem.current.sendNavigationEvents);

        hud.Hide();
        raiz.SetActive(estabaPrendido);
        yield return null;
    }

    private static string Nombre(GameObject go) => go != null ? go.name : "nada";

    // Mueve el cursor de la party hasta el slot pedido. Da vueltas como el
    // jugador; si el slot no es elegible, se corta y el Check lo delata.
    private IEnumerator LlevarCursorA(PartyScreen party, int slot)
    {
        if (party == null) yield break;

        for (int i = 0; i < 12 && party.CursorIndex != slot; i++)
        {
            party.HandleAction(GameAction.Right);
            yield return null;
        }
    }

    // Elegir relevo en combate usa la MISMA pantalla de equipo que el mundo,
    // con la opcion "sacar a batalla". Y cuando el Poken activo se debilita, esa
    // pantalla no se puede cancelar: el combate esta esperando la eleccion.
    private IEnumerator ProbarCambioEnCombate(PlayerModel player)
    {
        Menus.SetContext(MenuContextKind.Battle);

        foreach (PokenModel p in player.Team) p.SetCurrentHP(p.MaxHP);

        PartyScreenResult resultado = null;
        Menus.Open(MenuScreenId.Party, new PartyScreenRequest
        {
            Mode = PartyScreenMode.SelectSwitch,
            Context = MenuContextKind.Battle,
            CanCancel = false,
            // El slot 0 es el que esta en el campo.
            BlockedIndices = new HashSet<int> { 0 },
            Prompt = "¿A quién mandás ahora?"
        }, raw => resultado = raw as PartyScreenResult);
        yield return null;

        MenuScreen party = Menus.Top;
        Check("El relevo abre la pantalla de equipo nueva", party is PartyScreen);
        if (!(party is PartyScreen)) { Menus.CloseAll(); yield break; }

        // No se puede cancelar: avisa y se queda.
        party.HandleAction(GameAction.Cancel);
        yield return null;
        if (Menus.Top is MessageBoxScreen)
        {
            Menus.Top.HandleAction(GameAction.Confirm);
            yield return null;
        }
        Check("El reemplazo obligado no se puede cancelar",
            Menus.Top == party && resultado == null);

        // Submenu del Poken: la primera opcion tiene que ser sacarlo a pelear.
        party.HandleAction(GameAction.Confirm);
        yield return null;

        string primeraAccion = null;
        foreach (MenuOptionWidget w in party.GetComponentsInChildren<MenuOptionWidget>(false))
        {
            if (w.OptionId == "cancel") continue;   // el boton SALIR de la pantalla
            primeraAccion = w.OptionId;
            break;
        }
        Check($"El submenu ofrece sacar a batalla (primera opcion: {primeraAccion})",
            primeraAccion == "sendout");

        party.HandleAction(GameAction.Confirm);
        yield return null;

        bool valido = resultado != null && !resultado.Cancelled
                      && resultado.SelectedIndex > 0
                      && resultado.SelectedIndex < player.Team.Count
                      && !player.Team[resultado.SelectedIndex].IsFainted;
        Check($"Sacar a batalla devuelve un Poken valido y NO el que ya peleaba " +
              $"(slot {(resultado != null ? resultado.SelectedIndex : -1)})", valido);

        Menus.CloseAll();
        Menus.SetContext(MenuContextKind.Overworld);
        yield return null;
    }

    private IEnumerator ProbarGuardado(PlayerModel player)
    {
        // Estado esperado despues de recargar.
        string primerPoken = player.Team[0].Definition.pokenName;
        string primerMovimiento = player.Team[0].Moves.Count > 0
            ? player.Team[0].Moves[0].Definition.moveName : null;
        int objetosEnMochila = player.Inventory.HealItems.Count > 0
            ? player.Inventory.HealItems[0].Quantity : 0;
        string nombreObjeto = player.Inventory.HealItems.Count > 0
            ? player.Inventory.HealItems[0].Definition.itemName : null;
        // Un valor puesto a proposito: si nunca se toco el volumen no hay fila
        // en la tabla y no se estaria probando nada.
        GameSettings.MusicVolume = 0.35f;
        float volumenGuardado = GameSettings.MusicVolume;

        GameManager.Instance.SaveGame();
        yield return null;

        Check("Hay datos guardados", SaveService.Instance.HasSaveData());

        // Recarga real desde la base, sin tocar la partida en curso.
        var historia = new StoryModel();
        PlayerModel recargado = SaveService.Instance.LoadPlayer(historia);
        Check("La partida se puede volver a cargar", recargado != null);
        if (recargado == null) yield break;

        Check($"Sobrevive el orden del equipo ({primerPoken})",
            recargado.Team.Count > 0 && recargado.Team[0].Definition.pokenName == primerPoken);

        if (primerMovimiento != null && recargado.Team[0].Moves.Count > 0)
            Check($"Sobrevive el orden de los movimientos ({primerMovimiento})",
                recargado.Team[0].Moves[0].Definition.moveName == primerMovimiento);

        if (nombreObjeto != null)
        {
            int cantidad = 0;
            foreach (HealItemStack s in recargado.Inventory.HealItems)
                if (s.Definition.itemName == nombreObjeto) cantidad = s.Quantity;
            Check($"Sobrevive la cantidad de objetos ({objetosEnMochila})",
                cantidad == objetosEnMochila);
        }

        // Las opciones viven en la misma base pero en su propia tabla: no se
        // borran con la partida y se leen igual despues de recargar.
        float volumenLeido = SettingsService.Instance.GetFloat("audio.music", -1f);
        Check($"Sobrevive el volumen configurado ({volumenGuardado:0.00})",
            Mathf.Approximately(volumenLeido, volumenGuardado));
    }

    private IEnumerator ProbarCierre()
    {
        Menus.CloseAll();
        yield return null;

        Check("CloseAll deja la pila vacia", !MenuManager.AnyOpen && Menus.Top == null);

        int activos = 0;
        foreach (MenuScreen s in Menus.GetComponentsInChildren<MenuScreen>(true))
            if (s.gameObject.activeSelf) activos++;
        Check($"No queda ninguna pantalla activa ({activos})", activos == 0);
    }

    // ---------- utilidades ----------

    private void Check(string que, bool ok)
    {
        if (ok) { _ok++; Debug.Log($"[SmokeMenus] OK    {que}"); }
        else { _fallos++; Debug.LogError($"[SmokeMenus] FALLO {que}"); }
    }
}
