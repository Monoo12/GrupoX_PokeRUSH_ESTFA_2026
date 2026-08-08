using UnityEngine;

// Chispazo de impacto. Sin esto los golpes no se sienten: el rival pierde PS
// pero en pantalla no pasa nada, y el pack de UNITE no trae animacion de recibir
// daño en ninguna de las 78 especies.
//
// Es un ParticleSystem armado por codigo y no uno de los prefabs de FX del pack.
// Esos son sistemas de particulas completos atados al shader propio de UNITE, y
// cada habilidad tiene el suyo: engancharlos es un trabajo por especie. Este da
// el golpe legible que hace falta para juzgar la fase, y deja el lugar tomado
// para reemplazarlo por los del pack mas adelante.
public class HitBurst : MonoBehaviour
{
    [Header("Aspecto")]
    [Tooltip("Particulas del golpe que conecta.")]
    [SerializeField] private int cantidad = 26;
    [SerializeField] private float velocidad = 6f;
    [SerializeField] private float duracion = 0.35f;
    [SerializeField] private float tamaño = 0.22f;

    private ParticleSystem _sistema;
    private ParticleSystem.EmitParams _parametros;

    private void Awake()
    {
        _sistema = Construir();
    }

    /// Revienta el efecto en el punto dado. El color sale del tipo del ataque,
    /// asi el jugador aprende a leer que esta usando sin mirar el HUD.
    public void Reventar(Vector3 punto, AbilityDefinition habilidad, bool conecto)
    {
        if (_sistema == null) return;

        Color color = BattleFxView.ColorForType(habilidad != null ? habilidad.tipo : PokenType.Normal);

        // Al aire tambien se dibuja, pero apagado: el jugador tiene que ver que
        // el ataque salio y fallo, no que no paso nada.
        if (!conecto) color = Color.Lerp(color, Color.gray, 0.65f);

        _parametros = new ParticleSystem.EmitParams
        {
            position = punto,
            startColor = color,
            applyShapeToPosition = true,
        };

        _sistema.Emit(_parametros, conecto ? cantidad : cantidad / 3);
    }

    private ParticleSystem Construir()
    {
        var go = new GameObject("ChispazoDeImpacto");
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
        principal.gravityModifier = 1.2f;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.maxParticles = 400;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = false;   // solo se emite a mano, con Emit()

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.25f;

        ParticleSystem.SizeOverLifetimeModule escala = sistema.sizeOverLifetime;
        escala.enabled = true;
        escala.size = new ParticleSystem.MinMaxCurve(1f, Decreciente());

        // Aditivo: un impacto tiene que sumar luz, no pintar manchas opacas.
        var render = go.GetComponent<ParticleSystemRenderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;

        return sistema;
    }

    private static AnimationCurve Decreciente()
    {
        var curva = new AnimationCurve();
        curva.AddKey(0f, 1f);
        curva.AddKey(0.25f, 0.85f);
        curva.AddKey(1f, 0f);
        return curva;
    }

}
