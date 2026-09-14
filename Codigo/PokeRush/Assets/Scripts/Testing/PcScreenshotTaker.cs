using System.Collections;
using UnityEngine;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Abre el PC con Pokemon de prueba en el equipo y en las cajas, recorre lo
// basico con las mismas acciones que manda el teclado (agarrar, soltar, cambiar
// de caja, abrir el equipo y dejar uno en una caja) y saca capturas en
// Capturas/PC/ para revisar la pantalla sin jugar hasta un PC.
//
// Se lanza desde PokeRush > UI > Probar y capturar el PC. Los resultados salen
// por consola y por el Editor.log con la etiqueta [CapturaPC].
public class PcScreenshotTaker : MonoBehaviour
{
    public string carpeta = "Capturas/PC";

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

        PlayerModel jugador = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (jugador == null)
        {
            Debug.LogError("[CapturaPC] No hay partida cargada. Abri una escena de juego y volve a correrlo.");
            yield break;
        }

        // Apagar el PC guarda la partida: la DEMO no puede pisar el save de verdad.
        bool guardadoAntes = PcInteractable.GuardadoAutomatico;
        PcInteractable.GuardadoAutomatico = false;

        PokenDefinition[] especies = Resources.LoadAll<PokenDefinition>("Definitions/Pokens");
        for (int i = 0; jugador.Team.Count < 3 && i < especies.Length; i++)
            jugador.Team.Add(new PokenModel(especies[i], 10 + i));

        // Diez en la primera caja, salteados, y uno shiny.
        for (int i = 0; i < 10 && especies.Length > 0; i++)
        {
            var poken = new PokenModel(especies[(i * 37 + 5) % especies.Length], 5 + i);
            if (i == 3) poken.SetShiny(true);
            jugador.Storage.Set(0, i * 2, poken);
        }

        System.IO.Directory.CreateDirectory(carpeta);
        MenuManager menus = MenuManager.Instance;
        menus.SetContext(MenuContextKind.Overworld);

        var pc = menus.Open(MenuScreenId.PcStorage) as PcStorageScreen;
        Check("Se abre la pantalla del PC", pc != null);
        if (pc == null) yield break;
        yield return Foto("1-caja");

        // ---- agarrar y soltar dentro de la caja ----
        PokenModel primero = jugador.Storage.Get(0, 0);
        pc.HandleAction(GameAction.Confirm);
        Check("Aceptar sobre un Pokemon lo agarra", pc.Agarrando);
        yield return Foto("2-agarrado");

        pc.HandleAction(GameAction.Right);
        pc.HandleAction(GameAction.Confirm);
        Check("Soltarlo en un lugar vacio lo mueve",
              !pc.Agarrando && jugador.Storage.Get(0, 1) == primero && jugador.Storage.Get(0, 0) == null);

        // ---- cambiar de caja ----
        pc.HandleAction(GameAction.PageRight);
        Check("Pagina derecha pasa a la caja 2", pc.CajaActual == 1);
        yield return Foto("3-caja-2");
        pc.HandleAction(GameAction.PageLeft);
        Check("Pagina izquierda vuelve a la caja 1", pc.CajaActual == 0);

        // ---- equipo -> caja ----
        // Desde la fila de arriba: cuatro abajo llega a la ultima fila y uno mas a
        // los botones, parado en EQUIPO.
        for (int i = 0; i < 5; i++) pc.HandleAction(GameAction.Down);
        pc.HandleAction(GameAction.Confirm);
        yield return Foto("4-equipo");

        int equipoAntes = jugador.Team.Count;
        PokenModel lider = jugador.Team[0];
        pc.HandleAction(GameAction.Confirm);                 // agarra al del hueco 1
        Check("En el panel del equipo tambien se agarra", pc.Agarrando);
        for (int i = 0; i < 3; i++) pc.HandleAction(GameAction.Down); // hasta VOLVER
        pc.HandleAction(GameAction.Confirm);                 // cierra el panel con el Pokemon en la mano
        pc.HandleAction(GameAction.Up);                      // ultima fila de la caja
        pc.HandleAction(GameAction.Confirm);                 // lo deja ahi
        Check("Dejar uno del equipo en la caja lo saca del equipo",
              !pc.Agarrando && jugador.Team.Count == equipoAntes - 1 && jugador.Storage.Get(0, 25) == lider);
        yield return Foto("5-depositado");

        menus.CloseAll();
        PcInteractable.GuardadoAutomatico = guardadoAntes;
        Debug.Log($"[CapturaPC] TERMINADO. OK={_ok} FALLOS={_fallos}. Capturas en {carpeta}");

#if UNITY_EDITOR
        yield return null;
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Check(string nombre, bool paso)
    {
        if (paso) { _ok++; Debug.Log($"[CapturaPC] OK: {nombre}"); }
        else { _fallos++; Debug.LogError($"[CapturaPC] FALLO: {nombre}"); }
    }

    private IEnumerator Foto(string nombre)
    {
        // Dos frames para que el layout ya este acomodado.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot($"{carpeta}/{nombre}.png");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
    }
}
