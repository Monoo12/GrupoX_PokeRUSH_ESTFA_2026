using UnityEngine;

public class PokenView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    public void SetPoken(PokenModel pokenModel, bool isPlayerSide)
    {
        PokenDefinition definition = pokenModel.Definition;

        spriteRenderer.sprite = isPlayerSide
            ? definition.battleBackSprite
            : definition.battleFrontSprite;

        if (definition.battleAnimator != null && animator != null)
            animator.runtimeAnimatorController = definition.battleAnimator;
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

//isPlayerSide decide si se muestra el sprite de espalda (jugador, visto desde atrás) o de frente (enemigo, visto de frente). Esto refleja cómo se ve la batalla en los juegos clásicos. battleAnimator es opcional — si el Pokén todavía no tiene animaciones asignadas, simplemente se queda con el sprite estático.
