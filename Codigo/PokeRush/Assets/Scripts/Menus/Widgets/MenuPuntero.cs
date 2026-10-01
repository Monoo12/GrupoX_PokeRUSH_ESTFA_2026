using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Hace que un elemento de menu se pueda usar tambien con el mouse.
//
// Los menus se manejan con GameAction (teclado o joystick) y la pantalla es la
// unica que decide que pasa: esto no cambia eso, solo AVISA. Pasar el puntero
// por encima es "el cursor esta aca" y el clic es "aceptar aca". La pantalla
// se suscribe y hace lo mismo que con las teclas, asi el mouse no abre un
// camino distinto que se pueda desincronizar.
//
// Lo unico que resuelve por su cuenta es el estado APRETADO (se achica mientras
// el boton esta abajo), que con teclado no existe. Necesita un Graphic con
// Raycast Target en el objeto o en un hijo, y un GraphicRaycaster en el Canvas
// (el de MenuSystem ya lo tiene).
[DisallowMultipleComponent]
public class MenuPuntero : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Tooltip("Cuanto se achica mientras se aprieta. 1 = no se mueve.")]
    [SerializeField, Range(0.8f, 1f)] private float escalaApretado = 0.94f;

    // El puntero entro: la pantalla mueve su cursor hasta aca.
    public event Action Entro;
    // Clic izquierdo completo (apretar y soltar encima): la pantalla acepta.
    public event Action Clic;

    private Vector3 _escalaBase = Vector3.one;
    private bool _apretado;

    private void Awake() => _escalaBase = transform.localScale;

    public void OnPointerEnter(PointerEventData e) => Entro?.Invoke();

    public void OnPointerExit(PointerEventData e) => Soltar();

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _apretado = true;
        transform.localScale = _escalaBase * escalaApretado;
    }

    public void OnPointerUp(PointerEventData e) => Soltar();

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        Clic?.Invoke();
    }

    // Si la pantalla se cierra con el boton apretado, la proxima vez que se
    // abra tiene que estar del tamanio de siempre.
    private void OnDisable() => Soltar();

    private void Soltar()
    {
        if (!_apretado) return;
        _apretado = false;
        transform.localScale = _escalaBase;
    }
}
