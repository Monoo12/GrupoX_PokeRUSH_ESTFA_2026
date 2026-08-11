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
    [Tooltip("Sprite del impacto sobre el objetivo (vacio = destello generico). " +
             "Se ignora si impactFrames tiene contenido.")]
    public Sprite impactSprite;

    [Header("Secuencia del proyectil (opcional)")]
    [Tooltip("Frames que se reproducen MIENTRAS el proyectil viaja hacia el objetivo. " +
             "Es lo que hace que un Lanzallamas se vea como un chorro y no como una bola. " +
             "Con contenido, manda sobre projectileSprite.")]
    public Sprite[] projectileFrames;
    [Range(1f, 60f)]
    [Tooltip("Velocidad de projectileFrames. La secuencia se repite en loop durante el viaje.")]
    public float projectileFps = 20f;
    [Tooltip("Escala de los frames del proyectil, en unidades de mundo.")]
    public float projectileScale = 1f;

    [Header("Secuencia de impacto (opcional)")]
    [Tooltip("Frames de la animacion que se reproduce sobre el objetivo, en orden. " +
             "Con contenido, manda sobre impactSprite y la duracion pasa a ser frames/impactFps " +
             "en vez de impactTime.")]
    public Sprite[] impactFrames;
    [Range(1f, 60f)]
    [Tooltip("Velocidad de reproduccion de impactFrames.")]
    public float impactFps = 20f;
    [Tooltip("Escala de los frames sobre el objetivo (las hojas de 192px suelen " +
             "necesitar achicarse para no tapar la escena).")]
    public float impactScale = 1f;
    [Tooltip("Teñir los frames con el color del tipo. Dejalo en false para arte a " +
             "todo color (una llamarada ya viene naranja); en true para siluetas genericas.")]
    public bool tintImpactFrames = false;
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
