using System.Collections;
using UnityEngine;

// El impacto de un golpe: chispas que saltan y una onda que se abre en el suelo.
//
// Se arma con ParticleSystem de Unity y las TEXTURAS del pack, y no instanciando
// los prefabs de FX de UNITE. Esos prefabs son mallas estaticas: el FBX trae la
// geometria del destello pero ninguna animacion, porque UNITE las mueve desde su
// propio motor de efectos, que no viaja en el dump. Puestas tal cual quedan como
// una lamina congelada en el aire, por mas que se la haga girar.
//
// El arte igual sale del pack -sus glow son buenos-; lo que aporta Unity es el
// movimiento, que es justo lo que falta.
public class HitBurst : MonoBehaviour
{
    [Header("Arte del pack")]
    [Tooltip("Textura de las chispas. Del pack sirven las t_fx_*_glow_*.")]
    [SerializeField] private Texture2D texturaChispa;
    [Tooltip("Textura de la onda del suelo. Sirven los t_fx_Indicator_Decal_Circle_*.")]
    [SerializeField] private Texture2D texturaOnda;

    [Header("Chispas")]
    [SerializeField] private int cantidad = 24;
    [SerializeField] private float velocidad = 7f;
    [SerializeField] private float duracion = 0.4f;
    [SerializeField] private float tamaño = 0.35f;

    [Header("Onda del suelo")]
    [Tooltip("Cuanto se abre la onda respecto del radio del golpe.")]
    [SerializeField] private float alcanceDeOnda = 1.6f;
    [SerializeField] private float duracionDeOnda = 0.32f;

    private ParticleSystem _chispas;
    private Material _materialOnda;

    private void Awake()
    {
        _chispas = ConstruirChispas();
    }

    /// Revienta el impacto. El color sale del tipo del ataque, para que el
    /// jugador aprenda a leer que esta usando sin mirar el HUD.
    public void Reventar(Vector3 punto, AbilityDefinition habilidad, bool conecto)
    {
        Color color = BattleFxView.ColorForType(habilidad != null ? habilidad.tipo : PokenType.Normal);

        // Al aire tambien se dibuja, pero apagado: el jugador tiene que ver que
        // el ataque salio y fallo, no que no paso nada.
        if (!conecto) color = Color.Lerp(color, Color.gray, 0.7f);

        if (_chispas != null)
        {
            var parametros = new ParticleSystem.EmitParams
            {
                position = punto,
                startColor = color,
                applyShapeToPosition = true,
            };
            _chispas.Emit(parametros, conecto ? cantidad : cantidad / 3);
        }

        if (conecto && texturaOnda != null)
        {
            float radio = habilidad != null ? habilidad.radio : 1f;
            StartCoroutine(Onda(punto, radio * alcanceDeOnda, color));
        }
    }

    // Un disco plano que se abre y se apaga sobre el piso. Con la camara
    // inclinada de la ruta es lo que mas comunica "aca pego": las chispas se
    // pierden contra el fondo, la onda no.
    private IEnumerator Onda(Vector3 punto, float radioFinal, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());

        go.transform.position = punto - Vector3.up * (punto.y - transform.position.y) + Vector3.up * 0.08f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var render = go.GetComponent<Renderer>();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.sharedMaterial = MaterialDeOnda();

        var bloque = new MaterialPropertyBlock();

        for (float t = 0f; t < duracionDeOnda; t += Time.deltaTime)
        {
            float avance = t / duracionDeOnda;
            // Se abre rapido y frena al final, como una onda de choque real.
            float radio = Mathf.Lerp(radioFinal * 0.2f, radioFinal, Mathf.Sqrt(avance));
            go.transform.localScale = Vector3.one * radio * 2f;

            render.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 1f - avance));
            render.SetPropertyBlock(bloque);
            yield return null;
        }
        Destroy(go);
    }

    private Material MaterialDeOnda()
    {
        if (_materialOnda != null) return _materialOnda;

        _materialOnda = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _materialOnda.SetTexture("_BaseMap", texturaOnda);
        AjustarAditivo(_materialOnda);
        return _materialOnda;
    }

    private ParticleSystem ConstruirChispas()
    {
        var go = new GameObject("Chispas");
        go.transform.SetParent(transform, false);

        var sistema = go.AddComponent<ParticleSystem>();
        sistema.Stop();

        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = 1f;
        principal.loop = false;
        principal.playOnAwake = false;
        principal.startLifetime = duracion;
        principal.startSpeed = velocidad;
        principal.startSize = tamaño;
        principal.gravityModifier = 1.6f;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.maxParticles = 400;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = false;   // solo se emite a mano, con Emit()

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.2f;

        // Frenan al salir: sin esto vuelan a velocidad pareja y se ven como una
        // explosion de bolitas, no como un golpe.
        ParticleSystem.LimitVelocityOverLifetimeModule freno = sistema.limitVelocityOverLifetime;
        freno.enabled = true;
        freno.dampen = 0.35f;
        freno.limit = new ParticleSystem.MinMaxCurve(velocidad * 0.35f);

        ParticleSystem.SizeOverLifetimeModule escala = sistema.sizeOverLifetime;
        escala.enabled = true;
        escala.size = new ParticleSystem.MinMaxCurve(1f, Decreciente());

        ParticleSystem.RotationOverLifetimeModule giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        var render = go.GetComponent<ParticleSystemRenderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (texturaChispa != null) material.SetTexture("_BaseMap", texturaChispa);
        AjustarAditivo(material);
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;

        return sistema;
    }

    // Aditivo: un impacto suma luz, no pinta manchas opacas. Poner _Surface = 1
    // no alcanza, URP decide la mezcla por _SrcBlend y _DstBlend.
    private static void AjustarAditivo(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static AnimationCurve Decreciente()
    {
        var curva = new AnimationCurve();
        curva.AddKey(0f, 1f);
        curva.AddKey(0.25f, 0.9f);
        curva.AddKey(1f, 0f);
        return curva;
    }
}
