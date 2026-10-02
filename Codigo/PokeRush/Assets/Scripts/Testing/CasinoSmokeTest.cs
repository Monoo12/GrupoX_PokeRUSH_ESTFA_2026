using System.Collections;
using System.Reflection;
using UnityEngine;

// BANCO DE PRUEBAS del casino. No es parte del juego.
//
// Recorre el flujo del piso 1 tal como lo vive el jugador: interactuar con la
// maquina, apostar, girar, cobrar y cerrar. Verifica sobre los modelos reales
// (PlayerModel, MenuManager) que el dinero se mueva bien y que el control vuelva
// al mundo al cerrar la UI. Se ejecuta desde PokeRush/Casino/Probar el casino.
//
// Existe porque los smoke tests de editor prueban el motor, no la pantalla: el
// bug que importa aca es cobrar la apuesta y no pagar el premio, o quedarse con
// un menu abierto y el jugador sin control.
public class CasinoSmokeTest : MonoBehaviour
{
    [Tooltip("Frenar el Play Mode al terminar.")]
    public bool salirAlTerminar = true;

    [Tooltip("Ruta del asset de la maquina a probar.")]
    public string maquinaResource = "Definitions/Casino/MaquinaBasica";

    private int _ok;
    private int _fallos;

    private MenuManager Menus => MenuManager.Instance;

    private void Awake()
    {
        // Con la ventana en segundo plano el player loop se congela y la prueba
        // queda a medias sin decir nada.
        Application.runInBackground = true;
    }

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        yield return null;
        yield return null;

        PlayerModel jugador = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (jugador == null)
        {
            Debug.LogError("[SmokeCasino] No hay partida cargada. Abri una escena de juego y volve a correrlo.");
            yield break;
        }

        var maquina = Resources.Load<SlotMachineDefinition>(maquinaResource);
        Check($"Se encuentra la maquina en Resources/{maquinaResource}", maquina != null);
        if (maquina == null) yield break;

        string error;
        Check("La maquina esta bien configurada", maquina.Validar(out error));

        // Plata suficiente para varias tiradas, sin depender de como venga la
        // partida cargada.
        if (jugador.Money < 500) jugador.AddMoney(500 - jugador.Money);

        // Las peleas simuladas no pueden guardar la partida DEMO encima del save.
        bool guardadoAntes = CasinoDialogo.GuardadoAutomatico;
        CasinoDialogo.GuardadoAutomatico = false;

        yield return ProbarAbrirDesdeLaMaquina(jugador, maquina);
        yield return ProbarApostarYGirar(jugador);
        yield return ProbarCierreYControl();
        yield return ProbarRuleta(jugador);
        yield return ProbarEnfermeraYDeuda(jugador);
        yield return ProbarGuardia(jugador);
        yield return ProbarNpcConRuleta(jugador);
        yield return ProbarAscensor(jugador);

        CasinoDialogo.GuardadoAutomatico = guardadoAntes;
        Debug.Log($"[SmokeCasino] TERMINADO. OK={_ok} FALLOS={_fallos}");

#if UNITY_EDITOR
        if (salirAlTerminar)
        {
            yield return null;
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif
    }

    // ---------- 1. la maquina del mundo abre la UI ----------

    private SlotsScreen _pantalla;
    private SlotMachineInteractable _maquinaEnEscena;
    private bool _creadaPorLaPrueba;

    private IEnumerator ProbarAbrirDesdeLaMaquina(PlayerModel jugador, SlotMachineDefinition maquina)
    {
        // Si la escena abierta es el piso del casino, se prueba con UNA DE SUS
        // maquinas de verdad: es la unica forma de saber que las que colgo el
        // builder sobre el arte del equipo responden.
        _maquinaEnEscena = Object.FindAnyObjectByType<SlotMachineInteractable>();
        _creadaPorLaPrueba = _maquinaEnEscena == null;

        if (_creadaPorLaPrueba)
        {
            var go = new GameObject("[SmokeCasino] Tragaperras");
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            _maquinaEnEscena = go.AddComponent<SlotMachineInteractable>();
            SetCampo(_maquinaEnEscena, "maquina", maquina);
        }

        Check(_creadaPorLaPrueba
                  ? "No hay maquinas en la escena: se prueba con una armada al vuelo"
                  : $"La escena trae maquinas ya colgadas ({Object.FindObjectsByType<SlotMachineInteractable>().Length})",
              true);

        StoryModel historia = GameManager.Instance.Story;
        bool consumio = _maquinaEnEscena.Interact(jugador, historia);
        yield return null;

        Check("Interactuar con la maquina consume la interaccion", consumio);
        Check("No se cambio de escena para jugar",
              gameObject.scene == _maquinaEnEscena.gameObject.scene && Menus != null);

        _pantalla = Menus != null ? Menus.Top as SlotsScreen : null;
        Check("Se abre la pantalla de slots encima del nivel", _pantalla != null);
        Check("Con la pantalla abierta el jugador no tiene el control", MenuManager.AnyOpen);
    }

    // ---------- 2. apostar, girar y cobrar ----------

    private IEnumerator ProbarApostarYGirar(PlayerModel jugador)
    {
        if (_pantalla == null) yield break;

        int apuestaInicial = Campo<int>(_pantalla, "_apuesta");
        _pantalla.HandleAction(GameAction.Right);
        int apuestaSubida = Campo<int>(_pantalla, "_apuesta");
        Check($"La apuesta sube con la flecha ({apuestaInicial} -> {apuestaSubida})",
              apuestaSubida > apuestaInicial);

        _pantalla.HandleAction(GameAction.Left);
        Check("La apuesta baja con la otra flecha",
              Campo<int>(_pantalla, "_apuesta") == apuestaInicial);

        int saldoAntes = jugador.Money;
        int apuesta = Campo<int>(_pantalla, "_apuesta");

        _pantalla.HandleAction(GameAction.Confirm);
        yield return null;

        Check($"La apuesta se cobra al girar ({saldoAntes} -> {jugador.Money})",
              jugador.Money == saldoAntes - apuesta);

        // Mientras giran los rodillos la pantalla no acepta nada: ni cerrar.
        _pantalla.HandleAction(GameAction.Cancel);
        yield return null;
        Check("No se puede salir con los rodillos girando", Menus.Top == _pantalla);

        // Se espera a que frene sola (la anima el MenuManager con OnTick).
        float limite = Time.unscaledTime + 6f;
        while (Campo<object>(_pantalla, "_fase").ToString() != "Apostando" && Time.unscaledTime < limite)
            yield return null;

        var tirada = Campo<SlotSpinResult>(_pantalla, "_tirada");
        Check("La tirada termina sola", tirada != null &&
              Campo<object>(_pantalla, "_fase").ToString() == "Apostando");
        if (tirada == null) yield break;

        Check($"El premio se paga entero ({tirada.Pago}) y el saldo cierra " +
              $"({saldoAntes} -> {jugador.Money})",
              jugador.Money == saldoAntes - apuesta + tirada.Pago);

        // Y una segunda tirada, para descartar que la maquina quede trabada
        // despues de resolver una.
        int saldoAntes2 = jugador.Money;
        _pantalla.HandleAction(GameAction.Confirm);
        yield return null;
        Check("Se puede volver a girar despues de un resultado",
              jugador.Money == saldoAntes2 - Campo<int>(_pantalla, "_apuesta"));

        limite = Time.unscaledTime + 6f;
        while (Campo<object>(_pantalla, "_fase").ToString() != "Apostando" && Time.unscaledTime < limite)
            yield return null;
    }

    // ---------- 3. cerrar y volver al nivel ----------

    private IEnumerator ProbarCierreYControl()
    {
        if (_pantalla == null) yield break;

        _pantalla.HandleAction(GameAction.Cancel);
        yield return null;

        Check("La pantalla de slots se cierra", !_pantalla.IsOpen);

        // La maquina saluda con un resumen: es otra pantalla de la misma pila.
        if (Menus.Top is MessageBoxScreen resumen)
        {
            resumen.HandleAction(GameAction.Confirm);
            yield return null;
        }

        Check("Al cerrar todo, el jugador recupera el control",
              !MenuManager.AnyOpen && Menus.Top == null);

        if (_creadaPorLaPrueba && _maquinaEnEscena != null)
            Destroy(_maquinaEnEscena.gameObject);
    }


    // ---------- la ruleta de premios del piso 2 ----------

    private IEnumerator ProbarRuleta(PlayerModel jugador)
    {
        var mesa = Object.FindAnyObjectByType<PrizeWheelInteractable>();
        if (mesa == null)
        {
            Debug.Log("[SmokeCasino] Este piso no tiene ruleta: se saltea esa parte.");
            yield break;
        }

        StoryModel historia = GameManager.Instance.Story;
        Menus.CloseAll();
        yield return null;

        mesa.Interact(jugador, historia);
        yield return null;

        var pantalla = Menus.Top as PrizeWheelController;
        Check("La mesa abre la ruleta encima del piso", pantalla != null);
        if (pantalla == null) yield break;

        Check("La ruleta no oscurece el mapa", !pantalla.DimsBackground);

        int objetosAntes = jugador.Inventory.HealItems.Count;

        // --- un giro ---
        pantalla.OnSpinButton();
        yield return null;

        // Con la rueda girando no se puede salir ni encadenar otro giro.
        pantalla.HandleAction(GameAction.Cancel);
        yield return null;
        Check("No se puede salir con la rueda girando", Menus.Top == pantalla);

        // Frena sola y el premio sale por la caja de texto de siempre, no por
        // un panel propio.
        float limite = Time.unscaledTime + 12f;
        while (!(Menus.Top is MessageBoxScreen) && Time.unscaledTime < limite)
            yield return null;

        Check("La rueda frena sola y canta el premio", Menus.Top is MessageBoxScreen);
        Check("El premio sale por la caja de texto del juego, sin panel aparte",
              Menus.Top is MessageBoxScreen);

        if (Menus.Top is MessageBoxScreen cartel)
        {
            cartel.HandleAction(GameAction.Confirm);
            yield return null;
        }

        Check($"Algo entro a la mochila o el premio era solo de cartel " +
              $"({objetosAntes} -> {jugador.Inventory.HealItems.Count})",
              jugador.Inventory.HealItems.Count >= objetosAntes);

        // --- cerrar y recuperar el control ---
        pantalla.OnBackButton();
        yield return null;
        Check("La ruleta se cierra", !pantalla.IsOpen);

        // El cartel del objetivo del piso puede quedar arriba: se descarta.
        if (Menus.Top is MessageBoxScreen objetivo)
        {
            objetivo.HandleAction(GameAction.Confirm);
            yield return null;
        }

        Check("El objetivo del piso queda anotado en la historia",
              historia.HasFlag(StoryFlag.Casino_Ruleta_ObjetivoCumplido));
        Check("Despues de la ruleta el jugador recupera el control", !MenuManager.AnyOpen);
    }

    // ---------- 4. la enfermera y la deuda ----------

    private IEnumerator ProbarEnfermeraYDeuda(PlayerModel jugador)
    {
        var enfermera = Object.FindAnyObjectByType<CasinoNurseInteractable>();
        if (enfermera == null)
        {
            Debug.Log("[SmokeCasino] La escena no tiene enfermera: se saltea esa parte.");
            yield break;
        }
        if (jugador.Team.Count == 0)
        {
            Debug.Log("[SmokeCasino] Sin equipo no se puede probar la curacion.");
            yield break;
        }

        StoryModel historia = GameManager.Instance.Story;
        int costo = Campo<int>(enfermera, "costo");

        // --- cobrando: hay plata, se cura y se paga ---
        PokenModel primero = jugador.Team[0];
        primero.TakeDamage(primero.MaxHP / 2);
        int psHeridos = primero.CurrentHP;

        if (jugador.Money < costo) jugador.AddMoney(costo - jugador.Money);
        int saldoAntes = jugador.Money;

        Menus.CloseAll();
        yield return null;
        enfermera.Interact(jugador, historia);
        yield return null;
        yield return Contestar(true);

        Check($"La enfermera cura pagando ({psHeridos} -> {primero.CurrentHP} PS)",
              primero.CurrentHP == primero.MaxHP);
        Check($"Y cobra lo suyo ({saldoAntes} -> {jugador.Money})",
              jugador.Money == saldoAntes - costo);
        Check("Pagando no queda deuda", !jugador.HasDebt);

        // --- fiando: sin plata, cura igual y anota la deuda ---
        primero.TakeDamage(primero.MaxHP / 2);
        jugador.SpendMoney(jugador.Money);

        enfermera.Interact(jugador, historia);
        yield return null;
        yield return Contestar(true);

        Check("Sin plata la enfermera cura igual", primero.CurrentHP == primero.MaxHP);
        Check($"Y queda la deuda anotada ({jugador.Debt})", jugador.Debt == costo);

        // --- el ascensor no deja subir debiendo ---
        var ascensor = Object.FindAnyObjectByType<CasinoElevatorInteractable>();
        if (ascensor != null)
        {
            jugador.AddMoney(1000);   // plata de sobra: lo unico que frena es la deuda
            historia.ActivateFlag(Campo<StoryFlag>(ascensor, "otorgaFlag"));

            ascensor.Interact(jugador, historia);
            yield return null;

            Check("Con deuda encima el ascensor no sube",
                  Menus.Top is MessageBoxScreen);
            yield return Contestar(true);
        }

        // --- pagar la deuda ---
        enfermera.Interact(jugador, historia);
        yield return null;
        yield return Contestar(true);

        Check("Se le puede pagar la deuda a la enfermera", !jugador.HasDebt);
    }

    // Contesta la caja de mensajes que este arriba de la pila (y la cierra).
    //
    // OJO: la caja arranca con el NO resaltado a proposito (una pregunta
    // destructiva no se contesta que si machacando la tecla). Para decir que si
    // hay que mover la seleccion ANTES de aceptar, que es lo que hace el jugador.
    private IEnumerator Contestar(bool si)
    {
        if (Menus.Top is MessageBoxScreen caja)
        {
            if (si) caja.HandleAction(GameAction.Up);
            caja.HandleAction(si ? GameAction.Confirm : GameAction.Cancel);
            yield return null;
        }
        // Puede quedar un aviso encima del que se acaba de cerrar.
        if (Menus.Top is MessageBoxScreen aviso)
        {
            aviso.HandleAction(GameAction.Confirm);
            yield return null;
        }
    }

    // ---------- 5. el ascensor y el requisito del piso ----------

    private IEnumerator ProbarAscensor(PlayerModel jugador)
    {
        var ascensor = Object.FindAnyObjectByType<CasinoElevatorInteractable>();
        if (ascensor == null)
        {
            Debug.Log("[SmokeCasino] La escena no tiene ascensor: se saltea esa parte.");
            yield break;
        }

        StoryModel historia = GameManager.Instance.Story;
        // La deuda tiene su propia prueba: aca interesa el requisito del piso.
        jugador.PayDebt(jugador.Debt);

        StoryFlag flagDelPiso = Campo<StoryFlag>(ascensor, "otorgaFlag");
        // Cada piso pide lo suyo: el 1 pide plata y los otros piden un objetivo
        // cumplido. La prueba se adapta en vez de asumir el piso 1.
        bool porDinero = Campo<object>(ascensor, "requisito").ToString() == "DineroMinimo";
        int requerido = Campo<int>(ascensor, "dineroRequerido");
        StoryFlag flagPedido = Campo<StoryFlag>(ascensor, "flagRequerido");

        // Arranca sin el flag del piso: es lo que hace que el requisito se
        // vuelva a mirar.
        historia.ActiveFlags.Remove(flagDelPiso);

        // --- sin cumplir el requisito: no sube ---
        int guardado = jugador.Money;
        if (porDinero) jugador.SpendMoney(jugador.Money);      // a cero
        else historia.ActiveFlags.Remove(flagPedido);

        Menus.CloseAll();
        yield return null;

        ascensor.Interact(jugador, historia);
        yield return null;

        Check(porDinero
                  ? "Sin la plata pedida el ascensor avisa en vez de subir"
                  : $"Sin cumplir '{flagPedido}' el ascensor avisa en vez de subir",
              Menus.Top is MessageBoxScreen);
        Check("Y el piso NO queda marcado como superado", !historia.HasFlag(flagDelPiso));

        if (Menus.Top is MessageBoxScreen aviso)
        {
            aviso.HandleAction(GameAction.Confirm);
            yield return null;
        }

        // --- cumpliendolo: pregunta y anota el progreso ---
        if (porDinero) jugador.AddMoney(requerido);
        else historia.ActivateFlag(flagPedido);

        ascensor.Interact(jugador, historia);
        yield return null;

        Check(porDinero
                  ? $"Con {requerido} juntados el ascensor pregunta si subir"
                  : "Con el objetivo cumplido el ascensor pregunta si subir",
              Menus.Top is MessageBoxScreen);
        Check("El piso queda marcado como superado", historia.HasFlag(flagDelPiso));

        // Se contesta que NO: subir de verdad cambiaria de escena y cortaria la
        // prueba. Lo que importa ya quedo verificado.
        if (Menus.Top is MessageBoxScreen pregunta)
        {
            pregunta.HandleAction(GameAction.Cancel);
            yield return null;
        }

        Check("Al decir que no, el jugador sigue en el piso y con el control",
              !MenuManager.AnyOpen);

        jugador.SpendMoney(jugador.Money);
        jugador.AddMoney(guardado);
    }

    // ---------- utilidades ----------

    // ---------- 6. el guardia del ascensor ----------

    // Las peleas no se juegan: se llama al mismo callback que dispara el combate
    // al terminar, con una victoria armada. Lo que se prueba es lo que pasa
    // DESPUES (flags, cartel, ruleta), que es lo nuevo; el combate ya tiene sus
    // propias pruebas.
    private IEnumerator ProbarGuardia(PlayerModel jugador)
    {
        var guardia = Object.FindAnyObjectByType<CasinoElevatorGuard>();
        var dialogo = Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (guardia == null || dialogo == null)
        {
            Debug.Log("[SmokeCasino] Este piso no tiene guardia (o caja de dialogo): se saltea esa parte.");
            yield break;
        }

        StoryModel historia = GameManager.Instance.Story;
        historia.ActiveFlags.Remove(guardia.FlagPase);
        historia.ActiveFlags.Remove(guardia.FlagVencido);
        jugador.PayDebt(jugador.Debt);
        Menus.CloseAll();
        yield return null;

        // --- pagando ---
        if (jugador.Money < guardia.Precio + 100) jugador.AddMoney(guardia.Precio + 100 - jugador.Money);
        int antes = jugador.Money;

        guardia.Interact(jugador, historia);
        yield return null;
        Check("El guardia pregunta si le pagas", dialogo.IsOpen);

        dialogo.OnConfirm();               // "Pagar 500" es la primera opcion
        yield return null;
        Check($"Pagar le cobra {guardia.Precio}", jugador.Money == antes - guardia.Precio);
        Check("Pagar da el pase del ascensor", historia.HasFlag(guardia.FlagPase));
        Check("Y no cuenta como haberle ganado", !historia.HasFlag(guardia.FlagVencido));
        yield return CerrarDialogo(dialogo);

        // --- con el pase ya no cobra ---
        int conPase = jugador.Money;
        guardia.Interact(jugador, historia);
        yield return null;
        Check("Con el pase el guardia solo deja pasar", dialogo.IsOpen && jugador.Money == conPase);
        yield return CerrarDialogo(dialogo);

        // --- sin plata ofrece pelear; decir que no no da nada ---
        historia.ActiveFlags.Remove(guardia.FlagPase);
        jugador.SpendMoney(jugador.Money);
        guardia.Interact(jugador, historia);
        yield return null;
        Check("Sin plata el guardia ofrece pelear", dialogo.IsOpen);
        dialogo.OnNavigate(1);             // "Todavia no"
        dialogo.OnConfirm();
        yield return null;
        Check("Rechazar la pelea no da el pase", !historia.HasFlag(guardia.FlagPase));
        yield return CerrarDialogo(dialogo);

        // --- ganandole: pase, cartel y ruleta de Pokemon ---
        int equipoAntes = jugador.Team.Count;
        int plataAntes = jugador.Money;
        Llamar(guardia, "AlTerminar", new BattleOutcome { PlayerVictory = true }, historia);

        Check("Ganarle marca al guardia como vencido", historia.HasFlag(guardia.FlagVencido));
        Check("Ganarle tambien da el pase", historia.HasFlag(guardia.FlagPase));

        yield return EsperarHasta(() => dialogo.IsOpen, 2f);
        Check("Al volver al mapa el guardia dice algo", dialogo.IsOpen);

        yield return AvanzarHastaQueSeCierre(dialogo);
        yield return EsperarHasta(() => Menus.Top is PrizeWheelController, 2f);
        var ruleta = Menus.Top as PrizeWheelController;
        Check("Despues del cartel se abre su ruleta", ruleta != null);
        if (ruleta == null) yield break;

        yield return ProbarRuletaDePremio(ruleta);

        bool llegoPokemon = jugador.Team.Count == equipoAntes + 1;
        bool compenso = equipoAntes >= PrizeGranter.EquipoMaximo && jugador.Money > plataAntes;
        Check("La ruleta del guardia da un Pokemon (o plata si el equipo estaba lleno)", llegoPokemon || compenso);
    }

    // ---------- 7. un NPC que pelea una vez y da la ruleta ----------

    private IEnumerator ProbarNpcConRuleta(PlayerModel jugador)
    {
        CasinoTrainerNpc npc = null;
        foreach (var n in Object.FindObjectsByType<CasinoTrainerNpc>())
            if (n.Ruleta != null && System.Array.IndexOf(n.Ruleta.npcsQueLaPuedenDar, n.FlagVencido) >= 0) { npc = n; break; }

        var dialogo = Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (npc == null || dialogo == null)
        {
            Debug.Log("[SmokeCasino] Este piso no tiene NPCs que den la ruleta: se saltea esa parte.");
            yield break;
        }

        StoryModel historia = GameManager.Instance.Story;
        Menus.CloseAll();
        yield return null;

        // Se fuerza que le toque: todos los demas de la lista ya vencidos y
        // ninguna ruleta usada -> es el ultimo combate y le deben las cuatro.
        foreach (StoryFlag uso in CasinoRuletaDeNpcs.FlagsDeUso) historia.ActiveFlags.Remove(uso);
        foreach (StoryFlag otro in npc.Ruleta.npcsQueLaPuedenDar)
            if (otro != npc.FlagVencido) historia.ActivateFlag(otro);
        historia.ActiveFlags.Remove(npc.FlagVencido);

        Check($"Con todos los demas vencidos a '{npc.name}' le toca la ruleta seguro",
              CasinoRuletaDeNpcs.Probabilidad(historia, npc.Ruleta.npcsQueLaPuedenDar, npc.FlagVencido) >= 1f);

        Llamar(npc, "AlTerminar", new BattleOutcome { PlayerVictory = true }, historia);
        Check("Ganarle lo marca vencido", historia.HasFlag(npc.FlagVencido));
        Check("Y gasta una de las ruletas de la partida", CasinoRuletaDeNpcs.Usadas(historia) == 1);

        yield return EsperarHasta(() => dialogo.IsOpen, 2f);
        Check("Al volver al mapa el NPC dice algo", dialogo.IsOpen);
        yield return AvanzarHastaQueSeCierre(dialogo);
        yield return EsperarHasta(() => Menus.Top is PrizeWheelController, 2f);
        var ruleta = Menus.Top as PrizeWheelController;
        Check("Despues del cartel se abre la ruleta de premios", ruleta != null);
        if (ruleta != null) yield return ProbarRuletaDePremio(ruleta);

        // --- ya vencido: no vuelve a pelear ---
        npc.Interact(jugador, historia);
        yield return null;
        Check("Ya vencido, el NPC solo habla", dialogo.IsOpen && !MenuManager.AnyOpen);
        yield return CerrarDialogo(dialogo);

        // --- con las cuatro ruletas usadas, ganarle no la da ---
        foreach (StoryFlag uso in CasinoRuletaDeNpcs.FlagsDeUso) historia.ActivateFlag(uso);
        historia.ActiveFlags.Remove(npc.FlagVencido);
        Llamar(npc, "AlTerminar", new BattleOutcome { PlayerVictory = true }, historia);
        yield return EsperarHasta(() => dialogo.IsOpen, 2f);
        yield return AvanzarHastaQueSeCierre(dialogo);
        for (int i = 0; i < 5; i++) yield return null;
        Check("Con las cuatro ruletas usadas, ganarle no abre la ruleta", !(Menus.Top is PrizeWheelController));
    }

    // En modo premio: no se puede salir sin girar, entrega y se cierra sola.
    private IEnumerator ProbarRuletaDePremio(PrizeWheelController ruleta)
    {
        ruleta.HandleAction(GameAction.Cancel);
        yield return null;
        Check("La ruleta de premio no deja salir sin girar", Menus.Top == ruleta);

        ruleta.HandleAction(GameAction.Confirm);
        yield return EsperarHasta(() => Menus.Top is MessageBoxScreen, 10f);
        Check("Girar termina en el cartel del premio", Menus.Top is MessageBoxScreen);

        if (Menus.Top is MessageBoxScreen cartel) cartel.HandleAction(GameAction.Confirm);
        yield return EsperarHasta(() => !MenuManager.AnyOpen, 2f);
        Check("Cerrado el cartel, la ruleta de premio se cierra sola", !MenuManager.AnyOpen);
    }

    private static IEnumerator CerrarDialogo(DialogueController dialogo)
    {
        yield return AvanzarHastaQueSeCierre(dialogo);
    }

    private static IEnumerator AvanzarHastaQueSeCierre(DialogueController dialogo)
    {
        for (int i = 0; i < 10 && dialogo.IsOpen; i++)
        {
            dialogo.OnConfirm();
            yield return null;
        }
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float t = 0f;
        while (!condicion() && t < segundos)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void Llamar(object objetivo, string metodo, params object[] args)
    {
        MethodInfo m = objetivo.GetType().GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Debug.LogError($"[SmokeCasino] No existe {objetivo.GetType().Name}.{metodo}"); return; }
        m.Invoke(objetivo, args);
    }

    private void Check(string que, bool ok)
    {
        if (ok) { _ok++; Debug.Log($"[SmokeCasino] OK    {que}"); }
        else { _fallos++; Debug.LogError($"[SmokeCasino] FALLO {que}"); }
    }

    // La prueba mira el estado interno de la pantalla a proposito: verificar el
    // saldo contra lo que realmente sorteo la maquina es mas fuerte que leer un
    // texto de la UI.
    private static T Campo<T>(object objetivo, string nombre)
    {
        FieldInfo f = objetivo.GetType().GetField(nombre,
            BindingFlags.NonPublic | BindingFlags.Instance);
        return f == null ? default : (T)f.GetValue(objetivo);
    }

    private static void SetCampo(object objetivo, string nombre, object valor)
    {
        FieldInfo f = objetivo.GetType().GetField(nombre,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(objetivo, valor);
    }
}
