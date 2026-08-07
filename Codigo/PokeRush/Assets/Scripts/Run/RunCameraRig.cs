using UnityEngine;
using UnityEngine.InputSystem;

// Camara de la ruta: tercera persona elevada, mirando al Poken desde atras y
// arriba. Es el encuadre de UNITE, que deja ver el terreno alrededor sin perder
// de vista al personaje.
//
// La camara NO gira con el personaje. Si girara, el mapeo de WASD cambiaria
// mientras te movés y el control se volveria mareante. Con angulo fijo, "W" es
// siempre la misma direccion en pantalla.
//
// Ademas resuelve el punto al que apunta el mouse, que es lo que van a usar las
// habilidades apuntadas de la fase siguiente.
public class RunCameraRig : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform objetivo;

    [Header("Encuadre")]
    [Tooltip("Distancia en linea recta desde el objetivo hasta la camara. Es el " +
             "primer dial a mover para ajustar el encuadre: 6-7 deja ver bien la " +
             "animacion del Poken, 9-12 da mas lectura del terreno alrededor. " +
             "Los modelos de UNITE miden alrededor de 1,4 m.")]
    [SerializeField] private float distancia = 6.5f;
    [Tooltip("Inclinacion en grados. 45-55 es el rango tipico de un MOBA: mas " +
             "alto se vuelve cenital y se pierde la silueta del personaje.")]
    [SerializeField, Range(20f, 80f)] private float inclinacion = 48f;
    [Tooltip("Cuanto mira por encima de los pies del objetivo.")]
    [SerializeField] private float alturaDeMira = 1.2f;

    [Header("Seguimiento")]
    [Tooltip("Segundos que tarda en alcanzar al objetivo. 0 = pegada, sin suavizado.")]
    [SerializeField] private float suavizado = 0.12f;
    [Tooltip("Cuanto se corre la camara hacia el cursor, de 0 a 1. Deja ver un " +
             "poco mas del lado al que estas apuntando.")]
    [SerializeField, Range(0f, 0.5f)] private float adelantoHaciaCursor = 0.15f;

    private Vector3 _velocidadSuavizado;
    private bool _colocada;

    /// Punto del suelo bajo el cursor, a la altura del objetivo. Las habilidades
    /// apuntadas de la fase 3 se dirigen hacia aca.
    public Vector3 PuntoApuntado { get; private set; }

    public void Seguir(Transform nuevoObjetivo) => objetivo = nuevoObjetivo;

    private void LateUpdate()
    {
        if (objetivo == null) return;

        ActualizarPuntoApuntado();

        Vector3 centro = objetivo.position + Vector3.up * alturaDeMira;

        // Corrimiento hacia el cursor, acotado para que la camara no se despegue
        // del personaje cuando el mouse se va al borde de la pantalla.
        Vector3 corrimiento = Vector3.ClampMagnitude(
            (PuntoApuntado - objetivo.position) * adelantoHaciaCursor, distancia * 0.35f);
        centro += Vector3.ProjectOnPlane(corrimiento, Vector3.up);

        // La altura sale de la inclinacion y la distancia, no de un campo aparte:
        // con los dos por separado es facil dejarlos incoherentes y que la camara
        // termine mirando desde mucho mas arriba de lo que dice su angulo.
        Quaternion giro = Quaternion.Euler(inclinacion, 0f, 0f);
        Vector3 destino = centro - giro * Vector3.forward * distancia;

        // El primer cuadro se coloca de un salto. Si arrancara suavizando desde
        // donde quedo la camara en la escena, se la veria volar hasta su lugar
        // cada vez que empieza un nivel.
        if (!_colocada || suavizado <= 0f)
        {
            transform.position = destino;
            _velocidadSuavizado = Vector3.zero;
            _colocada = true;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, destino, ref _velocidadSuavizado, suavizado);
        }

        transform.LookAt(centro);
    }

    // El plano de apuntado esta a la altura del objetivo, no en y=0: si el
    // personaje sube una rampa, el cursor tiene que seguir cayendo sobre el
    // terreno que pisa.
    private void ActualizarPuntoApuntado()
    {
        Camera camara = GetComponent<Camera>();
        if (camara == null || Mouse.current == null) return;

        Ray rayo = camara.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plano = new Plane(Vector3.up, objetivo.position);

        if (plano.Raycast(rayo, out float distanciaAlPlano))
            PuntoApuntado = rayo.GetPoint(distanciaAlPlano);
    }
}
