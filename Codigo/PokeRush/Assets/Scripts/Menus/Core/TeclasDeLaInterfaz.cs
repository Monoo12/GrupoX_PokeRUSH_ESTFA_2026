using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Las mismas teclas en las pantallas hechas con botones de Unity (MenuSaves,
// la creacion de personaje) que en el resto del juego: X acepta, Z vuelve.
//
// Esas pantallas navegan con el EventSystem, que de fabrica solo acepta Enter,
// Espacio y el boton A ("submit") y no sabe nada de la X. Por eso en el
// playtest del 4-oct "a veces se aceptaba con X, a veces con Enter y a veces
// con el mouse". Esto suma la X (o la tecla que este en Opciones) sin tocar el
// EventSystem: aprieta el boton seleccionado.
public static class TeclasDeLaInterfaz
{
    // Enter, Espacio y el boton A ya los entrega el EventSystem: si se
    // aceptaran tambien aca, el boton se apretaria dos veces.
    public static bool LoManejaElEventSystem()
    {
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
               || (g != null && g.buttonSouth.wasPressedThisFrame);
    }

    // Aceptar: aprieta el boton seleccionado. True si lo hizo.
    public static bool Aceptar()
    {
        GameInputService input = GameInputService.Instance;
        if (input == null || !input.WasPressed(GameAction.Confirm) || LoManejaElEventSystem()) return false;
        input.Consume(GameAction.Confirm);

        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Button boton = sel != null ? sel.GetComponent<Button>() : null;
        if (boton == null || !boton.IsActive() || !boton.IsInteractable()) return false;
        boton.onClick.Invoke();
        return true;
    }

    // Volver: Z / Backspace / B, y Escape (que en estas pantallas siempre es volver).
    public static bool Volver()
    {
        GameInputService input = GameInputService.Instance;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!escape && (input == null || !input.WasPressed(GameAction.Cancel))) return false;
        input?.Consume(GameAction.Cancel, GameAction.Menu);
        return true;
    }
}
