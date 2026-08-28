using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
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
    // El marco dorado con gemas sale del windowskin 'choice 18' del pack, pero
    // con el relleno claro del centro VACIADO: ese relleno, al estirarse en las
    // 9 tajadas, aparecia como una raya blanca arriba de cada cartel. Lo genera
    // una vez CasinoPrefabBuilder y queda como asset normal.
    public const string MarcoPath = "Assets/Graphics/Casino/marco_casino.png";
    public const string MarcoOriginal = "Assets/Graphics/Windowskins/choice 18.png";
    // Tapete de la mesa. Sale de mativallej/casino-roulette-game (CC BY-SA 4.0),
    // ya reducido a 512x384: ver Documentacion/Creditos_De_Assets.md.
    public const string FieltroPath = "Assets/Graphics/Casino/mesa_fieltro.png";
    public const string AroPath = "Assets/Graphics/Casino/ruleta_aro.png";
    public const string CentroPath = "Assets/Graphics/Casino/ruleta_centro.png";
    public const string FichasPath = "Assets/Graphics/Casino/fichas.png";

    // El marco mide 48x48 y las gemas de las esquinas llegan hasta el pixel 16.
    // Ese es el borde que Unity NO estira: cortar antes (12) partia las gemas al
    // medio, y como el relleno claro ya no existe, no hay nada que tapar.
    private const float BordeDelMarco = 16f;

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

    // El tapete ya viene del tamanio del Canvas: se usa entero, sin recortes ni
    // mosaicos. Es un degrade, asi que repetirlo se notaria.
    public static Sprite Fieltro() => Importar(FieltroPath);

    public static Sprite Aro() => Importar(AroPath);
    public static Sprite Centro() => Importar(CentroPath);

    // Deja un PNG del pack listo para usar como sprite: Point y sin comprimir,
    // que es lo que mantiene los bordes limpios al escalar.
    public static Sprite Importar(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[CasinoUI] No encontre {path}.");
            return null;
        }

        // Se revisa el modo SIEMPRE, no solo cuando no hay sprite. Una textura
        // que quedo en Multiple devuelve un sub-sprite ('archivo_1') en vez de la
        // imagen entera, y lo que se dibuja es un pedazo suelto: es exactamente
        // lo que le pasaba al disco de la ruleta.
        if (importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single)
        {
            Sprite listo = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (listo != null) return listo;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Las fichas vienen en una tira de 15 de 32x32. Se corta una vez y quedan
    // como sprites sueltos, listos para usar de icono de apuesta.
    public static Sprite Ficha(int indice)
    {
        Sprite[] fichas = Fichas();
        if (fichas == null || fichas.Length == 0) return null;
        return fichas[Mathf.Clamp(indice, 0, fichas.Length - 1)];
    }

    public static Sprite[] Fichas()
    {
        var yaCortadas = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FichasPath))
            if (o is Sprite sp) yaCortadas.Add(sp);
        if (yaCortadas.Count > 1) return Ordenar(yaCortadas);

        var importer = AssetImporter.GetAtPath(FichasPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[CasinoUI] No encontre {FichasPath}.");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        var pares = new List<SpriteNameFileIdPair>();
        for (int i = 0; i < 15; i++)
        {
            var sr = new SpriteRect
            {
                name = $"ficha_{i:00}",
                spriteID = GUID.Generate(),
                rect = new Rect(i * 32f, 0f, 32f, 32f),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
            rects.Add(sr);
            pares.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        }

        provider.SetSpriteRects(rects.ToArray());
        var nombres = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nombres != null) nombres.SetNameFileIdPairs(pares);
        provider.Apply();
        AssetDatabase.ImportAsset(FichasPath, ImportAssetOptions.ForceUpdate);

        var salida = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FichasPath))
            if (o is Sprite sp) salida.Add(sp);
        return Ordenar(salida);
    }

    private static Sprite[] Ordenar(List<Sprite> fichas)
    {
        fichas.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return fichas.ToArray();
    }

    // Se compara contra el borde que queremos, no contra "cualquier borde": si
    // no, cambiar la medida no vuelve a importar y el marco queda como estaba.
    private static bool EsNueveTajadas(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        return importer != null &&
               Mathf.Approximately(importer.spriteBorder.x, BordeDelMarco);
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
        fondo.type = Image.Type.Simple;

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
