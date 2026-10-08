using System.IO;
using UnityEditor;
using UnityEngine;

// Deja el arte de la mesa de ruleta listo para usar.
//
// Va en Bilinear y NO en Point, al reves que el resto del pixel art del
// proyecto, porque este dibujo no es pixel art: es pintado, con degrades y
// sombreado suave. El CanvasScaler agranda todo 3,75x a 1920 de ancho, y Point
// sobre un degrade a escala no entera duplica pixeles de forma despareja y se
// lee como suciedad. Con el arte exportado a la resolucion de la hoja
// (ver Codigo/Herramientas/limpiar_ruleta.py) las piezas caen cerca de 1:1 en pantalla y
// Bilinear las deja limpias.
//
// De donde salen los roulette_*.png de Assets/Ruleta Nueva:
//
// La carpeta trajo 42 recortes sueltos que NO se pueden usar como estan:
//   1. Estan aplanados sobre blanco. Tienen canal alpha, pero no hay un solo
//      pixel transparente: la rueda y las fichas son rectangulos opacos con
//      las esquinas pintadas del fondo de la hoja.
//   2. Varios se llevaron el texto del label adentro del dibujo (titulo.png
//      tiene "titulo.png" escrito abajo).
//   3. Son versiones REESCALADAS de la hoja, no recortes exactos: la rueda
//      mide 290x325 suelta y 284x341 en sprite_sheet_completo.png.
//
// La hoja SI trae alpha de verdad, asi que los roulette_*.png se recortaron de
// ahi con Codigo/Herramientas/limpiar_ruleta.py: componentes conexas para encontrar cada
// pieza, circulo ajustado para separar las tres ruletas (que se superponen),
// borrado de los valores de ejemplo de los paneles y un pase de color que
// lleva la caoba a dorado y el casi-negro a azul noche. Los 42 originales
// quedaron intactos al lado.
public static class RouletteArtImporter
{
    public const string Carpeta = "Assets/Ruleta Nueva";
    public const string Prefijo = "roulette_";

    // El panel de la grilla se estira, asi que va en 9 tajadas. Mide 128x128 y
    // el marco dorado ocupa 20 px: cortar ahi deja las esquinas quietas.
    private const string FieltroPath = Carpeta + "/roulette_felt.png";
    private const float BordeDelFieltro = 20f;

    // El fieltro va al doble de densidad que el Canvas, asi que su ppu tambien:
    // sin esto el borde de 20 px se dibujaria de 20 unidades en vez de 10 y el
    // marco dorado saldria del doble de grueso.
    private const float PpuDelFieltro = 200f;

    [MenuItem("PokeRush/Casino/Importar el arte de la mesa de ruleta")]
    public static void ImportarTodo()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta))
        {
            Debug.LogError($"[Ruleta] No existe la carpeta {Carpeta}.");
            return;
        }

        int tocados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetExtension(path).ToLowerInvariant() != ".png") continue;
            // Solo los generados: los 42 originales se dejan como estan.
            if (!Path.GetFileName(path).StartsWith(Prefijo)) continue;
            if (Importar(path)) tocados++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Ruleta] Arte importado: {tocados} textura(s) configuradas en {Carpeta}.");
    }

    // Devuelve true si hubo que reimportar.
    private static bool Importar(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool esFieltro = path == FieltroPath;
        Vector4 borde = esFieltro
            ? new Vector4(BordeDelFieltro, BordeDelFieltro, BordeDelFieltro, BordeDelFieltro)
            : Vector4.zero;

        float ppu = esFieltro ? PpuDelFieltro : 100f;

        bool cambio = importer.textureType != TextureImporterType.Sprite
                      || importer.spriteImportMode != SpriteImportMode.Single
                      || importer.filterMode != FilterMode.Bilinear
                      || importer.textureCompression != TextureImporterCompression.Uncompressed
                      || importer.mipmapEnabled
                      || !importer.alphaIsTransparency
                      || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                      || importer.spriteBorder != borde;

        if (!cambio) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = ppu;
        importer.spriteBorder = borde;
        importer.SaveAndReimport();
        return true;
    }
}
