using System.Collections;
using UnityEngine;

// Feedback de golpe para los muñecos de prueba de la fase 3.
//
// El pack de UNITE NO trae animacion de recibir daño en ninguna de las 78
// especies, asi que el impacto hay que comunicarlo por otro lado. Aca se hace
// con un tinte rojo y un retroceso corto: alcanza para juzgar si pegar se siente
// contundente, que es el criterio de esta fase.
//
// Cuando exista la IA de ruta (fase 4) esto se reemplaza por su reaccion real.
[RequireComponent(typeof(RunCombatant))]
public class RunDummyFeedback : MonoBehaviour
{
    [Header("Golpe")]
    [SerializeField] private Color tinteDeGolpe = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float duracionDelTinte = 0.12f;
    [Tooltip("Unidades que retrocede al recibir un golpe.")]
    [SerializeField] private float retroceso = 0.35f;

    [Header("Derrota")]
    [Tooltip("Segundos hasta reaparecer con la vida llena. 0 = se queda caido.")]
    [SerializeField] private float reaparecerEn = 4f;

    private RunCombatant _combatiente;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _bloque;
    private Coroutine _tinte;
    private Vector3 _posicionInicial;

    private void Awake()
    {
        _combatiente = GetComponent<RunCombatant>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _bloque = new MaterialPropertyBlock();
        _posicionInicial = transform.position;
    }

    private void OnEnable()
    {
        _combatiente.Golpeado += AlSerGolpeado;
        _combatiente.Derrotado += AlSerDerrotado;
    }

    private void OnDisable()
    {
        _combatiente.Golpeado -= AlSerGolpeado;
        _combatiente.Derrotado -= AlSerDerrotado;
    }

    private void AlSerGolpeado(DamageResult resultado)
    {
        if (_tinte != null) StopCoroutine(_tinte);
        _tinte = StartCoroutine(Destellar(resultado));

        if (retroceso > 0f)
        {
            var controlador = GetComponent<CharacterController>();
            Vector3 paso = -transform.forward * retroceso;
            if (controlador != null) controlador.Move(paso);
        }
    }

    // La efectividad de tipos se lee en la intensidad del destello: un golpe
    // super efectivo destella mas fuerte y mas tiempo.
    private IEnumerator Destellar(DamageResult resultado)
    {
        float intensidad = Mathf.Clamp01(0.5f + resultado.Effectiveness * 0.25f);
        float duracion = duracionDelTinte * (resultado.IsCritical ? 1.8f : 1f);

        Tintar(Color.Lerp(Color.white, tinteDeGolpe, intensidad));
        yield return new WaitForSeconds(duracion);
        Tintar(Color.white);
        _tinte = null;
    }

    private void Tintar(Color color)
    {
        foreach (Renderer renderer in _renderers)
        {
            renderer.GetPropertyBlock(_bloque);
            _bloque.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(_bloque);
        }
    }

    private void AlSerDerrotado()
    {
        StartCoroutine(Caer());
    }

    private IEnumerator Caer()
    {
        Tintar(new Color(0.4f, 0.4f, 0.45f));
        foreach (Renderer renderer in _renderers) renderer.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);

        if (reaparecerEn <= 0f) yield break;

        yield return new WaitForSeconds(reaparecerEn);

        // Reaparece para poder seguir probando sin rearmar la escena.
        foreach (Renderer renderer in _renderers) renderer.transform.localRotation = Quaternion.identity;
        transform.position = _posicionInicial;
        _combatiente.Revivir();
        Tintar(Color.white);
    }
}
