using UnityEngine;

public class PokenView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Animacion")]
    [Tooltip("Frames por segundo de la tira de animacion del Poken.")]
    [SerializeField] private float frameRate = 30f;

    // Acceso directo para los efectos de batalla (BattleFxView)
    public SpriteRenderer Renderer => spriteRenderer;

    // Frames de la tira de animacion (los recorta SpriteAssigner en el editor)
    private Sprite[] _frames;
    private int _frameIndex;
    private float _frameTimer;

    // El que esta dibujado ahora (para su grito, en BattleFxView).
    public PokenModel Poken { get; private set; }

    public void SetPoken(PokenModel pokenModel, bool isPlayerSide)
    {
        DejarDeEsconder();
        Poken = pokenModel;
        PokenDefinition definition = pokenModel.Definition;

        // Con frames animados los usa; si no hay, cae al sprite estatico. Los
        // pide al modelo y no a la definicion: un shiny trae sus propios frames.
        _frames = isPlayerSide ? pokenModel.BackFrames : pokenModel.FrontFrames;
        _frameIndex = 0;
        _frameTimer = 0f;

        if (_frames != null && _frames.Length > 0)
        {
            spriteRenderer.sprite = _frames[0];
        }
        else
        {
            spriteRenderer.sprite = isPlayerSide ? pokenModel.BackSprite : pokenModel.FrontSprite;
        }

        if (definition.battleAnimator != null && animator != null)
            animator.runtimeAnimatorController = definition.battleAnimator;
    }

    // Cicla los frames de la tira en loop
    private void Update()
    {
        if (_frames == null || _frames.Length < 2 || frameRate <= 0f) return;

        _frameTimer += Time.deltaTime;
        float frameDuration = 1f / frameRate;
        while (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % _frames.Length;
        }
        spriteRenderer.sprite = _frames[_frameIndex];
    }

    // ---------- escondido (Excavar, Vuelo, Buceo...) ----------

    // Mientras carga un movimiento que lo deja fuera de alcance, el Poken no se
    // ve: se hunde (o sube) y desaparece, y vuelve al soltar el golpe. Solo es
    // el sprite: el GameObject sigue activo para que la barra y los efectos lo
    // encuentren.
    public bool Escondido { get; private set; }
    private Vector3 _posicionVisible;

    public System.Collections.IEnumerator Esconder(float segundos = 0.35f)
    {
        if (Escondido || spriteRenderer == null) yield break;
        Escondido = true;
        Transform t = spriteRenderer.transform;
        _posicionVisible = t.localPosition;
        Color c = spriteRenderer.color;
        for (float s = 0f; s < segundos; s += Time.deltaTime)
        {
            float k = s / segundos;
            t.localPosition = _posicionVisible + Vector3.down * (0.6f * k);
            spriteRenderer.color = new Color(c.r, c.g, c.b, 1f - k);
            yield return null;
        }
        spriteRenderer.enabled = false;
        t.localPosition = _posicionVisible;
        spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
    }

    public System.Collections.IEnumerator Aparecer(float segundos = 0.25f)
    {
        if (!Escondido || spriteRenderer == null) yield break;
        Escondido = false;
        Transform t = spriteRenderer.transform;
        Color c = spriteRenderer.color;
        spriteRenderer.enabled = true;
        for (float s = 0f; s < segundos; s += Time.deltaTime)
        {
            float k = s / segundos;
            t.localPosition = _posicionVisible + Vector3.down * (0.6f * (1f - k));
            spriteRenderer.color = new Color(c.r, c.g, c.b, k);
            yield return null;
        }
        t.localPosition = _posicionVisible;
        spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
    }

    // Vuelve a verse de una, sin animacion (cambio de Poken, fin de pelea).
    public void DejarDeEsconder()
    {
        if (!Escondido || spriteRenderer == null) return;
        Escondido = false;
        spriteRenderer.enabled = true;
        spriteRenderer.transform.localPosition = _posicionVisible;
        Color c = spriteRenderer.color;
        spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void PlayHitFeedback()
    {
        // shake o flash al recibir daño — se implementa despues
    }
}
