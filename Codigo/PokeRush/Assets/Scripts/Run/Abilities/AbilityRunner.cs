using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ejecuta habilidades: lleva los enfriamientos, reproduce el clip, mantiene
// abierta la ventana de daño y resuelve los impactos.
//
// Lo usan IGUAL el jugador y la IA. Que el rival ataque con el mismo sistema es
// lo que hace que sus golpes sean legibles: la misma anticipacion, la misma
// ventana, el mismo indicador. Si la IA tuviera su propio camino, sus ataques se
// sentirian tramposos.
//
// La linea de tiempo de un golpe tiene cuatro tramos, y hay que no confundirlos
// -confundir los dos ultimos es lo que hacia que las animaciones se vieran
// cortadas por la mitad:
//
//   0 .............. ventanaInicio    anticipacion; el rival todavia puede salirse
//   ventanaInicio .. ventanaFin       VENTANA DE DAÑO; golpea a quien este adentro
//   ventanaFin ..... bloqueo          recuperacion; no se puede mover ni encadenar
//   bloqueo ........ fin del clip     la animacion termina sola, ya con el control devuelto
[RequireComponent(typeof(RunCombatant))]
public class AbilityRunner : MonoBehaviour
{
    [Header("Habilidades")]
    [Tooltip("Habilidades con enfriamiento. En el modelo de UNITE son dos; la " +
             "definitiva entra en la fase 5.")]
    [SerializeField] private AbilityDefinition[] habilidades = new AbilityDefinition[2];

    [Header("Referencias")]
    [SerializeField] private RunLocomotionAnimator animador;
    [Tooltip("De aca sale el efecto de cada golpe segun su tipo. Una habilidad " +
             "puede traer el suyo propio y entonces gana el de la habilidad.")]
    [SerializeField] private FxCatalog catalogo;

    [Header("Depuracion")]
    [Tooltip("Dibuja en la vista de escena la hitbox del ultimo golpe. Es la " +
             "unica forma de ver si lo que golpea coincide con lo que se dibuja.")]
    [SerializeField] private bool dibujarHitbox = true;

    private RunCombatant _combatiente;
    private float[] _listaEn;
    private Coroutine _enCurso;

    // Quienes ya cobraron ESTE golpe. La ventana dura varios cuadros, asi que
    // sin esto un rival quieto adentro del area recibiria el ataque entero una
    // vez por cuadro.
    private readonly HashSet<RunCombatant> _yaGolpeados = new HashSet<RunCombatant>();

    // Identifica cada ejecucion. La animacion sigue corriendo despues de que se
    // libera el control, asi que hay que saber si el golpe que la lanzo sigue
    // siendo el ultimo: si no, cortarla apagaria la animacion del golpe nuevo.
    private int _ejecucionActual;

    // Ultima hitbox resuelta, para poder dibujarla.
    private AbilityDefinition _ultimaHabilidad;
    private Vector3 _ultimoPunto;
    private float _ultimaHasta;

    public int Cantidad => habilidades != null ? habilidades.Length : 0;
    public bool Ocupado => _enCurso != null;

    /// La habilidad que se esta ejecutando, o null. El indicador la usa para
    /// dejar de dibujarse mientras dura el golpe.
    public AbilityDefinition EnEjecucion { get; private set; }

    /// Se emite al CERRARSE la ventana de daño, con a cuantos alcanzo en total.
    /// La cadena de basicos la escucha para avanzar solo cuando pega: encadenar
    /// al aire deberia castigarse, no premiarse.
    public event System.Action<AbilityDefinition, int> Impacto;

    private void Awake()
    {
        _combatiente = GetComponent<RunCombatant>();
        if (animador == null) animador = GetComponent<RunLocomotionAnimator>();
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
        // anticipacion. Va colgado del Poken para que lo acompañe si se mueve.
        if (habilidad.fxLanzamiento != null)
            FxPlayer.Reproducir(habilidad.fxLanzamiento, transform.position + Vector3.up * 0.6f,
                                transform.rotation, habilidad.TamañoDeFx * 0.6f, transform);
        else if (catalogo != null)
            FxPlayer.Reproducir(catalogo.Lanzamiento(habilidad.tipo), transform.position + Vector3.up * 0.6f,
                                transform.rotation, habilidad.TamañoDeFx * 0.6f, transform);

        yield return new WaitForSeconds(habilidad.ventanaInicio);

        // Las habilidades sobre uno mismo no tienen ventana: se aplican y listo.
        if (habilidad.apuntado == AimMode.Propia)
        {
            _combatiente.AplicarControl(habilidad.control, Vector3.zero);
            Impacto?.Invoke(habilidad, 1);
        }
        else
        {
            yield return VentanaDeDaño(habilidad, puntoApuntado);
        }

        // Recuperacion: no puede encadenar hasta que pase el bloqueo.
        float restante = habilidad.bloqueo - habilidad.ventanaFin;
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

    // El golpe esta ACTIVO durante toda la ventana, no en un instante.
    //
    // Se consulta cuadro a cuadro con la posicion de ahora, asi que quien entra
    // al area con el ataque ya empezado igual la cobra, y un dash barre a todo
    // el que atropella en el camino. Cada rival cobra una sola vez por golpe.
    private IEnumerator VentanaDeDaño(AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        _yaGolpeados.Clear();

        if (dibujarHitbox)
        {
            _ultimaHabilidad = habilidad;
            _ultimoPunto = puntoApuntado;
        }

        // El arco es OPCIONAL y por defecto no hay ninguno. Ver FxCatalog: en un
        // golpe cuerpo a cuerpo la estela la hace la animacion, y cualquier
        // efecto suelto flotando delante del Poken se lee como una bola.
        //
        // Nace adelante y a media altura, no encima del cuerpo, para cuando se
        // le cargue uno a una habilidad concreta.
        Vector3 puntoArco = transform.position
                            + transform.forward * (habilidad.rango * 0.45f)
                            + Vector3.up * 0.8f;

        if (habilidad.fxArco != null)
            FxPlayer.Reproducir(habilidad.fxArco, puntoArco, transform.rotation, habilidad.TamañoDeFx);
        else if (catalogo != null)
            FxPlayer.Reproducir(catalogo.Arco(habilidad.tipo), puntoArco, transform.rotation, habilidad.TamañoDeFx);

        // El desplazamiento corre EN PARALELO a la ventana, no antes. Si se
        // esperara a que termine, el dash llegaria hasta el final y recien ahi
        // golpearia: atravesar a alguien no haria nada.
        if (habilidad.apuntado == AimMode.Dash)
            StartCoroutine(Desplazar(habilidad, puntoApuntado));

        // Piso de un cuadro: una ventana de duracion cero no llegaria a golpear
        // nunca, y es facil dejarla asi sin querer al afinar valores.
        float duracion = Mathf.Max(habilidad.ventanaFin - habilidad.ventanaInicio, 0.01f);

        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            if (dibujarHitbox) _ultimaHasta = Time.time + 0.5f;

            foreach (RunCombatant objetivo in HitResolver.Alcanzados(_combatiente, habilidad, puntoApuntado))
            {
                if (!_yaGolpeados.Add(objetivo)) continue;

                objetivo.RecibirGolpe(_combatiente, habilidad, transform.position);
                ReventarImpacto(habilidad, objetivo);
            }
            yield return null;
        }

        // Al aire: el golpe se marca EN EL SUELO, en el medio de donde barrio, y
        // mas chico. Asi el jugador ve que el ataque salio y donde llego -que es
        // lo que necesita para corregir la distancia- sin que aparezca un
        // destello flotando en el aire, que es lo que se lee como un proyectil.
        if (_yaGolpeados.Count == 0 && catalogo != null)
        {
            Vector3 punto = HitResolver.PuntoDeImpacto(_combatiente, habilidad, puntoApuntado, null);
            punto.y = transform.position.y + 0.12f;
            FxPlayer.Reproducir(catalogo.Impacto(habilidad.tipo), punto, transform.rotation,
                                habilidad.TamañoDeFx * 0.55f);
        }

        Impacto?.Invoke(habilidad, _yaGolpeados.Count);
    }

    // Un efecto por cada alcanzado: pegarle a tres tiene que verse como tres
    // golpes, no como uno.
    private void ReventarImpacto(AbilityDefinition habilidad, RunCombatant objetivo)
    {
        // Mira hacia afuera, en la direccion en que viajo el golpe: un impacto
        // orientado al reves se lee como si el rival hubiera pegado el.
        Vector3 hacia = objetivo.transform.position - transform.position;
        Quaternion rotacion = hacia.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(hacia.normalized, Vector3.up)
            : transform.rotation;

        // El impacto se dibuja al TAMAÑO DEL CUERPO que lo recibe, no al del
        // area de la habilidad: un area de 5 metros que pega en un solo rival
        // tiene que verse como un golpe en ese rival, no como una explosion que
        // lo tapa entero. El piso de 1,5 es igual de importante: por debajo de
        // eso deja de leerse como un golpe y pasa a ser una bolita.
        float tamaño = Mathf.Clamp(habilidad.TamañoDeFx, 1.5f, 2.5f);

        if (habilidad.fxImpacto != null)
            FxPlayer.Reproducir(habilidad.fxImpacto, objetivo.PuntoDeImpacto, rotacion, tamaño);
        else if (catalogo != null)
            FxPlayer.Reproducir(catalogo.Impacto(habilidad.tipo), objetivo.PuntoDeImpacto, rotacion, tamaño);
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

    // Dibuja la hitbox que de verdad se consulto. Sirve para contestar la unica
    // pregunta que importa cuando un ataque se siente injusto: si lo que golpea
    // es lo mismo que se ve en el suelo.
    private void OnDrawGizmos()
    {
        if (!dibujarHitbox || _ultimaHabilidad == null || Time.time > _ultimaHasta) return;

        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.9f);
        Vector3 origen = transform.position;
        Vector3 direccion = Vector3.ProjectOnPlane(_ultimoPunto - origen, Vector3.up);
        direccion = direccion.sqrMagnitude < 0.01f ? transform.forward : direccion.normalized;

        switch (_ultimaHabilidad.apuntado)
        {
            case AimMode.ConoFrontal:
                float mitad = _ultimaHabilidad.anguloCono * 0.5f;
                for (int i = 0; i <= 16; i++)
                {
                    Vector3 rayo = Quaternion.AngleAxis(-mitad + _ultimaHabilidad.anguloCono * i / 16f, Vector3.up) * direccion;
                    Gizmos.DrawLine(origen, origen + rayo * _ultimaHabilidad.rango);
                }
                break;

            case AimMode.SkillshotLineal:
                Gizmos.DrawWireSphere(origen, _ultimaHabilidad.radio);
                Gizmos.DrawWireSphere(origen + direccion * _ultimaHabilidad.rango, _ultimaHabilidad.radio);
                Gizmos.DrawLine(origen, origen + direccion * _ultimaHabilidad.rango);
                break;

            case AimMode.AreaEnSuelo:
                Gizmos.DrawWireSphere(
                    origen + Vector3.ClampMagnitude(_ultimoPunto - origen, _ultimaHabilidad.rango),
                    _ultimaHabilidad.radio);
                break;

            default:
                Gizmos.DrawWireSphere(origen, _ultimaHabilidad.radio);
                break;
        }
    }
}
