using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Una parte del tutorial de la habitacion: algo del cuarto que brilla y que, al
// apretar X al lado, explica una cosa del juego y la muestra.
//
//   Mochila    explica la mochila, da las Pociones de arranque y la abre
//   Equipo     explica el equipo y el Resumen, y abre la pantalla del equipo
//   Resumen    abre el Resumen del equipo
//   Pc         explica el PC de almacenamiento y lo abre
//   Guardar    explica el guardado y abre el menu de pausa (ahi esta Guardar)
//   Opciones   explica y abre las Opciones
//   SoloCharla solo explica (un poster, una nota)
//   Pelea      la pelea de practica contra 'rival'
//
// Queda HECHA al activarse su 'flag': el brillo (BrilloInteractuable, en el
// mismo objeto) lo toma de aca y se apaga solo, y la puerta lo cuenta. Las de
// menu quedan hechas apenas se abre el menu (lo que importa es haberlo visto);
// la pelea, solo si se gana.
//
// Lo que dice sale de 'explicacion', un DialogueDefinition que escribe el equipo
// (hablante y retrato incluidos): los textos se cambian sin abrir la escena. El
// dialogo, el combate y los menus se buscan solos al arrancar, asi la parte se
// puede copiar y pegar en otra escena sin cablear nada.
[RequireComponent(typeof(Collider2D))]
public class TutorialParte : MonoBehaviour, IInteractable, ReglasDelTutorial.IParte
{
    public enum Tipo { SoloCharla, Mochila, Equipo, Resumen, Pc, Guardar, Opciones, Pelea }

    [Header("Que parte es")]
    [Tooltip("Como se nombra en 'te falta ...'. Ej: la mochila")]
    [SerializeField] private string nombre = "la mochila";
    [Tooltip("Se activa al terminarla: apaga el brillo y cuenta para la puerta. Uno distinto " +
             "por parte (una parte nueva lleva un flag nuevo al final de StoryFlag).")]
    [SerializeField] private StoryFlag flag = StoryFlag.None;
    [SerializeField] private Tipo tipo = Tipo.SoloCharla;
    [Tooltip("Cuenta para abrir la puerta.")]
    [SerializeField] private bool obligatoria = true;
    [Tooltip("Se habilita recien al terminar las demas obligatorias. Es la pelea: asi se " +
             "llega sabiendo usar la mochila.")]
    [SerializeField] private bool esperaALasDemas = false;

    [Header("Textos")]
    [Tooltip("Lo que explica antes de abrir el menu o de pelear. El hablante y el retrato salen de aca.")]
    [SerializeField] private DialogueDefinition explicacion;
    [Tooltip("Al usarla de nuevo, ya hecha. En las de menu, vacio = abre el menu sin decir nada.")]
    [SerializeField, TextArea(2, 3)] private string mensajeYaHecha = "";
    [Tooltip("Si espera a las demas y todavia falta algo. {0} = lo que falta.")]
    [SerializeField, TextArea(2, 3)] private string mensajeEsperando = "Primero terminá lo demás: te falta {0}.";

    [Header("Objetos que da (opcional)")]
    [Tooltip("Se dan la primera vez, antes de abrir el menu. Si el jugador omite el tutorial, se los dan igual.")]
    [SerializeField] private InteractableTrigger.EntregaDeObjeto[] entrega;
    [Tooltip("{0} = lo que dio.")]
    [SerializeField] private string mensajeEntrega = "Recibiste {0}.";

    [Header("Solo si es la pelea")]
    [SerializeField] private EnemyDefinition rival;
    [SerializeField, TextArea(2, 3)] private string mensajeGanaste = "¡Bien peleado! Ya sabés lo básico.";
    [SerializeField, TextArea(2, 3)] private string mensajePerdiste =
        "¡Casi! Ya te curé el equipo. Hablame cuando quieras la revancha.";
    [SerializeField, TextArea(2, 3)] private string mensajeSinEquipo = "Sin ningún Pokémon no se puede pelear.";

    [Header("Referencias (opcionales: se buscan solas)")]
    [SerializeField] private DialogueController dialogueController;
    [SerializeField] private BattleController battleController;

    public string Nombre => nombre;
    public StoryFlag Flag => flag;
    public bool Obligatoria => obligatoria;
    public bool EsperaALasDemas => esperaALasDemas;
    public Tipo QueHace => tipo;
    public EnemyDefinition Rival => rival;
    public DialogueDefinition Explicacion => explicacion;

    // Explicando, con su menu abierto o peleando. Mientras tanto no responde.
    public bool Ocupada { get; private set; }

    // Lo que hay que decir cuando vuelva el mapa (despues de la pelea).
    private System.Action _pendiente;

    private string Hablante => explicacion != null ? explicacion.speakerName : "";
    private Sprite Retrato => explicacion != null ? explicacion.speakerPortrait : null;

    private void Awake()
    {
        // Include: el prefab de combate esta DESACTIVADO hasta que arranca una
        // pelea, y las busquedas por defecto ignoran lo inactivo.
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
    }

    // Durante la pelea la parte vive adentro de MapaRaiz, que MapBattleBridge
    // apaga: un objeto apagado no corre corrutinas y una caja abierta en ese
    // momento quedaria tapada por el combate. Lo que haya que decir al terminar
    // queda pendiente y corre cuando el mapa vuelve.
    private void OnEnable()
    {
        if (_pendiente != null) StartCoroutine(CorrerPendiente());
    }

    private void OnDestroy()
    {
        if (battleController != null) battleController.BattleEnded -= AlTerminarLaPelea;
    }

    // Todas las partes de la escena, no solo las que cuelgan del objeto
    // Tutorial: si el equipo engancha una parte a un mueble, sigue contando.
    // En el orden de la jerarquia, asi "te falta ..." las nombra siempre igual
    // (FindObjectsByType no garantiza ningun orden).
    public static List<TutorialParte> TodasLasDeLaEscena()
    {
        var partes = new List<TutorialParte>(FindObjectsByType<TutorialParte>(FindObjectsInactive.Include));
        partes.Sort((a, b) => CompararPorJerarquia(a.transform, b.transform));
        return partes;
    }

    private static int CompararPorJerarquia(Transform a, Transform b)
    {
        List<int> ca = Camino(a), cb = Camino(b);
        for (int i = 0; i < Mathf.Min(ca.Count, cb.Count); i++)
            if (ca[i] != cb[i]) return ca[i].CompareTo(cb[i]);
        return ca.Count.CompareTo(cb.Count);
    }

    // Los indices de hermano desde la raiz de la escena hasta 't'.
    private static List<int> Camino(Transform t)
    {
        var camino = new List<int>();
        for (; t != null; t = t.parent) camino.Insert(0, t.GetSiblingIndex());
        return camino;
    }

    // ---------- interactuar ----------

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null || Ocupada) return false;

        if (ReglasDelTutorial.Hecha(this, story))
        {
            UsarDeNuevo(player, story);
            return true;
        }

        if (esperaALasDemas)
        {
            List<ReglasDelTutorial.IParte> antes =
                ReglasDelTutorial.FaltanAntesDe(this, TodasLasDeLaEscena(), story);
            if (antes.Count > 0)
            {
                Decir(string.Format(mensajeEsperando, ReglasDelTutorial.Enumerar(antes)));
                return true;
            }
        }

        Ocupada = true;
        Explicar(story, () =>
        {
            if (tipo == Tipo.Pelea) Pelear(player);
            else HacerLaDeMenu(player, story);
        });
        return true;
    }

    private void HacerLaDeMenu(PlayerModel player, StoryModel story)
    {
        // Hecha apenas se muestra el menu: cuenta haberlo visto, no lo que se
        // haga adentro. Asi tampoco depende de que el menu avise al cerrarse.
        Completar(story);

        System.Action terminar = () =>
        {
            Ocupada = false;
            TutorialDeLaHabitacion tutorial = TutorialDeLaHabitacion.Buscar();
            if (tutorial != null) tutorial.AvisarProgreso(story);
        };

        // Los objetos antes del menu: la mochila se abre con las Pociones adentro.
        string recibido = Entregar(player);
        if (string.IsNullOrEmpty(recibido)) AbrirMenu(player, terminar);
        else Decir(string.Format(mensajeEntrega, recibido), () => AbrirMenu(player, terminar));
    }

    // Ya hecha: las de menu quedan como atajo (la mochila del cuarto abre la
    // mochila), la pelea no se repite y un cartel se puede volver a leer.
    private void UsarDeNuevo(PlayerModel player, StoryModel story)
    {
        if (tipo == Tipo.Pelea)
        {
            Decir(string.IsNullOrEmpty(mensajeYaHecha) ? mensajeGanaste : mensajeYaHecha);
            return;
        }

        if (tipo == Tipo.SoloCharla)
        {
            if (string.IsNullOrEmpty(mensajeYaHecha)) Explicar(story, null);
            else Decir(mensajeYaHecha);
            return;
        }

        Ocupada = true;
        System.Action abrir = () => AbrirMenu(player, () => Ocupada = false);
        if (string.IsNullOrEmpty(mensajeYaHecha)) abrir();
        else Decir(mensajeYaHecha, abrir);
    }

    // ---------- menus ----------

    // Abre la pantalla de esta parte y llama a 'alCerrar' cuando el jugador la
    // cierra. Si no hay nada que abrir (SoloCharla, un equipo vacio, un prefab
    // que falta) sigue de largo: la parte nunca queda trabada.
    private void AbrirMenu(PlayerModel player, System.Action alCerrar)
    {
        MenuManager menus = MenuManager.Instance;
        bool sinEquipo = player == null || player.Team.Count == 0;
        if (menus == null || tipo == Tipo.SoloCharla || tipo == Tipo.Pelea ||
            (sinEquipo && (tipo == Tipo.Equipo || tipo == Tipo.Resumen)))
        {
            alCerrar?.Invoke();
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        System.Action<object> cerrado = _ => alCerrar?.Invoke();
        MenuScreen abierta = null;

        switch (tipo)
        {
            case Tipo.Mochila:
                abierta = menus.Open(MenuScreenId.Bag, new BagScreenRequest
                {
                    Context = MenuContextKind.Overworld,
                    Pocket = ItemPocket.Medicine
                }, cerrado);
                break;

            case Tipo.Equipo:
                abierta = menus.Open(MenuScreenId.Party, new PartyScreenRequest
                {
                    Mode = PartyScreenMode.Browse,
                    Context = MenuContextKind.Overworld
                }, cerrado);
                break;

            case Tipo.Resumen:
                abierta = menus.Open(MenuScreenId.Summary,
                    new SummaryRequest { Team = player.Team, StartIndex = 0 }, cerrado);
                break;

            case Tipo.Pc:
                abierta = menus.Open(MenuScreenId.PcStorage, null, cerrado);
                break;

            // El menu de pausa entero: Guardar esta ahi, y de paso se ve el
            // resto (Pokemon, Bolsa, Opciones, Salir).
            case Tipo.Guardar:
                abierta = menus.Open(MenuScreenId.Pause, null, cerrado);
                break;

            case Tipo.Opciones:
                abierta = menus.Open(MenuScreenId.Options, null, cerrado);
                break;
        }

        if (abierta == null) alCerrar?.Invoke();
    }

    // ---------- la pelea ----------

    private void Pelear(PlayerModel player)
    {
        if (!RivalValido())
        {
            Debug.LogError($"[Tutorial] '{name}' es la pelea pero no tiene un rival con equipo.");
            Ocupada = false;
            return;
        }

        if (battleController == null)
        {
            Debug.LogError($"[Tutorial] '{name}' quiere pelear pero no hay BattleController en la escena. " +
                           "Correr PokeRush > Tutorial > Preparar la escena abierta.");
            Ocupada = false;
            return;
        }

        // Parejo: con el equipo curado la pelea arranca siempre. BattleController
        // aborta en silencio si no hay nadie en pie, y BattleEnded no llegaria.
        ReglasDelTutorial.CurarEquipo(player);
        if (player == null || !player.HasAlivePoken)
        {
            Decir(mensajeSinEquipo);
            Ocupada = false;
            return;
        }

        battleController.BattleEnded += AlTerminarLaPelea;
        battleController.StartBattle(player, rival);
    }

    private bool RivalValido()
    {
        if (rival == null || rival.pokenTeam == null) return false;
        foreach (EnemyTeamEntry e in rival.pokenTeam)
            if (e != null && e.species != null && e.level > 0) return true;
        return false;
    }

    private void AlTerminarLaPelea(BattleOutcome outcome)
    {
        battleController.BattleEnded -= AlTerminarLaPelea;

        GameManager gm = GameManager.Instance;
        PlayerModel player = gm != null ? gm.CurrentPlayer : null;
        StoryModel story = gm != null ? gm.Story : null;

        // Ganes o pierdas, del cuarto se sale entero.
        ReglasDelTutorial.CurarEquipo(player);
        bool gano = outcome != null && outcome.PlayerVictory;
        if (gano) Completar(story);

        AlVolverAlMapa(() => Decir(gano ? mensajeGanaste : mensajePerdiste, () =>
        {
            Ocupada = false;
            if (!gano) return;
            TutorialDeLaHabitacion tutorial = TutorialDeLaHabitacion.Buscar();
            if (tutorial != null) tutorial.AvisarProgreso(story);
        }));
    }

    // Corre 'accion' cuando el mapa este de vuelta. Si la parte no se apago
    // (una escena sin MapBattleBridge), corre en el frame siguiente.
    private void AlVolverAlMapa(System.Action accion)
    {
        _pendiente = accion;
        if (isActiveAndEnabled) StartCoroutine(CorrerPendiente());
    }

    private IEnumerator CorrerPendiente()
    {
        yield return null;

        System.Action accion = _pendiente;
        _pendiente = null;
        accion?.Invoke();
    }

    // ---------- utilidades ----------

    private void Completar(StoryModel story)
    {
        if (story != null && flag != StoryFlag.None) story.ActivateFlag(flag);
    }

    // Mete los objetos de la parte en la mochila y devuelve lo que dio
    // ("Poción x2"). Null = no da nada. Publico porque omitir el tutorial da lo
    // mismo que hacerlo: saltearlo no deja al jugador sin sus Pociones.
    public string Entregar(PlayerModel player)
    {
        if (entrega == null || entrega.Length == 0 || player == null) return null;

        var dados = new List<string>();
        foreach (InteractableTrigger.EntregaDeObjeto e in entrega)
        {
            if (e == null || e.objeto == null || e.cantidad <= 0) continue;
            player.Inventory.AddHealItem(e.objeto, e.cantidad);
            dados.Add(e.cantidad > 1 ? $"{e.objeto.itemName} x{e.cantidad}" : e.objeto.itemName);
        }
        if (dados.Count > 0) Sonidos.ObjetoEncontrado();

        return dados.Count == 0 ? null : ReglasDelTutorial.Enumerar(dados);
    }

    private void Explicar(StoryModel story, System.Action despues)
    {
        if (explicacion == null || explicacion.lines == null || explicacion.lines.Length == 0)
        {
            despues?.Invoke();
            return;
        }

        if (dialogueController != null) dialogueController.StartDialogue(explicacion, story, despues);
        else CasinoDialogo.Decir(null, Hablante, Retrato, explicacion.lines, despues);
    }

    private void Decir(string linea, System.Action alCerrar = null)
        => CasinoDialogo.Decir(dialogueController, Hablante, Retrato, new[] { linea }, alCerrar);

#if UNITY_EDITOR
    // En la Scene view: el recuadro de la parte y que es. Sirve para ubicarla
    // sobre el mueble despues de pegarla en la habitacion decorada.
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : default;
        if (b.size.sqrMagnitude < 0.01f) b = new Bounds(transform.position, new Vector3(0.9f, 0.9f, 0f));

        Gizmos.color = tipo == Tipo.Pelea ? new Color(1f, 0.45f, 0.35f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.01f));
        UnityEditor.Handles.Label(new Vector3(b.min.x, b.max.y + 0.4f, 0f), $"{tipo}: {nombre}");
    }
#endif
}
