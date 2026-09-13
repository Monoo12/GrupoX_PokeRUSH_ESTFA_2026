using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// BANCO DE PRUEBAS de la mesa de blackjack. No es parte del juego.
//
// Recorre el flujo tal como lo vive el jugador: abrir la mesa, poner fichas,
// repartir, pedir o plantarse, esperar al croupier, cobrar y cerrar. Verifica
// sobre los modelos reales (PlayerModel, MenuManager) que la plata se mueva
// bien y que el control vuelva al mundo al cerrar.
//
// Existe porque BlackjackSmokeTests (editor) prueba la LOGICA y no la pantalla.
// Los bugs que importan aca son otros: cobrar la apuesta y no pagar el premio,
// dejar un boton vivo en una fase que no corresponde, o quedarse con un menu
// abierto y el jugador sin control.
//
// Para que las condiciones de victoria se puedan probar de verdad, el zapato se
// ARMA a mano antes de cada ronda: con cartas al azar no se puede afirmar nada
// sobre el resultado, y un test que solo mira que "algo pase" no sirve.
//
// Se ejecuta desde PokeRush/Casino/Probar el blackjack.
public class BlackjackSmokeTest : MonoBehaviour
{
    [Tooltip("Frenar el Play Mode al terminar.")]
    public bool salirAlTerminar = true;

    [Tooltip("Guardar capturas del Game view en esta carpeta. Vacio = no saca ninguna.")]
    public string carpetaDeCapturas = "";

    private const BindingFlags Privados = BindingFlags.Instance | BindingFlags.NonPublic;

    // Indices de los botones, en el mismo orden que el enum Accion.
    private const int Pedir = 0;
    private const int Plantarse = 1;
    private const int Doblar = 2;
    private const int Dividir = 3;
    private const int Reiniciar = 4;
    private const int Salir = 5;

    private int _ok;
    private int _fallos;
    private BlackjackScreen _mesa;

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
            Debug.LogError("[SmokeBlackjack] No hay partida cargada. Abri una escena de juego y volve a correrlo.");
            yield break;
        }

        if (Menus == null)
        {
            Debug.LogError("[SmokeBlackjack] No hay MenuManager.");
            yield break;
        }

        // Plata redonda para que las cuentas se lean solas.
        int arranque = 5000;
        if (jugador.Money != arranque) jugador.AddMoney(arranque - jugador.Money);

        yield return ProbarAbrir();
        yield return ProbarApostar(jugador);
        yield return ProbarGanarPlantandose(jugador);
        yield return ProbarPasarse(jugador);
        yield return ProbarEmpate(jugador);
        yield return ProbarBlackjack(jugador);
        yield return ProbarDoblar(jugador);
        yield return ProbarDividir(jugador);
        yield return ProbarCerrar();

        if (_fallos == 0) Debug.Log($"[SmokeBlackjack] TODO OK: {_ok} checks pasaron.");
        else Debug.LogError($"[SmokeBlackjack] {_fallos} FALLARON, {_ok} pasaron.");

        if (salirAlTerminar)
        {
#if UNITY_EDITOR
            yield return null;
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    // ---------------------------------------------------------------- pasos

    private IEnumerator ProbarAbrir()
    {
        Menus.SetContext(MenuContextKind.Overworld);
        Menus.Open(MenuScreenId.Blackjack, new BlackjackScreenRequest(), null);
        yield return null;

        _mesa = Menus.Top as BlackjackScreen;
        Check("La mesa se abre y queda arriba de la pila", _mesa != null);
        Check("Con la mesa abierta el mundo no escucha input", MenuManager.AnyOpen);
        if (_mesa == null) yield break;

        Check("Arranca en fase de apuestas", Fase() == "Apostando");
        Check("Arranca sin cartas en la mesa", Cartas(0) == 0 && CartasDelCroupier() == 0);

        yield return Capturar("play_apostando");
    }

    private IEnumerator ProbarApostar(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        int antes = jugador.Money;
        int inicial = Apuesta();

        // Ficha de 100 (indice 5 de {1,5,10,25,50,100,500,1000}).
        _mesa.OnChipButton(5);
        yield return null;
        Check($"Poner una ficha de 100 suma a la apuesta (quedo en {Apuesta()})",
            Apuesta() == inicial + 100);

        _mesa.OnChipButton(5);
        yield return null;
        Check("Repetir la ficha acumula", Apuesta() == inicial + 200);

        Check("Poner fichas todavia no cobra nada", jugador.Money == antes);

        // Durante las apuestas no se puede pedir carta ni plantarse: no hay
        // mano abierta y dejar esos botones vivos es lo que rompe el flujo.
        Check("PLANTARSE esta apagado mientras se apuesta", !Habilitado(Plantarse));
        Check("DOBLAR esta apagado mientras se apuesta", !Habilitado(Doblar));
        Check("DIVIDIR esta apagado mientras se apuesta", !Habilitado(Dividir));
        Check("SALIR esta disponible mientras se apuesta", Habilitado(Salir));

        _mesa.OnActionButton(Reiniciar);
        yield return null;
        Check("REINICIAR limpia la apuesta", Apuesta() == 0);
    }

    // Se planta con 20 contra el 17 del croupier: tiene que cobrar 1 a 1.
    private IEnumerator ProbarGanarPlantandose(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        // Reparto: jugador, croupier, jugador, croupier.
        Acomodar(C(10), C(10), C(10), C(7));
        _mesa.OnActionButton(Pedir);
        yield return null;

        Check("Al repartir se cobra la apuesta", jugador.Money == antes - 100);
        Check("Repartiendo ya no acepta fichas", Fase() != "Apostando");

        yield return EsperarFase("TurnoJugador");
        Check("Termina el reparto y le toca al jugador", Fase() == "TurnoJugador");
        Check("El jugador recibe dos cartas", Cartas(0) == 2);
        Check("El croupier recibe dos cartas", CartasDelCroupier() == 2);
        Check("El jugador tiene 20", TotalDeLaMano(0) == 20);

        yield return Capturar("play_turno");

        _mesa.OnActionButton(Plantarse);
        yield return null;

        // Plantado, PEDIR tiene que dejar de responder: es la garantia de que
        // no se puede robar despues de cerrar la mano.
        Check("Despues de plantarse no se puede pedir", !Habilitado(Pedir));

        yield return EsperarFase("Cartel");
        Check("La ronda termina en el cartel del resultado", Fase() == "Cartel");
        Check("El croupier se planto en 17", TotalDelCroupier() == 17);
        Check($"Ganar 100 devuelve 200 (quedo en {jugador.Money})",
            jugador.Money == antes - 100 + 200);

        yield return Capturar("play_resultado");
        yield return CerrarCartel();
    }

    // Se pasa pidiendo: la mano se cierra sola y no cobra.
    private IEnumerator ProbarPasarse(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        // 10 + 6 para el jugador, y la que pida es otro 10: se pasa con 26.
        Acomodar(C(10), C(9), C(6), C(8), C(10));
        _mesa.OnActionButton(Pedir);
        yield return EsperarFase("TurnoJugador");

        Check("El jugador arranca con 16", TotalDeLaMano(0) == 16);

        _mesa.OnActionButton(Pedir);
        yield return null;

        Check("Pedir da una tercera carta", Cartas(0) == 3);
        Check("Pasarse cierra la mano sola", !Habilitado(Pedir));

        yield return EsperarFase("Cartel");
        Check("Pasarse pierde la apuesta", jugador.Money == antes - 100);

        yield return CerrarCartel();
    }

    private IEnumerator ProbarEmpate(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        Acomodar(C(10), C(10), C(9), C(9));
        _mesa.OnActionButton(Pedir);
        yield return EsperarFase("TurnoJugador");

        _mesa.OnActionButton(Plantarse);
        yield return EsperarFase("Cartel");

        Check("Empatar devuelve la apuesta y nada mas", jugador.Money == antes);

        yield return CerrarCartel();
    }

    private IEnumerator ProbarBlackjack(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        // As + K servido para el jugador; el croupier con 19 no lo empata.
        Acomodar(C(1), C(10), C(13), C(9));
        _mesa.OnActionButton(Pedir);

        // Con blackjack servido la mano se resuelve sola, sin turno del
        // jugador: no hay nada que decidir con 21.
        yield return EsperarFase("Cartel");

        Check("El blackjack servido no abre turno de jugador", Cartas(0) == 2);
        Check($"El blackjack paga 3 a 2 (quedo en {jugador.Money})",
            jugador.Money == antes - 100 + 250);

        yield return Capturar("play_blackjack");
        yield return CerrarCartel();
    }

    private IEnumerator ProbarDoblar(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        // 11 para el jugador y un 10 esperando: dobla y se planta en 21.
        Acomodar(C(6), C(10), C(5), C(6), C(10));
        _mesa.OnActionButton(Pedir);
        yield return EsperarFase("TurnoJugador");

        Check("Con 11 y dos cartas se puede doblar", Habilitado(Doblar));

        _mesa.OnActionButton(Doblar);
        yield return null;

        Check("Doblar cobra la segunda apuesta al toque", jugador.Money == antes - 200);
        Check("Doblar da una sola carta mas", Cartas(0) == 3);
        Check("Despues de doblar no se puede pedir", !Habilitado(Pedir));

        yield return EsperarFase("Cartel");
        Check($"Ganar la doblada devuelve 400 (quedo en {jugador.Money})",
            jugador.Money == antes - 200 + 400);

        yield return CerrarCartel();
    }

    private IEnumerator ProbarDividir(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        yield return Apostar(100);
        int antes = jugador.Money;

        // Par de 8 para el jugador. Al dividir, cada mano recibe una carta:
        // primero un 10 (18) y despues otro 10 (18). El croupier tiene 17.
        Acomodar(C(8), C(10), C(8), C(7), C(10), C(10));
        _mesa.OnActionButton(Pedir);
        yield return EsperarFase("TurnoJugador");

        Check("Un par de 8 se puede dividir", Habilitado(Dividir));

        _mesa.OnActionButton(Dividir);
        yield return null;

        Check("Dividir cobra la segunda apuesta", jugador.Money == antes - 200);
        Check("Dividir deja dos manos en juego", Manos() == 2);

        // Lo que se rompio una vez: dividir metia las dos cartas nuevas en la
        // mano y las dibujaba ya puestas, sin vuelo. Era la unica jugada del
        // juego sin animacion. Ahora pasa por la misma cola que el reparto
        // inicial, asi que tiene que quedar en fase de reparto y con las cartas
        // que ya estaban corriendose a su nuevo lugar.
        Check("Dividir reparte de a una y no de golpe", Fase() == "Repartiendo");
        Check("Al dividir las cartas viejas se corren deslizandose",
            CartasEnElAire() > 0);
        Check("Repartiendo el split no acepta ordenes", !Habilitado(Pedir));

        yield return EsperarFase("TurnoJugador");
        Check("Cada mano termina con dos cartas", Cartas(0) == 2 && Cartas(1) == 2);
        Check("Ya no se puede volver a dividir", !Habilitado(Dividir));

        yield return Capturar("play_split");

        // Se plantan las dos, una por una.
        _mesa.OnActionButton(Plantarse);
        yield return null;
        Check("Al plantar la primera se pasa a la segunda", ManoActual() == 1);

        _mesa.OnActionButton(Plantarse);
        yield return EsperarFase("Cartel");

        Check($"Las dos manos de 18 le ganan al 17 (quedo en {jugador.Money})",
            jugador.Money == antes - 200 + 400);

        yield return CerrarCartel();
    }

    private IEnumerator ProbarCerrar()
    {
        if (_mesa == null) yield break;

        Check("La mesa queda lista para apostar de nuevo", Fase() == "Apostando");

        _mesa.OnActionButton(Salir);
        yield return null;

        Check("SALIR cierra la mesa", !MenuManager.AnyOpen);
    }

    // ------------------------------------------------------------- ayudantes

    private IEnumerator Apostar(int cuanto)
    {
        // Se pone directo en el campo en vez de a fichazos: lo que se prueba
        // aca es el reparto, no la aritmetica de las fichas (eso ya lo mira
        // ProbarApostar).
        Poner("_apuesta", cuanto);
        yield return null;
    }

    // Deja el zapato armado para que las cartas salgan EN ESTE ORDEN.
    //
    // Robar() saca del final de la lista, asi que hay que darla vuelta. Y se
    // rellena con cartas de sobra porque la pantalla rearma el zapato cuando
    // baja del corte, y eso se llevaria puesto el orden preparado.
    private void Acomodar(params BlackjackCard[] enOrdenDeReparto)
    {
        var zapato = Campo("_zapato") as List<BlackjackCard>;
        if (zapato == null) return;

        zapato.Clear();
        for (int i = 0; i < 40; i++) zapato.Add(C(4));       // relleno
        for (int i = enOrdenDeReparto.Length - 1; i >= 0; i--) zapato.Add(enOrdenDeReparto[i]);
    }

    private static BlackjackCard C(int rango) => new BlackjackCard(BlackjackSuit.Picas, rango);

    private IEnumerator EsperarFase(string fase)
    {
        // Se espera por ESTADO y no por una cantidad de cuadros: los tiempos de
        // las animaciones estan en el Inspector y pueden cambiar.
        float limite = Time.unscaledTime + 25f;
        while (Fase() != fase && Time.unscaledTime < limite) yield return null;

        if (Fase() != fase)
            Debug.LogError($"[SmokeBlackjack] Se colgo esperando la fase '{fase}' " +
                           $"(quedo en '{Fase()}').");
    }

    private IEnumerator CerrarCartel()
    {
        // El cartel es la caja de texto de siempre: se cierra con Confirmar y
        // el foco tiene que volver a la mesa lista para otra vuelta.
        MenuScreen cartel = Menus.Top;
        Check("El cartel de arriba es la caja de texto", cartel != null && cartel != _mesa);
        if (cartel != null && cartel != _mesa) Menus.Close(cartel);
        yield return null;

        Check("Cerrar el cartel devuelve el foco a la mesa", Menus.Top == _mesa);
        Check("La mesa vuelve a la fase de apuestas", Fase() == "Apostando");
    }

    // ------------------------------------------------------------- reflexion

    private object Campo(string nombre)
    {
        FieldInfo f = typeof(BlackjackScreen).GetField(nombre, Privados);
        return f != null ? f.GetValue(_mesa) : null;
    }

    private void Poner(string nombre, object valor)
    {
        FieldInfo f = typeof(BlackjackScreen).GetField(nombre, Privados);
        if (f != null) f.SetValue(_mesa, valor);
    }

    private string Fase() => Campo("_fase")?.ToString() ?? "?";

    private int CartasEnElAire()
    {
        var vuelos = Campo("_vuelos") as System.Collections.ICollection;
        return vuelos != null ? vuelos.Count : 0;
    }

    private int Apuesta() => Campo("_apuesta") is int a ? a : -1;

    private int ManoActual() => Campo("_manoActual") is int m ? m : -1;

    private List<BlackjackHand> ManosDelJugador() => Campo("_manos") as List<BlackjackHand>;

    private int Manos()
    {
        List<BlackjackHand> manos = ManosDelJugador();
        return manos != null ? manos.Count : -1;
    }

    private int Cartas(int mano)
    {
        List<BlackjackHand> manos = ManosDelJugador();
        return manos != null && mano < manos.Count ? manos[mano].Cards.Count : -1;
    }

    private int TotalDeLaMano(int mano)
    {
        List<BlackjackHand> manos = ManosDelJugador();
        return manos != null && mano < manos.Count ? manos[mano].Total : -1;
    }

    private BlackjackHand Croupier() => Campo("_croupier") as BlackjackHand;

    private int CartasDelCroupier()
    {
        BlackjackHand c = Croupier();
        return c != null ? c.Cards.Count : -1;
    }

    private int TotalDelCroupier()
    {
        BlackjackHand c = Croupier();
        return c != null ? c.Total : -1;
    }

    // Le pregunta a la pantalla si un boton responde ahora mismo. Es el mismo
    // metodo que usan la pintura y el teclado, asi que si esto dice que no, el
    // boton no hace nada por ningun camino.
    private bool Habilitado(int accion)
    {
        MethodInfo m = typeof(BlackjackScreen).GetMethod("Habilitado", Privados);
        if (m == null) return false;

        System.Type tipo = typeof(BlackjackScreen).GetNestedType("Accion",
            BindingFlags.NonPublic);
        object valor = tipo != null ? System.Enum.ToObject(tipo, accion) : accion;
        return (bool)m.Invoke(_mesa, new[] { valor });
    }

    // Captura del Game view. Sirve para mirar la mesa como la ve el jugador,
    // que es la unica forma de confiar en como se ve: un render a mano en el
    // editor dibuja el texto contra un atlas de fuente que todavia no esta
    // armado y ensucia los carteles.
    private IEnumerator Capturar(string nombre)
    {
        if (string.IsNullOrEmpty(carpetaDeCapturas)) yield break;

        // Se espera a que no quede ninguna carta en el aire. Sin esto la
        // captura agarra la ultima a mitad de vuelo y parece que las cartas se
        // dibujan una encima de otra; costo un rato entender que la pantalla
        // estaba bien y la foto mal.
        float limite = Time.unscaledTime + 3f;
        while (CartasEnElAire() > 0 && Time.unscaledTime < limite) yield return null;

        System.IO.Directory.CreateDirectory(carpetaDeCapturas);
        string ruta = System.IO.Path.Combine(carpetaDeCapturas, nombre + ".png");

        ScreenCapture.CaptureScreenshot(ruta);
        // El archivo se escribe recien al final de un par de cuadros.
        for (int i = 0; i < 6; i++) yield return new WaitForEndOfFrame();
        Debug.Log($"[SmokeBlackjack] captura: {ruta}");
    }

    private void Check(string nombre, bool ok)
    {
        if (ok) { _ok++; Debug.Log($"[SmokeBlackjack] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[SmokeBlackjack] FALLO: {nombre}"); }
    }
}
