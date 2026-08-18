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
    // Un objeto y cuantos. Se usa para las pokeballs del piso, que no pelean ni
    // cortan nada: solo te dan cosas para la mochila.
    [System.Serializable]
    public class EntregaDeObjeto
    {
        public HealItemDefinition objeto;
        public int cantidad = 1;
    }

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
    [Tooltip("Objetos que entrega al usarlo (pokeballs del piso). Vacio = ninguno.")]
    [SerializeField] private EntregaDeObjeto[] entrega;

    [Header("Textos")]
    [Tooltip("Lo que dice el cartel al lograrlo. Ej: nodo 1 de 3 caido. Escudo reducido.")]
    [SerializeField, TextArea] private string mensajeExito = "Subsistema desactivado.";
    [Tooltip("Lo que dice si ya estaba hecho.")]
    [SerializeField] private string mensajeYaHecho = "Ya esta desactivado.";
    [Tooltip("Lo que dice si no alcanzan los PS para pagar el costo.")]
    [SerializeField] private string mensajeSinRecursos = "Tu equipo no aguanta la sobrecarga.";

    [Header("Pregunta previa (opcional)")]
    [Tooltip("Si esta puesto, primero pregunta y solo sigue si el jugador acepta. " +
             "Vacio = se ejecuta de una, como siempre.")]
    [SerializeField, TextArea(2, 4)] private string preguntaAntes = "";
    [Tooltip("Nombre que aparece arriba de la caja al preguntar.")]
    [SerializeField] private string nombreQuePregunta = "";
    [SerializeField] private Sprite retrato;
    [SerializeField] private string opcionSi = "Si";
    [SerializeField] private string opcionNo = "No";
    [Tooltip("Lo que dice si el jugador dice que no. Vacio = no dice nada.")]
    [SerializeField, TextArea(2, 4)] private string mensajeSiRechaza = "";

    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private SubsystemFeedbackView feedback;
    [Tooltip("Solo hace falta si se usa 'preguntaAntes'.")]
    [SerializeField] private DialogueController dialogueController;

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
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
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

        // 3) pregunta previa: si el diseniador puso una, no se hace nada
        //    hasta que el jugador acepte. La respuesta llega por callback, asi
        //    que aca se corta y se sigue adentro de Ejecutar().
        if (!string.IsNullOrEmpty(preguntaAntes) && dialogueController != null)
        {
            dialogueController.StartConfirm(nombreQuePregunta, retrato, preguntaAntes,
                opcionSi, opcionNo, acepto =>
                {
                    if (acepto) Ejecutar(player, story);
                    else if (!string.IsNullOrEmpty(mensajeSiRechaza))
                        dialogueController.StartMessage(nombreQuePregunta, retrato,
                            new[] { mensajeSiRechaza });
                });
            return true;
        }

        return Ejecutar(player, story);
    }

    // Lo que pasa una vez decidido que se sigue adelante. Separado de Interact
    // porque con pregunta previa esto corre DESPUES, cuando contesta el jugador.
    private bool Ejecutar(PlayerModel player, StoryModel story)
    {
        // costo en PS: el Rastreo del GDD. Se cobra ANTES de otorgar nada,
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
        Lograr(player, story);
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

    private void Lograr(PlayerModel player, StoryModel story)
    {
        if (otorgaFlag != StoryFlag.None) story.ActivateFlag(otorgaFlag);

        string loQueDio = EntregarObjetos(player);

        // Si el mensaje tiene {0}, se le mete lo que entrego. Asi el mismo campo
        // sirve para "Pocion x3" y para un cartel fijo de subsistema.
        string mensaje = mensajeExito;
        if (!string.IsNullOrEmpty(loQueDio) && mensaje.Contains("{0}"))
            mensaje = string.Format(mensaje, loQueDio);

        Avisar(mensaje);
        if (desactivarAlUsar) gameObject.SetActive(false);
    }

    // Mete los objetos en la mochila y devuelve el texto de lo que dio.
    private string EntregarObjetos(PlayerModel player)
    {
        if (entrega == null || entrega.Length == 0 || player == null) return null;

        var partes = new System.Collections.Generic.List<string>();
        foreach (EntregaDeObjeto e in entrega)
        {
            if (e == null || e.objeto == null || e.cantidad <= 0) continue;
            player.Inventory.AddHealItem(e.objeto, e.cantidad);
            partes.Add(e.cantidad > 1
                ? $"{e.objeto.itemName} x{e.cantidad}"
                : e.objeto.itemName);
        }

        if (partes.Count == 0) return null;

        string texto = string.Join(" y ", partes);
        Debug.Log($"[{titulo}] Entregado: {texto}.");
        return texto;
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

    // Orden de preferencia: el cartel de subsistemas si la escena lo tiene, si
    // no la caja de dialogo del mapa, y como ultimo recurso la consola. Sin
    // esto un boton sin SubsystemFeedbackView "funciona" pero el jugador no ve
    // ninguna respuesta.
    private void Avisar(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        if (feedback != null) { feedback.Show(titulo, mensaje); return; }
        if (dialogueController != null)
        {
            dialogueController.StartMessage(nombreQuePregunta, retrato, new[] { mensaje });
            return;
        }
        Debug.Log($"[{titulo}] {mensaje}");
    }
}
