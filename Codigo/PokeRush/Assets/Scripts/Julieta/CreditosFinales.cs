using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Los creditos finales del juego. Se llega aca despues de ganarle a Julieta.
//
// El texto sale de un TextAsset (Resources/Creditos/CreditosFinales.txt): el
// equipo lo edita como un .txt comun, sin abrir la escena. Formato:
//
//   # Titulo grande
//   ## Seccion (en color)
//   Una linea normal
//   (linea vacia = un espacio)
//
// Suben solos; manteniendo Aceptar van mas rapido. Al terminar se vuelve al
// menu principal. UI DE PRUEBA: se arma sola en runtime, como las vistas de la
// pelea, para que el equipo la reemplace cuando diseñe la definitiva.
public class CreditosFinales : MonoBehaviour
{
    public const string RutaDelTexto = "Creditos/CreditosFinales";

    [SerializeField] private TextAsset texto;
    [SerializeField] private Font fuente;
    [Tooltip("Pixeles por segundo, en la resolucion de referencia (1280x720).")]
    [SerializeField, Min(10f)] private float velocidad = 70f;
    [SerializeField, Min(1f)] private float multiplicadorAlApretar = 5f;
    [SerializeField] private string escenaAlTerminar = "Menu Principal";
    [SerializeField, Min(0f)] private float esperaAlFinal = 3f;
    [SerializeField] private AudioClip musica;
    [SerializeField] private Color colorDeSeccion = new Color(0.78f, 0.55f, 1f);

    private RectTransform _rollo;
    private float _alto;
    private bool _terminado;

    public bool Terminado => _terminado;
    public float Progreso => _alto > 0f && _rollo != null ? Mathf.Clamp01(_rollo.anchoredPosition.y / (_alto + 720f)) : 0f;

    private static GameInputService Input => GameInputService.Instance;

    private void Start()
    {
        if (texto == null) texto = Resources.Load<TextAsset>(RutaDelTexto);
        if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Armar(texto != null ? texto.text : "# FIN\n\nGracias por jugar.");

        if (musica != null)
        {
            var fuenteDeAudio = gameObject.AddComponent<AudioSource>();
            fuenteDeAudio.clip = musica;
            fuenteDeAudio.loop = true;
            fuenteDeAudio.Play();
        }

        StartCoroutine(Correr());
    }

    // Arma el rollo de texto: una fila por linea, apiladas de arriba a abajo.
    private void Armar(string contenido)
    {
        var go = new GameObject("Lienzo");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform fondo = LienzoDePeleaJulieta.Rect(go.transform, "Fondo", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        LienzoDePeleaJulieta.Imagen(fondo, Color.black);

        // El rollo arranca con su borde de arriba en el borde de abajo de la
        // pantalla y sube.
        _rollo = LienzoDePeleaJulieta.Rect(go.transform, "Rollo", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1100f, 10f));

        float y = 0f;
        foreach (string crudo in contenido.Replace("\r", "").Split('\n'))
        {
            string linea = crudo.TrimEnd();
            int tamanio = 26;
            Color color = Color.white;
            float alto = 38f;

            if (linea.StartsWith("## ")) { linea = linea.Substring(3); tamanio = 30; color = colorDeSeccion; alto = 52f; }
            else if (linea.StartsWith("# ")) { linea = linea.Substring(2); tamanio = 54; alto = 86f; }
            else if (linea.Length == 0) { y -= 26f; continue; }

            RectTransform fila = LienzoDePeleaJulieta.Rect(_rollo, "Linea", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, alto));
            Text t = LienzoDePeleaJulieta.Texto(fila, fuente, tamanio, TextAnchor.MiddleCenter, color);
            t.text = linea;
            y -= alto;
        }

        _alto = -y;
        _rollo.sizeDelta = new Vector2(1100f, _alto);
    }

    private IEnumerator Correr()
    {
        // Hasta que la ultima linea salga por arriba de la pantalla (720 de alto).
        float destino = _alto + 720f;
        while (_rollo.anchoredPosition.y < destino)
        {
            float v = velocidad * (Input.IsPressed(GameAction.Confirm) ? multiplicadorAlApretar : 1f);
            _rollo.anchoredPosition += new Vector2(0f, v * Time.unscaledDeltaTime);
            yield return null;
        }

        _terminado = true;
        yield return new WaitForSecondsRealtime(esperaAlFinal);

        if (!string.IsNullOrEmpty(escenaAlTerminar) && Application.CanStreamedLevelBeLoaded(escenaAlTerminar))
            FundidoAEscena.Ir(escenaAlTerminar, 1f);
        else
            Debug.LogWarning($"[Creditos] '{escenaAlTerminar}' no esta en Build Settings: me quedo aca.");
    }

    // Para el banco de pruebas: salta al final.
    public void Adelantar()
    {
        if (_rollo != null) _rollo.anchoredPosition = new Vector2(0f, _alto + 720f);
    }
}
