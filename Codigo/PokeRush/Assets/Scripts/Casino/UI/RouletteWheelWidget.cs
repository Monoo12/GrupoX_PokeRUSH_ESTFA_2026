using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La rueda de la ruleta.
//
// Son casillas TODAS IGUALES, como una ruleta de verdad, no porciones de
// distinto tamanio: la rueda no es un grafico de probabilidades. Lo que hace que
// un premio salga mas seguido es cuantas casillas ocupa, igual que en una ruleta
// donde un numero esta repetido: el premio comun aparece en varias casillas y el
// raro en una sola. Asi se lee de un vistazo sin que diga ningun porcentaje.
//
// En cada casilla va el SIMBOLO del premio, no un numero.
public class RouletteWheelWidget : MonoBehaviour
{
    [Tooltip("Circulo blanco: cada casilla es este sprite con relleno radial.")]
    [SerializeField] private Sprite circulo;
    [Tooltip("Contenedor que gira. Las casillas y los simbolos se crean adentro.")]
    [SerializeField] private RectTransform rueda;
    [Tooltip("El disco de la ruleta (centro verde y filo), encima de las casillas. " +
             "Tiene el aro vaciado para que se vean los colores de abajo.")]
    [SerializeField] private RectTransform disco;

    [Header("Medidas del aro")]
    [Tooltip("A que altura del radio va el simbolo, en fraccion del alto de la rueda.")]
    [Range(0.4f, 0.95f)] [SerializeField] private float radioDelSimbolo = 0.38f;
    [SerializeField] private Vector2 tamanioDelSimbolo = new Vector2(26f, 26f);

    // Que premio le toca a cada casilla.
    private readonly List<int> _premioPorCasilla = new List<int>();
    // Y al reves: en que casillas esta cada premio.
    private readonly Dictionary<int, List<int>> _casillasPorPremio = new Dictionary<int, List<int>>();

    public int Casillas => _premioPorCasilla.Count;

    public void Construir(RouletteTableDefinition mesa)
    {
        Limpiar();
        if (mesa == null || mesa.premios == null || rueda == null) return;

        Repartir(mesa);
        if (_premioPorCasilla.Count == 0) return;

        int total = _premioPorCasilla.Count;
        float porcion = 1f / total;

        for (int i = 0; i < total; i++)
        {
            RoulettePrize premio = mesa.premios[_premioPorCasilla[i]];

            var go = new GameObject($"Casilla_{i:00}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(rueda, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localEulerAngles = new Vector3(0f, 0f, -i * porcion * 360f);

            var img = go.AddComponent<Image>();
            img.sprite = circulo;
            img.color = premio.color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = porcion;
            img.raycastTarget = false;

            CrearSimbolo(premio, i, porcion);
        }

        // El disco (el centro de la ruleta) va ADELANTE de todo: tapa el vertice
        // donde se juntan las casillas y deja ver solo el aro con los simbolos.
        if (disco != null) disco.SetAsLastSibling();
    }

    // Un premio ocupa tantas casillas como pesa, con un minimo de una: si pesa
    // pero no entra en ninguna casilla, nunca se lo veria salir.
    private void Repartir(RouletteTableDefinition mesa)
    {
        var conPeso = new List<int>();
        int pesoTotal = 0;
        for (int i = 0; i < mesa.premios.Length; i++)
        {
            RoulettePrize p = mesa.premios[i];
            if (p == null || p.peso <= 0) continue;
            conPeso.Add(i);
            pesoTotal += p.peso;
        }

        if (conPeso.Count == 0 || pesoTotal <= 0) return;

        int casillas = Mathf.Max(mesa.casillas, conPeso.Count);

        // Una para cada uno, y el resto por peso (resto mayor).
        var cantidad = new Dictionary<int, int>();
        foreach (int i in conPeso) cantidad[i] = 1;

        int restantes = casillas - conPeso.Count;
        if (restantes > 0)
        {
            var sobrantes = new List<KeyValuePair<int, float>>();
            int repartidas = 0;

            foreach (int i in conPeso)
            {
                float exacto = mesa.premios[i].peso / (float)pesoTotal * restantes;
                int entero = Mathf.FloorToInt(exacto);
                cantidad[i] += entero;
                repartidas += entero;
                sobrantes.Add(new KeyValuePair<int, float>(i, exacto - entero));
            }

            sobrantes.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int k = 0; k < restantes - repartidas; k++)
                cantidad[sobrantes[k % sobrantes.Count].Key]++;
        }

        // Se intercalan en vez de quedar todas juntas: una ruleta con un bloque
        // gigante del mismo color de un lado se ve mal y se siente amanada.
        var pendientes = new Dictionary<int, int>(cantidad);
        while (_premioPorCasilla.Count < casillas)
        {
            bool puso = false;
            foreach (int i in conPeso)
            {
                if (pendientes[i] <= 0) continue;
                pendientes[i]--;
                _premioPorCasilla.Add(i);
                puso = true;
                if (_premioPorCasilla.Count >= casillas) break;
            }
            if (!puso) break;
        }

        for (int casilla = 0; casilla < _premioPorCasilla.Count; casilla++)
        {
            int premio = _premioPorCasilla[casilla];
            if (!_casillasPorPremio.TryGetValue(premio, out var lista))
                _casillasPorPremio[premio] = lista = new List<int>();
            lista.Add(casilla);
        }
    }

    private void CrearSimbolo(RoulettePrize premio, int casilla, float porcion)
    {
        if (premio.icono == null) return;

        var go = new GameObject($"Simbolo_{casilla:00}", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(rueda, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanioDelSimbolo;

        // Centro de su casilla, medido desde arriba y en sentido horario.
        float grados = (casilla + 0.5f) * porcion * 360f;
        float radianes = grados * Mathf.Deg2Rad;
        float radio = rueda.rect.height * radioDelSimbolo;

        rt.anchoredPosition = new Vector2(Mathf.Sin(radianes) * radio,
                                          Mathf.Cos(radianes) * radio);
        // Apunta hacia afuera, como los numeros de una ruleta.
        rt.localEulerAngles = new Vector3(0f, 0f, -grados);

        // Chapita oscura atras: sin ella, un simbolo claro sobre una casilla
        // clara (la estrella sobre el amarillo) directamente no se ve.
        if (circulo != null)
        {
            var fondo = new GameObject("Fondo", typeof(RectTransform));
            var fondoRt = (RectTransform)fondo.transform;
            fondoRt.SetParent(rt, false);
            fondoRt.anchorMin = fondoRt.anchorMax = new Vector2(0.5f, 0.5f);
            fondoRt.pivot = new Vector2(0.5f, 0.5f);
            fondoRt.sizeDelta = tamanioDelSimbolo + new Vector2(6f, 6f);

            var fondoImg = fondo.AddComponent<Image>();
            fondoImg.sprite = circulo;
            fondoImg.color = new Color(0f, 0f, 0f, 0.35f);
            fondoImg.raycastTarget = false;
        }

        var icono = new GameObject("Icono", typeof(RectTransform));
        var iconoRt = (RectTransform)icono.transform;
        iconoRt.SetParent(rt, false);
        iconoRt.anchorMin = iconoRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconoRt.pivot = new Vector2(0.5f, 0.5f);
        iconoRt.sizeDelta = tamanioDelSimbolo;

        var img = icono.AddComponent<Image>();
        img.sprite = premio.icono;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    // Una casilla al azar de las que muestran ese premio. La probabilidad ya la
    // decidio el motor: esto solo elige donde frenar para que lo que se ve
    // coincida con lo que salio.
    public int CasillaDe(int premio, IBattleRng rng)
    {
        if (!_casillasPorPremio.TryGetValue(premio, out var lista) || lista.Count == 0)
            return -1;
        return lista[rng != null ? rng.Range(0, lista.Count) : 0];
    }

    // Cuanto hay que girar para dejar esa casilla debajo de la aguja.
    public float AnguloDeCasilla(int casilla)
    {
        if (casilla < 0 || _premioPorCasilla.Count == 0) return 0f;
        return (casilla + 0.5f) / _premioPorCasilla.Count * 360f;
    }

    public void SetAngulo(float grados)
    {
        if (rueda != null) rueda.localEulerAngles = new Vector3(0f, 0f, grados);
    }

    private void Limpiar()
    {
        _premioPorCasilla.Clear();
        _casillasPorPremio.Clear();
        if (rueda == null) return;

        for (int i = rueda.childCount - 1; i >= 0; i--)
        {
            Transform hijo = rueda.GetChild(i);
            if (disco != null && hijo == disco) continue;

            // Se desprende ANTES de destruir: Destroy no borra hasta el final
            // del frame, y mientras tanto los viejos siguen contando como hijos.
            // Con eso, el orden de dibujo quedaba mezclado y el disco terminaba
            // en medio de las casillas, tapando media rueda.
            hijo.SetParent(null, false);
            Destroy(hijo.gameObject);
        }
    }
}
