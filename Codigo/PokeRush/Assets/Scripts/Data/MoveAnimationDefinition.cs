using UnityEngine;

// Animacion PROPIA de un movimiento concreto. Es opcional: si un MoveDefinition
// no tiene una asignada (campo animationOverride), el combate usa la animacion
// generica segun su categoria y tipo (embestida fisica / proyectil especial /
// destello de estado). Diseño crea estos assets solo para los movimientos que
// quiera destacar, sin tocar codigo.
[CreateAssetMenu(fileName = "NewMoveAnimation", menuName = "PokeRush/MoveAnimationDefinition")]
public class MoveAnimationDefinition : ScriptableObject
{
    [Header("Tiempos (segundos)")]
    [Tooltip("Anticipacion del atacante antes de lanzar (retroceso/carga).")]
    public float windupTime = 0.18f;
    [Tooltip("Viaje del proyectil hasta el objetivo (0 = golpe directo, sin proyectil).")]
    public float travelTime = 0.25f;
    [Tooltip("Duracion del impacto (flash + sacudida). El daño se aplica al terminar.")]
    public float impactTime = 0.22f;
    [Tooltip("Recuperacion del atacante despues del golpe.")]
    public float recoveryTime = 0.12f;

    [Header("Visual")]
    [Tooltip("Sprite del proyectil (vacio = particula generica teñida del color del tipo).")]
    public Sprite projectileSprite;
    [Tooltip("Sprite del impacto sobre el objetivo (vacio = destello generico).")]
    public Sprite impactSprite;
    [Tooltip("Tinte del proyectil/impacto. Alpha 0 = usar el color del tipo del movimiento.")]
    public Color tint = new Color(1f, 1f, 1f, 0f);
    [Range(0f, 0.3f)]
    [Tooltip("Fuerza de la sacudida de camara en el impacto (0 = sin sacudida).")]
    public float cameraShake = 0.06f;
    [Tooltip("Flash de pantalla en el impacto.")]
    public bool screenFlash = false;

    [Header("Sonido (opcional, cuando haya sfx)")]
    public AudioClip windupSound;
    public AudioClip impactSound;
}
