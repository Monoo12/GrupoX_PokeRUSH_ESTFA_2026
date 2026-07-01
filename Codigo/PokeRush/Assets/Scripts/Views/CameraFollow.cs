using UnityEngine;

// Hace que la camara siga suavemente a un objetivo (el jugador) en 2D,
// conservando su distancia en Z. Se coloca en el Main Camera.
public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Transform a seguir, normalmente el jugador.")]
    [SerializeField] private Transform target;

    [Header("Ajustes")]
    [Tooltip("Suavizado del seguimiento. 0 = instantaneo; mas alto = mas lento.")]
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("Corrimiento respecto del objetivo (util para descentrar la camara).")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    private Vector3 _velocity;

    // LateUpdate: corro la camara DESPUES de que el jugador ya se movio en el
    // frame, para evitar temblor.
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z); // conservo la Z de la camara

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}
