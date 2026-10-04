using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Los creditos del juego. Se llega aca despues de ganarle a Julieta y de la
// ultima escena en la Quarcho Cueva (el epilogo y la charla con Tixi los
// cuenta EscenaFinalEnLaCueva; ver FinalDelJuego).
//
//   1. Entra la musica de los creditos y aparecen el logo del grupo y el
//      titulo del juego.
//   2. El ROLLO sube solo. Su velocidad se calcula para que termine junto con
//      la cancion: los creditos duran lo que dura la musica.
//   3. PANTALLA FINAL: "FIN", el nombre del jugador y su equipo. Recien unos
//      segundos despues aparece el aviso de la tecla; con eso se vuelve a la
//      pantalla de inicio.
//
// No se puede acelerar ni saltear (pedido de Augusto): la tecla no hace nada
// hasta que aparece el aviso del final.
//
// El texto sale de un TextAsset (Resources/Creditos/CreditosFinales.txt): el
// equipo lo edita como un .txt comun, sin abrir la escena. Formato:
//
//   # Titulo grande
//   ## Seccion (en color)
//   @reparto        los personajes, con su retrato (lista 'reparto' de abajo)
//   Una linea normal
//   (linea vacia = un espacio)
//
// Se arma sola en runtime (como las vistas de la pelea): la escena solo tiene
// este componente, la camara y las referencias a fuentes, logo y retratos.
public class CreditosFinales : MonoBehaviour
{
    public const string RutaDelTexto = "Creditos/CreditosFinales";

    [System.Serializable]
    public class Personaje
    {
        public string nombre;
        [Tooltip("Una linea chica debajo del nombre. Opcional.")]
        public string papel;
        public Sprite retrato;
    }

    [Header("Contenido")]
    [SerializeField] private TextAsset texto;
    [SerializeField] private Sprite logoDelGrupo;
    [SerializeField] private string nombreDelGrupo = "LEVEL UP CREATORS";
    [SerializeField] private string tituloDelJuego = "POKERUSH";
    [SerializeField] private List<Personaje> reparto = new List<Personaje>();

    [Header("Fuentes (vacio = la de Unity)")]
    [SerializeField] private Font fuente;
    [SerializeField] private Font fuenteDeTitulos;

    [Header("Ritmo")]
    [Tooltip("Segundos que dura el rollo. 0 = lo que quede de la cancion de los creditos.")]
    [SerializeField, Min(0f)] private float duracionDelRollo = 0f;
    [Tooltip("Pixeles por segundo, en la resolucion de referencia (1280x720). Limites para que se lea.")]
    [SerializeField, Min(10f)] private float velocidadMinima = 32f;
    [SerializeField, Min(10f)] private float velocidadMaxima = 85f;
    [Tooltip("Lo que se reserva al final de la cancion para la pantalla de FIN.")]
    [SerializeField, Min(0f)] private float segundosParaElFinal = 14f;
    [Tooltip("Cuanto espera la pantalla de FIN antes de aceptar la tecla.")]
    [SerializeField, Min(0f)] private float esperaAntesDelAviso = 3.5f;
    [SerializeField] private string escenaAlTerminar = "Menu Principal";

    [Header("Musica (vacio = la del CatalogoDeAudio)")]
    [SerializeField] private AudioClip musica;

    [Header("Colores")]
    [SerializeField] private Color colorDeSeccion = new Color(0.47f, 0.78f, 1f);
    [SerializeField] private Color colorDeTitulo = new Color(1f, 0.86f, 0.35f);

    private const float AltoDeReferencia = 720f;

    private RectTransform _lienzo;
    private RectTransform _rollo;
    private float _alto;
    private bool _adelantar;
    private float _inicioDeLaMusica;
    private float _largoDeLaMusica;

    // Para el banco de pruebas.
    public bool Terminado { get; private set; }
    public bool EsperandoTecla { get; private set; }
    public float Progreso => _alto > 0f && _rollo != null
        ? Mathf.Clamp01(_rollo.anchoredPosition.y / (_alto + AltoDelLienzo))
        : 0f;

    // El alto real del lienzo en unidades de referencia: con una pantalla que
    // no es 16:9 no es 720 (el CanvasScaler mezcla ancho y alto), y con 720 fijo
    // las ultimas lineas quedaban asomadas arriba en la pantalla de FIN.
    private float AltoDelLienzo => _lienzo != null && _lienzo.rect.height > 1f ? _lienzo.rect.height : AltoDeReferencia;

    private static GameInputService Input => GameInputService.Instance;

    private void Start()
    {
        if (texto == null) texto = Resources.Load<TextAsset>(RutaDelTexto);
        if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fuenteDeTitulos == null) fuenteDeTitulos = fuente;
        Nitida(fuente);
        Nitida(fuenteDeTitulos);
        Font.textureRebuilt += Nitida;

        ArmarLienzo();
        StartCoroutine(Correr());
    }

    private void OnDestroy() => Font.textureRebuilt -= Nitida;

    // La tipografia de pixel art con filtro bilineal sale borrosa (ver PixelFontCrisp).
    private static void Nitida(Font f)
    {
        if (f == null || f.material == null || f.material.mainTexture == null) return;
        f.material.mainTexture.filterMode = FilterMode.Point;
    }

    // Para el banco de pruebas: corre todo de golpe hasta la pantalla de FIN.
    public void Adelantar() => _adelantar = true;

    // ---------- la secuencia ----------

    private IEnumerator Correr()
    {
        EmpezarMusica();
        yield return Tarjeta(TarjetaDelGrupo(), 1.2f, 2.8f, 1.0f);
        yield return Tarjeta(TarjetaDelTitulo(), 1.2f, 2.6f, 1.2f);

        ArmarRollo(texto != null ? texto.text : "# FIN\n\nGracias por jugar.");
        yield return Subir();

        yield return PantallaFinal();
    }

    private void EmpezarMusica()
    {
        CatalogoDeAudio c = CatalogoDeAudio.Actual;
        AudioClip clip = musica != null ? musica : c != null && c.creditos.Hay ? c.creditos.clip : null;
        _largoDeLaMusica = clip != null ? clip.length : 0f;
        _inicioDeLaMusica = Time.unscaledTime;
        if (clip == null) return;

        if (musica == null) MusicaDelJuego.Sonar(c.creditos, loop: false, fundido: 2f);
        else
        {
            AudioService.Instance.SetMusicVolume(c != null ? c.volumenBaseDeMusica : 0.5f);
            AudioService.Instance.PlayMusic(clip, false);
        }
    }

    // Un bloque aparece, se queda y se va. Nada de esto se puede saltear.
    private IEnumerator Tarjeta(RectTransform bloque, float entrada, float quieta, float salida)
    {
        var grupo = bloque.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        yield return Fundir(grupo, 0f, 1f, entrada);
        yield return Esperar(quieta);
        yield return Fundir(grupo, 1f, 0f, salida);
        Destroy(bloque.gameObject);
    }

    private IEnumerator Subir()
    {
        float recorrido = _alto + AltoDelLienzo;
        float segundos = duracionDelRollo > 0f
            ? duracionDelRollo
            : _largoDeLaMusica - (Time.unscaledTime - _inicioDeLaMusica) - segundosParaElFinal;
        float velocidad = segundos > 1f ? recorrido / segundos : velocidadMaxima;
        velocidad = Mathf.Clamp(velocidad, velocidadMinima, velocidadMaxima);
        Debug.Log($"[Creditos] Rollo de {recorrido:0} px a {velocidad:0.0} px/s ({recorrido / velocidad:0} s).");

        while (_rollo.anchoredPosition.y < AltoDelLienzo + _alto && !_adelantar)
        {
            _rollo.anchoredPosition += new Vector2(0f, velocidad * Time.unscaledDeltaTime);
            yield return null;
        }
        _rollo.gameObject.SetActive(false);
    }

    private IEnumerator PantallaFinal()
    {
        RectTransform bloque = Bloque("Final");
        var grupo = bloque.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        Texto(bloque, "FIN", fuenteDeTitulos, 96, colorDeTitulo, new Vector2(0f, 150f), 120f);

        PlayerModel jugador = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        string nombre = jugador != null && !string.IsNullOrEmpty(jugador.PlayerName) ? jugador.PlayerName : null;
        Texto(bloque, nombre != null ? $"Gracias por jugar, {nombre}." : "Gracias por jugar.",
              fuente, 36, Color.white, new Vector2(0f, 52f), 60f);

        if (jugador != null) EquipoEnFila(bloque, jugador, new Vector2(0f, -70f));

        Text aviso = Texto(bloque, "", fuente, 28, new Color(1f, 1f, 1f, 0f), new Vector2(0f, -240f), 50f);

        yield return Fundir(grupo, 0f, 1f, 2f);
        Terminado = true;
        yield return Esperar(esperaAntesDelAviso);

        string tecla = Input != null ? Input.GetDisplayName(GameAction.Confirm) : "X";
        aviso.text = $"Presioná {tecla} para volver al inicio";
        EsperandoTecla = true;

        // El aviso late suave; recien ahora la tecla cuenta.
        while (!Input.WasPressed(GameAction.Confirm) && !_adelantar)
        {
            float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f);
            aviso.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        Input.Consume(GameAction.Confirm);
        EsperandoTecla = false;
        Sonidos.Confirmar();

        MusicaDelJuego.Callar(2f);
        if (!string.IsNullOrEmpty(escenaAlTerminar) && Application.CanStreamedLevelBeLoaded(escenaAlTerminar))
            FundidoAEscena.Ir(escenaAlTerminar, 2f);
        else
            Debug.LogWarning($"[Creditos] '{escenaAlTerminar}' no esta en Build Settings: me quedo aca.");
    }

    // ---------- armado ----------

    private void ArmarLienzo()
    {
        var go = new GameObject("Lienzo");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, AltoDeReferencia);
        scaler.matchWidthOrHeight = 0.5f;
        _lienzo = (RectTransform)go.transform;

        RectTransform fondo = LienzoDePeleaJulieta.Rect(_lienzo, "Fondo", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        LienzoDePeleaJulieta.Imagen(fondo, Color.black);
    }

    private RectTransform Bloque(string nombre)
        => LienzoDePeleaJulieta.Rect(_lienzo, nombre, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                                     Vector2.zero, Vector2.zero);

    private RectTransform TarjetaDelGrupo()
    {
        RectTransform b = Bloque("Grupo");
        if (logoDelGrupo != null)
        {
            // El logo es azul oscuro sobre transparente: sobre el negro no se
            // leeria. Va sobre blanco, como un splash, y despues funde a negro.
            LienzoDePeleaJulieta.Imagen(LienzoDePeleaJulieta.Rect(b, "Blanco", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.white);
            RectTransform rt = LienzoDePeleaJulieta.Rect(b, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(420f, 420f));
            Image img = LienzoDePeleaJulieta.Imagen(rt, Color.white);
            img.sprite = logoDelGrupo;
            img.preserveAspect = true;
            Text presenta = Texto(b, "presenta", fuente, 32, new Color(0.12f, 0.22f, 0.4f), new Vector2(0f, -200f), 50f);
            presenta.GetComponent<Shadow>().enabled = false;
        }
        else
        {
            Texto(b, nombreDelGrupo, fuenteDeTitulos, 48, Color.white, new Vector2(0f, 30f), 70f);
            Texto(b, "presenta", fuente, 32, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, -40f), 50f);
        }
        return b;
    }

    private RectTransform TarjetaDelTitulo()
    {
        RectTransform b = Bloque("Titulo");
        Texto(b, tituloDelJuego, fuenteDeTitulos, 112, colorDeTitulo, new Vector2(0f, 20f), 140f);
        return b;
    }

    // Una fila por linea, apiladas de arriba a abajo. El rollo arranca con su
    // borde de arriba en el borde de abajo de la pantalla.
    private void ArmarRollo(string contenido)
    {
        _rollo = LienzoDePeleaJulieta.Rect(_lienzo, "Rollo", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1100f, 10f));

        float y = 0f;
        foreach (string crudo in contenido.Replace("\r", "").Split('\n'))
        {
            string linea = crudo.TrimEnd();

            if (linea.Trim() == "@reparto")
            {
                y = ArmarReparto(y);
                continue;
            }

            int tamanio = 32;
            Color color = Color.white;
            Font f = fuente;
            float alto = 44f;

            if (linea.StartsWith("## ")) { linea = linea.Substring(3); tamanio = 32; color = colorDeSeccion; f = fuenteDeTitulos; alto = 60f; }
            else if (linea.StartsWith("# ")) { linea = linea.Substring(2); tamanio = 64; color = colorDeTitulo; f = fuenteDeTitulos; alto = 96f; }
            else if (linea.Length == 0) { y -= 28f; continue; }

            RectTransform fila = LienzoDePeleaJulieta.Rect(_rollo, "Linea", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, alto));
            Text t = LienzoDePeleaJulieta.Texto(fila, f, tamanio, TextAnchor.MiddleCenter, color);
            t.text = linea;
            y -= alto;
        }

        _alto = -y;
        _rollo.sizeDelta = new Vector2(1100f, _alto);
    }

    // El reparto: retrato a la izquierda, nombre y papel a la derecha, uno
    // abajo del otro.
    private float ArmarReparto(float y)
    {
        const float alto = 132f;
        foreach (Personaje p in reparto)
        {
            if (p == null || string.IsNullOrEmpty(p.nombre)) continue;

            RectTransform fila = LienzoDePeleaJulieta.Rect(_rollo, p.nombre, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, alto));

            if (p.retrato != null)
            {
                RectTransform rt = LienzoDePeleaJulieta.Rect(fila, "Retrato", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(-130f, 0f), new Vector2(116f, 116f));
                Image img = LienzoDePeleaJulieta.Imagen(rt, Color.white);
                img.sprite = p.retrato;
                img.preserveAspect = true;
            }

            RectTransform nombre = LienzoDePeleaJulieta.Rect(fila, "Nombre", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(-40f, string.IsNullOrEmpty(p.papel) ? 0f : 16f), new Vector2(520f, 44f));
            Text tn = LienzoDePeleaJulieta.Texto(nombre, fuenteDeTitulos, 32, TextAnchor.MiddleLeft, Color.white);
            tn.text = p.nombre;

            if (!string.IsNullOrEmpty(p.papel))
            {
                RectTransform papel = LienzoDePeleaJulieta.Rect(fila, "Papel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(-40f, -22f), new Vector2(520f, 36f));
                Text tp = LienzoDePeleaJulieta.Texto(papel, fuente, 24, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.8f));
                tp.text = p.papel;
            }
            y -= alto;
        }
        return y;
    }

    // El equipo con el que se termino el juego, de frente y en fila.
    private void EquipoEnFila(RectTransform padre, PlayerModel jugador, Vector2 centro)
    {
        var sprites = new List<Sprite>();
        foreach (PokenModel p in jugador.Team)
        {
            if (p == null || p.Definition == null) continue;
            Sprite s = p.Definition.battleFrontSprite;
            if (s != null) sprites.Add(s);
        }
        if (sprites.Count == 0) return;

        const float ancho = 128f;
        float x0 = -(sprites.Count - 1) * ancho * 0.5f;
        for (int i = 0; i < sprites.Count; i++)
        {
            RectTransform rt = LienzoDePeleaJulieta.Rect(padre, "Poken", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), centro + new Vector2(x0 + i * ancho, 0f), new Vector2(120f, 120f));
            Image img = LienzoDePeleaJulieta.Imagen(rt, Color.white);
            img.sprite = sprites[i];
            img.preserveAspect = true;
        }
    }

    private Text Texto(RectTransform padre, string contenido, Font f, int tamanio, Color color, Vector2 posicion, float alto)
    {
        RectTransform rt = LienzoDePeleaJulieta.Rect(padre, "Texto", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), posicion, new Vector2(0f, alto));
        Text t = LienzoDePeleaJulieta.Texto(rt, f, tamanio, TextAnchor.MiddleCenter, color);
        t.text = contenido;
        return t;
    }

    // ---------- tiempo ----------

    private IEnumerator Fundir(CanvasGroup grupo, float desde, float hasta, float segundos)
    {
        for (float t = 0f; t < segundos && !_adelantar; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / segundos);
            yield return null;
        }
        grupo.alpha = hasta;
    }

    private IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos && !_adelantar; t += Time.unscaledDeltaTime)
            yield return null;
    }
}
