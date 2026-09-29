using UnityEngine;
using UnityEngine.InputSystem;

// BANCO DE PRUEBAS. No es parte del juego: lo pone solo el builder de la escena
// de prueba (BossJulieta_Prueba), no el de la arena definitiva.
//
// Atajos para probar la pelea de Julieta sin jugarla entera cada vez:
//   F9   reinicia todo desde cero (corta la pelea si esta en curso, devuelve el
//        equipo a como entro) y la arranca de nuevo, sin caminar ni charlar.
//   F10  da por cumplida la fase actual: en el proximo turno suena lo que le
//        faltaba y pasa a la siguiente.
//   F11  deja al Pokemon activo de Julieta en 1 PS.
//
// Vive en 'Sistema', fuera del mapa, para seguir escuchando durante la pelea.
// Apaga el guardado automatico: la partida de prueba no tiene que pisar la de
// verdad cuando se gana.
public class PeleaJulietaBancoDePrueba : MonoBehaviour
{
    private PeleaJulieta _pelea;
    private bool _relanzar;

    private void Awake()
    {
        CasinoDialogo.GuardadoAutomatico = false;
    }

    private void OnDestroy() => CasinoDialogo.GuardadoAutomatico = true;

    private void Start()
    {
        _pelea = FindAnyObjectByType<PeleaJulieta>(FindObjectsInactive.Include);
        if (_pelea == null) Debug.LogWarning("[Julieta] Banco de pruebas sin PeleaJulieta en la escena.");
    }

    private void Update()
    {
        if (_pelea == null || _pelea.Combate == null) return;

        // El relanzamiento espera a que la pelea anterior termine de cerrarse
        // (Abandonar pasa por el cartel de derrota y la vuelta al mapa).
        if (_relanzar && !_pelea.Combate.EnCurso && !_pelea.Combate.gameObject.activeInHierarchy)
        {
            _relanzar = false;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentPlayer != null) _pelea.Lanzar(gm.CurrentPlayer);
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f9Key.wasPressedThisFrame)
        {
            _pelea.Reiniciar();
            _relanzar = true;
        }
        else if (kb.f10Key.wasPressedThisFrame)
        {
            _pelea.AdelantarFase();
        }
        else if (kb.f11Key.wasPressedThisFrame && _pelea.Combate != null && _pelea.Combate.EnCurso)
        {
            PokenModel rival = _pelea.Combate.Modelo.ActiveEnemyPoken;
            rival.SetCurrentHP(1);
            _pelea.Combate.RefrescarHud();
            Debug.Log($"[Julieta] (prueba) {rival.Definition.pokenName} queda en 1 PS.");
        }
    }

    private void OnGUI()
    {
        if (_pelea == null) return;
        string fase = _pelea.Peleando ? _pelea.Fase.ToString() : "sin pelea";
        GUI.Label(new Rect(8, Screen.height - 24, 900, 22),
            $"PRUEBA · F9 reiniciar y pelear · F10 adelantar fase · F11 rival a 1 PS · fase: {fase}");
    }
}
