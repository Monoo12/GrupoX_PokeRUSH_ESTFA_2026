using System.IO;
using UnityEditor;
using UnityEngine;

// Deja el arte de la ruleta de premios listo para usar: Sprite, Point y sin
// comprimir, que es lo unico que hace que el pixel art no salga lavado.
//
// De donde salen los PNG de Assets/Graphics/Ruleta:
//
// La carpeta original "Nuevo Intento ruleta" trae 53 PNG que en realidad son
// RECORTES de sprite_sheet_completo.png, y traen tres problemas que hacen que
// no se puedan usar como estan:
//   1. Ninguno tiene canal alpha. El cuadriculado de "transparencia" esta
//      pintado como pixeles, asi que puestos sobre el mapa se ven como un
//      rectangulo gris.
//   2. Cada recorte incluye el texto del label ("btn_girar.png") y un pedazo
//      del asset de al lado.
//   3. Los segmento_*.png no son sectores de 45 grados sino ocho dibujos de
//      forma distinta entre si, asi que no se pueden apoyar uno al lado del
//      otro para armar una rueda; y ruleta_base.png es una rueda de SIETE
//      gajos con los iconos ya pegados.
//
// Por eso los assets de esta carpeta se re-extrajeron de la hoja: recorte por
// asset, alpha calculado desde el borde, escalados al tamano con el que se usan
// en el Canvas de 512x384, y el disco armado con ocho gajos exactos usando los
// colores de los segmento_*.png. Los originales quedaron intactos.
//
// La excepcion es fx_premio.png, que NO sale de la hoja: es la version mejorada
// (la pokeball abriendose con confeti) que paso Augusto aparte, ya con alpha de
// verdad, recortada y bajada a 86x83.
public static class PrizeWheelArtImporter
{
    public const string Carpeta = "Assets/Graphics/Ruleta";

    [MenuItem("PokeRush/Casino/Importar el arte de la ruleta de premios")]
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
            if (Importar(path)) tocados++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Ruleta] Arte importado: {tocados} textura(s) configuradas como " +
                  "pixel art en " + Carpeta + ".");
    }

    // Devuelve true si hubo que reimportar.
    private static bool Importar(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool cambio = importer.textureType != TextureImporterType.Sprite
                      || importer.spriteImportMode != SpriteImportMode.Single
                      || importer.filterMode != FilterMode.Point
                      || importer.textureCompression != TextureImporterCompression.Uncompressed
                      || importer.mipmapEnabled
                      || !importer.alphaIsTransparency;

        if (!cambio) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return true;
    }
}
