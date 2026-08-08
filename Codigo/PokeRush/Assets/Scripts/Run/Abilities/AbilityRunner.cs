using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ejecuta habilidades: lleva los enfriamientos, reproduce el clip, abre la
// ventana de daño en el instante configurado y resuelve el impacto.
//
// Lo usan IGUAL el jugador y la IA. Que el rival ataque con el mismo sistema es
// lo que hace que sus golpes sean legibles: la misma anticipacion, la misma
// ventana, el mismo indicador. Si la IA tuviera su propio camino, sus ataques se
// sentirian tramposos.
[RequireComponent(typeof(RunCombatant))]
public class AbilityRunner : MonoBehaviour
{
    [Header("Habilidades")]
    [Tooltip("Habilidades con enfriamiento. En el modelo de UNITE son dos; la " +
             "definitiva entra en la fase 5.")]
    [SerializeField] private AbilityDefinition[] habilidades = new AbilityDefinition[2];

    [Header("Referencias")]
    [SerializeField] private RunLocomotionAnimator animador;
    [SerializeField] private HitBurst efectos;

    private RunCombatant _combatiente;
    private float[] _listaEn;
    private Coroutine _enCurso;

    // Identifica cada ejecucion. La animacion sigue corriendo despues de que se
    // libera el control, asi que hay que saber si el golpe que la lanzo sigue
    // siendo el ultimo: si no, cortarla apagaria la animacion del golpe nuevo.
    private int _ejecucionActual;

    public int Cantidad => habilidades != null ? habilidades.Length : 0;
    public bool Ocupado => _enCurso != null;

    /// La habilidad que se esta ejecutando, o null. El indicador la usa para
    /// dejar de dibujarse mientras dura el golpe.
    public AbilityDefinition EnEjecucion { get; private set; }

    /// Se emite al resolver el golpe, con cuantos alcanzo. La cadena de basicos
    /// la escucha para avanzar solo cuando pega: encadenar al aire deberia
    /// castigarse, no premiarse.
    public event System.Action<AbilityDefinition, int> Impacto;

    private void Awake()
    {
        _combatiente = GetComponent<RunCombatant>();
        if (animador == null) animador = GetComponent<RunLocomotionAnimator>();
        if (efectos == null) efectos = GetComponent<HitBurst>();
        _listaEn = new float[Cantidad];
    }

    public AbilityDefinition Habilidad(int indice)
    {
        if (habilidades == null || indice < 0 || indice >= habilidades.Length) return null;
        return habilidades[indice];
    }

    /// Cuanto le falta para estar lista, de 1 (recien usada) a 0 (lista). Es lo
    /// que dibuja el relleno del icono en el HUD.
    public float Enfriamiento01(int indice)
    {
        AbilityDefinition habilidad = Habilidad(indice);
        if (habilidad == null || habilidad.enfriamiento <= 0f) return 0f;

        float restante = _listaEn[indice] - Time.time;
        return Mathf.Clamp01(restante / habilidad.enfriamiento);
    }

    public bool Disponible(int indice)
    {
        return Habilidad(indice) != null
               && !Ocupado
               && _combatiente.PuedeActuar
               && Time.time >= _listaEn[indice];
    }

    public void Usar(int indice, Vector3 puntoApuntado)
    {
        if (!Disponible(indice)) return;

        AbilityDefinition habilidad = habilidades[indice];
        _listaEn[indice] = Time.time + habilidad.enfriamiento;
        _enCurso = StartCoroutine(Ejecutar(habilidad, puntoApuntado));
    }

    /// Ejecuta una habilidad suelta, sin ocupar un slot ni consumir enfriamiento.
    /// Lo usa la cadena de ataques basicos.
    public Coroutine EjecutarSuelta(AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        if (habilidad == null || Ocupado || !_combatiente.PuedeActuar) return null;

        _enCurso = StartCoroutine(Ejecutar(habilidad, puntoApuntado));
        return _enCurso;
    }

    private IEnumerator Ejecutar(AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        EnEjecucion = habilidad;
        int mia = ++_ejecucionActual;

        // Encarar el objetivo antes de arrancar: pegarle a algo que quedo a la
        // espalda se ve roto por mas que el impacto resuelva bien.
        Encarar(puntoApuntado);
        if (animador != null) animador.ReproducirAccion(habilidad.clip);

        // El FX de lanzamiento sale con la animacion, no con el golpe: es la
        // anticipacion que le avisa al rival que algo viene.
        if (habilidad.fxLanzamiento != null)
            UniteFx.Reproducir(habilidad.fxLanzamiento,
                               transform.position + Vector3.up * 0.5f, transform.rotation,
                               Mathf.Max(0.3f, habilidad.ventanaFin), habilidad.TamañoDeFx,
                               BattleFxView.ColorForType(habilidad.tipo));

        // Anticipacion. Esta ventana es el telegrafiado: mientras dura, el rival
        // todavia puede leer el golpe y salirse.
        yield return new WaitForSeconds(habilidad.ventanaInicio);

        if (habilidad.apuntado == AimMode.Dash)
            yield return Desplazar(habilidad, puntoApuntado);

        Golpear(habilidad, puntoApuntado);

        // Recuperacion: no puede encadenar hasta que pase el bloqueo.
        float restante = Mathf.Max(0f, habilidad.bloqueo - habilidad.ventanaInicio);
        if (restante > 0f) yield return new WaitForSeconds(restante);

        EnEjecucion = null;
        _enCurso = null;

        // La ANIMACION sigue hasta el final aunque ya hayas recuperado el
        // control. Son dos cosas distintas y antes estaban atadas al mismo
        // valor: con bloqueo al 50% del clip, la patada se cortaba por la mitad
        // y volvia de golpe a la pose de espera. Eso es lo que se veia roto.
        if (animador != null)
        {
            float sobra = habilidad.Duracion - habilidad.bloqueo;
            if (sobra > 0f) yield return new WaitForSeconds(sobra);

            // Solo si en el medio no arranco otro golpe.
            if (mia == _ejecucionActual) animador.CortarAccion();
        }
    }

    private void Encarar(Vector3 puntoApuntado)
    {
        Vector3 hacia = Vector3.ProjectOnPlane(puntoApuntado - transform.position, Vector3.up);
        if (hacia.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(hacia, Vector3.up);
    }

    // El dash lo mueve el controlador y no la animacion, porque los clips del
    // pack vienen sin root motion: corren en el lugar.
    private IEnumerator Desplazar(AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        var controlador = GetComponent<CharacterController>();
        Vector3 direccion = Vector3.ProjectOnPlane(puntoApuntado - transform.position, Vector3.up).normalized;
        if (direccion.sqrMagnitude < 0.01f) direccion = transform.forward;

        float duracion = Mathf.Max(0.05f, habilidad.ventanaFin - habilidad.ventanaInicio);
        float velocidad = habilidad.rango / duracion;

        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            Vector3 paso = direccion * velocidad * Time.deltaTime;
            if (controlador != null) controlador.Move(paso);
            else transform.position += paso;
            yield return null;
        }
    }

    private void Golpear(AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        if (habilidad.apuntado == AimMode.Propia)
        {
            _combatiente.AplicarControl(habilidad.control, Vector3.zero);
            Impacto?.Invoke(habilidad, 1);
            return;
        }

        List<RunCombatant> alcanzados = HitResolver.Alcanzados(_combatiente, habilidad, puntoApuntado);
        foreach (RunCombatant objetivo in alcanzados)
            objetivo.RecibirGolpe(_combatiente, habilidad, transform.position);

        // Un FX de impacto por cada alcanzado: pegarle a tres tiene que verse
        // como tres golpes, no como uno.
        if (habilidad.fxImpacto != null)
        {
            foreach (RunCombatant objetivo in alcanzados)
                UniteFx.Reproducir(habilidad.fxImpacto, objetivo.PuntoDeImpacto,
                                   Quaternion.LookRotation(objetivo.transform.position - transform.position),
                                   0.4f, habilidad.TamañoDeFx,
                                   BattleFxView.ColorForType(habilidad.tipo));
        }

        // El chispazo procedural queda de respaldo: cubre el golpe al aire, que
        // no tiene FX propio en el pack, y las especies sin efectos copiados.
        if (efectos != null && (habilidad.fxImpacto == null || alcanzados.Count == 0))
            efectos.Reventar(HitResolver.PuntoDeImpacto(_combatiente, habilidad, puntoApuntado, alcanzados),
                             habilidad, alcanzados.Count > 0);

        Impacto?.Invoke(habilidad, alcanzados.Count);
    }
}
