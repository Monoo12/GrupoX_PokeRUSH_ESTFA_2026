using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// La ultima escena del juego, en la Quarcho Cueva (historia.pdf: "Epílogo" y
// "QuarchoCueva"). La agrega FinalDelJuego al cargar la cueva despues de
// vencer a Julieta; no vive en la escena.
//
//   1. EPILOGO    la cueva arranca tapada de negro; las lineas del epilogo
//                 aparecen de a una ("Horas después...").
//   2. LA CUEVA   se destapa con el PJ frente a Tixi, y Tixi charla (el
//                 DialogueDefinition GuionDeJulieta.enLaCueva).
//   3. CIERRE     la camara se aleja despacio y en una pantalla aparece la
//                 ultima conexion, linea por linea ("NOMBRE EN CLAVE: ...").
//                 La pantalla se apaga y vienen los creditos.
//
// Todo el texto sale de GuionDeJulieta. Mientras dura, el jugador no se mueve
// (PlayerInputController.Frenar) y Tixi no habla sola.
//
// Lo de pantalla es UI de prueba, como el resto de la pelea: un lienzo propio
// que se arma solo.
public class EscenaFinalEnLaCueva : MonoBehaviour
{
    // Arriba de todo lo de la cueva, abajo del fundido entre escenas (1000).
    private const int Orden = 900;

    private const float SegundosMinimosPorLinea = 4.5f;
    private const float SegundosPorLineaDeTerminal = 0.85f;
    private const float AlejamientoFinal = 1.7f;
    private const float SegundosDelAlejamiento = 9f;

    private static readonly Color Verde = new Color(0.45f, 1f, 0.55f);

    private GuionDeJulieta _guion;
    private Font _fuente;
    private RectTransform _lienzo;
    private Image _negro;
    private DialogueController _dialogo;

    // Para el banco de pruebas.
    public string Etapa { get; private set; } = "arrancando";
    public string UltimaLineaDeTerminal { get; private set; }
    // Cuantas veces mas rapido corren las esperas (el epilogo, la terminal).
    // 1 en el juego; el banco de pruebas lo sube para no esperar un minuto.
    public float Ritmo { get; set; } = 1f;
    public float AlejamientoDeLaCamara { get; private set; } = 1f;

    private static GameInputService Input => GameInputService.Instance;

    private void Awake()
    {
        _guion = Resources.Load<GuionDeJulieta>(FinalDelJuego.RutaDelGuion);
        _fuente = _guion != null && _guion.fuente != null
            ? _guion.fuente
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ArmarLienzo();
        PlayerInputController.Frenar(this);
    }

    private void OnDestroy() => PlayerInputController.Soltar(this);

    private void Start() => StartCoroutine(Correr());

    private IEnumerator Correr()
    {
        // Un frame para que la cueva termine de arrancar (jugador, Tixi, caja).
        yield return null;
        _dialogo = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        PararFrenteATixi();

        while (FundidoAEscena.EnCurso) yield return null;

        Etapa = "epilogo";
        if (_guion != null && _guion.epilogo != null)
            foreach (string linea in _guion.epilogo)
                if (!string.IsNullOrWhiteSpace(linea)) yield return LineaSobreNegro(linea.Trim());

        Etapa = "cueva";
        yield return Fundir(_negro, 1f, 0f, 2.2f);
        yield return Esperar(0.6f);

        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;
        if (_guion != null && _guion.enLaCueva != null && _dialogo != null)
        {
            Etapa = "charla";
            bool termino = false;
            _dialogo.StartDialogue(_guion.enLaCueva, story, () => termino = true);
            yield return null;
            // Si la caja se cierra sin avisar (Cancelar), igual se sigue.
            while (!termino && _dialogo.IsOpen) yield return null;
        }

        Etapa = "ultima conexion";
        Camera cam = Camera.main;
        CameraFollow camara = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (camara != null) StartCoroutine(Alejar(camara));
        yield return Esperar(1.2f);
        yield return UltimaConexion();

        Etapa = "creditos";
        FinalDelJuego.IrALosCreditos(2.5f);
    }

    // ---------- la cueva ----------

    // El PJ aparece parado frente a Tixi, mirandola. Lo que haya alrededor lo
    // decide la cueva: se prueba abajo, a los costados y arriba. Primero a dos
    // celdas: los personajes miden mas de dos de alto y, pegados, el PJ le tapa
    // medio cuerpo a Tixi.
    private void PararFrenteATixi()
    {
        PlayerController jugador = FindAnyObjectByType<PlayerController>();
        QuarchoTixiNpc tixi = FindAnyObjectByType<QuarchoTixiNpc>();
        if (jugador == null || tixi == null) return;

        int obstaculos = LayerMask.GetMask("Obstacles");
        bool Libre(Vector2 p) => Physics2D.OverlapPoint(p, obstaculos) == null;

        Vector2 centro = tixi.transform.position;
        Vector2[] lados = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
        foreach (int distancia in new[] { 2, 1 })
        foreach (Vector2 lado in lados)
        {
            Vector2 p = centro + lado * distancia;
            if (!Libre(p) || (distancia == 2 && !Libre(centro + lado))) continue;
            jugador.TeleportTo(p, -lado);
            tixi.GetComponent<NpcQueCamina>()?.Mirar(lado);
            return;
        }
    }

    private IEnumerator Alejar(CameraFollow camara)
    {
        for (float t = 0f; t < SegundosDelAlejamiento; t += Time.deltaTime * Ritmo)
        {
            float k = t / SegundosDelAlejamiento;
            camara.Alejamiento = AlejamientoDeLaCamara = Mathf.Lerp(1f, AlejamientoFinal, k * k * (3f - 2f * k));
            yield return null;
        }
        camara.Alejamiento = AlejamientoDeLaCamara = AlejamientoFinal;
    }

    // ---------- la pantalla ----------

    // "En una de las pantallas aparece una ultima linea de texto": una terminal
    // que se escribe sola y, al final, se apaga como un televisor viejo. Va abajo
    // y no tapa todo: arriba se sigue viendo la cueva que se aleja.
    private IEnumerator UltimaConexion()
    {
        if (_guion == null || _guion.ultimaConexion == null || _guion.ultimaConexion.Length == 0) yield break;

        RectTransform marco = LienzoDePeleaJulieta.Rect(_lienzo, "Terminal", new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(720f, 250f));
        LienzoDePeleaJulieta.Imagen(marco, new Color(0.55f, 0.6f, 0.65f, 1f));
        RectTransform pantalla = LienzoDePeleaJulieta.Rect(marco, "Pantalla", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -16f));
        LienzoDePeleaJulieta.Imagen(pantalla, new Color(0.01f, 0.04f, 0.02f, 1f));
        RectTransform zonaDeTexto = LienzoDePeleaJulieta.Rect(pantalla, "Texto", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-48f, -40f));
        Text texto = LienzoDePeleaJulieta.Texto(zonaDeTexto, _fuente, 20, TextAnchor.UpperLeft, Verde);
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.lineSpacing = FuentePixel.Es(_fuente) ? 1.25f : 1.1f;

        var grupo = marco.gameObject.AddComponent<CanvasGroup>();
        yield return Fundir(grupo, 0f, 1f, 0.8f);

        // Se ven las ultimas lineas, como en una consola que va subiendo.
        const int Visibles = 10;
        var escritas = new System.Collections.Generic.List<string>();
        foreach (string crudo in _guion.ultimaConexion)
        {
            string linea = DialogueController.Marcas(crudo ?? "");
            UltimaLineaDeTerminal = linea;
            escritas.Add("");
            for (int i = 1; i <= linea.Length; i++)
            {
                escritas[escritas.Count - 1] = "> " + linea.Substring(0, i);
                texto.text = Ultimas(escritas, Visibles) + (i < linea.Length ? "_" : "");
                yield return Esperar(0.025f);
            }
            texto.text = Ultimas(escritas, Visibles);
            yield return Esperar(SegundosPorLineaDeTerminal);
        }

        // El cursor titila un rato sobre la ultima linea.
        for (int i = 0; i < 6; i++)
        {
            texto.text = Ultimas(escritas, Visibles) + (i % 2 == 0 ? "\n> _" : "\n> ");
            yield return Esperar(0.45f);
        }

        // "La pantalla se apaga": se aplasta hasta una raya y desaparece, y el
        // resto se va a negro.
        Etapa = "pantalla apagada";
        StartCoroutine(Fundir(_negro, 0f, 1f, 1.4f));
        Vector3 escala = marco.localScale;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            marco.localScale = new Vector3(escala.x, Mathf.Lerp(escala.y, 0.01f, t / 0.35f), 1f);
            yield return null;
        }
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            marco.localScale = new Vector3(Mathf.Lerp(escala.x, 0f, t / 0.25f), 0.01f, 1f);
            yield return null;
        }
        marco.gameObject.SetActive(false);
        yield return Esperar(1.6f);
    }

    private static string Ultimas(System.Collections.Generic.List<string> lineas, int cuantas)
    {
        int desde = Mathf.Max(0, lineas.Count - cuantas);
        return string.Join("\n", lineas.GetRange(desde, lineas.Count - desde));
    }

    // ---------- el negro y el epilogo ----------

    private void ArmarLienzo()
    {
        var go = new GameObject("Lienzo");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Orden;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        _lienzo = (RectTransform)go.transform;

        // Arranca en negro: la cueva se arma por debajo mientras corre el epilogo.
        _negro = LienzoDePeleaJulieta.Imagen(LienzoDePeleaJulieta.Rect(_lienzo, "Negro", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.black);
    }

    // Una linea del epilogo: aparece, se queda lo que tarda en leerse y se va.
    // Con la tecla de aceptar se pasa a la siguiente (despues de un momento,
    // para que nadie se la saltee sin querer).
    private IEnumerator LineaSobreNegro(string crudo)
    {
        RectTransform caja = LienzoDePeleaJulieta.Rect(_lienzo, "Epilogo", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-240f, 0f));
        Text t = LienzoDePeleaJulieta.Texto(caja, _fuente, 32, TextAnchor.MiddleCenter, Color.white);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.lineSpacing = FuentePixel.Es(_fuente) ? FuentePixel.Interlineado : 1.3f;
        t.text = DialogueController.Marcas(crudo);

        var grupo = caja.gameObject.AddComponent<CanvasGroup>();
        yield return Fundir(grupo, 0f, 1f, 1.2f);

        float quieta = Mathf.Max(SegundosMinimosPorLinea, 2.5f + crudo.Length * 0.05f);
        for (float s = 0f; s < quieta; s += Time.unscaledDeltaTime * Ritmo)
        {
            if (s > 1f && Input != null && Input.WasPressed(GameAction.Confirm))
            {
                Input.Consume(GameAction.Confirm);
                break;
            }
            yield return null;
        }

        yield return Fundir(grupo, 1f, 0f, 1.2f);
        Destroy(caja.gameObject);
    }

    private IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime * Ritmo)
            yield return null;
    }

    private IEnumerator Fundir(Graphic g, float desde, float hasta, float segundos)
    {
        Color c = g.color;
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime * Ritmo)
        {
            g.color = new Color(c.r, c.g, c.b, Mathf.Lerp(desde, hasta, t / segundos));
            yield return null;
        }
        g.color = new Color(c.r, c.g, c.b, hasta);
    }

    private IEnumerator Fundir(CanvasGroup g, float desde, float hasta, float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime * Ritmo)
        {
            g.alpha = Mathf.Lerp(desde, hasta, t / segundos);
            yield return null;
        }
        g.alpha = hasta;
    }
}
