using UnityEngine;

// Las luces del nivel se apagan cuando se enciende un flag: el Data Center de
// Nestor queda a media luz cuando su servidor se desconecta ("Las luces del
// datacenter comienzan a apagarse").
//
// Es un rectangulo oscuro pegado a la camara, en el MUNDO y no en un Canvas:
// oscurece el mapa y los personajes, pero no la caja de dialogo ni los menus,
// que son UI. Parpadea un par de veces al apagarse y despues queda quieto, con
// un latido suave de luz de emergencia.
//
// Si el flag ya estaba al cargar la escena, arranca apagado de una, sin el
// parpadeo. No hace falta cablear nada: se pone en cualquier objeto del mapa.
public class ApagonDelNivel : MonoBehaviour
{
    [Header("Cuando")]
    [SerializeField] private StoryFlag flagQueApaga = StoryFlag.Historia_Servidor01Desconectado;

    [Header("Como se ve")]
    [Tooltip("Cuanto oscurece al final. 0 = nada, 1 = negro. Con el material Lit del " +
             "proyecto el velo pega menos de lo que dice el numero: 0,65 se ve como un " +
             "tercio menos de luz.")]
    [Range(0f, 1f)] [SerializeField] private float oscuridad = 0.65f;
    [SerializeField] private Color color = new Color(0.02f, 0.02f, 0.07f, 1f);
    [Tooltip("Cuanto late la luz de emergencia, en fraccion de 'oscuridad'. 0 = quieto.")]
    [Range(0f, 0.5f)] [SerializeField] private float latido = 0.12f;
    [SerializeField] private float periodoDelLatido = 2.4f;
    [Tooltip("Segundos del parpadeo al apagarse.")]
    [Min(0f)] [SerializeField] private float duracionDelParpadeo = 1.6f;
    [Tooltip("Por encima de todo lo del mapa (el jugador va en 15).")]
    [SerializeField] private int sortingOrder = 500;

    private SpriteRenderer _velo;
    private bool _apagado;
    private float _desde = -1f;

    public bool Apagado => _apagado;

    private void Update()
    {
        StoryModel story = GameManager.Instance != null ? GameManager.Instance.Story : null;

        // Se mira el flag cada frame (un Contains) en vez de escuchar el
        // evento: NewGame y Continuar cambian el StoryModel y quien se suscribio
        // antes no se entera nunca mas.
        if (!_apagado)
        {
            if (story == null || flagQueApaga == StoryFlag.None || !story.HasFlag(flagQueApaga)) return;

            _apagado = true;
            // Apagado ya al llegar: sin parpadeo.
            _desde = Time.timeSinceLevelLoad < 0.5f ? -999f : Time.time;
            Armar();
        }

        Seguir();
        Pintar();
    }

    private void Armar()
    {
        if (_velo != null) return;

        var go = new GameObject("[Apagon]");
        go.transform.SetParent(transform, false);
        _velo = go.AddComponent<SpriteRenderer>();
        _velo.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        _velo.sortingOrder = sortingOrder;
    }

    // El velo sigue a la camara y la tapa entera.
    private void Seguir()
    {
        Camera cam = Camera.main;
        if (cam == null || _velo == null) return;

        float alto = cam.orthographic ? cam.orthographicSize * 2f + 2f : 40f;
        float ancho = alto * Mathf.Max(1f, cam.aspect) + 2f;
        Vector3 p = cam.transform.position;
        _velo.transform.position = new Vector3(p.x, p.y, 0f);
        _velo.transform.localScale = new Vector3(ancho, alto, 1f);
    }

    private void Pintar()
    {
        if (_velo == null) return;

        float t = Time.time - _desde;
        float a;
        if (t < duracionDelParpadeo)
        {
            // Tres cortes de luz cada vez mas largos, como un tubo que se muere.
            float fase = t / Mathf.Max(0.01f, duracionDelParpadeo);
            float corte = Mathf.Sin(fase * Mathf.PI * 6f) > 0.2f ? 1f : 0.25f;
            a = oscuridad * Mathf.Lerp(corte, 1f, fase * fase);
        }
        else
        {
            float pulso = Mathf.Sin((Time.time / Mathf.Max(0.1f, periodoDelLatido)) * Mathf.PI * 2f);
            a = oscuridad * (1f - latido * (0.5f + 0.5f * pulso));
        }

        _velo.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(a));
    }
}
