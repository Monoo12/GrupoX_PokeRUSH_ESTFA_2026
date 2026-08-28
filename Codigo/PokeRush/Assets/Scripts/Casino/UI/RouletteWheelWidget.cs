using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La rueda de la ruleta.
//
// Se arma sola a partir de la mesa: una porcion por casilla, y cada porcion
// ocupa lo que pesa. Eso importa: el tamanio de la porcion ES su probabilidad,
// asi que la rueda no le miente al jugador. Si el equipo rebalancea la mesa
// desde el Inspector, la rueda cambia sola.
public class RouletteWheelWidget : MonoBehaviour
{
    [Tooltip("Circulo blanco: cada porcion es este sprite con relleno radial.")]
    [SerializeField] private Sprite circulo;
    [Tooltip("Contenedor que gira. Las porciones se crean adentro.")]
    [SerializeField] private RectTransform rueda;
    [Tooltip("Tapa del centro, para que la rueda se vea como un aro.")]
    [SerializeField] private RectTransform centro;

    private readonly List<float> _inicios = new List<float>();   // en vueltas (0..1)
    private readonly List<float> _tamanios = new List<float>();

    public int Casillas => _inicios.Count;

    public void Construir(RouletteTableDefinition mesa)
    {
        Limpiar();
        if (mesa == null || mesa.premios == null || rueda == null) return;

        int total = mesa.PesoTotal;
        if (total <= 0) return;

        float acumulado = 0f;
        foreach (RoulettePrize premio in mesa.premios)
        {
            if (premio == null || premio.peso <= 0) continue;

            float porcion = premio.peso / (float)total;

            var go = new GameObject($"Porcion_{premio.nombre}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(rueda, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Cada porcion arranca donde termino la anterior.
            rt.localEulerAngles = new Vector3(0f, 0f, -acumulado * 360f);

            var img = go.AddComponent<Image>();
            img.sprite = circulo;
            img.color = premio.color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = porcion;
            img.raycastTarget = false;

            _inicios.Add(acumulado);
            _tamanios.Add(porcion);
            acumulado += porcion;
        }

        // El centro se dibuja al final para tapar el vertice de las porciones.
        if (centro != null) centro.SetAsLastSibling();
    }

    // Angulo (en grados) al que hay que girar la rueda para que la casilla
    // quede justo debajo de la aguja, que apunta arriba.
    public float AnguloDe(int indiceDeCasilla)
    {
        if (indiceDeCasilla < 0 || indiceDeCasilla >= _inicios.Count) return 0f;

        float centroDeLaCasilla = _inicios[indiceDeCasilla] + _tamanios[indiceDeCasilla] * 0.5f;
        // Las porciones crecen en sentido horario desde arriba: para traer una
        // hasta la aguja hay que girar la rueda al reves.
        return centroDeLaCasilla * 360f;
    }

    public void SetAngulo(float grados)
    {
        if (rueda != null) rueda.localEulerAngles = new Vector3(0f, 0f, grados);
    }

    private void Limpiar()
    {
        _inicios.Clear();
        _tamanios.Clear();
        if (rueda == null) return;

        for (int i = rueda.childCount - 1; i >= 0; i--)
        {
            Transform hijo = rueda.GetChild(i);
            if (centro != null && hijo == centro) continue;
            Destroy(hijo.gameObject);
        }
    }
}
