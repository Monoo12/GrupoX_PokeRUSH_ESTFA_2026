using UnityEngine;

// Punto de interaccion de la torre. Cubre los tres tipos de corte de subsistema
// del GDD con un solo componente, porque la diferencia entre ellos es de datos,
// no de logica:
//
//   Camaras  -> combate contra un guardia          (asignar 'combate')
//   Firewall -> combate corto, uno por nodo        (asignar 'combate', un flag por nodo)
//   Rastreo  -> cuesta PS, sin combate             (asignar 'costoHP')
//   Energia  -> gratis, cuesta encontrarlo         (solo 'otorgaFlag')
//
// Los cortes por COMBATE no otorgan el flag desde aca: lo hace
// EventController.OnBattleEnded leyendo el defeatGrantsFlag del EnemyDefinition,
// que ya existia. Configuralo en el asset del enemigo, no aca.
//
// Necesita un Collider2D (isTrigger o no, da igual: no usa colisiones, lo
// detecta PlayerController.Interact con un OverlapCircle) en una capa que este
// dentro del 'interactableMask' del jugador.
[RequireComponent(typeof(Collider2D))]
public class InteractableTrigger : MonoBehaviour, IInteractable
{
    [Header("Identidad")]
    [Tooltip("Titulo del cartel de confirmacion. Ej: FIREWALL")]
    [SerializeField] private string titulo = "SUBSISTEMA";

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, solo se puede usar teniendo ya este flag. None = siempre.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [Tooltip("Mensaje cuando todavia no se cumple el requisito.")]
    [SerializeField] private string mensajeBloqueado = "Esta cerrado. Falta algo mas.";

    [Header("Que hace")]
    [Tooltip("Flag que activa. Para los cortes por COMBATE dejalo en None y " +
             "configura el defeatGrantsFlag del EnemyDefinition.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;
    [Tooltip("Enemigo contra el que se pelea. Vacio = no hay combate.")]
    [SerializeField] private NPCDefinition combate;
    [Tooltip("PS que cuesta usarlo (el Rastreo del GDD). 0 = gratis.")]
    [SerializeField] private int costoHP = 0;

    [Header("Textos")]
    [Tooltip("Lo que dice el cartel al lograrlo. Ej: nodo 1 de 3 caido. Escudo reducido.")]
    [SerializeField, TextArea] private string mensajeExito = "Subsistema desactivado.";
    [Tooltip("Lo que dice si ya estaba hecho.")]
    [SerializeField] private string mensajeYaHecho = "Ya esta desactivado.";
    [Tooltip("Lo que dice si no alcanzan los PS para pagar el costo.")]
    [SerializeField] private string mensajeSinRecursos = "Tu equipo no aguanta la sobrecarga.";

    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private SubsystemFeedbackView feedback;

    [Header("Presentacion")]
    [Tooltip("Apagar el objeto una vez usado. Util para puertas o paneles de un solo uso.")]
    [SerializeField] private bool desactivarAlUsar = false;

    private bool _esperandoCombate;

    private void Awake()
    {
        // Se resuelven solas para que el equipo pueda soltar el prefab en la
        // escena sin cablear nada. Se pueden pisar desde el Inspector.
        // Include: el prefab de combate esta DESACTIVADO hasta que arranca la
        // pelea, y las busquedas por defecto ignoran lo inactivo. Sin esto el
        // trigger no lo encuentra y tira 'no hay BattleController en la escena'.
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        if (feedback == null)
            feedback = FindAnyObjectByType<SubsystemFeedbackView>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null) return false;
        if (_esperandoCombate) return false;

        // 1) ya estaba hecho
        if (otorgaFlag != StoryFlag.None && story.HasFlag(otorgaFlag))
        {
            Avisar(mensajeYaHecho);
            return true;
        }

        // 2) requisito sin cumplir (ej: el nodo C detras de las camaras)
        if (requiereFlag != StoryFlag.None && !story.HasFlag(requiereFlag))
        {
            Avisar(mensajeBloqueado);
            return true;
        }

        // 3) costo en PS: el Rastreo del GDD. Se cobra ANTES de otorgar nada,
        //    y si no alcanza no pasa nada (no se puede quedar sin equipo).
        if (costoHP > 0 && !CobrarHP(player))
        {
            Avisar(mensajeSinRecursos);
            return true;
        }

        // 4) combate: el flag lo otorga EventController al ganar, leyendo el
        //    defeatGrantsFlag del enemigo. Aca solo lanzamos la pelea.
        if (combate != null)
        {
            if (battleController == null)
            {
                Debug.LogError($"[Interactable] '{name}' quiere pelear contra " +
                               $"'{combate.characterName}' pero no hay BattleController en la escena.");
                return true;
            }

            _esperandoCombate = true;
            battleController.BattleEnded += OnBattleEnded;
            battleController.StartBattle(player, combate);
            return true;
        }

        // 5) sin combate: se resuelve en el momento
        Lograr(story);
        return true;
    }

    private void OnBattleEnded(BattleOutcome outcome)
    {
        battleController.BattleEnded -= OnBattleEnded;
        _esperandoCombate = false;

        if (!outcome.PlayerVictory) return;

        // El flag ya lo activo EventController con el defeatGrantsFlag del
        // enemigo; aca solo mostramos el cartel y apagamos el objeto si toca.
        Avisar(mensajeExito);
        if (desactivarAlUsar) gameObject.SetActive(false);
    }

    private void Lograr(StoryModel story)
    {
        if (otorgaFlag != StoryFlag.None) story.ActivateFlag(otorgaFlag);
        Avisar(mensajeExito);
        if (desactivarAlUsar) gameObject.SetActive(false);
    }

    // Cobra los PS repartiendo desde el primer Poken con vida. Nunca deja a
    // ninguno en 0: el GDD dice que los subsistemas no pueden bloquear nada, y
    // quedarte sin equipo en un pasillo seria justamente eso.
    private bool CobrarHP(PlayerModel player)
    {
        int disponible = 0;
        foreach (PokenModel p in player.Team)
            if (!p.IsFainted) disponible += p.CurrentHP - 1;

        if (disponible < costoHP) return false;

        int restante = costoHP;
        foreach (PokenModel p in player.Team)
        {
            if (restante <= 0) break;
            if (p.IsFainted) continue;

            int puede = p.CurrentHP - 1;
            if (puede <= 0) continue;

            int saca = Mathf.Min(puede, restante);
            p.SetCurrentHP(p.CurrentHP - saca);
            restante -= saca;
        }
        return true;
    }

    private void Avisar(string mensaje)
    {
        if (feedback != null) feedback.Show(titulo, mensaje);
        else Debug.Log($"[{titulo}] {mensaje}");
    }
}
