using UnityEngine;

// Base de toda pantalla de menu.
//
// Contrato: el MenuManager abre, cierra y le manda el input SOLO a la pantalla
// que esta arriba de la pila. Una pantalla nunca lee teclas por su cuenta ni se
// activa/desactiva sola: si lo hiciera, dos menus escucharian la misma tecla y
// volveriamos al problema que este sistema viene a resolver.
public abstract class MenuScreen : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("Identidad de esta pantalla. El MenuManager la busca por aca.")]
    [SerializeField] private MenuScreenId screenId;

    public MenuScreenId ScreenId => screenId;

    // Quien la abrio. Lo setea el MenuManager antes del OnOpen.
    public MenuManager Manager { get; internal set; }

    public bool IsOpen { get; private set; }

    // Contexto efectivo: el de la peticion si vino, si no el global del manager.
    public MenuContextKind Context { get; internal set; }

    // Resultado que se le devuelve a quien la abrio. Las subclases lo llenan
    // antes de cerrarse.
    protected object Result { get; set; }

    internal object OpenInternal(object payload, MenuContextKind context)
    {
        Context = context;
        Result = null;
        gameObject.SetActive(true);
        IsOpen = true;
        OnOpen(payload);
        return Result;
    }

    internal object CloseInternal()
    {
        OnClose();
        IsOpen = false;
        gameObject.SetActive(false);
        return Result;
    }

    // Se llama al abrir, con lo que haya mandado quien la abrio (puede ser null).
    protected virtual void OnOpen(object payload) { }

    // Se llama justo antes de desactivarse. Aca se suelta lo que se haya tomado
    // (escuchas de input, referencias a modelos).
    protected virtual void OnClose() { }

    // Vuelve a quedar arriba de la pila porque se cerro la que estaba encima.
    public virtual void OnFocusGained() { }

    // Otra pantalla se abrio encima.
    public virtual void OnFocusLost() { }

    // Input logico. Solo lo recibe la pantalla de arriba de la pila.
    public abstract void HandleAction(GameAction action);

    // Un frame de la pantalla que esta arriba (tiempo sin escalar).
    public virtual void OnTick(float unscaledDeltaTime) { }

    // Cierra esta pantalla y le devuelve el resultado a quien la abrio.
    protected void CloseSelf()
    {
        if (Manager != null) Manager.Close(this);
    }

    // ---------- atajos que usan casi todas las pantallas ----------

    // Aviso simple: se cierra con Aceptar. onClosed corre despues.
    protected void ShowMessage(string message, System.Action onClosed = null)
    {
        if (Manager == null || string.IsNullOrEmpty(message)) { onClosed?.Invoke(); return; }
        Manager.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = message },
            _ => onClosed?.Invoke());
    }

    // Pregunta de SI/NO.
    protected void Ask(string question, System.Action<bool> onAnswer)
    {
        if (Manager == null) { onAnswer?.Invoke(false); return; }
        Manager.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = question, IsQuestion = true },
            result => onAnswer?.Invoke(result is MessageBoxResult r && r.Confirmed));
    }

    // El jugador de la partida en curso. Todas las pantallas lo sacan de aca en
    // vez de recibirlo por Inspector: es lo que permite tirar el prefab en una
    // escena nueva y que ande sin configurar nada.
    protected PlayerModel Player
        => GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
}
