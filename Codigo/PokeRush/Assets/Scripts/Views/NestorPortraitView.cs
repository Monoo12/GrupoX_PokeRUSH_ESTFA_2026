using UnityEngine;
using UnityEngine.UI;

// Retrato de Nestor durante los combates del nivel 2. Recibe un
// NestorPortraitSnapshot (datos puros) y decide como se ve. No conoce
// NestorPredictionSystem, no conoce el combate y no toma ninguna decision.
//
// Misma regla que BattlePredictionView: TODAS las referencias son opcionales.
// Si falta una, esa parte no se dibuja y el resto sigue andando. Eso permite
// rehacer el retrato entero a mano desde Unity —otro marco, otro tamaño, un
// globo de dialogo con cola, animaciones— sin tocar una linea de logica.
//
// Para reemplazarlo por una version mas linda: duplicar el GameObject, armar
// la jerarquia que se quiera, arrastrar los campos que existan y listo.
public class NestorPortraitView : MonoBehaviour
{
    [Header("Raiz")]
    [Tooltip("Se prende y apaga entero. Si esta vacio se usa este GameObject.")]
    [SerializeField] private GameObject root;

    [Header("Retrato")]
    [Tooltip("Image donde va la cara. Solo cabeza y pecho: los sprites de " +
             "cuerpo entero no entran en el recuadro.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("Indice = enum NestorMood (0=Neutral, 1=Pensando, 2=Concentrado, " +
             "3=Confiado, 4=Sorprendido, 5=Preocupado, 6=Furioso).")]
    [SerializeField] private Sprite[] retratosPorAnimo;
    [Tooltip("Marco/fondo del recuadro. Se tiñe cuando Nestor interviene.")]
    [SerializeField] private Image marco;

    [Header("Globo de dialogo")]
    [Tooltip("Se prende solo en los turnos en los que Nestor dice algo.")]
    [SerializeField] private GameObject globo;
    [SerializeField] private Text dialogoText;

    [Header("Colores")]
    [SerializeField] private Color colorTranquilo = new Color(0.16f, 0.17f, 0.24f, 0.85f);
    [SerializeField] private Color colorDestacado = new Color(1f, 0.78f, 0.25f, 0.95f);

    [Header("Latido cuando interviene (opcional)")]
    [SerializeField] private CanvasGroup pulseGroup;
    [SerializeField] private float pulseSpeed = 4.5f;
    [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.3f;

    [Header("Empujon de escala al intervenir (opcional)")]
    [Tooltip("Cuanto crece el recuadro en el turno de la lectura. 1 = no crece.")]
    [SerializeField, Range(1f, 1.4f)] private float escalaDestacado = 1.12f;
    [SerializeField] private float velocidadDeEscala = 8f;

    private bool _pulsing;
    private Vector3 _escalaBase = Vector3.one;
    private float _escalaObjetivo = 1f;

    private void Awake()
    {
        if (root == null) root = gameObject;
        _escalaBase = root.transform.localScale;

        // OJO: aca NO se apaga nada, y es a proposito.
        //
        // 'root' es normalmente ESTE mismo GameObject, y el builder lo deja
        // guardado desactivado. Un objeto inactivo no recibe Awake, asi que
        // esto corre recien cuando Render() lo prende con SetActive(true) —
        // que dispara Awake en el acto. Un root.SetActive(false) aca lo
        // volveria a apagar en el medio de ese Render y se comeria justo la
        // primera lectura de Nestor del combate.
        //
        // Que arranque escondido lo garantiza el prefab, y ademas
        // BattleController llama a Render con Activa=false al empezar todo
        // combate que no sea del nivel 2, o sea que se esconde solo igual.
    }

    private void Update()
    {
        if (root == null) return;

        if (_pulsing && pulseGroup != null)
            pulseGroup.alpha = 1f - pulseDepth * (0.5f - 0.5f * Mathf.Cos(Time.time * pulseSpeed));

        // El empujon vuelve solo: si el combate corta en medio de una lectura
        // (fin de turno, KO, fin de pelea) el recuadro no queda agrandado.
        Vector3 objetivo = _escalaBase * _escalaObjetivo;
        if (root.transform.localScale != objetivo)
            root.transform.localScale = Vector3.Lerp(
                root.transform.localScale, objetivo, Time.deltaTime * velocidadDeEscala);
    }

    // Unico punto de entrada. Snapshot.Activa == false lo apaga.
    public void Render(NestorPortraitSnapshot snapshot)
    {
        if (root == null) root = gameObject;

        if (!snapshot.Activa)
        {
            Hide();
            return;
        }

        root.SetActive(true);

        if (portraitImage != null)
        {
            Sprite cara = SpriteDe(snapshot.Animo);
            portraitImage.sprite = cara;
            portraitImage.enabled = cara != null;
        }

        // El globo aparece SOLO cuando hay frase. La mayoria de los turnos no
        // hay, y eso es lo que hace que cuando habla se lea.
        bool hayDialogo = !string.IsNullOrEmpty(snapshot.Dialogo);
        if (dialogoText != null) dialogoText.text = snapshot.Dialogo;
        if (globo != null) globo.SetActive(hayDialogo);
        else if (dialogoText != null) dialogoText.gameObject.SetActive(hayDialogo);

        if (marco != null)
            marco.color = snapshot.Destacado ? colorDestacado : colorTranquilo;

        _pulsing = snapshot.Destacado && pulseGroup != null;
        if (!_pulsing && pulseGroup != null) pulseGroup.alpha = 1f;

        _escalaObjetivo = snapshot.Destacado ? escalaDestacado : 1f;
    }

    public void Hide()
    {
        _pulsing = false;
        _escalaObjetivo = 1f;
        if (pulseGroup != null) pulseGroup.alpha = 1f;
        // El globo no es hijo de 'root' en el prefab: sin esto, apagar a Nestor
        // con la pelea en curso (la de Julieta lo desconecta) dejaba su ultima
        // frase flotando en pantalla sin el retrato.
        if (globo != null) globo.SetActive(false);
        else if (dialogoText != null) dialogoText.gameObject.SetActive(false);
        if (root != null)
        {
            root.transform.localScale = _escalaBase;
            root.SetActive(false);
        }
    }

    private Sprite SpriteDe(NestorMood animo)
    {
        int index = (int)animo;
        if (retratosPorAnimo == null || retratosPorAnimo.Length == 0) return null;

        // Un animo sin sprite asignado cae al primero de la lista en vez de
        // dejar el recuadro vacio: es preferible una cara repetida a un hueco.
        if (index < 0 || index >= retratosPorAnimo.Length || retratosPorAnimo[index] == null)
            return retratosPorAnimo[0];

        return retratosPorAnimo[index];
    }
}
