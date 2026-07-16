using UnityEngine;

public class PokenView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Animacion")]
    [Tooltip("Frames por segundo de la tira de animacion del Poken.")]
    [SerializeField] private float frameRate = 30f;

    // Frames de la tira de animacion (los recorta SpriteAssigner en el editor)
    private Sprite[] _frames;
    private int _frameIndex;
    private float _frameTimer;

    public void SetPoken(PokenModel pokenModel, bool isPlayerSide)
    {
        PokenDefinition definition = pokenModel.Definition;

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
