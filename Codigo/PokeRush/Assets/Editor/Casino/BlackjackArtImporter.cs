using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Deja el arte de Assets/BlackJack God listo para usar.
//
// Va en Point y NO en Bilinear, al reves que la mesa de ruleta: aquel dibujo es
// pintado, con degrades, y este es pixel art de verdad (bordes duros, paleta
// corta). Bilinear sobre pixel art lo deja lavado; Point mantiene el borde.
//
// Las dos hojas generadas (bj_cartas.png y bj_fichas.png) se cortan en grillas
// exactas con el SpriteEditorDataProvider y no con el slicer automatico. Es la
// misma razon por la que CasinoUiKit.Fichas() lo hace asi: en Unity 6 el campo
// 'spritesheet' del .meta se ignora, y el slicer automatico por celdas vuelve a
// detectar los bordes por alpha, con lo que una carta con la esquina
// transparente sale corrida respecto de las demas.
//
// De donde salen los bj_*.png: los originales de la carpeta estan bien (alpha
// de verdad, paleta correcta), pero las 52 cartas vienen en cuatro tiras a un
// paso de 48,46 px y con cada carta de un tamanio distinto, y las fichas traen
// el valor escrito debajo y cortado al medio. scripts/cortar_arte_blackjack.py
// rearma las cartas en una grilla exacta, arregla las dos cartas de treboles
// que la hoja trajo mal dibujadas y deja las fichas solo con el disco. Los
// originales quedan intactos al lado.
public static class BlackjackArtImporter
{
    public const string Carpeta = "Assets/BlackJack God";
    public const string Prefijo = "bj_";

    public const string CartasPath = Carpeta + "/bj_cartas.png";
    public const string FichasPath = Carpeta + "/bj_fichas.png";
    public const string CroupierPath = Carpeta + "/bj_croupier.png";

    // La grilla de bj_cartas.png: 13 columnas (As..K) y 5 filas (los cuatro
    // palos mas una con los dos reversos). La celda la fija el script de
    // recorte; si cambia alla, cambia aca.
    public const int Columnas = 13;
    public const int FilasDePalos = 4;
    public const int CeldaAncho = 104;
    public const int CeldaAlto = 152;

    public const int Fichas = 8;
    public const int FichaAncho = 95;
    public const int FichaAlto = 90;

    // Los cuatro cuadros del croupier, sacados de la fila 'de frente' de
    // Graphics/Characters/noble.png (ver cortar_arte_blackjack.py).
    public const int CroupierCuadros = 4;
    public const int CroupierAncho = 108;
    public const int CroupierAlto = 180;

    private static readonly string[] Palos = { "picas", "corazones", "diamantes", "treboles" };

    [MenuItem("PokeRush/Casino/Importar el arte del blackjack")]
    public static void ImportarTodo()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta))
        {
            Debug.LogError($"[Blackjack] No existe la carpeta {Carpeta}.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(CartasPath) == null)
        {
            Debug.LogError($"[Blackjack] Falta {CartasPath}. Corre primero " +
                           "'python scripts/cortar_arte_blackjack.py'.");
            return;
        }

        int sueltos = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetExtension(path).ToLowerInvariant() != ".png") continue;
            // Las dos hojas se cortan aparte, en Multiple.
            if (path == CartasPath || path == FichasPath || path == CroupierPath) continue;
            if (Simple(path)) sueltos++;
        }

        int cartas = CortarCartas();
        int fichas = CortarFichas();
        int croupier = CortarCroupier();

        AssetDatabase.Refresh();
        Debug.Log($"[Blackjack] Arte importado: {sueltos} textura(s) sueltas, " +
                  $"{cartas} carta(s), {fichas} ficha(s) y {croupier} cuadro(s) del " +
                  $"croupier cortados en {Carpeta}.");
    }

    // ------------------------------------------------------------- sueltos

    // Deja un PNG listo para usar como sprite entero.
    private static bool Simple(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool cambio = importer.textureType != TextureImporterType.Sprite
                      || importer.spriteImportMode != SpriteImportMode.Single
                      || importer.filterMode != FilterMode.Point
                      || importer.textureCompression != TextureImporterCompression.Uncompressed
                      || importer.mipmapEnabled
                      || !importer.alphaIsTransparency
                      || importer.maxTextureSize < 2048;

        if (!cambio) return false;

        Comun(importer, SpriteImportMode.Single);
        importer.SaveAndReimport();
        return true;
    }

    private static void Comun(TextureImporter importer, SpriteImportMode modo)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = modo;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100;
        // La hoja de cartas mide 2028 de ancho: con el maximo por defecto en
        // 2048 pasa raspando, pero si alguien agrega una columna se reescala
        // sola y todas las celdas quedan corridas medio pixel.
        importer.maxTextureSize = 4096;
    }

    // -------------------------------------------------------------- cartas

    // Nombre de cada sprite. El builder los busca por aca, asi que renombrarlos
    // rompe el prefab hasta volver a construirlo.
    public static string NombreDeCarta(int palo, int columna)
        => $"carta_{Palos[palo]}_{columna + 1:00}";

    public const string ReversoRojo = "reverso_rojo";
    public const string ReversoAzul = "reverso_azul";

    private static int CortarCartas()
    {
        var rects = new List<SpriteRect>();

        // El origen de coordenadas de un sprite es abajo a la izquierda y la
        // hoja se armo de arriba hacia abajo, asi que la fila 0 (picas) es la
        // de mas arriba y hay que darla vuelta.
        int alto = 5 * CeldaAlto;

        for (int fila = 0; fila < FilasDePalos; fila++)
            for (int col = 0; col < Columnas; col++)
                rects.Add(Celda(NombreDeCarta(fila, col),
                    col * CeldaAncho, alto - (fila + 1) * CeldaAlto, CeldaAncho, CeldaAlto));

        rects.Add(Celda(ReversoRojo, 0, 0, CeldaAncho, CeldaAlto));
        rects.Add(Celda(ReversoAzul, CeldaAncho, 0, CeldaAncho, CeldaAlto));

        return Aplicar(CartasPath, rects);
    }

    private static int CortarFichas()
    {
        var rects = new List<SpriteRect>();
        int[] valores = { 1, 5, 10, 25, 50, 100, 500, 1000 };

        for (int i = 0; i < Fichas; i++)
            rects.Add(Celda($"ficha_{valores[i]:0000}", i * FichaAncho, 0, FichaAncho, FichaAlto));

        return Aplicar(FichasPath, rects);
    }

    public static string NombreDeCroupier(int cuadro) => $"croupier_{cuadro}";

    private static int CortarCroupier()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(CroupierPath) == null) return 0;

        var rects = new List<SpriteRect>();
        for (int i = 0; i < CroupierCuadros; i++)
            rects.Add(Celda(NombreDeCroupier(i), i * CroupierAncho, 0,
                CroupierAncho, CroupierAlto));

        return Aplicar(CroupierPath, rects);
    }

    private static SpriteRect Celda(string nombre, int x, int y, int ancho, int alto)
    {
        return new SpriteRect
        {
            name = nombre,
            spriteID = GUID.Generate(),
            rect = new Rect(x, y, ancho, alto),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static int Aplicar(string path, List<SpriteRect> rects)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[Blackjack] No encontre {path}.");
            return 0;
        }

        // El modo se pone ANTES de pedir el data provider: en Single, el
        // provider devuelve un unico rect y las celdas se pierden.
        Comun(importer, SpriteImportMode.Multiple);
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        provider.SetSpriteRects(rects.ToArray());

        var pares = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect r in rects) pares.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        var nombres = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nombres != null) nombres.SetNameFileIdPairs(pares);

        provider.Apply();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        return rects.Count;
    }

    // --------------------------------------------------------------- buscar

    // Los sprites ya cortados de una hoja, por nombre.
    public static Dictionary<string, Sprite> SpritesDe(string path)
    {
        var mapa = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) mapa[sp.name] = sp;
        return mapa;
    }

    public static Sprite Suelto(string archivo)
    {
        string path = $"{Carpeta}/{archivo}.png";
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) Debug.LogWarning($"[Blackjack] No encontre el sprite {path}.");
        return sp;
    }
}
