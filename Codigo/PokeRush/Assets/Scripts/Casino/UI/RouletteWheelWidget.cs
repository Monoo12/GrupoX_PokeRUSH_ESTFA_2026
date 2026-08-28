using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La rueda de premios del casino.
//
// Se arma con las piezas de Assets/Premios: el aro de madera, los sectores de
// colores, los dos aros de luces (apagadas y encendidas), el buje central, la
// aguja y las patas. El dibujo de los sectores son DOCE casillas de 30 grados
// centradas en el tope, asi que la rueda tiene doce lugares y cada premio ocupa
// tantos como pesa: el comun aparece en dos o tres y el raro en uno solo. Por
// eso la rueda no necesita decir ningun porcentaje.
//
// En cada lugar van el bicho (o el objeto), su nombre y lo que da.
public class RouletteWheelWidget : MonoBehaviour
{
    [Header("Piezas de la rueda")]
    [Tooltip("Aro de sectores de colores. Es lo que gira.")]
    [SerializeField] private RectTransform sectores;
    [Tooltip("Contenedor de los premios. Gira junto con los sectores.")]
    [SerializeField] private RectTransform contenido;
    [Tooltip("Aro de luces apagadas.")]
    [SerializeField] private Image lucesApagadas;
    [Tooltip("Aro de luces encendidas. Parpadea encima del otro.")]
    [SerializeField] private Image lucesEncendidas;

    [Header("Medidas (en fraccion del alto de la rueda)")]
    [Tooltip("Altura a la que va el nombre del premio, cerca del borde.")]
    [Range(0.1f, 0.5f)] [SerializeField] private float radioDelNombre = 0.315f;
    [Tooltip("Altura a la que va el bicho o el objeto.")]
    [Range(0.1f, 0.5f)] [SerializeField] private float radioDelIcono = 0.242f;
    // El buje central del dibujo tapa hasta el 0,15 del alto: lo que se ponga
    // mas adentro no se ve.
    [Tooltip("Altura a la que va lo que da el premio, apenas afuera del buje.")]
    [Range(0.05f, 0.4f)] [SerializeField] private float radioDelPremio = 0.178f;
    [SerializeField] private Vector2 tamanioDelIcono = new Vector2(40f, 40f);

    [Header("Luces")]
    [Tooltip("Cada cuanto parpadea el aro de luces, en segundos.")]
    [Min(0.05f)] [SerializeField] private float parpadeo = 0.4f;

    [Header("Tipografia")]
    [SerializeField] private Font fuente;
    [SerializeField] private int cuerpoDelNombre = 11;
    [SerializeField] private int cuerpoDelPremio = 10;

    // Que premio le toca a cada casilla, y al reves.
    private readonly List<int> _premioPorCasilla = new List<int>();
    private readonly Dictionary<int, List<int>> _casillasPorPremio = new Dictionary<int, List<int>>();

    private float _tiempoDeLuces;

    public int Casillas => _premioPorCasilla.Count;

    // Las casillas dibujadas en el arte: doce de treinta grados.
    public const int CasillasDelDibujo = 12;

    private void Update()
    {
        if (lucesEncendidas == null) return;

        _tiempoDeLuces += Time.unscaledDeltaTime;
        if (_tiempoDeLuces < parpadeo) return;

        _tiempoDeLuces = 0f;
        lucesEncendidas.enabled = !lucesEncendidas.enabled;
    }

    public void Construir(RouletteTableDefinition mesa)
    {
        Limpiar();
        if (mesa == null || mesa.premios == null || contenido == null) return;

        Repartir(mesa);
        if (_premioPorCasilla.Count == 0) return;

        float paso = 360f / _premioPorCasilla.Count;
        float alto = contenido.rect.height;

        for (int i = 0; i < _premioPorCasilla.Count; i++)
        {
            RoulettePrize premio = mesa.premios[_premioPorCasilla[i]];
            // Centro de la casilla: el sector 0 del dibujo esta centrado arriba.
            float grados = i * paso;

            Poner(Nombre(premio), grados, radioDelNombre * alto, cuerpoDelNombre,
                  new Color(1f, 1f, 1f, 0.95f), $"Nombre_{i:00}");

            PonerIcono(premio, grados, radioDelIcono * alto, $"Icono_{i:00}");

            Poner(Detalle(premio), grados, radioDelPremio * alto, cuerpoDelPremio,
                  new Color(1f, 0.95f, 0.7f, 0.95f), $"Detalle_{i:00}");
        }
    }

    // ---------- contenido de cada casilla ----------

    private void Poner(string texto, float grados, float radio, int cuerpo,
                       Color color, string nombreDelObjeto)
    {
        if (string.IsNullOrEmpty(texto)) return;

        var go = new GameObject(nombreDelObjeto, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        Colocar(rt, grados, radio, new Vector2(78f, 18f));

        var t = go.AddComponent<Text>();
        t.text = texto;
        t.font = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = cuerpo;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        var sombra = go.AddComponent<Outline>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sombra.effectDistance = new Vector2(1f, -1f);
    }

    private void PonerIcono(RoulettePrize premio, float grados, float radio, string nombreDelObjeto)
    {
        if (premio.icono == null) return;

        var go = new GameObject(nombreDelObjeto, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        Colocar(rt, grados, radio, tamanioDelIcono);

        var img = go.AddComponent<Image>();
        img.sprite = premio.icono;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    // Cada cosa se acuesta sobre su casilla: girada hacia afuera, como los
    // carteles de una rueda de feria.
    private void Colocar(RectTransform rt, float grados, float radio, Vector2 tamanio)
    {
        rt.SetParent(contenido, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanio;

        float radianes = grados * Mathf.Deg2Rad;
        rt.anchoredPosition = new Vector2(Mathf.Sin(radianes) * radio,
                                          Mathf.Cos(radianes) * radio);
        rt.localEulerAngles = new Vector3(0f, 0f, -grados);
    }

    private static string Nombre(RoulettePrize premio)
        => premio.nombre != null ? premio.nombre.ToUpperInvariant() : "";

    // Lo que da, en dos o tres caracteres: es la linea chica de adentro.
    private static string Detalle(RoulettePrize premio)
    {
        switch (premio.tipo)
        {
            case RoulettePrizeKind.Dinero: return "+" + premio.monto;
            case RoulettePrizeKind.PerderDinero: return "-" + premio.monto;
            case RoulettePrizeKind.Objeto: return premio.cantidad > 1 ? "x" + premio.cantidad : "";
            case RoulettePrizeKind.Poken: return "Nv" + premio.nivel;
            case RoulettePrizeKind.DanioAlEquipo: return "-" + premio.monto + "% PS";
            case RoulettePrizeKind.PerderObjeto: return "-1";
            default: return "";
        }
    }

    // ---------- reparto de casillas ----------

    // Un premio ocupa tantas casillas como pesa, con un minimo de una: si pesa
    // pero no entra en ninguna, nunca se lo veria salir.
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

        // Las casillas las manda el DIBUJO: son doce sectores pintados. Pedir
        // otra cantidad dejaria los premios corridos respecto de los colores.
        int casillas = CasillasDelDibujo;
        if (conPeso.Count > casillas)
        {
            Debug.LogWarning($"[Ruleta] La mesa tiene {conPeso.Count} premios y la rueda " +
                             $"dibujada solo {casillas} casillas: sobran premios.");
        }

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
            for (int k = 0; k < restantes - repartidas && sobrantes.Count > 0; k++)
                cantidad[sobrantes[k % sobrantes.Count].Key]++;
        }

        // Intercaladas: dos casillas iguales pegadas se leen como una sola
        // porcion grande y la rueda parece amanada.
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

    // ---------- giro ----------

    // Una casilla al azar de las que muestran ese premio. La probabilidad ya la
    // decidio el motor: esto solo elige donde frenar.
    public int CasillaDe(int premio, IBattleRng rng)
    {
        if (!_casillasPorPremio.TryGetValue(premio, out var lista) || lista.Count == 0)
            return -1;
        return lista[rng != null ? rng.Range(0, lista.Count) : 0];
    }

    public float AnguloDeCasilla(int casilla)
    {
        if (casilla < 0 || _premioPorCasilla.Count == 0) return 0f;
        return casilla * (360f / _premioPorCasilla.Count);
    }

    public void SetAngulo(float grados)
    {
        var giro = new Vector3(0f, 0f, grados);
        if (sectores != null) sectores.localEulerAngles = giro;
        if (contenido != null) contenido.localEulerAngles = giro;
    }

    private void Limpiar()
    {
        _premioPorCasilla.Clear();
        _casillasPorPremio.Clear();
        if (contenido == null) return;

        for (int i = contenido.childCount - 1; i >= 0; i--)
        {
            Transform hijo = contenido.GetChild(i);
            // Se desprende ANTES de destruir: Destroy no borra hasta el final
            // del frame y mientras tanto los viejos siguen contando como hijos.
            hijo.SetParent(null, false);
            Destroy(hijo.gameObject);
        }
    }
}
