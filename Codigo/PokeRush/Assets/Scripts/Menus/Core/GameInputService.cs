using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Unico punto de entrada de input del juego.
//
// Por que existe: antes cada sistema construia sus propias InputAction con las
// teclas escritas a mano (PlayerInputController), asi que remapear una tecla
// obligaba a tocar codigo en varios archivos y el menu de Opciones no tendria
// nada que configurar. Aca las teclas viven en un solo mapa, se guardan en la
// misma base del save y cualquiera pregunta por GameAction, no por tecla.
//
// Se auto-inicializa: el primero que toque GameInputService.Instance crea un
// GameObject oculto que sobrevive los cambios de escena y lo tickea. No hay
// que agregar nada a ninguna escena.
public class GameInputService
{
    private static GameInputService _instance;
    public static GameInputService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameInputService();
                _instance.Initialize();
            }
            return _instance;
        }
    }

    // Ritmo de la repeticion al mantener una direccion apretada. Sin esto,
    // navegar una lista larga obliga a golpear la tecla una vez por fila.
    public const float RepeatDelay = 0.32f;
    public const float RepeatRate = 0.10f;

    // Separador de los bindings de una accion dentro del texto guardado.
    private const char BindingSeparator = '|';

    private readonly Dictionary<GameAction, InputAction> _actions =
        new Dictionary<GameAction, InputAction>();
    private readonly Dictionary<GameAction, string[]> _bindings =
        new Dictionary<GameAction, string[]>();
    private readonly Dictionary<GameAction, float> _heldTime =
        new Dictionary<GameAction, float>();
    private readonly Dictionary<GameAction, float> _nextRepeat =
        new Dictionary<GameAction, float>();
    private readonly HashSet<GameAction> _repeatPending = new HashSet<GameAction>();

    private int _lastTickFrame = -1;
    private int _listenStartFrame = -1;

    // Acciones ya "usadas" en el frame actual.
    //
    // Existe por un choque concreto: la Z es a la vez Menu (abrir) y Cancel
    // (volver). El MenuManager cerraba el menu con la Z y, en el MISMO frame,
    // el PlayerInputController veia esa misma Z todavia apretada, no habia
    // ningun menu abierto, y lo volvia a abrir. Resultado: el menu se abria una
    // vez y despues la tecla no hacia nada.
    //
    // El frame se limpia solo al consultarlo, y no en Tick(), para no depender
    // del orden de ejecucion de los scripts.
    private readonly HashSet<GameAction> _consumed = new HashSet<GameAction>();
    private int _consumedFrame = -1;

    // Mientras se espera que el jugador apriete una tecla para reasignarla,
    // TODO el input logico se apaga: si no, la tecla elegida ademas confirma el
    // renglon que la estaba pidiendo.
    public bool IsRebinding { get; private set; }

    // Avisa cuando cambian los bindings (lo escucha la UI de Opciones para
    // redibujar los nombres de tecla).
    public event Action BindingsChanged;

    private GameInputService() { }

    // ---------- bindings por defecto ----------

    // El primer binding de cada accion es el "principal": es el que se muestra
    // y el unico que reasigna el menu de Opciones. Los que siguen son alias
    // (gamepad, teclas alternativas) y quedan siempre disponibles.
    public static string[] DefaultBindings(GameAction action)
    {
        switch (action)
        {
            case GameAction.Up: return new[] { "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up" };
            case GameAction.Down: return new[] { "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down" };
            case GameAction.Left: return new[] { "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left" };
            case GameAction.Right: return new[] { "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right" };
            case GameAction.Confirm: return new[] { "<Keyboard>/x", "<Keyboard>/enter", "<Gamepad>/buttonSouth" };
            case GameAction.Cancel: return new[] { "<Keyboard>/z", "<Keyboard>/backspace", "<Gamepad>/buttonEast" };
            case GameAction.Menu: return MenuBindingsFrom(DefaultBindings(GameAction.Cancel));
            case GameAction.Run: return new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Gamepad>/buttonWest" };
            case GameAction.PageLeft: return new[] { "<Keyboard>/q", "<Gamepad>/leftShoulder" };
            case GameAction.PageRight: return new[] { "<Keyboard>/e", "<Gamepad>/rightShoulder" };
            default: return new string[0];
        }
    }

    public static GameAction[] AllActions()
        => (GameAction[])Enum.GetValues(typeof(GameAction));

    // Acciones que tiene sentido ofrecer para reasignar en el menu de Opciones.
    // Las direcciones van juntas al principio para que la lista se lea como el
    // pad de un mando.
    //
    // Menu NO esta: no tiene tecla propia. La tecla de "Volver" es la que abre
    // el menu desde el mundo, que es lo que el jugador ya espera, y tenerla como
    // fila aparte solo servia para poder desincronizarlas.
    public static readonly GameAction[] RebindableActions =
    {
        GameAction.Up, GameAction.Down, GameAction.Left, GameAction.Right,
        GameAction.Confirm, GameAction.Cancel, GameAction.Run,
        GameAction.PageLeft, GameAction.PageRight
    };

    // Acciones que no se configuran porque salen de otra. Las salteamos al
    // buscar teclas repetidas: Menu comparte tecla con Volver a proposito, y
    // avisar de ese choque seria avisar de algo que el jugador no puede evitar.
    public static bool IsDerived(GameAction action) => action == GameAction.Menu;

    // Atajos fijos que abren el menu ademas de la tecla de Volver. No se
    // reasignan y ninguna otra accion puede quedarse con ellos.
    private static readonly string[] MenuShortcuts =
    {
        "<Keyboard>/escape", "<Gamepad>/start"
    };

    public static bool IsMenuShortcut(string path)
    {
        foreach (string atajo in MenuShortcuts)
            if (string.Equals(atajo, path, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Menu = la tecla de Volver + los atajos fijos.
    private static string[] MenuBindingsFrom(string[] cancelPaths)
    {
        var paths = new List<string>(cancelPaths);
        foreach (string atajo in MenuShortcuts)
            if (!paths.Contains(atajo)) paths.Add(atajo);
        return paths.ToArray();
    }

    // Etiqueta que se muestra en el menu de Opciones para cada accion.
    public static string DisplayLabel(GameAction action)
    {
        switch (action)
        {
            case GameAction.Up: return "Arriba";
            case GameAction.Down: return "Abajo";
            case GameAction.Left: return "Izquierda";
            case GameAction.Right: return "Derecha";
            case GameAction.Confirm: return "Aceptar";
            case GameAction.Cancel: return "Volver / Menú";
            case GameAction.Menu: return "Menu";
            case GameAction.Run: return "Correr";
            case GameAction.PageLeft: return "Pagina anterior";
            case GameAction.PageRight: return "Pagina siguiente";
            default: return action.ToString();
        }
    }

    // ---------- ciclo de vida ----------

    private void Initialize()
    {
        foreach (GameAction action in AllActions())
        {
            _heldTime[action] = -1f;
            _nextRepeat[action] = 0f;

            // Las derivadas se arman abajo. Se borra lo que haya guardado de
            // cuando Menu era configurable, asi no revive una tecla vieja.
            if (IsDerived(action))
            {
                SettingsService.Instance.Delete(SettingKey(action));
                continue;
            }

            string guardado = SettingsService.Instance.GetString(SettingKey(action), null);
            string[] paths = ParseBindings(guardado) ?? DefaultBindings(action);
            _bindings[action] = paths;
            _actions[action] = BuildAction(action, paths);
        }

        SyncMenuConVolver();

        if (Application.isPlaying) GameInputRuntime.Ensure();
    }

    private static string SettingKey(GameAction action) => "input." + action;

    private static string[] ParseBindings(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return null;
        string[] partes = texto.Split(BindingSeparator);
        var limpio = new List<string>();
        foreach (string p in partes)
            if (!string.IsNullOrWhiteSpace(p)) limpio.Add(p.Trim());
        return limpio.Count > 0 ? limpio.ToArray() : null;
    }

    private static InputAction BuildAction(GameAction action, string[] paths)
    {
        var input = new InputAction(action.ToString(), InputActionType.Button);
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            input.AddBinding(path);
        }
        input.Enable();
        return input;
    }

    // Lo llama GameInputRuntime una vez por frame. El guard por frameCount deja
    // que cualquiera lo llame sin miedo a acelerar la repeticion al doble.
    public void Tick(float unscaledDeltaTime)
    {
        if (_lastTickFrame == Time.frameCount) return;
        _lastTickFrame = Time.frameCount;

        _repeatPending.Clear();
        if (IsRebinding) return;

        foreach (GameAction action in AllActions())
        {
            if (!_actions.TryGetValue(action, out InputAction input) || input == null) continue;

            if (!input.IsPressed())
            {
                _heldTime[action] = -1f;
                continue;
            }

            // Recien apretada: el primer disparo lo entrega WasPressed, aca solo
            // arranca el cronometro de la repeticion.
            if (_heldTime[action] < 0f)
            {
                _heldTime[action] = 0f;
                _nextRepeat[action] = RepeatDelay;
                continue;
            }

            _heldTime[action] += unscaledDeltaTime;
            if (_heldTime[action] >= _nextRepeat[action])
            {
                _nextRepeat[action] = _heldTime[action] + RepeatRate;
                _repeatPending.Add(action);
            }
        }
    }

    // ---------- lectura ----------

    public bool WasPressed(GameAction action)
    {
        if (IsRebinding) return false;
        if (EstaConsumida(action)) return false;
        return _actions.TryGetValue(action, out InputAction input)
               && input != null && input.WasPressedThisFrame();
    }

    // Marca la accion como ya atendida en este frame: nadie mas la va a ver.
    public void Consume(GameAction action)
    {
        LimpiarSiCambioElFrame();
        _consumed.Add(action);
    }

    public void Consume(params GameAction[] actions)
    {
        LimpiarSiCambioElFrame();
        foreach (GameAction a in actions) _consumed.Add(a);
    }

    private bool EstaConsumida(GameAction action)
    {
        LimpiarSiCambioElFrame();
        return _consumed.Contains(action);
    }

    private void LimpiarSiCambioElFrame()
    {
        if (_consumedFrame == Time.frameCount) return;
        _consumedFrame = Time.frameCount;
        _consumed.Clear();
    }

    public bool IsPressed(GameAction action)
    {
        if (IsRebinding) return false;
        return _actions.TryGetValue(action, out InputAction input)
               && input != null && input.IsPressed();
    }

    // Apretada ahora O repitiendo por mantenerla. Es lo que usan las listas.
    public bool WasPressedOrRepeated(GameAction action)
    {
        if (EstaConsumida(action)) return false;
        return WasPressed(action) || _repeatPending.Contains(action);
    }

    // Direccion de navegacion como vector (para el movimiento en el mundo).
    public Vector2 ReadDirection()
    {
        if (IsRebinding) return Vector2.zero;
        float x = (IsPressed(GameAction.Right) ? 1f : 0f) - (IsPressed(GameAction.Left) ? 1f : 0f);
        float y = (IsPressed(GameAction.Up) ? 1f : 0f) - (IsPressed(GameAction.Down) ? 1f : 0f);
        Vector2 dir = new Vector2(x, y);

        // El stick analogico no pasa por las acciones de direccion (son botones):
        // se lee aparte para no perder el gamepad en el mundo.
        Gamepad pad = Gamepad.current;
        if (pad != null && dir == Vector2.zero) dir = pad.leftStick.ReadValue();
        return dir;
    }

    // ---------- configuracion ----------

    public string[] GetBindings(GameAction action)
        => _bindings.TryGetValue(action, out string[] paths) ? paths : new string[0];

    // Nombre legible de la tecla principal, para mostrar en Opciones.
    public string GetDisplayName(GameAction action)
    {
        string[] paths = GetBindings(action);
        return paths.Length == 0 ? "---" : DisplayNameForPath(paths[0]);
    }

    // Lo mismo para un control suelto: "<Keyboard>/leftShift" -> "LEFT SHIFT".
    // Lo usa el aviso de tecla repetida, que tiene el path pero no la accion.
    public static string DisplayNameForPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "---";
        string legible = InputControlPath.ToHumanReadableString(
            path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        return string.IsNullOrEmpty(legible) ? path : legible.ToUpperInvariant();
    }

    public bool UsesPath(GameAction action, string path)
    {
        foreach (string actual in GetBindings(action))
            if (string.Equals(actual, path, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Cuantos controles le quedan a una accion. Opciones lo mira antes de
    // ofrecer sacarle una tecla: dejarla en cero la mostraria como "---" y
    // encima el guardado vacio le devolveria los defaults al proximo arranque.
    public int BindingCount(GameAction action) => GetBindings(action).Length;

    // Acciones que YA tienen ese control, sea como tecla principal o como
    // alias. Sin esto, Opciones deja poner la misma tecla en las diez filas y
    // el juego se queda sin forma de navegar.
    public List<GameAction> FindConflicts(string path, GameAction exceptuada)
    {
        var choques = new List<GameAction>();
        if (string.IsNullOrEmpty(path)) return choques;

        foreach (GameAction action in AllActions())
        {
            if (action == exceptuada || IsDerived(action)) continue;
            if (UsesPath(action, path)) choques.Add(action);
        }
        return choques;
    }

    // Reemplaza SOLO la tecla principal y conserva los alias: reasignar
    // "Aceptar" a la K no deberia hacerte perder el boton del gamepad.
    public void SetPrimaryBinding(GameAction action, string path)
    {
        if (string.IsNullOrEmpty(path) || IsDerived(action)) return;

        string[] actuales = GetBindings(action);
        var nuevos = new List<string> { path };
        for (int i = 1; i < actuales.Length; i++)
            if (!string.Equals(actuales[i], path, StringComparison.OrdinalIgnoreCase))
                nuevos.Add(actuales[i]);

        SaveBindings(action, nuevos.ToArray());
    }

    // Le saca ese control a una accion (lo pide Opciones cuando el jugador
    // acepta robarle la tecla a otra fila). Si era la principal, el primer
    // alias pasa a mostrarse. Nunca la deja sin ninguno.
    public void RemoveBinding(GameAction action, string path)
    {
        if (string.IsNullOrEmpty(path) || IsDerived(action)) return;

        string[] actuales = GetBindings(action);
        var nuevos = new List<string>();
        foreach (string actual in actuales)
            if (!string.Equals(actual, path, StringComparison.OrdinalIgnoreCase))
                nuevos.Add(actual);

        // No la tenia, o es la unica que le queda: no se toca nada.
        if (nuevos.Count == actuales.Length || nuevos.Count == 0) return;

        SaveBindings(action, nuevos.ToArray());
    }

    private void SaveBindings(GameAction action, string[] paths)
    {
        ApplyBindings(action, paths);
        SettingsService.Instance.SetString(SettingKey(action),
            string.Join(BindingSeparator.ToString(), paths));

        // Menu no se guarda: sigue a Volver siempre.
        if (action == GameAction.Cancel) SyncMenuConVolver();

        BindingsChanged?.Invoke();
    }

    private void SyncMenuConVolver()
        => ApplyBindings(GameAction.Menu,
            MenuBindingsFrom(GetBindings(GameAction.Cancel)));

    public void ResetToDefaults()
    {
        foreach (GameAction action in AllActions())
        {
            if (IsDerived(action)) continue;
            ApplyBindings(action, DefaultBindings(action));
            SettingsService.Instance.Delete(SettingKey(action));
        }
        SyncMenuConVolver();
        BindingsChanged?.Invoke();
    }

    private void ApplyBindings(GameAction action, string[] paths)
    {
        if (_actions.TryGetValue(action, out InputAction vieja) && vieja != null)
        {
            vieja.Disable();
            vieja.Dispose();
        }
        _bindings[action] = paths;
        _actions[action] = BuildAction(action, paths);
        _heldTime[action] = -1f;
    }

    // ---------- captura de una tecla para reasignar ----------
    //
    // No se usa InputSystem.onAnyButtonPress: ese observable vive en una
    // extension del paquete que cambia de namespace entre versiones. Consultar
    // los controles a mano es dos lineas mas y no se rompe al actualizar.

    // Arranca la escucha. Mientras dure, todas las acciones logicas devuelven
    // false (ver IsRebinding), asi la tecla elegida no confirma nada.
    public void BeginListening()
    {
        IsRebinding = true;
        // Se saltea el frame en curso: la tecla que abrio el modo "apreta una
        // tecla" sigue registrada como recien apretada y se auto-asignaria.
        _listenStartFrame = Time.frameCount;
    }

    public void CancelListening()
    {
        IsRebinding = false;
    }

    // Devuelve el path del primer control apretado, o null si todavia no se
    // apreto nada. Lo llama la pantalla de Opciones una vez por frame.
    public string PollPressedControl()
    {
        if (!IsRebinding || Time.frameCount <= _listenStartFrame) return null;

        Keyboard teclado = Keyboard.current;
        if (teclado != null)
        {
            foreach (KeyControl tecla in teclado.allKeys)
                if (tecla.wasPressedThisFrame) return "<Keyboard>/" + tecla.name;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            foreach (InputControl control in pad.allControls)
            {
                if (control is ButtonControl boton && boton.wasPressedThisFrame)
                    return "<Gamepad>/" + control.name;
            }
        }

        return null;
    }

}
