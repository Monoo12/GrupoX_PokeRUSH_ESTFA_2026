using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Le da a un boton de uGUI los estados que se sienten en un juego terminado:
//
//   encima / seleccionado   crece un poco y prende su 'resaltado'
//   apretado                se achica mientras el boton esta abajo
//   elegido                 queda marcado (una ficha que ya se eligio)
//   deshabilitado           se apaga a medias y no reacciona
//
// No reemplaza la transicion del Button (SpriteSwap o ColorTint, la que tenga):
// se suma a ella. Funciona igual con mouse, teclado o joystick, porque el
// teclado mueve la SELECCION del EventSystem y eso tambien cuenta como "encima".
// Todo en tiempo sin escalar: anda aunque el juego este en pausa.
[RequireComponent(typeof(Selectable))]
public class BotonAnimado : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Escala")]
    [SerializeField] private float escalaEncima = 1.05f;
    [SerializeField] private float escalaApretado = 0.95f;
    [Tooltip("Cuanto tarda en llegar a la escala nueva. Mas alto = mas rapido.")]
    [SerializeField] private float velocidad = 16f;

    [Header("Estados")]
    [Tooltip("Se prende con el cursor encima o con la seleccion del teclado (un marco, una flecha). Opcional.")]
    [SerializeField] private GameObject resaltado;
    [Tooltip("Se prende cuando la opcion quedo ELEGIDA, no solo señalada. Opcional.")]
    [SerializeField] private GameObject marcaDeElegido;
    [Tooltip("Transparencia cuando el boton no se puede usar.")]
    [SerializeField, Range(0f, 1f)] private float alfaDeshabilitado = 0.4f;

    private Selectable _selectable;
    private CanvasGroup _grupo;
    private Vector3 _escalaBase = Vector3.one;
    private bool _encima, _seleccionado, _apretado, _elegido;

    // Una ficha elegida queda grande y marcada aunque el cursor se vaya.
    public bool Elegido
    {
        get => _elegido;
        set
        {
            _elegido = value;
            if (marcaDeElegido != null) marcaDeElegido.SetActive(value);
        }
    }

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _escalaBase = transform.localScale;
        _grupo = GetComponent<CanvasGroup>();
        if (_grupo == null) _grupo = gameObject.AddComponent<CanvasGroup>();
        if (marcaDeElegido != null) marcaDeElegido.SetActive(_elegido);
    }

    private void OnDisable()
    {
        // Al volver a mostrarse no tiene que arrastrar un "apretado" viejo.
        _encima = _apretado = false;
        transform.localScale = _escalaBase;
        if (resaltado != null) resaltado.SetActive(false);
    }

    private void Update()
    {
        bool usable = _selectable == null || _selectable.IsInteractable();

        float objetivo = 1f;
        if (usable)
        {
            if (_apretado) objetivo = escalaApretado;
            else if (_encima || _seleccionado || _elegido) objetivo = escalaEncima;
        }

        float k = 1f - Mathf.Exp(-velocidad * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, _escalaBase * objetivo, k);

        float alfa = usable ? 1f : alfaDeshabilitado;
        _grupo.alpha = Mathf.MoveTowards(_grupo.alpha, alfa, Time.unscaledDeltaTime * 6f);

        if (resaltado != null)
        {
            bool prendido = usable && (_encima || _seleccionado);
            if (resaltado.activeSelf != prendido) resaltado.SetActive(prendido);
        }
    }

    public void OnPointerEnter(PointerEventData e) => _encima = true;
    public void OnPointerExit(PointerEventData e) { _encima = false; _apretado = false; }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) _apretado = true;
    }

    public void OnPointerUp(PointerEventData e) => _apretado = false;
    public void OnSelect(BaseEventData e) => _seleccionado = true;
    public void OnDeselect(BaseEventData e) => _seleccionado = false;
}
