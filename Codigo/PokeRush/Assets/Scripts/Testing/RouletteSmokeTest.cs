using System.Collections;
using System.Reflection;
using UnityEngine;

// BANCO DE PRUEBAS de la mesa de ruleta. No es parte del juego.
//
// Recorre el flujo tal como lo vive el jugador: abrir la mesa, elegir ficha,
// poner fichas al numero y al color, girar, esperar la animacion, cobrar y
// cerrar. Verifica sobre los modelos reales (PlayerModel, MenuManager) que la
// plata se mueva bien y que el control vuelva al mundo al cerrar.
//
// Existe porque RouletteSmokeTests (editor) prueba la LOGICA y no la pantalla.
// Los bugs que importan aca son otros: cobrar la apuesta y no pagar el premio,
// que la bola frene en un numero distinto al que se canta, o quedarse con un
// menu abierto y el jugador sin control.
//
// Se ejecuta desde PokeRush/Casino/Probar la ruleta.
public class RouletteSmokeTest : MonoBehaviour
{
    [Tooltip("Frenar el Play Mode al terminar.")]
    public bool salirAlTerminar = true;

    [Tooltip("Guardar capturas del Game view en esta carpeta. Vacio = no saca ninguna.")]
    public string carpetaDeCapturas = "";

    private const BindingFlags Privados = BindingFlags.Instance | BindingFlags.NonPublic;

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
            Debug.LogError("[SmokeRuleta] No hay partida cargada. Abri una escena de juego y volve a correrlo.");
            yield break;
        }

        if (Menus == null)
        {
            Debug.LogError("[SmokeRuleta] No hay MenuManager.");
            yield break;
        }

        // Plata redonda para que las cuentas se lean solas.
        int arranque = 2000;
        if (jugador.Money != arranque) jugador.AddMoney(arranque - jugador.Money);

        yield return ProbarAbrir();
        yield return ProbarApostar(jugador);
        yield return ProbarGirar(jugador);
        yield return ProbarCerrar();

        if (_fallos == 0) Debug.Log($"[SmokeRuleta] TODO OK: {_ok} checks pasaron.");
        else Debug.LogError($"[SmokeRuleta] {_fallos} FALLARON, {_ok} pasaron.");

        if (salirAlTerminar)
        {
#if UNITY_EDITOR
            yield return null;
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    // ---------------------------------------------------------------- pasos

    private RouletteScreen _mesa;

    private IEnumerator ProbarAbrir()
    {
        Menus.SetContext(MenuContextKind.Overworld);
        Menus.Open(MenuScreenId.Roulette, new RouletteScreenRequest(), null);
        yield return null;

        _mesa = Menus.Top as RouletteScreen;
        Check("La mesa se abre y queda arriba de la pila", _mesa != null);
        Check("Con la mesa abierta el mundo no escucha input", MenuManager.AnyOpen);
        if (_mesa == null) yield break;

        Check("Arranca sin nada apostado", Total() == 0);
        Check("Arranca en fase de apuestas", Fase() == "Apostando");
    }

    private IEnumerator ProbarApostar(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        int antes = jugador.Money;

        // Ficha de 100 (indice 3 de {1,5,25,100,500,1000}).
        _mesa.OnChipButton(3);
        _mesa.OnCellButton(17);
        yield return null;
        Check("Poner una ficha al 17 suma 100 a la apuesta", Total() == 100);

        _mesa.OnColorButton(0);          // rojo
        yield return null;
        Check("Poner una ficha al rojo suma otros 100", Total() == 200);

        // La misma casilla dos veces se acumula.
        _mesa.OnCellButton(17);
        yield return null;
        Check("Repetir casilla acumula", Total() == 300);

        Check("Poner fichas todavia no cobra nada", jugador.Money == antes);

        _mesa.OnClearButton();
        yield return null;
        Check("LIMPIAR saca todas las fichas", Total() == 0);

        // Apuesta definitiva para el giro: 100 al 17 y 200 al rojo.
        _mesa.OnChipButton(3);
        _mesa.OnCellButton(17);
        _mesa.OnColorButton(0);
        _mesa.OnColorButton(0);
        yield return null;
        Check("Queda la apuesta armada en 300", Total() == 300);

        yield return Capturar("play_apostando");
    }

    private IEnumerator ProbarGirar(PlayerModel jugador)
    {
        if (_mesa == null) yield break;

        int antes = jugador.Money;
        int apostado = Total();

        _mesa.OnSpinButton();
        yield return null;

        Check("Al girar se cobra la apuesta", jugador.Money == antes - apostado);
        Check("Girando ya no acepta mas fichas", Fase() != "Apostando");

        // A mitad del giro, para ver la bola corriendo y la rueda movida.
        for (int i = 0; i < 60; i++) yield return null;
        yield return Capturar("play_girando");

        // La animacion completa: giro + celebracion. Se espera por estado, no
        // por tiempo fijo, para que no dependa de los segundos del Inspector.
        float limite = Time.unscaledTime + 20f;
        while (Fase() != "Cartel" && Time.unscaledTime < limite) yield return null;

        Check("La tirada termina y saca el cartel del resultado", Fase() == "Cartel");
        if (Fase() != "Cartel") yield break;

        yield return Capturar("play_resultado");

        int ganador = (int)Campo("_ganador");
        Check($"Sale un numero valido (salio {ganador})",
            ganador >= 0 && ganador < RouletteBoard.Casilleros);

        // LO QUE MAS IMPORTA: que la bola haya quedado sobre el casillero que
        // se canta. Si esto se rompe, el juego paga bien pero parece trampa.
        var bola = Campo("ball") as RectTransform;
        Check("La pantalla tiene la bola enganchada", bola != null);
        if (bola != null)
        {
            float anguloBola = Mathf.Atan2(bola.anchoredPosition.x, bola.anchoredPosition.y)
                               * Mathf.Rad2Deg;
            float anguloCasillero = RouletteBoard.AnguloFinalDelCasillero(
                ganador, (float)Campo("_anguloRueda"));
            float error = Mathf.Abs(Mathf.DeltaAngle(anguloBola, anguloCasillero));
            Check($"La bola frena sobre el casillero cantado (error {error:0.##} grados)", error < 1f);
        }

        // El pago tiene que ser exactamente el que dicta la mesa.
        int esperado = 0;
        if (ganador == 17) esperado += 100 + 100 * RouletteBoard.PagoDe(RouletteBetKind.Numero);
        if (RouletteBoard.EsRojo(ganador)) esperado += 200 + 200 * RouletteBoard.PagoDe(RouletteBetKind.Rojo);

        int pagado = (int)Campo("_pagado");
        Check($"Paga lo que corresponde al {ganador} (esperado {esperado}, pago {pagado})",
            pagado == esperado);
        Check("La plata del jugador refleja el pago",
            jugador.Money == antes - apostado + esperado);
    }

    private IEnumerator ProbarCerrar()
    {
        if (_mesa == null) yield break;

        // El cartel del premio es la caja de texto de siempre: se cierra con
        // Confirmar y el foco tiene que volver a la mesa lista para otra vuelta.
        MenuScreen cartel = Menus.Top;
        Check("El cartel de arriba es la caja de texto", cartel != null && cartel != _mesa);
        if (cartel != null && cartel != _mesa) Menus.Close(cartel);
        yield return null;

        Check("Cerrar el cartel devuelve el foco a la mesa", Menus.Top == _mesa);
        Check("La mesa queda lista para apostar de nuevo", Fase() == "Apostando");
        Check("Las fichas de la vuelta anterior se levantaron", Total() == 0);

        _mesa.OnBackButton();
        yield return null;

        Check("VOLVER cierra la mesa", !MenuManager.AnyOpen);
    }

    // ------------------------------------------------------------- reflexion

    private object Campo(string nombre)
    {
        FieldInfo f = typeof(RouletteScreen).GetField(nombre, Privados);
        return f != null ? f.GetValue(_mesa) : null;
    }

    private string Fase() => Campo("_fase")?.ToString() ?? "?";

    private int Total()
    {
        var dic = Campo("_apuestas") as System.Collections.IDictionary;
        if (dic == null) return -1;
        int t = 0;
        foreach (object v in dic.Values) t += (int)v;
        return t;
    }

    // Captura del Game view. Sirve para mirar la mesa como la ve el jugador,
    // que es la unica forma de confiar en como se ve: un render a mano en el
    // editor dibuja el texto contra un atlas de fuente que todavia no esta
    // armado y ensucia los carteles.
    private IEnumerator Capturar(string nombre)
    {
        if (string.IsNullOrEmpty(carpetaDeCapturas)) yield break;

        System.IO.Directory.CreateDirectory(carpetaDeCapturas);
        string ruta = System.IO.Path.Combine(carpetaDeCapturas, nombre + ".png");

        ScreenCapture.CaptureScreenshot(ruta);
        // El archivo se escribe recien al final de un par de cuadros.
        for (int i = 0; i < 6; i++) yield return new WaitForEndOfFrame();
        Debug.Log($"[SmokeRuleta] captura: {ruta}");
    }

    private void Check(string nombre, bool ok)
    {
        if (ok) { _ok++; Debug.Log($"[SmokeRuleta] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[SmokeRuleta] FALLO: {nombre}"); }
    }
}
