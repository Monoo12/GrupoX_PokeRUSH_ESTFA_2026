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

        yield return ProbarAbrirDesdeLaMaquina(jugador, maquina);
        yield return ProbarApostarYGirar(jugador);
        yield return ProbarCierreYControl();
        yield return ProbarEnfermeraYDeuda(jugador);
        yield return ProbarAscensor(jugador);

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
                  : $"La escena trae maquinas ya colgadas ({Object.FindObjectsByType<SlotMachineInteractable>(FindObjectsSortMode.None).Length})",
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
        int requerido = Campo<int>(ascensor, "dineroRequerido");
        // La deuda tiene su propia prueba: aca interesa el requisito de plata.
        jugador.PayDebt(jugador.Debt);
        StoryFlag flagDelPiso = Campo<StoryFlag>(ascensor, "otorgaFlag");

        // Arranca sin el flag: es lo que hace que el requisito se vuelva a mirar.
        historia.ActiveFlags.Remove(flagDelPiso);

        // --- sin la plata pedida: no sube ---
        int guardado = jugador.Money;
        jugador.SpendMoney(jugador.Money);                 // a cero
        Menus.CloseAll();
        yield return null;

        ascensor.Interact(jugador, historia);
        yield return null;

        Check("Sin la plata pedida el ascensor avisa en vez de subir",
              Menus.Top is MessageBoxScreen);
        Check("Y el piso NO queda marcado como superado", !historia.HasFlag(flagDelPiso));

        if (Menus.Top is MessageBoxScreen aviso)
        {
            aviso.HandleAction(GameAction.Confirm);
            yield return null;
        }

        // --- con la plata pedida: pregunta y anota el progreso ---
        jugador.AddMoney(requerido);
        ascensor.Interact(jugador, historia);
        yield return null;

        Check($"Con {requerido} juntados el ascensor pregunta si subir",
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
