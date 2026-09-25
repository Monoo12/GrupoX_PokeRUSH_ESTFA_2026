using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Caja de texto del mapa. Usa Text legacy a proposito: es lo mismo que usa el
// HUD de combate (BarraMensajes), asi los dos se ven igual sin arrastrar TMP.
//
// Ademas de mostrar lineas, sabe mostrar una pregunta con opciones. La
// seleccion se dibuja con un prefijo y un color: no hace falta un objeto
// "selector" aparte, aunque se puede asignar uno si se quiere una flecha.
//
// Con dos opciones (Si/No) usa el panel tal cual esta armado en la escena. Con
// mas (los menus de los NPCs de la Quarcho Cueva) clona el ultimo renglon las
// veces que haga falta y apoya el panel ARRIBA de la caja, a la derecha, como
// la ventana de opciones de los juegos: asi no tapa el texto de la pregunta.
public class DialogueView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Text speakerNameText;
    [SerializeField] private Image speakerPortraitImage;
    [SerializeField] private Text dialogueLineText;

    [Header("Opciones")]
    [Tooltip("Contenedor de las opciones. Se prende solo cuando hay pregunta.")]
    [SerializeField] private GameObject choicePanel;
    [Tooltip("Un Text por opcion. Alcanza con dos: si hacen falta mas se clona el ultimo.")]
    [SerializeField] private Text[] choiceTexts = new Text[2];

    [Header("Estilo de la seleccion")]
    [SerializeField] private string prefijoSeleccionado = "> ";
    [SerializeField] private Color colorSeleccionado = Color.white;
    [SerializeField] private Color colorNormal = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("Pista de avance")]
    [Tooltip("Cartelito tipo 'X para seguir'. Opcional.")]
    [SerializeField] private GameObject continueHint;

    // Los renglones de verdad: los del Inspector mas los clonados.
    private readonly List<Text> _renglones = new List<Text>();
    private RectTransform _panel;
    private bool _medido;
    // Como vino armado el panel, para volver a dejarlo igual con dos opciones.
    private Vector2 _anclaMin, _anclaMax, _pivote, _posicion, _tamanio;
    // Distancia entre renglones y margen de arriba, medidos de los dos Text del
    // prefab. Con uno solo se usan los del DialogoCanvas (26 y 8).
    private float _paso = 26f;
    private float _margen = 8f;
    private float _altoDeRenglon = 24f;
    // Primera opcion visible cuando no entran todas.
    private int _inicio;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void DisplayLine(string speakerName, Sprite portrait, string line)
    {
        if (speakerNameText != null)
        {
            speakerNameText.text = speakerName;
            // Sin nombre no dejamos el hueco: queda un cartel de sistema.
            speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(speakerName));
        }

        if (dialogueLineText != null) dialogueLineText.text = line;

        if (speakerPortraitImage != null)
        {
            if (portrait != null)
            {
                speakerPortraitImage.sprite = portrait;
                speakerPortraitImage.gameObject.SetActive(true);
            }
            else
            {
                speakerPortraitImage.gameObject.SetActive(false);
            }
        }
    }

    // Prende el panel de opciones y marca cual esta elegida.
    //
    // Si arriba de la caja no entran todas (una ventana de juego muy ancha deja
    // el Canvas bajito), se muestran las que entran y la lista se corre con la
    // seleccion. Las flechas avisan que hay mas arriba o abajo.
    public void ShowChoices(string[] opciones, int seleccion)
    {
        Medir();
        int cuantas = opciones != null ? opciones.Length : 0;
        int visibles = Mathf.Min(cuantas, FilasQueEntran(cuantas));
        if (seleccion < _inicio) _inicio = seleccion;
        if (seleccion >= _inicio + visibles) _inicio = seleccion - visibles + 1;
        _inicio = Mathf.Clamp(_inicio, 0, Mathf.Max(0, cuantas - visibles));
        AsegurarRenglones(visibles);

        if (choicePanel != null) choicePanel.SetActive(true);
        if (continueHint != null) continueHint.SetActive(false);

        float masAncho = 0f;
        for (int i = 0; i < _renglones.Count; i++)
        {
            Text renglon = _renglones[i];
            if (renglon == null) continue;

            bool hay = i < visibles;
            renglon.gameObject.SetActive(hay);
            if (!hay) continue;

            int opcion = _inicio + i;
            bool elegida = opcion == seleccion;
            string flecha = i == 0 && _inicio > 0 ? MasArriba
                : i == visibles - 1 && opcion < cuantas - 1 ? MasAbajo : "";
            renglon.text = (elegida ? prefijoSeleccionado : "") + opciones[opcion] + flecha;
            renglon.color = elegida ? colorSeleccionado : colorNormal;

            // El ancho se mide con el prefijo puesto: si no, la opcion mas
            // larga se corta justo cuando el cursor pasa por ella.
            string medida = renglon.text;
            if (!elegida) renglon.text = prefijoSeleccionado + opciones[opcion] + flecha;
            masAncho = Mathf.Max(masAncho, renglon.preferredWidth);
            renglon.text = medida;
        }

        AcomodarPanel(visibles, masAncho, cuantas > choiceTexts.Length);
    }

    private const string MasArriba = "  ^";
    private const string MasAbajo = "  v";

    // Cuantas opciones entran en la ventana de arriba de la caja. Con las que
    // entran en el panel de Si/No no se calcula nada: van adentro de la caja.
    private int FilasQueEntran(int cuantas)
    {
        if (cuantas <= choiceTexts.Length) return cuantas;

        Canvas canvas = GetComponentInParent<Canvas>();
        var caja = transform as RectTransform;
        if (canvas == null || caja == null) return cuantas;

        float altoDelCanvas = ((RectTransform)canvas.rootCanvas.transform).rect.height;
        float disponible = altoDelCanvas - caja.rect.height - 8f;
        int filas = Mathf.FloorToInt((disponible - _margen * 2f - _altoDeRenglon) / _paso) + 1;
        return Mathf.Clamp(filas, choiceTexts.Length, cuantas);
    }

    public void HideChoices()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        if (continueHint != null) continueHint.SetActive(true);
    }

    // ---------- mas de dos opciones ----------

    private void Medir()
    {
        if (_medido) return;
        _medido = true;

        foreach (Text t in choiceTexts)
            if (t != null) _renglones.Add(t);

        _panel = choicePanel != null ? choicePanel.transform as RectTransform : null;
        if (_panel != null)
        {
            _anclaMin = _panel.anchorMin;
            _anclaMax = _panel.anchorMax;
            _pivote = _panel.pivot;
            _posicion = _panel.anchoredPosition;
            _tamanio = _panel.sizeDelta;
        }

        if (_renglones.Count > 0)
        {
            var primero = (RectTransform)_renglones[0].transform;
            _margen = Mathf.Abs(primero.anchoredPosition.y);
            _altoDeRenglon = Mathf.Max(1f, primero.sizeDelta.y);
        }
        if (_renglones.Count > 1)
        {
            var a = (RectTransform)_renglones[0].transform;
            var b = (RectTransform)_renglones[1].transform;
            float paso = Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y);
            if (paso > 1f) _paso = paso;
        }
    }

    private void AsegurarRenglones(int cuantas)
    {
        while (_renglones.Count < cuantas && _renglones.Count > 0)
        {
            Text modelo = _renglones[_renglones.Count - 1];
            Text nuevo = Instantiate(modelo, modelo.transform.parent);
            nuevo.name = "Opcion" + _renglones.Count;

            var rt = (RectTransform)nuevo.transform;
            rt.anchoredPosition = ((RectTransform)modelo.transform).anchoredPosition - new Vector2(0f, _paso);
            _renglones.Add(nuevo);
        }
    }

    // 'filas' = los renglones que se ven. 'lista' = hay mas opciones que las del panel
    // de Si/No: va la ventana de arriba de la caja aunque se vean dos.
    private void AcomodarPanel(int filas, float anchoDelTexto, bool lista)
    {
        if (_panel == null) return;

        if (!lista)
        {
            _panel.anchorMin = _anclaMin;
            _panel.anchorMax = _anclaMax;
            _panel.pivot = _pivote;
            _panel.anchoredPosition = _posicion;
            _panel.sizeDelta = _tamanio;
            return;
        }

        // Ventana arriba de la caja, contra el borde derecho. El ancho sale de
        // la opcion mas larga: "Ruleta de Pokémon ($400)" no entra en los 104
        // del panel de Si/No.
        float ancho = Mathf.Max(_tamanio.x, anchoDelTexto + 24f);
        float alto = _margen * 2f + _paso * (filas - 1) + _altoDeRenglon;

        _panel.anchorMin = new Vector2(1f, 1f);
        _panel.anchorMax = new Vector2(1f, 1f);
        _panel.pivot = new Vector2(1f, 0f);
        _panel.anchoredPosition = new Vector2(_posicion.x, 4f);
        _panel.sizeDelta = new Vector2(ancho, alto);
    }
}
