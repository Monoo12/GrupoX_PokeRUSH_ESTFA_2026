using UnityEngine;

// Que hace falta para poder subir con el ascensor.
//
// Los valores nuevos van SIEMPRE al final: las escenas guardan el requisito
// como numero.
public enum CasinoRequisitoTipo
{
    // Sube siempre. Para probar, o para un piso sin condicion.
    Ninguno,
    // Hay que tener juntada cierta plata.
    DineroMinimo,
    // Hay que haber cumplido un objetivo que activo su propia mecanica (piso 1:
    // el pase del guardia; piso 2: el del guardia VIP).
    Flag,
    // Hay que tener un ejemplar de cierta especie, en el equipo o en el PC, y
    // opcionalmente shiny (piso 3: el Cosmog shiny de Tacconi).
    TenerPokemon
}

// Un piso al que se puede ir con el ascensor.
[System.Serializable]
public class CasinoPisoDestino
{
    [Tooltip("Nombre EXACTO de la escena (tiene que estar en Build Settings).")]
    public string escena;
    [Tooltip("Como se lee en la opcion. Ej: 'PISO 1'.")]
    public string nombre;
}

// El ascensor que conecta los pisos del casino.
//
// Es el unico que decide si se puede subir: la condicion no vive en la puerta
// dibujada ni en la UI, asi el equipo de nivel puede mover, cambiar o rehacer la
// puerta sin tocar la progresion. Se cuelga de un objeto con Collider2D pegado a
// las puertas, en una capa que este dentro del interactableMask del jugador.
//
// Cuando la condicion se cumple una vez, queda registrada en un StoryFlag (que
// SaveService ya guarda): el jugador no tiene que volver a juntarla si baja y
// sube, ni si cierra el juego.
//
// Bajar no pide nada: volver a un piso anterior tiene que poder hacerse siempre
// (a jugar a las tragaperras por plata, a la enfermeria, a buscar a un NPC). La
// deuda solo frena la subida, porque bloquear la bajada podria dejar al jugador
// encerrado sin forma de juntar la plata para pagarla.
[RequireComponent(typeof(Collider2D))]
public class CasinoElevatorInteractable : MonoBehaviour, IInteractable
{
    [Header("A donde lleva")]
    [Tooltip("Nombre EXACTO de la escena del piso siguiente (tiene que estar en Build Settings). " +
             "Vacio = este ascensor solo baja.")]
    [SerializeField] private string escenaDestino = "";
    [Tooltip("Nombre del piso, para los carteles. Ej: 'el piso 2'.")]
    [SerializeField] private string nombreDelDestino = "el piso siguiente";

    [Header("Bajar")]
    [Tooltip("Pisos a los que se puede bajar, del mas cercano al mas lejano. Bajar no pide nada.")]
    [SerializeField] private CasinoPisoDestino[] pisosDeAbajo = new CasinoPisoDestino[0];

    [Header("Condicion para subir")]
    [SerializeField] private CasinoRequisitoTipo requisito = CasinoRequisitoTipo.DineroMinimo;
    [Tooltip("Plata que hay que tener juntada, con requisito = DineroMinimo.")]
    [Min(0)] [SerializeField] private int dineroRequerido = 500;
    [Tooltip("Flag que hay que tener, con requisito = Flag.")]
    [SerializeField] private StoryFlag flagRequerido = StoryFlag.None;
    [Tooltip("Especie que hay que tener, con requisito = TenerPokemon.")]
    [SerializeField] private PokenDefinition especieRequerida;
    [Tooltip("Con requisito = TenerPokemon: ademas tiene que ser shiny.")]
    [SerializeField] private bool debeSerShiny = true;

    [Header("Progreso")]
    [Tooltip("Flag que se activa al cumplir la condicion por primera vez. " +
             "Con esto el piso queda superado para siempre.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;

    [Header("Deuda")]
    [Tooltip("Con una deuda abierta con la enfermeria y plata para pagarla, el " +
             "ascensor no sube hasta que se pague. Sin plata sube igual: la deuda " +
             "queda anotada (si no, el jugador quedaria encerrado).")]
    [SerializeField] private bool bloqueaConDeuda = true;
    [Tooltip("Cartel cuando no paga la deuda pudiendo. {0} = lo que se debe.")]
    [SerializeField, TextArea(2, 3)] private string mensajeConDeuda =
        "El de seguridad te frena: primero pagale las {0} monedas a la enfermeria.";
    [Tooltip("Pregunta cuando hay deuda y alcanza la plata. {0} = lo que se debe.")]
    [SerializeField, TextArea(2, 3)] private string preguntaPagarDeuda =
        "Le debés {0} monedas a la enfermería y el de seguridad no te deja subir sin pagar. ¿Le pagás ahora?";

    [Header("Llegada")]
    [Tooltip("Donde aparece el jugador al llegar a ESTE piso por el ascensor, respecto de " +
             "este objeto. Por defecto, la celda de abajo: puertas en la pared de arriba. " +
             "Si las puertas estan en la pared de abajo (piso final), va (0, 1).")]
    [SerializeField] private Vector2 llegada = new Vector2(0f, -1f);

    [Header("Textos")]
    [SerializeField] private string mensajeSubiendo = "El ascensor se abre.";
    [SerializeField] private string mensajeBajando = "El ascensor baja.";
    [Tooltip("Cartel cuando falta plata. {0} = lo que falta, {1} = lo pedido.")]
    [SerializeField, TextArea(2, 3)] private string mensajeFaltaDinero =
        "Necesitas conseguir mas dinero. Te faltan {0}.";
    [SerializeField, TextArea(2, 3)] private string mensajeFaltaObjetivo =
        "El ascensor no se abre todavia. Te falta algo en este piso.";
    [Tooltip("Cartel cuando falta el Pokemon. {0} = la especie.")]
    [SerializeField, TextArea(2, 3)] private string mensajeFaltaPokemon =
        "El de seguridad no te deja subir: arriba solo entran los que tienen un {0} shiny.";
    [Tooltip("Preguntar antes de subir. Con esto en false sube de una.")]
    [SerializeField] private bool preguntarAntesDeSubir = true;

    public string EscenaDestino => escenaDestino;
    public CasinoRequisitoTipo Requisito => requisito;
    public PokenDefinition EspecieRequerida => especieRequerida;
    public CasinoPisoDestino[] PisosDeAbajo => pisosDeAbajo;
    public Vector3 PuntoDeLlegada => transform.position + (Vector3)llegada;

    // A que escena se esta viajando por ascensor. Lo lee el ascensor de ESA
    // escena al arrancar para dejar al jugador frente a sus puertas y no en el
    // spawn de la escena, que esta en la entrada del piso.
    private static string _llegandoA;
    private static SfxEnCurso _viaje;

    private void Start()
    {
        if (string.IsNullOrEmpty(_llegandoA) || _llegandoA != gameObject.scene.name) return;
        _llegandoA = null;

        var jugador = FindAnyObjectByType<PlayerController>();
        if (jugador != null)
            jugador.TeleportTo(PuntoDeLlegada, MirandoAlSalir());

        // El zumbido del viaje viene sonando desde el piso anterior (la fuente
        // sobrevive el cambio de escena): se corta y suena la campanita.
        _viaje?.Parar(0.25f);
        _viaje = null;
        Sonidos.AscensorLlega();
    }

    // Al salir del ascensor se mira hacia el piso, de espaldas a las puertas:
    // para el mismo lado en que quedo la llegada.
    private Vector2 MirandoAlSalir()
    {
        if (llegada == Vector2.zero) return Vector2.down;
        return Mathf.Abs(llegada.x) > Mathf.Abs(llegada.y)
            ? new Vector2(Mathf.Sign(llegada.x), 0f)
            : new Vector2(0f, Mathf.Sign(llegada.y));
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        bool haySubida = !string.IsNullOrEmpty(escenaDestino);
        bool hayBajada = CuantosPisosDeAbajo() > 0;

        if (!hayBajada)
        {
            IntentarSubir(player, story, preguntarAntesDeSubir);
            return true;
        }

        if (!haySubida)
        {
            ElegirPisoDeAbajo(0);
            return true;
        }

        PreguntarOpciones("¿Adónde vas?", "SUBIR", "BAJAR", eleccion =>
        {
            if (eleccion == Eleccion.Primera) IntentarSubir(player, story, false);
            else if (eleccion == Eleccion.Segunda) ElegirPisoDeAbajo(0);
        });
        return true;
    }

    // ---------- subir ----------

    private void IntentarSubir(PlayerModel player, StoryModel story, bool preguntar)
    {
        if (string.IsNullOrEmpty(escenaDestino))
        {
            Debug.LogError($"[Casino] El ascensor '{name}' no tiene escena de destino.");
            return;
        }

        // La deuda frena la subida si se puede pagar, incluso con el piso ya
        // superado: si no, bastaria con subir una vez para no pagarla nunca
        // mas. Sin plata para pagarla se sube igual (playtest del 5-oct: la
        // deuda trabada dejaba al jugador encerrado, sin plata ni peleas).
        if (bloqueaConDeuda && player.HasDebt && player.Money >= player.Debt)
        {
            Preguntar(string.Format(preguntaPagarDeuda, player.Debt), paga =>
            {
                if (!paga)
                {
                    Avisar(string.Format(mensajeConDeuda, player.Debt));
                    return;
                }
                player.PayDebt(player.Debt);
                IntentarSubir(player, story, preguntar);
            });
            return;
        }

        bool yaLoPaso = otorgaFlag != StoryFlag.None && story != null && story.HasFlag(otorgaFlag);

        if (!yaLoPaso && !CumpleRequisito(player, story, out string faltante))
        {
            Avisar(faltante);
            return;
        }

        // La primera vez que cumple, queda anotado.
        if (!yaLoPaso && otorgaFlag != StoryFlag.None && story != null)
            story.ActivateFlag(otorgaFlag);

        if (!preguntar) { Viajar(escenaDestino, mensajeSubiendo); return; }

        Preguntar($"Subir a {nombreDelDestino}?", acepto =>
        {
            if (acepto) Viajar(escenaDestino, mensajeSubiendo);
        });
    }

    // Publico para los tests: la condicion sin carteles ni escenas.
    public bool CumpleRequisito(PlayerModel player, StoryModel story, out string mensaje)
    {
        mensaje = null;

        switch (requisito)
        {
            case CasinoRequisitoTipo.Ninguno:
                return true;

            case CasinoRequisitoTipo.DineroMinimo:
                if (player != null && player.Money >= dineroRequerido) return true;
                mensaje = string.Format(mensajeFaltaDinero,
                    dineroRequerido - (player != null ? player.Money : 0), dineroRequerido);
                return false;

            case CasinoRequisitoTipo.Flag:
                if (flagRequerido == StoryFlag.None) return true;
                if (story != null && story.HasFlag(flagRequerido)) return true;
                mensaje = mensajeFaltaObjetivo;
                return false;

            case CasinoRequisitoTipo.TenerPokemon:
                if (especieRequerida == null) return true;
                if (PcReglas.TieneEspecie(player, especieRequerida, debeSerShiny)) return true;
                string especie = string.IsNullOrEmpty(especieRequerida.pokenName)
                    ? especieRequerida.name
                    : especieRequerida.pokenName;
                mensaje = string.Format(mensajeFaltaPokemon, especie);
                return false;
        }

        return true;
    }

    // ---------- bajar ----------

    private int CuantosPisosDeAbajo()
    {
        if (pisosDeAbajo == null) return 0;
        int n = 0;
        foreach (CasinoPisoDestino p in pisosDeAbajo)
            if (p != null && !string.IsNullOrEmpty(p.escena)) n++;
        return n;
    }

    private CasinoPisoDestino PisoDeAbajo(int i)
    {
        int n = 0;
        foreach (CasinoPisoDestino p in pisosDeAbajo)
        {
            if (p == null || string.IsNullOrEmpty(p.escena)) continue;
            if (n == i) return p;
            n++;
        }
        return null;
    }

    // La caja solo tiene dos opciones: con dos pisos se eligen de una, y con mas
    // se va de a uno ("PISO 2" o "MAS ABAJO").
    private void ElegirPisoDeAbajo(int desde)
    {
        int quedan = CuantosPisosDeAbajo() - desde;
        if (quedan <= 0) return;

        CasinoPisoDestino este = PisoDeAbajo(desde);
        if (quedan == 1)
        {
            Viajar(este.escena, mensajeBajando);
            return;
        }

        CasinoPisoDestino siguiente = PisoDeAbajo(desde + 1);
        string otra = quedan == 2 ? Nombre(siguiente) : "MÁS ABAJO";

        PreguntarOpciones("¿A qué piso bajás?", Nombre(este), otra, eleccion =>
        {
            if (eleccion == Eleccion.Primera) Viajar(este.escena, mensajeBajando);
            else if (eleccion == Eleccion.Segunda)
            {
                if (quedan == 2) Viajar(siguiente.escena, mensajeBajando);
                else ElegirPisoDeAbajo(desde + 1);
            }
        });
    }

    private static string Nombre(CasinoPisoDestino piso)
        => piso == null ? "" : (string.IsNullOrEmpty(piso.nombre) ? piso.escena : piso.nombre);

    // ---------- viaje ----------

    private void Viajar(string escena, string mensaje)
    {
        // El cambio de piso SI cambia de escena: son cuatro mapas distintos y es
        // lo que hace el resto del juego. Lo que no cambia de escena son los
        // minijuegos, que se abren encima del piso.
        //
        // El progreso viaja: GameManager es DontDestroyOnLoad, asi que el
        // dinero, el equipo y los flags cruzan solos.
        _llegandoA = escena;
        Avisar(mensaje);
        _viaje = Sonidos.AscensorViaja();
        SceneLoaderService.Instance.LoadScene(escena);
    }

    // ---------- cajas ----------

    // Los carteles salen por la caja de mensajes del sistema de menus, la misma
    // que usa el resto del juego.
    private void Avisar(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Casino] {mensaje}"); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje });
    }

    private void Preguntar(string pregunta, System.Action<bool> respuesta)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null) { respuesta?.Invoke(true); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = pregunta, IsQuestion = true },
            resultado => respuesta?.Invoke(resultado is MessageBoxResult r && r.Confirmed));
    }

    private enum Eleccion { Ninguna, Primera, Segunda }

    private void PreguntarOpciones(string pregunta, string primera, string segunda, System.Action<Eleccion> alElegir)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null) { alElegir?.Invoke(Eleccion.Primera); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = pregunta, IsQuestion = true, YesLabel = primera, NoLabel = segunda },
            resultado =>
            {
                // Volver no elige nada: sin esto, cancelar contaba como la
                // segunda opcion y te bajaba de piso.
                var r = resultado as MessageBoxResult;
                if (r == null || r.Cancelled) { alElegir?.Invoke(Eleccion.Ninguna); return; }
                alElegir?.Invoke(r.Confirmed ? Eleccion.Primera : Eleccion.Segunda);
            });
    }
}
