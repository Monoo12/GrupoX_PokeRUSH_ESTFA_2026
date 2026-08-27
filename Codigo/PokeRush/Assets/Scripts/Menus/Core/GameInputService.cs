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
            case GameAction.Menu: return new[] { "<Keyboard>/z", "<Keyboard>/escape", "<Gamepad>/start" };
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
    public static readonly GameAction[] RebindableActions =
    {
        GameAction.Up, GameAction.Down, GameAction.Left, GameAction.Right,
        GameAction.Confirm, GameAction.Cancel, GameAction.Menu, GameAction.Run,
        GameAction.PageLeft, GameAction.PageRight
    };

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
            case GameAction.Cancel: return "Volver";
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
            string guardado = SettingsService.Instance.GetString(SettingKey(action), null);
            string[] paths = ParseBindings(guardado) ?? DefaultBindings(action);
            _bindings[action] = paths;
            _actions[action] = BuildAction(action, paths);
            _heldTime[action] = -1f;
            _nextRepeat[action] = 0f;
        }

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
        if (paths.Length == 0) return "---";
        string legible = InputControlPath.ToHumanReadableString(
            paths[0], InputControlPath.HumanReadableStringOptions.OmitDevice);
        return string.IsNullOrEmpty(legible) ? paths[0] : legible.ToUpperInvariant();
    }

    // Reemplaza SOLO la tecla principal y conserva los alias: reasignar
    // "Aceptar" a la K no deberia hacerte perder el boton del gamepad.
    public void SetPrimaryBinding(GameAction action, string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        string[] actuales = GetBindings(action);
        var nuevos = new List<string> { path };
        for (int i = 1; i < actuales.Length; i++)
            if (actuales[i] != path) nuevos.Add(actuales[i]);

        ApplyBindings(action, nuevos.ToArray());
        SettingsService.Instance.SetString(SettingKey(action),
            string.Join(BindingSeparator.ToString(), nuevos));
        BindingsChanged?.Invoke();
    }

    public void ResetToDefaults()
    {
        foreach (GameAction action in AllActions())
        {
            ApplyBindings(action, DefaultBindings(action));
            SettingsService.Instance.Delete(SettingKey(action));
        }
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
