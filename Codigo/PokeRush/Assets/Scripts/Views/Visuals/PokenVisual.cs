using UnityEngine;

// Representacion visual de un Poken en el mundo 3D, sin comprometerse con como
// esta dibujado. Hay dos implementaciones: SpritePokenVisual (el sprite de
// siempre, plantado como billboard en la escena 3D) y ModelPokenVisual (un
// modelo 3D). PokenView elige una u otra por especie, asi el juego puede tener
// modelos donde los haya y sprites en las 800+ especies que no.
//
// Todo lo que BattleFxView necesita hacerle a un Poken (teñirlo, desvanecerlo,
// parpadearlo) pasa por aca. Ninguna vista de combate vuelve a tocar un
// SpriteRenderer directamente.
public abstract class PokenVisual : MonoBehaviour
{
    [Header("Animacion")]
    [Tooltip("Animator opcional. Si la especie trae battleAnimator, se le aplica aca.")]
    [SerializeField] protected Animator animator;

    // Alto aproximado del Poken en unidades de mundo. Los efectos lo usan para
    // apuntar el impacto a la altura del cuerpo en vez de a los pies. Con
    // sprites es 1 (el importador los normaliza a 1 unidad); con modelos 3D
    // sale del bounding box, que varia mucho entre un Diglett y un Onix.
    public virtual float Height => 1f;

    // Punto donde revientan los golpes: el centro del cuerpo, no los pies.
    public Vector3 HitPoint => transform.position + Vector3.up * (Height * 0.5f);

    // Carga la especie. isPlayerSide decide frente/espalda con sprites y
    // orientacion con modelos.
    public abstract void Bind(PokenDefinition definition, bool isPlayerSide);

    // Tinte multiplicativo, incluyendo alpha. Color.white = sin efecto.
    // Con esto se hacen los pulsos de estado y los fundidos al debilitarse.
    public abstract void SetTint(Color tint);

    // Prende y apaga el dibujado sin desactivar el GameObject (el parpadeo al
    // recibir un golpe). Desactivar el objeto cortaria las corrutinas.
    public abstract void SetRendererVisible(bool visible);

    protected void ApplyAnimator(PokenDefinition definition)
    {
        if (animator != null && definition != null && definition.battleAnimator != null)
            animator.runtimeAnimatorController = definition.battleAnimator;
    }
}
