using System;
using System.Collections.Generic;
using UnityEngine;

// El unico dueño del input de menus y de la pila de pantallas abiertas.
//
// Por que una pila y no "cada vista se prende y apaga sola": el enunciado pide
// que Pause -> Bolsa -> Objeto -> Party -> aplicar -> volver a Bolsa -> volver a
// Pause funcione sin que queden paneles duplicados ni inputs colgados. Con una
// pila eso sale gratis: solo la de arriba recibe teclas y cerrar siempre
// devuelve el foco a la de abajo.
//
// Se auto-instancia desde Resources/Menus/MenuSystem: una escena nueva no
// necesita agregar NADA para tener los menus.
public class MenuManager : MonoBehaviour
{
    public const string ResourcePath = "Menus/MenuSystem";

    private static MenuManager _instance;

    // Ojo: esto instancia el prefab si no existia. Usar HasInstance/AnyOpen para
    // preguntar sin crear nada.
    public static MenuManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            if (!Application.isPlaying) return null;

            // Puede haber quedado uno puesto a mano en la escena.
            _instance = FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);
            if (_instance != null) return _instance;

            MenuManager prefab = Resources.Load<MenuManager>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[Menus] Falta el prefab en Resources/{ResourcePath}. " +
                               "Sin el no se puede abrir ningun menu.");
                return null;
            }

            _instance = Instantiate(prefab);
            _instance.name = prefab.name;
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    // Lo pregunta PlayerInputController: con un menu abierto, el mundo no
    // escucha nada.
    public static bool AnyOpen => _instance != null && _instance._stack.Count > 0;

    [Header("Jerarquia")]
    [Tooltip("Donde se instancian las pantallas. Si queda vacio se usa el propio Canvas.")]
    [SerializeField] private RectTransform screenRoot;
    [Tooltip("Fondo oscuro que se muestra mientras haya al menos una pantalla abierta. Opcional.")]
    [SerializeField] private GameObject dimmer;

    [Header("Pantallas")]
    [Tooltip("Un prefab por pantalla. El id lo declara cada prefab en su propio " +
             "componente MenuScreen; el orden de esta lista no importa.")]
    [SerializeField] private List<MenuScreen> screenPrefabs = new List<MenuScreen>();

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioClip navigateClip;
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip cancelClip;

    // Contexto global: lo mueve BattleController al entrar/salir de combate.
    public MenuContextKind Context { get; private set; } = MenuContextKind.Overworld;

    private class Entry
    {
        public MenuScreen Screen;
        public Action<object> OnClosed;
    }

    private readonly List<Entry> _stack = new List<Entry>();

    // Instancias ya creadas por id. Se reusan; si hace falta abrir dos veces la
    // misma pantalla anidada (una caja de mensaje sobre otra) se crea otra.
    private readonly Dictionary<MenuScreenId, List<MenuScreen>> _pool =
        new Dictionary<MenuScreenId, List<MenuScreen>>();

    private readonly Dictionary<MenuScreenId, MenuScreen> _prefabsById =
        new Dictionary<MenuScreenId, MenuScreen>();

    public MenuScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1].Screen : null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (screenRoot == null) screenRoot = transform as RectTransform;

        foreach (MenuScreen prefab in screenPrefabs)
        {
            if (prefab == null) continue;
            if (_prefabsById.ContainsKey(prefab.ScreenId))
            {
                Debug.LogWarning($"[Menus] Hay dos prefabs con el id {prefab.ScreenId}. " +
                                 $"Se ignora '{prefab.name}'.");
                continue;
            }
            _prefabsById[prefab.ScreenId] = prefab;
        }

        if (dimmer != null) dimmer.SetActive(false);

        // Volumen guardado aplicado apenas arranca el juego, sin depender de que
        // alguien abra Opciones.
        GameSettings.ApplyOnce();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void SetContext(MenuContextKind context) => Context = context;

    // ---------- abrir / cerrar ----------

    public MenuScreen Open(MenuScreenId id, object payload = null, Action<object> onClosed = null)
    {
        MenuScreen screen = Take(id);
        if (screen == null) return null;

        if (_stack.Count > 0) _stack[_stack.Count - 1].Screen.OnFocusLost();

        screen.Manager = this;
        screen.transform.SetAsLastSibling();

        MenuContextKind context = ResolveContext(payload);
        _stack.Add(new Entry { Screen = screen, OnClosed = onClosed });

        RefreshDimmer();
        screen.OpenInternal(payload, context);
        return screen;
    }

    // Abre la pantalla si esta cerrada; la cierra si es la que esta arriba.
    public void Toggle(MenuScreenId id, object payload = null)
    {
        if (Top != null && Top.ScreenId == id) Close(Top);
        else if (_stack.Count == 0) Open(id, payload);
    }

    public void Close(MenuScreen screen)
    {
        int index = _stack.FindLastIndex(e => e.Screen == screen);
        if (index < 0) return;

        // Cerrar algo que no esta arriba tambien cierra lo que tenga encima: si
        // no, quedarian pantallas vivas sin nadie que les mande input.
        var cerradas = new List<Entry>();
        var resultados = new List<object>();
        for (int i = _stack.Count - 1; i >= index; i--)
        {
            Entry entry = _stack[i];
            _stack.RemoveAt(i);
            resultados.Add(entry.Screen.CloseInternal());
            Release(entry.Screen);
            cerradas.Add(entry);
        }

        if (_stack.Count > 0) _stack[_stack.Count - 1].Screen.OnFocusGained();
        RefreshDimmer();

        // Los callbacks van al FINAL y con la pila ya consistente: uno de ellos
        // puede abrir o cerrar otra pantalla (el "¿guardar?" que abre el
        // "guardando..."), y hacerlo a mitad del recorrido dejaba la pila
        // mutando debajo del propio bucle.
        for (int i = 0; i < cerradas.Count; i++)
            cerradas[i].OnClosed?.Invoke(resultados[i]);
    }

    public void CloseAll()
    {
        if (_stack.Count == 0) return;
        Close(_stack[0].Screen);
    }

    // El oscurecedor lo decide la pantalla RAIZ de la pila, no la de arriba.
    // Si mirara la de arriba, la caja de mensajes que abre la ruleta de premios
    // volveria a oscurecer el mapa que la ruleta justamente deja a la vista, y
    // el cartel del premio se veria sobre un fondo negro.
    private void RefreshDimmer()
    {
        if (dimmer == null) return;
        dimmer.SetActive(_stack.Count > 0 && _stack[0].Screen.DimsBackground);
    }

    private MenuContextKind ResolveContext(object payload)
    {
        switch (payload)
        {
            case BagScreenRequest bag when bag.Context.HasValue: return bag.Context.Value;
            case PartyScreenRequest party when party.Context.HasValue: return party.Context.Value;
            default: return Context;
        }
    }

    // ---------- pool de instancias ----------

    private MenuScreen Take(MenuScreenId id)
    {
        if (!_pool.TryGetValue(id, out List<MenuScreen> instancias))
        {
            instancias = new List<MenuScreen>();
            _pool[id] = instancias;
        }

        foreach (MenuScreen instancia in instancias)
            if (instancia != null && !instancia.IsOpen) return instancia;

        if (!_prefabsById.TryGetValue(id, out MenuScreen prefab) || prefab == null)
        {
            Debug.LogError($"[Menus] No hay prefab asignado para la pantalla {id}. " +
                           "Arrastralo a 'Screen Prefabs' del MenuManager.");
            return null;
        }

        MenuScreen nueva = Instantiate(prefab, screenRoot);
        nueva.name = prefab.name;
        nueva.gameObject.SetActive(false);
        instancias.Add(nueva);
        return nueva;
    }

    private void Release(MenuScreen screen)
    {
        // No se destruye: queda apagada en el pool para la proxima apertura.
        // Instanciar la mochila entera cada vez que se abre se nota.
        screen.transform.SetAsFirstSibling();
    }

    // ---------- input ----------

    private static readonly GameAction[] NavigationActions =
    {
        GameAction.Up, GameAction.Down, GameAction.Left, GameAction.Right
    };

    private void Update()
    {
        GameInputService input = GameInputService.Instance;
        input.Tick(Time.unscaledDeltaTime);

        if (_stack.Count == 0) return;

        MenuScreen top = _stack[_stack.Count - 1].Screen;
        top.OnTick(Time.unscaledDeltaTime);

        // La pantalla puede haberse cerrado dentro de su propio OnTick
        // (ej: la caja de "Guardando..." con cierre automatico).
        if (_stack.Count == 0 || _stack[_stack.Count - 1].Screen != top) return;

        foreach (GameAction nav in NavigationActions)
        {
            if (!input.WasPressedOrRepeated(nav)) continue;
            input.Consume(nav);
            PlayClip(navigateClip, Sonidos.Cursor);
            top.HandleAction(nav);
            if (_stack.Count == 0 || _stack[_stack.Count - 1].Screen != top) return;
        }

        if (input.WasPressed(GameAction.PageLeft)) { top.HandleAction(GameAction.PageLeft); if (Changed(top)) return; }
        if (input.WasPressed(GameAction.PageRight)) { top.HandleAction(GameAction.PageRight); if (Changed(top)) return; }

        if (input.WasPressed(GameAction.Confirm))
        {
            input.Consume(GameAction.Confirm);
            PlayClip(confirmClip, Sonidos.Confirmar);
            top.HandleAction(GameAction.Confirm);
            if (Changed(top)) return;
        }

        // Cancel y Menu comparten tecla por defecto (Z). Dentro de un menu la Z
        // siempre es "volver", y se consumen LAS DOS: si no, el mundo ve esa
        // misma Z despues de que el menu se cerro y lo vuelve a abrir en el
        // mismo frame.
        if (input.WasPressed(GameAction.Cancel) || input.WasPressed(GameAction.Menu))
        {
            input.Consume(GameAction.Cancel, GameAction.Menu);
            PlayClip(cancelClip, Sonidos.Cancelar);
            top.HandleAction(GameAction.Cancel);
        }
    }

    private bool Changed(MenuScreen top)
        => _stack.Count == 0 || _stack[_stack.Count - 1].Screen != top;

    // El clip asignado en el prefab manda; sin clip suena el del catalogo de
    // audio (lo normal: el prefab lo regenera un builder y no los trae).
    private static void PlayClip(AudioClip clip, Action delCatalogo)
    {
        if (clip != null) AudioService.Instance.PlaySFX(clip);
        else delCatalogo?.Invoke();
    }
}
