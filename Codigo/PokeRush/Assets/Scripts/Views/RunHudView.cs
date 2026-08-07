using UnityEngine;
using UnityEngine.UI;

// HUD de la ruta: barra de PS, cadena de basicos y enfriamientos.
//
// No recicla el HUD clasico de combate: aquel esta armado alrededor de menus por
// turnos (LUCHAR / POKEMON / MOCHILA / HUIR) y aca no hay menus, hay estado que
// se lee de un vistazo mientras te movés.
//
// Los sprites salen del pack de UNITE, que trae su propio HUD: la barra de vida
// de su modo boss rush y el boton redondo de habilidad. Si faltan, cae a
// rectangulos planos y sigue funcionando.
//
// Se arma por codigo para que la escena de prueba salga de un click. La UI
// definitiva la hace el equipo.
public class RunHudView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RunCombatant jugador;
    [SerializeField] private AbilityRunner ejecutor;
    [SerializeField] private BasicAttackChain basico;

    [Header("Sprites del pack")]
    [SerializeField] private Sprite fondoBarra;
    [SerializeField] private Sprite rellenoBarra;
    [SerializeField] private Sprite marcoHabilidad;
    [SerializeField] private Sprite fondoPanel;

    [Header("Colores")]
    [SerializeField] private Color vidaAlta = new Color(0.35f, 0.92f, 0.45f);
    [SerializeField] private Color vidaMedia = new Color(0.98f, 0.80f, 0.22f);
    [SerializeField] private Color vidaBaja = new Color(0.95f, 0.28f, 0.28f);

    private Image _relleno;
    private Text _textoVida;
    private Text _textoCadena;
    private Image[] _iconos;
    private Image[] _sombras;
    private Outline[] _brillos;

    private void Start()
    {
        Construir();

        if (jugador != null)
        {
            jugador.VidaCambiada += RefrescarVida;
            if (jugador.Poken != null) RefrescarVida(jugador.Poken.CurrentHP, jugador.Poken.MaxHP);
        }
    }

    private void OnDestroy()
    {
        if (jugador != null) jugador.VidaCambiada -= RefrescarVida;
    }

    private void Update()
    {
        if (_iconos == null || ejecutor == null) return;

        for (int i = 0; i < _iconos.Length; i++)
        {
            // El velo negro baja a medida que se enfria: lleno = recien usada.
            _sombras[i].fillAmount = ejecutor.Enfriamiento01(i);

            bool lista = ejecutor.Disponible(i);
            _iconos[i].color = lista ? Color.white : new Color(0.45f, 0.45f, 0.5f);
            // Un halo cuando esta lista: se ve de reojo sin tener que mirar el HUD.
            if (_brillos[i] != null) _brillos[i].enabled = lista;
        }

        if (_textoCadena != null && basico != null)
        {
            _textoCadena.text = basico.SiguienteEsPotenciado
                ? $"REMATE  {basico.Siguiente + 1}/{basico.Largo}"
                : $"Golpe {basico.Siguiente + 1}/{basico.Largo}";
            _textoCadena.color = basico.SiguienteEsPotenciado
                ? new Color(1f, 0.78f, 0.25f)
                : new Color(0.85f, 0.88f, 0.92f);
        }
    }

    private void RefrescarVida(int actual, int maximo)
    {
        if (_relleno == null) return;

        float proporcion = maximo > 0 ? actual / (float)maximo : 0f;
        _relleno.fillAmount = proporcion;
        _relleno.color = proporcion > 0.5f ? vidaAlta : proporcion > 0.2f ? vidaMedia : vidaBaja;

        if (_textoVida != null) _textoVida.text = $"{actual} / {maximo}";
    }

    private void Construir()
    {
        var lienzo = gameObject.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;

        var escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        // --- Barra de PS, abajo a la izquierda ---
        RectTransform marco = Caja("Vida", new Vector2(48f, 48f), new Vector2(460f, 40f),
                                   fondoBarra, fondoBarra != null ? Color.white : new Color(0f, 0f, 0f, 0.6f));

        var relleno = new GameObject("Relleno", typeof(Image));
        relleno.transform.SetParent(marco, false);
        _relleno = relleno.GetComponent<Image>();
        _relleno.sprite = rellenoBarra != null ? rellenoBarra : Blanco();
        _relleno.type = Image.Type.Filled;
        _relleno.fillMethod = Image.FillMethod.Horizontal;
        Estirar(relleno.GetComponent<RectTransform>(), 6f);

        _textoVida = Etiqueta(marco, "", 22, TextAnchor.MiddleCenter);
        Estirar(_textoVida.GetComponent<RectTransform>(), 0f);

        // --- Cadena de basicos, encima de la barra ---
        RectTransform cadena = Caja("Cadena", new Vector2(48f, 96f), new Vector2(240f, 34f), null, Color.clear);
        _textoCadena = Etiqueta(cadena, "", 22, TextAnchor.MiddleLeft);
        Estirar(_textoCadena.GetComponent<RectTransform>(), 0f);

        // --- Iconos de habilidad, abajo a la derecha ---
        int cantidad = ejecutor != null ? ejecutor.Cantidad : 0;
        _iconos = new Image[cantidad];
        _sombras = new Image[cantidad];
        _brillos = new Outline[cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            RectTransform icono = Caja($"Habilidad{i + 1}",
                new Vector2(1500f + i * 116f, 52f), new Vector2(104f, 104f),
                marcoHabilidad, Color.white);

            _iconos[i] = icono.GetComponent<Image>();
            _brillos[i] = icono.gameObject.AddComponent<Outline>();
            _brillos[i].effectColor = new Color(1f, 0.95f, 0.5f, 0.8f);
            _brillos[i].effectDistance = new Vector2(3f, -3f);

            var velo = new GameObject("Enfriamiento", typeof(Image));
            velo.transform.SetParent(icono, false);
            _sombras[i] = velo.GetComponent<Image>();
            _sombras[i].sprite = marcoHabilidad != null ? marcoHabilidad : Blanco();
            _sombras[i].color = new Color(0.02f, 0.02f, 0.05f, 0.8f);
            _sombras[i].type = Image.Type.Filled;
            _sombras[i].fillMethod = Image.FillMethod.Radial360;
            _sombras[i].fillOrigin = (int)Image.Origin360.Top;
            _sombras[i].fillClockwise = false;
            Estirar(velo.GetComponent<RectTransform>(), 0f);

            Text tecla = Etiqueta(icono, (i + 1).ToString(), 34, TextAnchor.MiddleCenter);
            Estirar(tecla.GetComponent<RectTransform>(), 0f);
        }
    }

    // --- Ayudas de construccion -------------------------------------------

    private RectTransform Caja(string nombre, Vector2 posicion, Vector2 tamaño, Sprite sprite, Color color)
    {
        var go = new GameObject(nombre, typeof(Image));
        go.transform.SetParent(transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamaño;

        var imagen = go.GetComponent<Image>();
        imagen.sprite = sprite != null ? sprite : Blanco();
        imagen.color = color;
        imagen.raycastTarget = false;
        return rect;
    }

    private static Text Etiqueta(Transform padre, string texto, int tamaño, TextAnchor alineacion)
    {
        var go = new GameObject("Texto", typeof(Text));
        go.transform.SetParent(padre, false);

        var t = go.GetComponent<Text>();
        t.text = texto;
        t.fontSize = tamaño;
        t.alignment = alineacion;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;

        var sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sombra.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static void Estirar(RectTransform rect, float margen)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(margen, margen);
        rect.offsetMax = new Vector2(-margen, -margen);
    }

    // Un sprite blanco de 1x1: los Image con fillAmount necesitan sprite, si no
    // el relleno se ignora y la barra se ve siempre llena. Es el mismo bug que
    // hubo que corregir en el HUD de combate.
    private static Sprite _blanco;
    private static Sprite Blanco()
    {
        if (_blanco == null)
        {
            Texture2D textura = Texture2D.whiteTexture;
            _blanco = Sprite.Create(textura, new Rect(0f, 0f, textura.width, textura.height),
                                    new Vector2(0.5f, 0.5f));
        }
        return _blanco;
    }
}
