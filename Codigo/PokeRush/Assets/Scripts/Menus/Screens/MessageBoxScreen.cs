using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Caja de texto de los menus: avisos, confirmaciones y el "Guardando...".
//
// Es una pantalla mas de la pila, no un panel que cada menu prende por su
// cuenta. Asi, mientras esta abierta, el menu de abajo deja de recibir teclas
// solo: no hace falta que nadie se acuerde de desactivarlo.
public class MessageBoxScreen : MenuScreen
{
    [Header("Referencias")]
    [SerializeField] private Text messageText;
    [Tooltip("Contenedor de los botones SI/NO. Se apaga en los avisos simples.")]
    [SerializeField] private GameObject choiceRoot;
    [SerializeField] private MenuOptionWidget yesOption;
    [SerializeField] private MenuOptionWidget noOption;
    [Tooltip("Flechita/indicador de 'segui'. Se prende en los avisos simples.")]
    [SerializeField] private GameObject continueHint;

    private MessageBoxRequest _request;
    private readonly MessageBoxResult _result = new MessageBoxResult();
    private bool _yesSelected;
    private float _autoCloseLeft;

    protected override void OnOpen(object payload)
    {
        _request = payload as MessageBoxRequest ?? new MessageBoxRequest { Message = payload as string };
        _result.Confirmed = false;
        Result = _result;

        if (messageText != null) messageText.text = _request.Message ?? string.Empty;

        bool pregunta = _request.IsQuestion;
        if (choiceRoot != null) choiceRoot.SetActive(pregunta);
        if (continueHint != null) continueHint.SetActive(!pregunta && _request.ClosableByPlayer);

        if (pregunta)
        {
            if (yesOption != null) yesOption.SetText(_request.YesLabel);
            if (noOption != null) noOption.SetText(_request.NoLabel);
            // Arranca en NO: si la pregunta es destructiva (salir sin guardar),
            // machacar la tecla de aceptar no deberia contestar que si.
            _yesSelected = false;
            RefreshSelection();
        }

        _autoCloseLeft = _request.AutoCloseSeconds;
    }

    public override void OnTick(float unscaledDeltaTime)
    {
        if (_autoCloseLeft <= 0f) return;

        _autoCloseLeft -= unscaledDeltaTime;
        if (_autoCloseLeft <= 0f) CloseSelf();
    }

    public override void HandleAction(GameAction action)
    {
        if (!_request.ClosableByPlayer) return;

        if (_request.IsQuestion)
        {
            switch (action)
            {
                case GameAction.Up:
                case GameAction.Left:
                case GameAction.Down:
                case GameAction.Right:
                    _yesSelected = !_yesSelected;
                    RefreshSelection();
                    return;

                case GameAction.Confirm:
                    _result.Confirmed = _yesSelected;
                    CloseSelf();
                    return;

                case GameAction.Cancel:
                    _result.Confirmed = false;
                    CloseSelf();
                    return;
            }
            return;
        }

        // Aviso simple: cualquiera de las dos teclas lo cierra. Obligar a usar
        // solo Aceptar para descartar un cartel se siente trabado.
        if (action == GameAction.Confirm || action == GameAction.Cancel)
        {
            _result.Confirmed = action == GameAction.Confirm;
            CloseSelf();
        }
    }

    // Cierre desde afuera, para la caja no interactiva del guardado.
    public void ForceClose() => CloseSelf();

    private void RefreshSelection()
    {
        if (yesOption != null) yesOption.SetSelected(_yesSelected);
        if (noOption != null) noOption.SetSelected(!_yesSelected);
    }
}
