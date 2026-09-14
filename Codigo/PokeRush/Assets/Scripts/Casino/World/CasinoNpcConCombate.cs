using System.Collections;
using UnityEngine;

// Base de los NPCs del casino que pelean: el apostador, los que pelean una vez
// y el guardia del ascensor.
//
// Resuelve las dos cosas que tienen en comun y que son faciles de hacer mal:
//
//   1. Esperar el final del combate. Se suscribe a BattleEnded solo mientras
//      dura SU pelea, asi dos NPCs de la misma escena no reaccionan a la pelea
//      del otro.
//
//   2. Mostrar algo DESPUES del combate. Durante la pelea el NPC vive adentro
//      de MapaRaiz, que MapBattleBridge apaga: un objeto apagado no puede
//      correr corrutinas, y una caja abierta en ese momento quedaria tapada
//      por el combate. Por eso lo que haya que decir se deja pendiente y corre
//      cuando el mapa vuelve (OnEnable), un frame despues.
//
// No otorga flags por su cuenta: eso lo decide cada NPC. Las escenas del casino
// no tienen EventController, asi que el defeatGrantsFlag del enemigo NO se
// aplica solo; cada NPC lo activa al ganar.
public abstract class CasinoNpcConCombate : MonoBehaviour
{
    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] protected BattleController battleController;
    [SerializeField] protected DialogueController dialogueController;

    [Header("Textos comunes")]
    [SerializeField] protected string nombre = "NPC";
    [SerializeField] protected Sprite retrato;
    [SerializeField, TextArea(2, 3)] protected string mensajeSinEquipo =
        "Con el equipo así no se puede pelear. Pasá por la enfermería.";

    protected bool EsperandoCombate { get; private set; }

    private System.Action<BattleOutcome> _alTerminar;
    private System.Action _pendiente;

    protected virtual void Awake()
    {
        // Include: el prefab de combate esta DESACTIVADO hasta que arranca la
        // pelea, y las busquedas por defecto ignoran lo inactivo.
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    protected virtual void OnEnable()
    {
        if (_pendiente != null) StartCoroutine(CorrerPendiente());
    }

    protected virtual void OnDestroy()
    {
        if (battleController != null) battleController.BattleEnded -= OnBattleEnded;
    }

    // ---------- combate ----------

    protected bool LanzarCombate(PlayerModel jugador, EnemyDefinition rival,
                                 System.Action<BattleOutcome> alTerminar)
    {
        if (rival == null)
        {
            Debug.LogError($"[Casino] '{name}' no tiene rival asignado.");
            return false;
        }

        // BattleController aborta en silencio si no hay nadie en pie, y
        // entonces BattleEnded no llega nunca y el NPC quedaria esperando.
        if (jugador == null || !jugador.HasAlivePoken)
        {
            Decir(mensajeSinEquipo);
            return false;
        }

        if (battleController == null)
        {
            Debug.LogError($"[Casino] '{name}' quiere pelear pero no hay BattleController en la escena.");
            return false;
        }

        EsperandoCombate = true;
        _alTerminar = alTerminar;
        battleController.BattleEnded += OnBattleEnded;
        battleController.StartBattle(jugador, rival);
        return true;
    }

    private void OnBattleEnded(BattleOutcome outcome)
    {
        battleController.BattleEnded -= OnBattleEnded;
        EsperandoCombate = false;

        var cb = _alTerminar;
        _alTerminar = null;
        cb?.Invoke(outcome);
    }

    // Corre 'accion' cuando el mapa este de vuelta. Si ya esta activo (una
    // escena sin MapBattleBridge), corre en el frame siguiente.
    protected void AlVolverAlMapa(System.Action accion)
    {
        _pendiente = accion;
        if (isActiveAndEnabled) StartCoroutine(CorrerPendiente());
    }

    private IEnumerator CorrerPendiente()
    {
        yield return null;

        var accion = _pendiente;
        _pendiente = null;
        accion?.Invoke();
    }

    // ---------- flags ----------

    protected static void Activar(StoryModel story, StoryFlag flag)
    {
        if (story != null && flag != StoryFlag.None) story.ActivateFlag(flag);
    }

    // ---------- cajas ----------

    protected void Decir(string linea, System.Action alCerrar = null)
    {
        CasinoDialogo.Decir(dialogueController, nombre, retrato, new[] { linea }, alCerrar);
    }

    protected void Decir(string[] lineas, System.Action alCerrar = null)
    {
        CasinoDialogo.Decir(dialogueController, nombre, retrato, lineas, alCerrar);
    }

    protected void Preguntar(string pregunta, string si, string no, System.Action<bool> respuesta)
    {
        CasinoDialogo.Preguntar(dialogueController, nombre, retrato, pregunta, si, no, respuesta);
    }
}
