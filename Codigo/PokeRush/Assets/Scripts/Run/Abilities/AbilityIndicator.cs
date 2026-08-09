using UnityEngine;

// Dibuja en el suelo hacia donde va a caer la habilidad mientras se apunta.
//
// Sin esto el combate apuntado es injugable: no hay forma de saber si el rival
// entra en el area antes de gastar el enfriamiento.
//
// Tres decisiones que vienen de que la version anterior no se leia:
//
//   LA TEXTURA SE GENERA ACA. Antes salia de los decals de UNITE, que son arte
//   pensado para su motor: alfa en 1 en todo el cuadro, borde difuso, y el color
//   metido en el propio pixel. Puestos en un quad se veia el rectangulo de la
//   textura en vez de la forma. Generandola por codigo se sabe exactamente que
//   se dibuja -relleno translucido y borde nitido- y el contorno cae justo donde
//   termina la hitbox.
//
//   LA FORMA ES LA MISMA QUE GOLPEA. Cada modo de apuntado tiene su malla y sus
//   medidas salen de los mismos campos que lee HitResolver. Un indicador que no
//   coincide con la hitbox es peor que no tener indicador: enseña mal.
//
//   SE APOYA EN EL SUELO DE VERDAD. La altura sale de un rayo hacia abajo y el
//   decal se acuesta sobre la normal del terreno. Antes se usaba la altura del
//   Poken, asi que en cualquier desnivel el area quedaba flotando o enterrada.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AbilityIndicator : MonoBehaviour
{
    [Header("Aspecto")]
    [SerializeField] private Color color = new Color(0.4f, 0.85f, 1f, 1f);
    [Tooltip("Color del anillo que marca hasta donde se puede alejar el area.")]
    [SerializeField] private Color colorAlcance = new Color(0.6f, 0.75f, 0.9f, 0.5f);
    [Tooltip("Cuanto se levanta del piso para no pelear con el z-fighting.")]
    [SerializeField] private float alturaSobreElPiso = 0.06f;

    [Header("Apoyo en el suelo")]
    [Tooltip("Desde cuanto mas arriba del punto apuntado se tira el rayo.")]
    [SerializeField] private float alturaDeSondeo = 4f;

    private static readonly RaycastHit[] Sondeo = new RaycastHit[16];

    private MeshFilter _filtro;
    private MeshRenderer _render;
    private Material _material;

    private MeshFilter _alcanceFiltro;
    private MeshRenderer _alcanceRender;
    private Transform _alcance;

    private Mesh _quad;
    private Mesh _circulo;

    // El cono se rearma solo si cambia el angulo: son mallas chicas, pero
    // regenerarlas en cada cuadro de apuntado seria basura para nada.
    private Mesh _cono;
    private float _anguloDelCono = -1f;

    private Texture2D _texturaRadial;
    private Texture2D _texturaMarco;
    private Texture2D _texturaAnillo;
    private Texture2D _texturaActual;

    private void Awake()
    {
        _quad = ConstruirQuad();
        _circulo = ConstruirCirculo();

        _texturaRadial = TexturaRadial();
        _texturaMarco = TexturaMarco();
        _texturaAnillo = TexturaAnillo();

        _filtro = GetComponent<MeshFilter>();
        _filtro.sharedMesh = _circulo;

        _render = GetComponent<MeshRenderer>();
        _material = MaterialDeDecal(color);
        _render.sharedMaterial = _material;
        _render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ConstruirAlcance();
        Ocultar();
    }

    // Segundo decal, centrado SIEMPRE en el Poken, que marca hasta donde puede
    // alejarse el area. Sin el, el circulo parece nacer del mouse y no hay forma
    // de saber si el punto elegido esta dentro del alcance.
    private void ConstruirAlcance()
    {
        var go = new GameObject("Alcance", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform.parent, false);

        _alcanceFiltro = go.GetComponent<MeshFilter>();
        _alcanceFiltro.sharedMesh = _circulo;

        Material material = MaterialDeDecal(colorAlcance);
        material.SetTexture("_BaseMap", _texturaAnillo);

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

        _alcanceRender.enabled = false;

        switch (habilidad.apuntado)
        {
            case AimMode.ConoFrontal:
                UsarMalla(MallaDeCono(habilidad.anguloCono), _texturaMarco);
                Apoyar(transform, origen, direccion);
                // El cono se genera con radio 1, asi que la escala ES el alcance.
                transform.localScale = Vector3.one * habilidad.rango;
                break;

            case AimMode.SkillshotLineal:
            case AimMode.Dash:
                // El quad nace en el lanzador y se estira hacia adelante, asi
                // que se lo corre media longitud para que el pivote quede atras.
                UsarMalla(_quad, _texturaMarco);
                Apoyar(transform, origen + direccion * (habilidad.rango * 0.5f), direccion);
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.rango);
                break;

            case AimMode.AreaEnSuelo:
                Vector3 centro = origen + Vector3.ClampMagnitude(puntoApuntado - origen, habilidad.rango);
                UsarMalla(_circulo, _texturaRadial);
                Apoyar(transform, centro, direccion);
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.radio * 2f);

                _alcanceFiltro.sharedMesh = _circulo;
                Apoyar(_alcance, origen, direccion);
                _alcance.localScale = new Vector3(habilidad.rango * 2f, 1f, habilidad.rango * 2f);
                _alcanceRender.enabled = true;
                break;

            case AimMode.AreaPropia:
                UsarMalla(_circulo, _texturaRadial);
                Apoyar(transform, origen, direccion);
                transform.localScale = new Vector3(habilidad.radio * 2f, 1f, habilidad.radio * 2f);
                break;

            default:
                Ocultar();
                return;
        }
        _render.enabled = true;
    }

    // Deja el decal acostado sobre el terreno que tenga debajo.
    //
    // El rayo sale de bien arriba y se queda con el primer impacto que NO sea un
    // combatiente: si no, apuntar sobre un rival pondria el area a la altura de
    // su cabeza. Sin nada abajo -un barranco- se deja el punto como vino, que es
    // mejor que hacer desaparecer el indicador.
    private void Apoyar(Transform destino, Vector3 punto, Vector3 direccion)
    {
        Vector3 posicion = punto;
        Vector3 normal = Vector3.up;

        int cantidad = Physics.RaycastNonAlloc(
            punto + Vector3.up * alturaDeSondeo, Vector3.down, Sondeo,
            alturaDeSondeo * 3f, ~0, QueryTriggerInteraction.Ignore);

        float mejor = float.MaxValue;
        for (int i = 0; i < cantidad; i++)
        {
            if (Sondeo[i].distance >= mejor) continue;
            if (Sondeo[i].collider.GetComponentInParent<RunCombatant>() != null) continue;

            mejor = Sondeo[i].distance;
            posicion = Sondeo[i].point;
            normal = Sondeo[i].normal;
        }

        // La direccion se proyecta sobre el plano del suelo para que el decal no
        // se tuerza al acostarse en una rampa.
        Vector3 adelante = Vector3.ProjectOnPlane(direccion, normal);
        Quaternion rotacion = adelante.sqrMagnitude < 0.0001f
            ? Quaternion.FromToRotation(Vector3.up, normal)
            : Quaternion.LookRotation(adelante.normalized, normal);

        destino.SetPositionAndRotation(posicion + normal * alturaSobreElPiso, rotacion);
    }

    private void UsarMalla(Mesh malla, Texture2D textura)
    {
        _filtro.sharedMesh = malla;
        if (textura == _texturaActual) return;

        _texturaActual = textura;
        _material.SetTexture("_BaseMap", textura);
    }

    private Mesh MallaDeCono(float angulo)
    {
        if (_cono != null && Mathf.Approximately(angulo, _anguloDelCono)) return _cono;

        _anguloDelCono = angulo;
        _cono = ConstruirCono(angulo);
        return _cono;
    }

    // --- Material -----------------------------------------------------------

    // Mezcla alfa comun y no aditiva. Aditivo se ve muy bien sobre el gris
    // oscuro de la caja de pruebas, pero se lava hasta desaparecer sobre arena o
    // pasto claros, que es donde va a correr el juego de verdad. Con alfa el
    // indicador se lee igual sobre cualquier fondo.
    private static Material MaterialDeDecal(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);

        // Poner _Surface = 1 NO alcanza: URP decide la mezcla por _SrcBlend y
        // _DstBlend, y si quedan en los valores opacos el decal se dibuja con el
        // fondo negro de su textura.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Sin escribir profundidad: si escribiera, el indicador taparia al Poken
        // cuando se le pisa encima.
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    // --- Texturas generadas -------------------------------------------------

    private const int Lado = 256;
    private const float Relleno = 0.20f;
    private const float Grosor = 0.035f;

    // Para las areas circulares: relleno parejo y un borde nitido justo en el
    // radio. El borde es lo que hace que se entienda donde TERMINA el area; sin
    // el, un degradado suave deja la duda de si el rival del limite entra.
    private static Texture2D TexturaRadial()
    {
        return Generar((u, v) =>
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            if (r > 1f) return 0f;
            return Mathf.Max(Relleno, Banda(r, 1f - Grosor));
        });
    }

    // Anillo solo, sin relleno: es el limite de alcance, y rellenarlo taparia el
    // area de verdad que va adentro.
    private static Texture2D TexturaAnillo()
    {
        return Generar((u, v) =>
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            return r > 1f ? 0f : Banda(r, 1f - Grosor * 0.6f);
        });
    }

    // Para el cono y la linea. Se dibuja el contorno en el espacio de la malla:
    // los lados (u = 0 y u = 1) y el frente (v = 1). El borde de atras se deja
    // abierto a proposito, porque esa es la parte que nace del propio Poken.
    //
    // Al estirar la malla el borde queda de distinto grosor en cada eje. Se
    // acepta: es lo que pasa con cualquier decal en un quad, y el contorno sigue
    // cayendo exactamente donde termina la hitbox, que es lo que importa.
    private static Texture2D TexturaMarco()
    {
        return Generar((u, v) =>
        {
            float borde = Mathf.Max(Banda(u, 0f), Mathf.Max(Banda(u, 1f), Banda(v, 1f)));
            return Mathf.Max(Relleno, borde);
        });
    }

    // Pico suave centrado en `centro`. Da un borde nitido pero sin escalones.
    private static float Banda(float x, float centro)
    {
        float d = Mathf.Abs(x - centro) / Grosor;
        return d >= 1f ? 0f : 1f - d * d;
    }

    private static Texture2D Generar(System.Func<float, float, float> alfa)
    {
        var textura = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false)
        {
            // Clamp y no Repeat: con Repeat el borde de un lado se filtra contra
            // el del opuesto y aparece una linea fantasma cruzando la forma.
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixeles = new Color32[Lado * Lado];
        for (int y = 0; y < Lado; y++)
        {
            for (int x = 0; x < Lado; x++)
            {
                float a = Mathf.Clamp01(alfa((x + 0.5f) / Lado, (y + 0.5f) / Lado));
                // Blanco: el color lo pone el material, asi el mismo mapa sirve
                // para el indicador del jugador y para el aviso de un rival.
                pixeles[y * Lado + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        textura.SetPixels32(pixeles);
        textura.Apply(false, true);
        return textura;
    }

    // --- Mallas -------------------------------------------------------------

    // Abanico de radio 1 que abre `angulo` grados centrado en +Z. Las UV van con
    // u = a lo ancho del arco y v = distancia a la punta, para que la textura de
    // marco dibuje los dos lados y el frente.
    private static Mesh ConstruirCono(float angulo)
    {
        int lados = Mathf.Clamp(Mathf.CeilToInt(angulo / 6f), 6, 96);
        var vertices = new Vector3[lados + 2];
        var uvs = new Vector2[lados + 2];
        var triangulos = new int[lados * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0f);

        float mitad = angulo * 0.5f;
        for (int i = 0; i <= lados; i++)
        {
            float fraccion = i / (float)lados;
            float grados = -mitad + angulo * fraccion;
            Vector3 direccion = Quaternion.AngleAxis(grados, Vector3.up) * Vector3.forward;

            vertices[i + 1] = direccion;
            uvs[i + 1] = new Vector2(fraccion, 1f);
        }

        for (int i = 0; i < lados; i++)
        {
            triangulos[i * 3] = 0;
            triangulos[i * 3 + 1] = i + 1;
            triangulos[i * 3 + 2] = i + 2;
        }

        var malla = new Mesh { name = "ConoIndicador" };
        malla.vertices = vertices;
        malla.uv = uvs;
        malla.triangles = triangulos;
        malla.RecalculateNormals();
        return malla;
    }

    // Disco de diametro 1 en el plano XZ.
    private static Mesh ConstruirCirculo()
    {
        const int lados = 64;
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

    // Quad de 1x1 en el plano XZ. El pivote va en el borde de atras, no en el
    // centro: una linea o un dash nacen del Poken y crecen hacia adelante.
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
