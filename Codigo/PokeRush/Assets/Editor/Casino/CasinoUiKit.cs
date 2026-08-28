using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Piezas visuales compartidas por las pantallas del casino.
//
// Todo sale del pack que ya esta en el proyecto, no hay arte nuevo:
//   - el marco dorado con gemas es un windowskin de Graphics/Windowskins,
//     recortado a 9 partes para que estire sin deformarse;
//   - el fieltro verde es el tapete de Triple Triad, que mide 512x384, o sea
//     exactamente la resolucion de referencia del Canvas.
//
// Existe para que la ruleta, el blackjack y lo que venga se vean como la misma
// sala y no como tres menus distintos pegados.
public static class CasinoUiKit
{
    public const string MarcoPath = "Assets/Graphics/Windowskins/choice 18.png";
    public const string FieltroPath = "Assets/Graphics/UI/Triple Triad/bg.png";

    // El marco mide 48x48 y su parte ornamentada ocupa 14 pixeles por lado: ese
    // es el borde que Unity NO estira.
    private const float BordeDelMarco = 14f;

    public static Sprite Marco()
    {
        var existente = AssetDatabase.LoadAssetAtPath<Sprite>(MarcoPath);
        if (existente != null && EsNueveTajadas(MarcoPath)) return existente;

        var importer = AssetImporter.GetAtPath(MarcoPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[CasinoUI] No encontre {MarcoPath}.");
            return existente;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        // Sin esto el marco se estira entero y las esquinas quedan aplastadas.
        importer.spriteBorder = new Vector4(BordeDelMarco, BordeDelMarco,
                                            BordeDelMarco, BordeDelMarco);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(MarcoPath);
    }

    private const string FieltroRecortado = "Assets/Sprites/UI/Casino/fieltro.png";

    // El tapete del pack sirve por el color y la textura, pero trae DIBUJADA la
    // grilla 3x3 del Triple Triad, que se ve como marcas sueltas en el fondo.
    // Se recorta un parche limpio de una esquina y despues se usa en mosaico:
    // queda el mismo verde del juego, sin la grilla de otro minijuego.
    public static Sprite Fieltro()
    {
        var recortado = AssetDatabase.LoadAssetAtPath<Sprite>(FieltroRecortado);
        if (recortado != null) return recortado;

        if (!System.IO.File.Exists(FieltroPath))
        {
            Debug.LogWarning($"[CasinoUI] No encontre {FieltroPath}.");
            return null;
        }

        var fuente = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        fuente.LoadImage(System.IO.File.ReadAllBytes(FieltroPath));

        // Zona limpia: arriba a la izquierda, entre el borde y donde arranca la
        // grilla dibujada.
        const int lado = 64;
        var parche = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        parche.SetPixels(fuente.GetPixels(40, 300, lado, lado));
        parche.Apply();

        MenuUiKit.EnsureFolder("Assets/Sprites/UI/Casino");
        System.IO.File.WriteAllBytes(FieltroRecortado, parche.EncodeToPNG());
        Object.DestroyImmediate(fuente);
        Object.DestroyImmediate(parche);

        AssetDatabase.ImportAsset(FieltroRecortado, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(FieltroRecortado) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(FieltroRecortado);
    }

    private static bool EsNueveTajadas(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        return importer != null && importer.spriteBorder.x > 0f;
    }

    // Panel con tapete y marco: la caja base de cualquier pantalla del casino.
    // Devuelve el nodo INTERIOR, con el margen del marco ya descontado, asi lo
    // que se cuelgue adentro no se mete abajo del dorado.
    public static GameObject Panel(string nombre, Transform padre, Vector2 tamanio,
                                   out GameObject exterior)
    {
        exterior = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(exterior, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, tamanio);

        GameObject tapete = MenuUiKit.Node("Tapete", exterior.transform);
        MenuUiKit.Full(tapete);
        Image fondo = MenuUiKit.AddImage(tapete, Fieltro(), Color.white);
        // En mosaico: el parche es chico y se repite, asi el panel puede tener
        // cualquier tamanio sin estirar la textura.
        fondo.type = Image.Type.Tiled;

        GameObject marco = MenuUiKit.Node("Marco", exterior.transform);
        MenuUiKit.Full(marco);
        Image borde = MenuUiKit.AddImage(marco, Marco(), Color.white);
        borde.type = Image.Type.Sliced;
        borde.fillCenter = false;   // el centro lo pone el tapete
        borde.raycastTarget = false;

        GameObject interior = MenuUiKit.Node("Contenido", exterior.transform);
        var rt = (RectTransform)interior.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(BordeDelMarco, BordeDelMarco);
        rt.offsetMax = new Vector2(-BordeDelMarco, -BordeDelMarco);

        return interior;
    }

    // Cartelito interno (visores, cajas de texto) con el mismo marco, mas chico.
    public static GameObject Cartel(string nombre, Transform padre, Vector2 pos,
                                    Vector2 tamanio, Color fondo)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(go, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, tamanio);
        MenuUiKit.AddImage(go, null, fondo);

        GameObject marco = MenuUiKit.Node("Marco", go.transform);
        MenuUiKit.Full(marco);
        Image borde = MenuUiKit.AddImage(marco, Marco(), Color.white);
        borde.type = Image.Type.Sliced;
        borde.fillCenter = false;
        borde.raycastTarget = false;

        return go;
    }
}
