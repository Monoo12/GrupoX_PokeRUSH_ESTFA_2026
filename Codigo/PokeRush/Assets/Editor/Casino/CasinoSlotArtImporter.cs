using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Deja lista para usar el arte de tragamonedas que ya viene en el pack
// (Assets/Graphics/UI/Slot Machine): la maquina entera, la sombra del rodillo y
// la tira de simbolos Pokemon.
//
// No dibuja nada nuevo: configura el importador y corta la tira. Es lo que
// permite que la pantalla de slots use el arte del juego en vez de cuadrados de
// colores con una letra.
public static class CasinoSlotArtImporter
{
    public const string Carpeta = "Assets/Graphics/UI/Slot Machine";
    public const string Maquina = Carpeta + "/bg.png";
    public const string Sombra = Carpeta + "/ReelOverlay.png";
    public const string Simbolos = Carpeta + "/images.png";

    // La tira son 8 simbolos de 64x48, en este orden. Los nombres son los que
    // van a quedar en el asset y los que busca el builder.
    public static readonly string[] NombresDeSimbolo =
    {
        "slot_cereza",
        "slot_magnemite",
        "slot_gastly",
        "slot_pikachu",
        "slot_psyduck",
        "slot_siete_rojo",
        "slot_siete_azul",
        "slot_replay"
    };

    public const int AnchoSimbolo = 64;
    public const int AltoSimbolo = 48;

    [MenuItem("PokeRush/Casino/Importar el arte de la tragaperras")]
    public static void ImportarTodo()
    {
        ImportarSimple(Maquina);
        ImportarSimple(Sombra);
        CortarSimbolos();
        AssetDatabase.Refresh();
        Debug.Log("[SlotArt] Arte de la tragaperras importado y la tira cortada en " +
                  NombresDeSimbolo.Length + " simbolos.");
    }

    // Pixel art: Point y sin comprimir. Con el filtro bilineal por defecto la
    // maquina se ve lavada y los bordes borrosos, que es exactamente el problema
    // que se ve en pantalla.
    private static void ImportarSimple(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[SlotArt] No existe {path}."); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();
    }

    // Corta la tira en los 8 simbolos.
    //
    // Se hace con SpriteEditorDataProvider y NO escribiendo 'spritesheet' en el
    // importer: en Unity 6 ese campo se ignora y el slicer automatico pisa la
    // grilla con lo que le parece.
    private static void CortarSimbolos()
    {
        var importer = AssetImporter.GetAtPath(Simbolos) as TextureImporter;
        if (importer == null) { Debug.LogError($"[SlotArt] No existe {Simbolos}."); return; }

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

        for (int i = 0; i < NombresDeSimbolo.Length; i++)
        {
            var sr = new SpriteRect
            {
                name = NombresDeSimbolo[i],
                spriteID = GUID.Generate(),
                rect = new Rect(i * AnchoSimbolo, 0, AnchoSimbolo, AltoSimbolo),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
            rects.Add(sr);
            pares.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        }

        provider.SetSpriteRects(rects.ToArray());

        // Sin esto los sprites cambian de fileID en cada reimport y las
        // referencias de los assets quedan en None.
        var nombres = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nombres != null)
        {
            nombres.SetNameFileIdPairs(pares);
        }

        provider.Apply();
        AssetDatabase.ImportAsset(Simbolos, ImportAssetOptions.ForceUpdate);
    }

    // Los simbolos ya cortados, en el orden de NombresDeSimbolo.
    public static Sprite[] CargarSimbolos()
    {
        var porNombre = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Simbolos))
            if (o is Sprite s) porNombre[s.name] = s;

        var salida = new Sprite[NombresDeSimbolo.Length];
        for (int i = 0; i < NombresDeSimbolo.Length; i++)
            porNombre.TryGetValue(NombresDeSimbolo[i], out salida[i]);
        return salida;
    }

    public static Sprite CargarSprite(string path)
        => AssetDatabase.LoadAssetAtPath<Sprite>(path);
}
