using UnityEngine;

// Hace que el objeto mire siempre a la camara. Es lo que permite que un sprite
// 2D conviva en una escena 3D sin verse como una hoja de papel de canto cuando
// la camara se mueve.
//
// Por defecto gira solo en Y (yaw). Si girara en los tres ejes, con una camara
// picada el sprite se inclinaria hacia atras y el Poken pareceria flotar en vez
// de estar parado. Los sprites se importan con el pivot en los pies
// (SpriteAssigner), asi que girando en Y rotan apoyados en el piso.
public class Billboard : MonoBehaviour
{
    [Header("Camara")]
    [Tooltip("Camara a la que mirar. Vacio = Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Ajustes")]
    [Tooltip("Si esta activo el sprite tambien se inclina con la camara (billboard " +
             "esferico). Dejarlo apagado para personajes parados en el piso.")]
    [SerializeField] private bool tiltWithCamera;

    private Camera _camera;

    // LateUpdate: giro DESPUES de que la camara ya se movio en este frame, para
    // que el sprite no quede un frame atrasado.
    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        if (tiltWithCamera)
        {
            transform.rotation = cam.transform.rotation;
            return;
        }

        // Un sprite se ve de frente cuando su +Z apunta en la misma direccion que
        // el forward de la camara, o sea en la direccion camara -> objeto.
        Vector3 away = transform.position - cam.transform.position;
        away.y = 0f; // aplano: solo yaw

        // La camara esta justo encima o debajo: no hay yaw que calcular, dejo la
        // rotacion como estaba en vez de meter un giro sin sentido.
        if (away.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    // Camera.main hace un FindGameObjectWithTag, asi que lo cacheo. Revalido si
    // la camara cacheada murio (cambio de escena).
    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (_camera == null) _camera = Camera.main;
        return _camera;
    }
}
