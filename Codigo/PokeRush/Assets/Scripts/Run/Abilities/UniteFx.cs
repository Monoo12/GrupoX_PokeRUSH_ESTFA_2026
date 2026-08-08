using UnityEngine;

// Reproduce un efecto del pack de UNITE.
//
// Los FX del dump son MALLAS ESTATICAS: el FBX trae la geometria del destello o
// la estela, pero ninguna animacion — UNITE las anima desde su propio motor de
// efectos, que no viaja en el archivo. Instanciarlas sin mas deja una forma
// congelada en el aire.
//
// Por eso el movimiento se pone aca: aparece pequeño, crece de golpe y se
// desvanece. Es el arco de cualquier impacto y alcanza para que se lea.
//
// La escala es la misma 55x de los modelos: estos FBX vienen a la escala nativa
// del juego (~0,01 unidades).
public class UniteFx : MonoBehaviour
{
    // OJO: los FX NO usan el factor 55x de los modelos, y tampoco un factor fijo.
    // Cada efecto viene con su propio tamaño nativo -algunos 0,02 unidades,
    // otros 0,66- asi que cualquier multiplicador constante deja unos gigantes y
    // otros invisibles. La escala se DERIVA: se mide la malla y se la lleva al
    // tamaño que se pide en unidades de mundo.

    // Cuanto gira el efecto sobre su eje de avance mientras vive. Las mallas del
    // pack son estaticas y UNITE las mueve desde su motor; sin algo de giro se
    // ven como un bodoque clavado en el aire.
    private const float GradosPorSegundo = 220f;

    // Cuanto corre la textura por segundo. Es lo que le da sensacion de flujo a
    // los glow y las estelas, que estan hechas para leerse en movimiento.
    private const float ArrastreDeTextura = 1.6f;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _bloque;
    private float _duracion;
    private float _nacio;
    private float _escalaFinal;
    private Color _tinte = Color.white;
    private float _giroInicial;
    private Quaternion _rotacionBase;

    /// Instancia el efecto en el mundo y lo destruye solo al terminar.
    /// `tamaño` es cuanto tiene que medir el efecto EN UNIDADES DE MUNDO: para
    /// un golpe cuerpo a cuerpo, algo del orden del radio de la habilidad.
    public static UniteFx Reproducir(GameObject prefab, Vector3 posicion, Quaternion rotacion,
                                     float duracion = 0.45f, float tamaño = 2f, Color? tinte = null)
    {
        if (prefab == null) return null;

        GameObject instancia = Instantiate(prefab, posicion, rotacion);
        var fx = instancia.AddComponent<UniteFx>();
        fx.Iniciar(duracion, tamaño, tinte ?? Color.white);
        return fx;
    }

    private void Iniciar(float duracion, float tamaño, Color tinte)
    {
        _duracion = Mathf.Max(0.05f, duracion);
        _escalaFinal = EscalaPara(tamaño);
        _nacio = Time.time;
        _tinte = tinte;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _bloque = new MaterialPropertyBlock();

        foreach (Renderer renderer in _renderers)
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Un giro de arranque distinto en cada golpe: repetir el mismo ataque
        // tres veces seguidas con el destello identico se nota y se ve barato.
        _giroInicial = Random.Range(0f, 360f);
        _rotacionBase = transform.rotation;
        transform.localScale = Vector3.one * (_escalaFinal * 0.5f);
    }

    // Devuelve el factor que lleva el efecto al tamaño pedido.
    //
    // Se miden los RENDERERS del objeto ya instanciado con la raiz en 1, y no
    // las mallas: estos prefabs escalan sus mallas desde los transforms hijos,
    // asi que el bounds de la malla sola se queda corto y el efecto termina
    // varias veces mas grande de lo pedido.
    private float EscalaPara(float tamaño)
    {
        transform.localScale = Vector3.one;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return Mathf.Max(0.1f, tamaño);

        Bounds limites = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) limites.Encapsulate(renderers[i].bounds);

        Vector3 medida = limites.size;
        float nativo = Mathf.Max(medida.x, Mathf.Max(medida.y, medida.z));

        // Sin nada medible no hay de donde derivar: queda un tamaño razonable.
        if (nativo < 0.0001f) return Mathf.Max(0.1f, tamaño);

        return Mathf.Max(0.1f, tamaño) / nativo;
    }

    private void Update()
    {
        float t = (Time.time - _nacio) / _duracion;
        if (t >= 1f) { Destroy(gameObject); return; }

        // Crece rapido al principio y despues se queda: el golpe tiene que
        // aparecer de golpe, no inflarse despacio.
        float crecimiento = Mathf.Clamp01(t / 0.25f);
        transform.localScale = Vector3.one * Mathf.Lerp(_escalaFinal * 0.5f, _escalaFinal, crecimiento);

        // Gira sobre su eje de avance. Las mallas del pack no traen animacion,
        // asi que sin esto quedan clavadas en el aire.
        transform.localRotation = _rotacionBase * Quaternion.AngleAxis(
            _giroInicial + GradosPorSegundo * (Time.time - _nacio), Vector3.forward);

        // Se desvanece sobre el final. Va por MaterialPropertyBlock porque los
        // materiales son sub-assets COMPARTIDOS del FBX: escribirles el color
        // se lo cambiaria a todas las instancias, y de forma permanente.
        //
        // El TINTE es imprescindible, no decorativo: las texturas del pack son
        // glow en escala de grises -UNITE las colorea desde su motor-, asi que
        // sin tinte todos los efectos salen blancos y sin identidad.
        float alfa = 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);
        var color = new Color(_tinte.r, _tinte.g, _tinte.b, alfa);

        // La textura corre para que el glow fluya en vez de quedar congelado.
        var arrastre = new Vector2(0f, -ArrastreDeTextura * (Time.time - _nacio));

        foreach (Renderer renderer in _renderers)
        {
            renderer.GetPropertyBlock(_bloque);
            _bloque.SetColor("_BaseColor", color);
            _bloque.SetVector("_BaseMap_ST", new Vector4(1f, 1f, arrastre.x, arrastre.y));
            renderer.SetPropertyBlock(_bloque);
        }
    }
}
