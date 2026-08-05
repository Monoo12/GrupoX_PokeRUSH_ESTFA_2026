using UnityEngine;

// El Poken dibujado con un modelo 3D. El modelo sale del campo battleModel de
// la especie (los .glb los baja el importador y los convierte glTFast).
//
// Solo se usa en las especies que tienen modelo cargado; el resto sigue con
// SpritePokenVisual. Eso permite ir migrando de a poco sin romper nada: hoy
// pueden ser 20 especies con modelo y 950 con sprite, en el mismo combate.
public class ModelPokenVisual : PokenVisual
{
    [Header("Escala")]
    [Tooltip("Alto en unidades de mundo al que se normaliza cualquier modelo. " +
             "Los .glb vienen en escalas muy distintas: sin esto un Onix tapa la " +
             "pantalla y un Diglett no se ve.")]
    [SerializeField] private float targetHeight = 1f;
    [Tooltip("Desactivar para respetar la escala original del modelo (util si ya " +
             "viene con el tamaño correcto).")]
    [SerializeField] private bool normalizeHeight = true;

    [Header("Orientacion")]
    [Tooltip("Giro en Y del modelo del lado del jugador (se ve de espaldas).")]
    [SerializeField] private float playerSideYaw;
    [Tooltip("Giro en Y del modelo del lado rival (se ve de frente).")]
    [SerializeField] private float enemySideYaw = 180f;

    // Nombre de la propiedad de color en URP Lit. Con MaterialPropertyBlock se
    // tiñe sin instanciar materiales (no rompe el batching ni deja materiales
    // huerfanos en memoria).
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private GameObject _instance;
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private MaterialPropertyBlock _block;
    private float _height = 1f;

    public override float Height => _height;

    public override void Bind(PokenDefinition definition, bool isPlayerSide)
    {
        // Cada cambio de Poken reemplaza el modelo entero.
        if (_instance != null) Destroy(_instance);
        _renderers = null;
        _originalColors = null;
        _height = 1f;

        if (definition == null || definition.battleModel == null)
        {
            Debug.LogWarning($"[PokeRush] {name}: la especie " +
                             $"'{(definition != null ? definition.pokenName : "null")}' no tiene " +
                             "battleModel. Deberia dibujarse con SpritePokenVisual.");
            return;
        }

        _instance = Instantiate(definition.battleModel, transform);
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation =
            Quaternion.Euler(0f, isPlayerSide ? playerSideYaw : enemySideYaw, 0f);
        _instance.transform.localScale = Vector3.one;

        _renderers = _instance.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (_renderers.Length == 0)
        {
            Debug.LogWarning($"[PokeRush] El modelo de '{definition.pokenName}' no tiene renderers.");
            return;
        }

        if (normalizeHeight) NormalizeHeight();
        else _height = MeasureWorldHeight();

        CacheOriginalColors();
        ApplyAnimator(definition);
    }

    // Escala el modelo para que mida targetHeight en el mundo. Se mide con
    // localScale = 1 y despues se aplica el factor, asi la escala del objeto
    // padre (que la pone el builder de escena) ya queda contemplada.
    private void NormalizeHeight()
    {
        float worldHeight = MeasureWorldHeight();
        if (worldHeight <= 0.0001f)
        {
            _height = targetHeight;
            return;
        }

        _instance.transform.localScale = Vector3.one * (targetHeight / worldHeight);
        _height = targetHeight;
    }

    private float MeasureWorldHeight()
    {
        Bounds bounds = default;
        bool first = true;
        foreach (Renderer r in _renderers)
        {
            if (r == null) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        return first ? 0f : bounds.size.y;
    }

    // URP Lit hace baseMap * _BaseColor, asi que para que el tinte sea
    // multiplicativo (igual que con sprites) hay que partir del color propio de
    // cada material, no pisarlo con blanco.
    private void CacheOriginalColors()
    {
        _block ??= new MaterialPropertyBlock();
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            _originalColors[i] = r != null && r.sharedMaterial != null
                                 && r.sharedMaterial.HasProperty(BaseColorId)
                ? r.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
        }
    }

    public override void SetTint(Color tint)
    {
        if (_renderers == null) return;
        _block ??= new MaterialPropertyBlock();

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            Color baseColor = _originalColors != null && i < _originalColors.Length
                ? _originalColors[i]
                : Color.white;

            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, baseColor * tint);
            r.SetPropertyBlock(_block);
        }
    }

    public override void SetRendererVisible(bool visible)
    {
        if (_renderers == null) return;
        foreach (Renderer r in _renderers)
            if (r != null) r.enabled = visible;
    }
}
