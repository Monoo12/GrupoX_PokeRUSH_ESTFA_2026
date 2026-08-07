using UnityEngine;

// Dibuja en el suelo hacia donde va a caer la habilidad mientras se apunta.
//
// Sin esto el combate apuntado es injugable: no hay forma de saber si el rival
// entra en el area antes de gastar el enfriamiento.
//
// La forma es un quad generado por codigo, pero la TEXTURA sale del pack de
// UNITE: son los decals genericos que el juego usa para lo mismo
// (t_fx_Indicator_rectangle para las lineas, t_fx_Indicator_Decal_Circle para
// las areas). Se hace asi y no instanciando los prefabs de FX del pack porque
// esos vienen con la malla a la escala nativa del juego (~0.02 unidades) y con
// su propio shader: habria que reescalarlos y traducirlos a URP por cada
// habilidad. Con un quad propio, el tamaño sale del rango y del radio de la
// habilidad, que es lo que tiene que ser exacto.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AbilityIndicator : MonoBehaviour
{
    [Header("Decals del pack")]
    [Tooltip("Para SkillshotLineal y Dash. El pack usa t_fx_Indicator_rectangle_*.")]
    [SerializeField] private Texture2D decalLinea;
    [Tooltip("Para AreaEnSuelo y AreaPropia. El pack usa t_fx_Indicator_Decal_Circle_*.")]
    [SerializeField] private Texture2D decalArea;
    [Tooltip("Anillo que marca hasta donde llega la habilidad, pegado al Poken.")]
    [SerializeField] private Texture2D decalAlcance;

    [Header("Aspecto")]
    [SerializeField] private Color color = new Color(0.45f, 0.85f, 1f, 0.75f);
    [Tooltip("Cuanto se levanta del piso para no pelear con el z-fighting.")]
    [SerializeField] private float alturaSobreElPiso = 0.05f;

    private MeshRenderer _render;
    private Material _material;
    private Texture2D _decalActual;

    // Segundo quad, centrado SIEMPRE en el Poken, que marca hasta donde llega la
    // habilidad. Sin el, el circulo de area parece nacer del mouse y no hay
    // forma de saber si el punto elegido esta dentro del alcance.
    private MeshRenderer _alcanceRender;
    private Transform _alcance;

    private MeshFilter _filtro;
    private Mesh _quad;
    private Mesh _circulo;

    private void Awake()
    {
        _quad = ConstruirQuad();
        _circulo = ConstruirCirculo();

        _filtro = GetComponent<MeshFilter>();
        _filtro.sharedMesh = _quad;

        _render = GetComponent<MeshRenderer>();
        _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _material.SetColor("_BaseColor", color);

        // Transparencia aditiva, configurada a mano. Poner _Surface = 1 NO
        // alcanza: URP decide la mezcla por _SrcBlend/_DstBlend, y si quedan en
        // los valores opacos el decal se dibuja con el fondo negro de su textura.
        // Aditivo (DstBlend = One) y no alpha comun, porque un indicador tiene
        // que sumar luz sobre el piso, no taparlo.
        _material.SetFloat("_Surface", 1f);
        _material.SetFloat("_Blend", 1f);
        _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        _material.SetFloat("_ZWrite", 0f);
        // Sin escribir profundidad: si escribiera, el indicador taparia al Poken
        // cuando se le pisa encima.
        _material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _render.sharedMaterial = _material;
        _render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ConstruirAlcance();
        Ocultar();
    }

    private void ConstruirAlcance()
    {
        var go = new GameObject("Alcance", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform.parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = _circulo;

        var material = new Material(_material);
        material.SetColor("_BaseColor", new Color(0.5f, 0.7f, 0.9f, 0.45f));
        material.SetTexture("_BaseMap", decalAlcance != null ? decalAlcance : decalArea);

        _alcanceRender = go.GetComponent<MeshRenderer>();
        _alcanceRender.sharedMaterial = material;
        _alcanceRender.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _alcance = go.transform;
    }

    public void Ocultar()
    {
        if (_render != null) _render.enabled = false;
        if (_alcanceRender != null) _alcanceRender.enabled = false;
    }

    /// Redibuja el indicador para esta habilidad, apuntando al punto dado.
    public void Mostrar(Transform lanzador, AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        if (habilidad == null) { Ocultar(); return; }

        Vector3 origen = lanzador.position;
        Vector3 direccion = Vector3.ProjectOnPlane(puntoApuntado - origen, Vector3.up);
        if (direccion.sqrMagnitude < 0.01f) direccion = lanzador.forward;
        direccion.Normalize();

        switch (habilidad.apuntado)
        {
            case AimMode.SkillshotLineal:
            case AimMode.Dash:
                // El quad nace en el lanzador y se estira hacia adelante, asi
                // que se lo corre media longitud para que el pivote quede atras.
                Vector3 centroLinea = origen + direccion * (habilidad.rango * 0.5f);
                centroLinea.y = origen.y + alturaSobreElPiso;
                transform.SetPositionAndRotation(centroLinea, Quaternion.LookRotation(direccion, Vector3.up));
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.rango);
                _filtro.sharedMesh = _quad;
                UsarDecal(decalLinea);
                // La linea ya nace del Poken, no hace falta marcar el alcance.
                _alcanceRender.enabled = false;
                break;

            case AimMode.AreaEnSuelo:
                Vector3 centro = origen + Vector3.ClampMagnitude(puntoApuntado - origen, habilidad.rango);
                centro.y = origen.y + alturaSobreElPiso;
                transform.SetPositionAndRotation(centro, Quaternion.identity);
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.radio * 2f);
                _filtro.sharedMesh = _circulo;
                UsarDecal(decalArea);

                // El circulo de alcance queda pegado al Poken: asi se ve de un
                // vistazo que el area esta limitada y desde donde se mide.
                _alcance.position = origen + Vector3.up * (alturaSobreElPiso * 0.5f);
                _alcance.localScale = new Vector3(habilidad.rango * 2f, 1f, habilidad.rango * 2f);
                _alcanceRender.enabled = true;
                break;

            case AimMode.AreaPropia:
                transform.SetPositionAndRotation(origen + Vector3.up * alturaSobreElPiso, Quaternion.identity);
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.radio * 2f);
                _filtro.sharedMesh = _circulo;
                UsarDecal(decalArea);
                _alcanceRender.enabled = false;
                break;

            default:
                Ocultar();
                return;
        }
        _render.enabled = true;
    }

    private void UsarDecal(Texture2D decal)
    {
        if (decal == _decalActual) return;

        _decalActual = decal;
        _material.SetTexture("_BaseMap", decal);
    }

    // Disco de diametro 1 en el plano XZ. Se usa para las areas en vez del quad
    // porque los decals circulares del pack no tienen las esquinas
    // transparentes: con un quad se veria el rectangulo. Con la malla circular
    // la silueta sale bien sea cual sea la textura.
    private static Mesh ConstruirCirculo()
    {
        const int lados = 48;
        var vertices = new Vector3[lados + 1];
        var uvs = new Vector2[lados + 1];
        var triangulos = new int[lados * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < lados; i++)
        {
            float angulo = i / (float)lados * Mathf.PI * 2f;
            float x = Mathf.Cos(angulo) * 0.5f;
            float z = Mathf.Sin(angulo) * 0.5f;

            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);

            triangulos[i * 3] = 0;
            triangulos[i * 3 + 1] = i + 1;
            triangulos[i * 3 + 2] = i + 2 > lados ? 1 : i + 2;
        }

        var malla = new Mesh { name = "CirculoIndicador" };
        malla.vertices = vertices;
        malla.uv = uvs;
        malla.triangles = triangulos;
        malla.RecalculateNormals();
        return malla;
    }

    // Quad de 1x1 en el plano XZ, centrado. El tamaño real lo pone la escala.
    private static Mesh ConstruirQuad()
    {
        var malla = new Mesh { name = "QuadIndicador" };
        malla.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
        };
        malla.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        malla.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        malla.RecalculateNormals();
        return malla;
    }
}
