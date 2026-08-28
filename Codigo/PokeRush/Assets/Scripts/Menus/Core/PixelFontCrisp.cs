using UnityEngine;
using UnityEngine.UI;

// Deja la tipografia de pixel art nitida.
//
// El problema: el Canvas trabaja a 512x384 y se escala a la resolucion real
// (x3,75 en 1080p). Unity genera el atlas de la fuente con filtro bilineal, asi
// que al agrandarlo los bordes se interpolan y el texto chico sale gris y
// baboso: es lo que se ve en los carteles de controles.
//
// La solucion es poner el atlas en Point. Hay que rehacerlo cada vez que Unity
// regenera la textura de la fuente (pasa al aparecer un caracter nuevo), por eso
// no alcanza con hacerlo una vez en el Awake.
//
// Va en la raiz del sistema de menus y arregla TODAS las pantallas de una.
public class PixelFontCrisp : MonoBehaviour
{
    [Tooltip("Fuentes a mantener nitidas. Vacio = todas las que encuentre en los Text de abajo.")]
    [SerializeField] private Font[] fuentes;

    private void OnEnable()
    {
        Font.textureRebuilt += AlRegenerar;
        AplicarATodas();
    }

    private void OnDisable()
    {
        Font.textureRebuilt -= AlRegenerar;
    }

    private void Start() => AplicarATodas();

    private void AlRegenerar(Font font) => Aplicar(font);

    public void AplicarATodas()
    {
        if (fuentes != null)
            foreach (Font f in fuentes) Aplicar(f);

        foreach (Text t in GetComponentsInChildren<Text>(true))
            Aplicar(t.font);
    }

    private static void Aplicar(Font font)
    {
        if (font == null || font.material == null) return;

        Texture textura = font.material.mainTexture;
        if (textura == null || textura.filterMode == FilterMode.Point) return;

        textura.filterMode = FilterMode.Point;
        textura.anisoLevel = 0;
    }
}
