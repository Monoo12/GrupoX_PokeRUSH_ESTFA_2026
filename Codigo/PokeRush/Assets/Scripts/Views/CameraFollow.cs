using UnityEngine;

// Hace que la camara siga suavemente a un objetivo (el jugador) en 2D,
// conservando su distancia en Z. Se coloca en el Main Camera.
//
// RAYAS ENTRE TILES
// Los tiles se dibujan a escala no entera y, con el suavizado, la camara queda
// casi siempre en posiciones sub-pixel. Cuando el borde entre dos tiles cae
// justo en el centro de un pixel, la GPU lee el pixel del tile de AL LADO en la
// hoja y aparece una raya fina que va y viene al caminar. Se arregla con dos
// cosas, que tocan solo lo que se dibuja (el suavizado se calcula igual):
//   1. cada celda mide una cantidad ENTERA de pixeles de pantalla: el zoom se
//      ajusta apenas (a 1080p no cambia: 7.5 = 72 px por celda);
//   2. la camara se pega a la grilla de pixeles.
// Con las dos, los bordes de los tiles caen siempre entre pixeles y nunca
// sobre uno.
[RequireComponent(typeof(Camera))]
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

    [Header("Pixeles")]
    [Tooltip("Pega la camara a la grilla de pixeles y deja cada celda con una cantidad " +
             "entera de pixeles. Sin esto aparecen rayas finas entre tiles que van y " +
             "vienen al caminar. El zoom puede variar un 1-3% segun la resolucion; " +
             "a 1080p queda exacto.")]
    [SerializeField] private bool evitarRayasEntreTiles = true;
    [Tooltip("Unidades de mundo por celda del Grid (normalmente 1).")]
    [SerializeField] private float tamanioDeCelda = 1f;

    private Camera _cam;
    // El zoom de diseno (el que esta guardado en la escena). El que se usa se
    // recalcula a partir de este en cada frame, por si cambia la resolucion.
    private float _orthoBase;
    // Posicion suavizada SIN redondear: el suavizado trabaja sobre esta. Si
    // trabajara sobre la redondeada, la velocidad del SmoothDamp saltaria de a
    // pixel y la camara temblaria.
    private Vector3 _posSuave;
    private Vector3 _ultimaDibujada;
    private Vector3 _velocity;

    // Multiplica el zoom de diseno: 1 = el de la escena, mas = la camara se
    // aleja. Lo usa el final del juego en la cueva ("la camara se aleja
    // lentamente"). Con evitarRayasEntreTiles el zoom va de a pixel por celda.
    public float Alejamiento { get; set; } = 1f;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _orthoBase = _cam.orthographicSize;
        Resincronizar();
    }

    // Al volver de un combate (MapBattleBridge apaga el mapa entero) se sigue
    // desde donde quedo, sin inercia vieja.
    private void OnEnable()
    {
        Resincronizar();
    }

    private void Resincronizar()
    {
        _posSuave = transform.position;
        _ultimaDibujada = transform.position;
        _velocity = Vector3.zero;
    }

    // LateUpdate: corro la camara DESPUES de que el jugador ya se movio en el
    // frame, para evitar temblor.
    private void LateUpdate()
    {
        // Si alguien movio la camara desde afuera (un teletransporte, una
        // herramienta), se sigue desde ahi en vez de arrastrarla de vuelta.
        if ((transform.position - _ultimaDibujada).sqrMagnitude > 0.0001f)
            _posSuave = transform.position;

        if (target != null)
        {
            Vector3 desired = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                _posSuave.z); // conservo la Z de la camara

            _posSuave = Vector3.SmoothDamp(_posSuave, desired, ref _velocity, smoothTime);
        }

        Vector3 dibujada = _posSuave;
        float ortho = _orthoBase * Mathf.Max(0.1f, Alejamiento);
        if (evitarRayasEntreTiles && _cam != null && _cam.orthographic && _cam.pixelHeight > 0)
        {
            float celda = Mathf.Max(0.0001f, tamanioDeCelda);
            int pixeles = PixelesPorCelda(_cam.pixelHeight, ortho, celda);
            _cam.orthographicSize = _cam.pixelHeight * celda / (2f * pixeles);
            dibujada = PegarAlPixel(_posSuave, pixeles / celda, _cam.pixelWidth, _cam.pixelHeight);
        }
        else if (_cam != null && !Mathf.Approximately(Alejamiento, 1f))
        {
            _cam.orthographicSize = ortho;
        }

        transform.position = dibujada;
        _ultimaDibujada = dibujada;
    }

    // Cuantos pixeles de pantalla mide una celda con el zoom mas parecido al de
    // diseno. Nunca menos de 1.
    public static int PixelesPorCelda(int altoEnPixeles, float orthoBase, float celda = 1f)
    {
        if (orthoBase <= 0f) return 1;
        return Mathf.Max(1, Mathf.RoundToInt(altoEnPixeles * celda / (2f * orthoBase)));
    }

    // Redondea la posicion para que los bordes de celda (multiplos enteros de
    // la celda) caigan entre pixeles de pantalla.
    //
    // El centro de la camara se dibuja en el centro de la pantalla: con un ancho
    // o un alto impar ese centro esta en medio pixel, y por eso se resta la
    // mitad de la pantalla antes de redondear.
    public static Vector3 PegarAlPixel(Vector3 posicion, float pixelesPorUnidad, int ancho, int alto)
    {
        if (pixelesPorUnidad <= 0f) return posicion;

        float x = (Mathf.Round(posicion.x * pixelesPorUnidad - ancho * 0.5f) + ancho * 0.5f) / pixelesPorUnidad;
        float y = (Mathf.Round(posicion.y * pixelesPorUnidad - alto * 0.5f) + alto * 0.5f) / pixelesPorUnidad;
        return new Vector3(x, y, posicion.z);
    }
}
