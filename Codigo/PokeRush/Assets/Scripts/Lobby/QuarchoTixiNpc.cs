using System.Collections;
using UnityEngine;

// Tixi, quien te recibe en la Quarcho Cueva.
//
// La PRIMERA vez que llegas habla solo, sin que te acerques: cuenta su parte
// del lore (el asset 'lore', que escribe el equipo), te da una ayuda para
// arrancar y te ofrece la tienda antes de mandarte al primer nivel. Recien
// ahi se activa Lobby_TixiPresentado, que es lo que abre la salida de la cueva.
//
// Al volver de cada nivel tambien habla sola, una vez: lo que cuenta sale de
// 'alVolver' (la primera charla que tenga algo para decir en esta partida; cada
// una se marca con su flag y no se repite). Asi el regreso del Data Center y el
// del casino se escriben en los assets.
//
// Despues es un menu: comprar, viajar al siguiente nivel, charlar o nada. El
// siguiente nivel lo decide RecorridoDeLaHistoria segun los flags, asi que Tixi
// no sabe de escenas.
//
// Cuando habla sola camina hasta el jugador (NpcQueCamina) y, cuando ya no
// queda ninguna caja ni menu abiertos, vuelve a su lugar. Mientras tanto el
// jugador queda frenado (PlayerInputController.Frenar). Si le hablas vos ya
// estas al lado: solo se da vuelta para mirarte.
//
// El sprite (Graphics/Characters/tixi) y el retrato (Graphics/Trainers/TIXI)
// salen del arte que paso el equipo (Graphics/Tixi).
[RequireComponent(typeof(Collider2D))]
public class QuarchoTixiNpc : MonoBehaviour, IInteractable
{
    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private DialogueController dialogueController;

    [Header("Tixi")]
    [SerializeField] private string nombre = "TIXI";
    [Tooltip("Retrato de la caja de dialogo. Provisorio.")]
    [SerializeField] private Sprite retrato;

    [Header("La primera vez")]
    [Tooltip("El lore que cuenta al conocerte. El texto lo escribe el equipo en el asset.")]
    [SerializeField] private DialogueDefinition lore;
    [Tooltip("Habla solo apenas llegas a la cueva por primera vez.")]
    [SerializeField] private bool hablaAlLlegar = true;
    [Tooltip("Segundos de espera antes de hablar solo, para que se vea la cueva.")]
    [Min(0f)] [SerializeField] private float esperaAlLlegar = 0.6f;
    [Tooltip("Plata que te da al presentarse, una sola vez. Con los 100 de arranque " +
             "no alcanza ni para una Pocion. 0 = no da nada.")]
    [Min(0)] [SerializeField] private int regaloDeBienvenida = 1000;
    [Tooltip("{0} = la plata.")]
    [SerializeField, TextArea(2, 4)] private string mensajeRegalo =
        "Tomá estas {0} monedas para arrancar. Con eso te alcanza para algo de mi tienda.";
    [SerializeField, TextArea(2, 4)] private string preguntaTienda = "Antes de salir, ¿querés ver lo que tengo?";
    [SerializeField] private string opcionVer = "Ver";
    [SerializeField] private string opcionDespues = "Después";

    [Header("Al volver de un nivel")]
    [Tooltip("Charlas que Tixi dice sola al llegar a la cueva. Suena la primera que " +
             "tenga algo para decir en esta partida (sus variantes deciden cuando) y " +
             "cada una se marca con su flag para no repetirse.")]
    [SerializeField] private DialogueDefinition[] alVolver = new DialogueDefinition[0];

    [Header("La tienda")]
    [Tooltip("Lo que vende. Usa 'healItemsForSale'; el precio es el buyPrice de cada objeto.")]
    [SerializeField] private VendorDefinition tienda;

    [Header("El menu")]
    [SerializeField, TextArea(2, 4)] private string saludo = "¿Qué necesitás?";
    [SerializeField] private string opcionComprar = "Comprar";
    [SerializeField] private string opcionViajar = "Viajar";
    [SerializeField] private string opcionCharlar = "Charlar";
    [SerializeField] private string opcionNada = "Nada";
    [Tooltip("Lo que dice al elegir Charlar. Vacio = el consejo de a donde ir.")]
    [SerializeField] private DialogueDefinition charla;
    [Tooltip("{0} = el siguiente nivel.")]
    [SerializeField, TextArea(2, 4)] private string consejo =
        "Lo que sigue es {0}. Cuando estés listo, salí por la puerta del este o pedime que te lleve.";
    [SerializeField, TextArea(2, 4)] private string consejoFinal = "Ya no queda nada por delante. Descansá.";
    [Tooltip("{0} = el nivel.")]
    [SerializeField, TextArea(2, 4)] private string preguntaViaje = "¿Te llevo hacia {0}?";
    [SerializeField] private string opcionIr = "Vamos";
    [SerializeField] private string opcionNoIr = "Todavía no";
    [SerializeField, TextArea(2, 4)] private string mensajeSinTienda = "Hoy no tengo nada para vender.";

    [Header("Caminar hasta vos")]
    [Tooltip("Al hablar sola se acerca al jugador y despues vuelve a su lugar. " +
             "Sin NpcQueCamina habla desde donde esta.")]
    [SerializeField] private bool caminaHastaVos = true;
    [Tooltip("Opcional: se busca en el mismo objeto.")]
    [SerializeField] private NpcQueCamina caminante;
    [Tooltip("Segundos sin cajas ni menus abiertos para dar la charla por terminada. " +
             "Entre una caja y la siguiente de la misma charla hay frames vacios.")]
    [Min(0.1f)] [SerializeField] private float pausaDeFin = 0.4f;

    public VendorDefinition Tienda => tienda;
    public int RegaloDeBienvenida => regaloDeBienvenida;
    // Ni hablando ni caminando: se le puede hablar. Publico para los tests.
    public bool Libre => !_presentandose && !_contandoVuelta && !_enEscena;

    private bool _presentandose;
    private float _llegada = -1f;
    private bool _contandoVuelta;
    // Desde que frena al jugador hasta que vuelve a su lugar.
    private bool _enEscena;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (caminante == null) caminante = GetComponent<NpcQueCamina>();
    }

    // Si la escena se apaga a mitad de la charla, el jugador no puede quedar frenado.
    private void OnDisable()
    {
        PlayerInputController.Soltar(this);
        _enEscena = false;
    }

    // ---------- la primera vez ----------

    private void Update()
    {
        if (_presentandose || _contandoVuelta || _enEscena) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Story == null || gm.CurrentPlayer == null) return;
        if (MenuManager.AnyOpen || (dialogueController != null && dialogueController.IsOpen)) return;

        if (_llegada < 0f) _llegada = Time.time;
        if (Time.time - _llegada < esperaAlLlegar) return;

        if (hablaAlLlegar)
        {
            if (!gm.Story.HasFlag(StoryFlag.Lobby_TixiPresentado)) { Presentarse(gm.CurrentPlayer, gm.Story); return; }
            hablaAlLlegar = false;
        }

        // Ya presentada: lo que haya para contar de la vuelta de un nivel.
        DialogueDefinition vuelta = VueltaQueToca(gm.Story);
        if (vuelta == null || dialogueController == null) return;

        _contandoVuelta = true;
        Debug.Log($"[Tixi] Cuenta la vuelta: {vuelta.name}.");
        StoryModel story = gm.Story;
        Conversar(() => dialogueController.StartDialogue(vuelta, story, () =>
        {
            _contandoVuelta = false;
            CasinoDialogo.Guardar();
        }), acercarse: true);
    }

    // La primera charla de 'alVolver' que tiene algo para decir. Null = nada.
    // Publico para los tests.
    public DialogueDefinition VueltaQueToca(StoryModel story)
    {
        if (alVolver == null || story == null || !story.HasFlag(StoryFlag.Lobby_TixiPresentado)) return null;
        foreach (DialogueDefinition d in alVolver)
            if (d != null && d.TieneQueDecir(story)) return d;
        return null;
    }

    private void Presentarse(PlayerModel player, StoryModel story)
    {
        _presentandose = true;

        System.Action despues = () =>
        {
            TerminarPresentacion(player, story);
            _presentandose = false;
            hablaAlLlegar = false;
        };

        Conversar(() =>
        {
            if (lore != null && dialogueController != null) dialogueController.StartDialogue(lore, story, despues);
            else Decir(lore != null ? lore.lines : new[] { "¡Llegaste! Te estaba esperando." }, despues);
        }, acercarse: true);
    }

    // Lo que pasa al cerrar el lore. Publico para los tests: flag, regalo y
    // guardado, sin cajas.
    public void TerminarPresentacion(PlayerModel player, StoryModel story)
    {
        if (story == null || story.HasFlag(StoryFlag.Lobby_TixiPresentado)) return;

        story.ActivateFlag(StoryFlag.Lobby_TixiPresentado);
        if (player != null && regaloDeBienvenida > 0)
        {
            player.AddMoney(regaloDeBienvenida);
            Sonidos.ObjetoRecibido();
        }
        CasinoDialogo.Guardar();

        if (dialogueController == null) return;

        if (regaloDeBienvenida > 0)
            Decir(new[] { string.Format(mensajeRegalo, regaloDeBienvenida) }, () => OfrecerTienda(story));
        else
            OfrecerTienda(story);
    }

    private void OfrecerTienda(StoryModel story)
    {
        Preguntar(preguntaTienda, opcionVer, opcionDespues, ver =>
        {
            if (ver) AbrirTienda(() => Aconsejar(story));
            else Aconsejar(story);
        });
    }

    // ---------- despues ----------

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null || !Libre) return false;

        if (!story.HasFlag(StoryFlag.Lobby_TixiPresentado))
        {
            Presentarse(player, story);
            return true;
        }

        // Ya estas al lado: no camina, solo te mira mientras charlan.
        Conversar(() => CasinoDialogo.Elegir(dialogueController, nombre, retrato, saludo,
            new[] { opcionComprar, opcionViajar, opcionCharlar, opcionNada },
            eleccion =>
            {
                switch (eleccion)
                {
                    case 0: AbrirTienda(null); break;
                    case 1:
                        ViajeEntreNiveles.Preguntar(dialogueController, nombre, retrato, story,
                            preguntaViaje, opcionIr, opcionNoIr);
                        break;
                    case 2: Charlar(story); break;
                }
            }), acercarse: false);
        return true;
    }

    // ---------- caminar hasta vos y volver ----------

    // Frena al jugador, se acerca (o solo lo mira), corre 'hablar' (que abre la
    // primera caja) y espera a que no quede nada abierto: la charla puede ser
    // una cadena (lore, regalo, pregunta, tienda, consejo) y no hay un unico
    // callback al final. Recien ahi vuelve a su lugar y suelta al jugador.
    //
    // No depender de callbacks tambien cubre DialogueController.Cancelar, que
    // cierra la caja sin avisar.
    private void Conversar(System.Action hablar, bool acercarse)
    {
        if (!isActiveAndEnabled) { hablar(); return; }
        StartCoroutine(ConversarRutina(hablar, acercarse));
    }

    private IEnumerator ConversarRutina(System.Action hablar, bool acercarse)
    {
        _enEscena = true;
        PlayerInputController.Frenar(this);
        PlayerController jugador = FindAnyObjectByType<PlayerController>();

        if (jugador != null)
        {
            // Que termine el paso que venia dando: cortarlo a mitad se ve como un salto.
            float limite = Time.unscaledTime + 1f;
            while (jugador.IsMoving && Time.unscaledTime < limite) yield return null;

            if (caminante != null)
            {
                if (acercarse && caminaHastaVos) yield return caminante.AcercarseA(jugador.transform);
                else caminante.Mirar(jugador.transform.position - transform.position);
            }

            // Y el jugador lo mira a el.
            if (!jugador.IsMoving)
                jugador.TeleportTo(jugador.transform.position, Cardinal(transform.position - jugador.transform.position));
        }

        hablar();

        float quieto = 0f;
        while (quieto < pausaDeFin)
        {
            yield return null;
            bool abierto = MenuManager.AnyOpen || (dialogueController != null && dialogueController.IsOpen);
            quieto = abierto ? 0f : quieto + Time.unscaledDeltaTime;
        }

        if (caminante != null)
            yield return caminante.VolverASuLugar(jugador != null ? jugador.transform : null);

        PlayerInputController.Soltar(this);
        _enEscena = false;
    }

    private static Vector2 Cardinal(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? Vector2.right : Vector2.left;
        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private void Charlar(StoryModel story)
    {
        if (charla != null && dialogueController != null) dialogueController.StartDialogue(charla, story);
        else Aconsejar(story);
    }

    private void Aconsejar(StoryModel story)
    {
        RecorridoDeLaHistoria.Nivel siguiente = RecorridoDeLaHistoria.SiguienteNivel(story);
        Decir(new[] { siguiente != null ? string.Format(consejo, siguiente.Nombre) : consejoFinal });
    }

    private void AbrirTienda(System.Action alCerrar)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null || tienda == null || tienda.healItemsForSale == null || tienda.healItemsForSale.Length == 0)
        {
            Decir(new[] { mensajeSinTienda }, alCerrar);
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Shop,
            new ShopScreenRequest { Vendedor = nombre, Objetos = tienda.healItemsForSale },
            resultado =>
            {
                if (resultado is ShopScreenResult r && r.Compras > 0) CasinoDialogo.Guardar();
                alCerrar?.Invoke();
            });
    }

    private void Decir(string[] lineas, System.Action alCerrar = null)
        => CasinoDialogo.Decir(dialogueController, nombre, retrato, lineas, alCerrar);

    private void Preguntar(string texto, string si, string no, System.Action<bool> respuesta)
        => CasinoDialogo.Preguntar(dialogueController, nombre, retrato, texto, si, no, respuesta);
}
