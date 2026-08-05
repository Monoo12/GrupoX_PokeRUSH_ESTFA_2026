using UnityEngine;

// El Poken dibujado con los sprites de siempre, ahora parados dentro de la
// escena 3D (un billboard que mira a la camara). Es la representacion por
// defecto: sirve para las 800+ especies importadas de PokeAPI, que solo tienen
// sprites. Conserva la animacion por tira de frames del PokenView 2D.
[RequireComponent(typeof(SpriteRenderer))]
public class SpritePokenVisual : PokenVisual
{
    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animacion")]
    [Tooltip("Frames por segundo de la tira de animacion del Poken.")]
    [SerializeField] private float frameRate = 30f;

    // Frames de la tira de animacion (los recorta SpriteAssigner en el editor)
    private Sprite[] _frames;
    private int _frameIndex;
    private float _frameTimer;

    // El importador normaliza los sprites a 1 unidad de alto con el pivot en
    // los pies, asi que el alto es directamente la escala del objeto.
    public override float Height => transform.lossyScale.y;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void Bind(PokenDefinition definition, bool isPlayerSide)
    {
        // Con frames animados los usa; si no hay, cae al sprite estatico
        _frames = isPlayerSide ? definition.battleBackFrames : definition.battleFrontFrames;
        _frameIndex = 0;
        _frameTimer = 0f;

        if (_frames != null && _frames.Length > 0)
        {
            spriteRenderer.sprite = _frames[0];
        }
        else
        {
            spriteRenderer.sprite = isPlayerSide
                ? definition.battleBackSprite
                : definition.battleFrontSprite;
        }

        ApplyAnimator(definition);
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

    public override void SetTint(Color tint)
    {
        if (spriteRenderer != null) spriteRenderer.color = tint;
    }

    public override void SetRendererVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }
}
