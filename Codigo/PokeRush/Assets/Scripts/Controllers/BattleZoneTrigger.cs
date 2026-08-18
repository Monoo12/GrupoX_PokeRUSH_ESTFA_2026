using UnityEngine;

// Dispara un combate al ENTRAR a una zona (pasto alto, pasillo vigilado, la
// linea de vision de un entrenador). Es el complemento de InteractableTrigger,
// que pide apretar el boton de interactuar.
//
// Flujo completo: Trigger -> BattleController.StartBattle -> combate ->
// BattleEnded -> MapBattleBridge devuelve el mapa. Este componente no sabe nada
// de la transicion ni del resultado: de eso ya se ocupan el puente y el
// EventController.
//
// USO
//   1. Poner el componente en un GameObject con un Collider2D en 'Is Trigger'.
//   2. Asignar 'combate' (entrenador/boss) O 'salvaje' (encuentro).
//   3. El BattleController se busca solo; se puede pisar desde el Inspector.
//
// OJO con el rearme: MapBattleBridge desactiva el mapa entero durante la pelea,
// asi que al volver Unity puede re-disparar el OnTriggerEnter2D con el jugador
// todavia parado adentro. Por eso el trigger queda DESARMADO hasta que el
// jugador sale de la zona; sin esto el combate se relanzaria en bucle.
[RequireComponent(typeof(Collider2D))]
public class BattleZoneTrigger : MonoBehaviour
{
    [Header("Contra quien se pelea")]
    [Tooltip("Entrenador o boss. Tiene prioridad sobre el encuentro salvaje.")]
    [SerializeField] private NPCDefinition combate;
    [Tooltip("Encuentro salvaje. Se usa solo si 'combate' esta vacio.")]
    [SerializeField] private WildEncounterDefinition salvaje;

    [Header("Cuando salta")]
    [Tooltip("Probabilidad de que el paso dispare el combate. 100 = siempre " +
             "(entrenadores); menos, para el pasto alto.")]
    [SerializeField, Range(1, 100)] private int probabilidad = 100;
    [Tooltip("Se apaga despues del primer combate. Tipico de los entrenadores.")]
    [SerializeField] private bool unaSolaVez = true;

    [Header("Condiciones de historia")]
    [Tooltip("Solo salta teniendo este flag. None = sin condicion.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [Tooltip("Deja de saltar cuando este flag esta activo (ej: entrenador ya vencido).")]
    [SerializeField] private StoryFlag omitirSiFlag = StoryFlag.None;

    [Header("Referencias (opcional: se buscan solas)")]
    [SerializeField] private BattleController battleController;

    // El jugador esta adentro y ya se decidio por este ingreso: no se vuelve a
    // evaluar hasta que salga.
    private bool _armado = true;
    private bool _esperandoCombate;
    private bool _gastado;

    private void Awake()
    {
        // FindObjectsInactive.Include es OBLIGATORIO: el prefab de combate vive
        // DESACTIVADO en la escena (lo prende MapBattleBridge al empezar la
        // pelea), y las busquedas por defecto ignoran lo inactivo. Sin esto el
        // fallback no encuentra nada y solo funciona si la referencia
        // serializada esta intacta — que se rompe al regenerar el prefab,
        // porque cambian los fileID internos.
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);

        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[BattleZone] '{name}': el Collider2D no esta en 'Is Trigger'. " +
                             "Sin eso el jugador choca contra la zona en vez de entrar.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_armado || _gastado || _esperandoCombate) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        // Desde el momento en que el jugador entra, este ingreso ya se evaluo:
        // salga o no el combate, hay que salir y volver para reintentar.
        _armado = false;
        Lanzar();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _armado = true;
    }

    private void Lanzar()
    {
        GameManager gm = GameManager.Instance;
        PlayerModel player = gm?.CurrentPlayer;
        StoryModel story = gm?.Story;

        if (player == null)
        {
            Debug.LogWarning($"[BattleZone] '{name}': no hay partida cargada, no se lanza el combate.");
            return;
        }

        if (story != null)
        {
            if (requiereFlag != StoryFlag.None && !story.HasFlag(requiereFlag)) return;
            if (omitirSiFlag != StoryFlag.None && story.HasFlag(omitirSiFlag)) return;
        }

        // Un equipo sin Pokens con vida no puede pelear: el combate no
        // arrancaria y quedaria la pantalla trabada.
        if (!BattleTeamRules.TieneAlgunoVivo(player.Team))
        {
            Debug.LogWarning($"[BattleZone] '{name}': el equipo no tiene Pokens con vida.");
            return;
        }

        if (probabilidad < 100 && Random.Range(0, 100) >= probabilidad) return;

        if (battleController == null)
        {
            Debug.LogError($"[BattleZone] '{name}': no hay BattleController en la escena. " +
                           "Soltá el prefab SistemaDeCombate.");
            return;
        }

        _esperandoCombate = true;
        battleController.BattleEnded += OnBattleEnded;

        if (combate != null) battleController.StartBattle(player, combate);
        else if (salvaje != null) battleController.StartWildBattle(player, salvaje);
        else
        {
            Debug.LogWarning($"[BattleZone] '{name}': no tiene ni 'combate' ni 'salvaje' asignado.");
            battleController.BattleEnded -= OnBattleEnded;
            _esperandoCombate = false;
        }
    }

    private void OnBattleEnded(BattleOutcome outcome)
    {
        battleController.BattleEnded -= OnBattleEnded;
        _esperandoCombate = false;

        // Solo se gasta si el jugador GANO: si perdio o escapo, la zona sigue
        // ahi. Los flags y las recompensas los aplica EventController.
        if (unaSolaVez && outcome != null && outcome.PlayerVictory) _gastado = true;
    }
}
