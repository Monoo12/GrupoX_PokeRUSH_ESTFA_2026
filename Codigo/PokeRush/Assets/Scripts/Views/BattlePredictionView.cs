using UnityEngine;
using UnityEngine.UI;

// Presentacion de la Prediccion de Nestor. Recibe un PredictionSnapshot (datos
// puros) y decide como se ve. No conoce PredictionState, no conoce el combate y
// no toma ninguna decision de reglas.
//
// TODAS las referencias son opcionales: si falta una, esa parte simplemente no
// se dibuja. Eso permite rehacer el panel entero a mano desde Unity —cambiar
// sprites, layout, tamaños, agregar animaciones— sin tocar una linea de logica,
// que es justo lo que se pidio.
//
// Para reemplazarla por una version mas linda: duplicar el GameObject, armar la
// jerarquia que se quiera, arrastrar los campos que existan y listo.
public class BattlePredictionView : MonoBehaviour
{
    [Header("Raiz")]
    [Tooltip("Se prende y apaga entero. Si esta vacio se usa este GameObject.")]
    [SerializeField] private GameObject root;

    [Header("Textos (todos opcionales)")]
    [Tooltip("Renglon principal: 'LECTURA DE NESTOR' o 'ANULADO: ATACAR'.")]
    [SerializeField] private Text titleText;
    [Tooltip("Renglon secundario: cuantos turnos faltan o el aviso anticipado.")]
    [SerializeField] private Text detailText;

    [Header("Icono de la categoria (opcional)")]
    [Tooltip("Indice = enum PredictedCategory (0=None, 1=Ataque, 2=Cambio, 3=Objeto, 4=Estado).")]
    [SerializeField] private Sprite[] iconsByCategory;
    [SerializeField] private Image categoryIcon;

    [Header("Progreso hasta la proxima lectura (opcional)")]
    [Tooltip("Image en modo Filled. Se llena a medida que se acerca la lectura.")]
    [SerializeField] private Image progressFill;

    [Header("Colores por estado")]
    [Tooltip("Se aplican a los Graphic de 'Tint Targets'. Si la lista esta " +
             "vacia no se tiñe nada y mandan los colores puestos a mano.")]
    [SerializeField] private Color colorTranquilo = new Color(0.85f, 0.85f, 0.90f);
    [SerializeField] private Color colorAviso = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color colorBloqueo = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private Graphic[] tintTargets;

    [Header("Latido cuando hay bloqueo (opcional)")]
    [SerializeField] private CanvasGroup pulseGroup;
    [SerializeField] private float pulseSpeed = 3.5f;
    [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.25f;

    private bool _pulsing;

    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);
    }

    private void Update()
    {
        if (!_pulsing || pulseGroup == null) return;
        pulseGroup.alpha = 1f - pulseDepth * (0.5f - 0.5f * Mathf.Cos(Time.time * pulseSpeed));
    }

    // Unico punto de entrada. Snapshot.Activa == false la apaga.
    public void Render(PredictionSnapshot snapshot)
    {
        if (root == null) root = gameObject;

        if (!snapshot.Activa)
        {
            Hide();
            return;
        }

        root.SetActive(true);

        if (titleText != null) titleText.text = snapshot.Titulo;
        if (detailText != null) detailText.text = snapshot.Detalle;

        // Icono: el de la categoria anulada, o el de la anticipada si solo hay aviso
        if (categoryIcon != null)
        {
            PredictedCategory aMostrar = snapshot.Categoria != PredictedCategory.None
                ? snapshot.Categoria
                : snapshot.Anticipada;
            Sprite icon = SpriteDe(aMostrar);
            categoryIcon.sprite = icon;
            categoryIcon.enabled = icon != null;
        }

        // Progreso: 1 cuando la lectura es este turno, baja a medida que se aleja
        if (progressFill != null)
        {
            int total = Mathf.Max(1, snapshot.TurnosEntreLecturas);
            float restante = Mathf.Clamp(snapshot.TurnosParaProxima, 0, total);
            progressFill.fillAmount = snapshot.HayBloqueo ? 1f : 1f - restante / total;
        }

        Color color = snapshot.HayBloqueo
            ? colorBloqueo
            : (snapshot.HayAviso ? colorAviso : colorTranquilo);
        if (tintTargets != null)
            foreach (Graphic g in tintTargets)
                if (g != null) g.color = color;

        _pulsing = snapshot.HayBloqueo && pulseGroup != null;
        if (!_pulsing && pulseGroup != null) pulseGroup.alpha = 1f;
    }

    public void Hide()
    {
        _pulsing = false;
        if (pulseGroup != null) pulseGroup.alpha = 1f;
        if (root != null) root.SetActive(false);
    }

    private Sprite SpriteDe(PredictedCategory categoria)
    {
        int index = (int)categoria;
        if (iconsByCategory == null || index < 0 || index >= iconsByCategory.Length) return null;
        return iconsByCategory[index];
    }
}
