using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Mezcla las animaciones de espera y de carrera segun lo rapido que se mueva el
// Poken, y encima de eso puede reproducir un clip suelto (un ataque).
//
// Viene del experimento de combate de accion (rama Ruta-Accion), que se
// descarto. Se conserva entero porque las dos partes siguen sirviendo: la
// mezcla de locomocion para una zona de exploracion, y ReproducirAccion para
// disparar el clip de un movimiento durante un combate por turnos.
//
// Se usa Playables y no un AnimatorController por lo mismo que en
// ModelPokenVisual: los clips vienen adentro del FBX y un controller habria que
// crearlo como asset, uno por especie. Con Playables se reproducen los clips
// sueltos y agregar una especie nueva no crea ningun asset.
//
// El bucle se hace rebobinando a mano en vez de poner wrapMode = Loop, porque
// eso mutaria el AnimationClip, que es un sub-asset COMPARTIDO del FBX: cambiarlo
// afectaria a cualquier otro uso del mismo clip.
public class RunLocomotionAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private RunPlayerController controlador;

    [Header("Clips")]
    [Tooltip("Animacion de espera, en bucle.")]
    [SerializeField] private AnimationClip espera;
    [Tooltip("Animacion de carrera, en bucle.")]
    [SerializeField] private AnimationClip carrera;

    [Header("Mezcla")]
    [Tooltip("Segundos que tarda en pasar de espera a carrera.")]
    [SerializeField] private float transicion = 0.12f;
    [Tooltip("Segundos que tarda en volver a espera. Va mas corto que la entrada " +
             "a proposito: el personaje frena rapido, y si la animacion tardara lo " +
             "mismo seguiria corriendo un instante despues de haberse detenido.")]
    [SerializeField] private float transicionAEspera = 0.06f;
    [Tooltip("Acelera el clip de carrera con la velocidad real. Sin esto los pies " +
             "patinan cuando el personaje avanza mas rapido que su animacion.")]
    [SerializeField] private bool ajustarRitmoAlAvance = true;
    [Tooltip("Rapidez01 a la que el clip de carrera corre a velocidad 1x.")]
    [SerializeField, Range(0.1f, 1f)] private float rapidezDeReferencia = 0.85f;

    [Header("Acciones")]
    [Tooltip("Segundos de mezcla al entrar y salir de un clip de ataque.")]
    [SerializeField] private float mezclaDeAccion = 0.08f;

    private PlayableGraph _grafo;
    private AnimationMixerPlayable _mezclador;
    private AnimationClipPlayable _playableEspera;
    private AnimationClipPlayable _playableCarrera;
    private AnimationClipPlayable _playableAccion;
    private bool _hayAccion;
    private float _peso;
    private float _pesoAccion;

    /// Reproduce un clip de ataque encima de la locomocion. La entrada 2 del
    /// mezclador se recrea con cada clip: AnimationClipPlayable queda atado al
    /// clip con el que se creo y no se le puede cambiar.
    public void ReproducirAccion(AnimationClip clip)
    {
        if (!_grafo.IsValid() || clip == null) return;

        DestruirAccion();
        _playableAccion = AnimationClipPlayable.Create(_grafo, clip);
        _playableAccion.SetApplyFootIK(false);
        _grafo.Connect(_playableAccion, 0, _mezclador, 2);
        _hayAccion = true;
    }

    public void CortarAccion() => _hayAccion = false;

    private void DestruirAccion()
    {
        if (!_playableAccion.IsValid()) return;

        _mezclador.DisconnectInput(2);
        _playableAccion.Destroy();
    }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        controlador = GetComponentInParent<RunPlayerController>();
    }

    private void OnEnable()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (controlador == null) controlador = GetComponentInParent<RunPlayerController>();
        if (animator == null || espera == null || carrera == null) return;

        _grafo = PlayableGraph.Create($"{name}-locomocion");
        // 3 entradas: espera, carrera y la capa de acciones (ataques).
        _mezclador = AnimationMixerPlayable.Create(_grafo, 3);

        _playableEspera = AnimationClipPlayable.Create(_grafo, espera);
        _playableCarrera = AnimationClipPlayable.Create(_grafo, carrera);
        _playableEspera.SetApplyFootIK(false);
        _playableCarrera.SetApplyFootIK(false);

        _grafo.Connect(_playableEspera, 0, _mezclador, 0);
        _grafo.Connect(_playableCarrera, 0, _mezclador, 1);

        AnimationPlayableOutput salida = AnimationPlayableOutput.Create(_grafo, "Animacion", animator);
        salida.SetSourcePlayable(_mezclador);

        _mezclador.SetInputWeight(0, 1f);
        _mezclador.SetInputWeight(1, 0f);
        _grafo.Play();
    }

    private void OnDisable()
    {
        _hayAccion = false;
        _pesoAccion = 0f;
        if (_grafo.IsValid()) _grafo.Destroy();
    }

    private void Update()
    {
        if (!_grafo.IsValid()) return;

        float objetivo = controlador != null ? controlador.Rapidez01 : 0f;

        // El peso persigue a la rapidez en vez de copiarla: sin esto, tocar y
        // soltar la tecla produce un parpadeo entre las dos animaciones.
        float duracion = objetivo > _peso ? transicion : transicionAEspera;
        _peso = duracion <= 0f
            ? objetivo
            : Mathf.MoveTowards(_peso, objetivo, Time.deltaTime / duracion);

        // La capa de accion se impone sobre la locomocion, y lo que le sobra de
        // peso se reparte entre espera y carrera. Asi el ataque tapa el correr
        // sin cortarlo de golpe.
        float objetivoAccion = _hayAccion ? 1f : 0f;
        _pesoAccion = mezclaDeAccion <= 0f
            ? objetivoAccion
            : Mathf.MoveTowards(_pesoAccion, objetivoAccion, Time.deltaTime / mezclaDeAccion);

        float locomocion = 1f - _pesoAccion;
        _mezclador.SetInputWeight(0, locomocion * (1f - _peso));
        _mezclador.SetInputWeight(1, locomocion * _peso);
        if (_playableAccion.IsValid()) _mezclador.SetInputWeight(2, _pesoAccion);

        if (ajustarRitmoAlAvance && objetivo > 0.01f)
            _playableCarrera.SetSpeed(Mathf.Max(0.4f, objetivo / rapidezDeReferencia));

        Rebobinar(_playableEspera, espera.length);
        Rebobinar(_playableCarrera, carrera.length);
    }

    private static void Rebobinar(AnimationClipPlayable playable, float duracion)
    {
        if (duracion <= 0f) return;

        double tiempo = playable.GetTime();
        if (tiempo >= duracion) playable.SetTime(tiempo % duracion);
    }
}
