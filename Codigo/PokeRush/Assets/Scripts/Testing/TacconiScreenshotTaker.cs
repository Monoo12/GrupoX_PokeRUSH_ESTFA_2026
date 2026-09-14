using System.Collections;
using UnityEngine;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Abre la mesa de blackjack como la abre Tacconi (apostando un Pokemon, sin
// fichas), juega una mano con las mismas acciones del teclado y saca capturas
// en Capturas/Tacconi/ para revisar la pantalla sin llegar al piso 3.
//
// Se lanza desde PokeRush > UI > Probar y capturar la mesa de Tacconi. Los
// resultados salen por consola y por el Editor.log con la etiqueta [CapturaTacconi].
public class TacconiScreenshotTaker : MonoBehaviour
{
    public string carpeta = "Capturas/Tacconi";

    private int _ok;
    private int _fallos;

    private void Awake()
    {
        // Con la ventana de Unity en segundo plano el player loop se congela.
        Application.runInBackground = true;
    }

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        yield return null;
        yield return null;

        System.IO.Directory.CreateDirectory(carpeta);
        MenuManager menus = MenuManager.Instance;
        menus.SetContext(MenuContextKind.Overworld);

        bool cerro = false;
        BlackjackScreenResult resultado = null;

        MenuScreen mesa = menus.Open(MenuScreenId.Blackjack,
            new BlackjackScreenRequest
            {
                Titulo = "La mesa de Tacconi",
                ApuestaPokemon = "Gastly",
                PremioPokemon = "su Cosmog shiny",
                Rival = "Tacconi"
            },
            r => { cerro = true; resultado = r as BlackjackScreenResult; });

        Check("Se abre la mesa en modo apuesta de Pokemon", mesa is BlackjackScreen);
        if (mesa == null) yield break;
        yield return Foto("1-apostando");

        // Arranca parado en PEDIR: aceptar reparte sin poner fichas.
        mesa.HandleAction(GameAction.Confirm);
        yield return new WaitForSecondsRealtime(3f);
        yield return Foto("2-turno");

        // PLANTARSE esta a la derecha de PEDIR. Si el reparto todavia no
        // termino se ignora y el bucle de abajo sigue pidiendo hasta cerrar.
        mesa.HandleAction(GameAction.Right);
        mesa.HandleAction(GameAction.Confirm);

        bool fotoDelCartel = false;
        float limite = Time.realtimeSinceStartup + 40f;
        while (!cerro && Time.realtimeSinceStartup < limite)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (cerro) break;

            MenuScreen arriba = menus.Top;
            if (arriba != null && arriba != mesa && !fotoDelCartel)
            {
                // Dejar que termine de escribirse el cartel antes de la foto.
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Foto("3-resultado");
                fotoDelCartel = true;
            }

            if (menus.Top != null) menus.Top.HandleAction(GameAction.Confirm);
        }

        Check("Cerrado el cartel, la mesa se cierra sola", cerro);
        Check("Devuelve el resultado de la apuesta",
              resultado != null && resultado.ResultadoApuestaPokemon != BlackjackOutcome.Pendiente);
        Check("No se jugo plata", resultado != null && resultado.Neto == 0);
        Debug.Log($"[CapturaTacconi] Resultado de la mano: {(resultado != null ? resultado.ResultadoApuestaPokemon.ToString() : "ninguno")}");

        menus.CloseAll();
        Debug.Log($"[CapturaTacconi] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");

#if UNITY_EDITOR
        yield return null;
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[CapturaTacconi] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[CapturaTacconi] FALLO: {nombre}"); }
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
