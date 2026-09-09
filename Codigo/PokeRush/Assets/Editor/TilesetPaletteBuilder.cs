using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Toma los trozos de tileset de Assets/Sprites/Tilesets/<Nombre>/, los recorta
// en tiles de 32x32 y arma una Tile Palette por tileset, lista para pintar.
//
// Por que los trozos y no los PNG originales: los sheets de RPG Maker miden
// hasta 23.370 px de alto y el limite duro de textura de Unity son 16.384. Los
// que lo superan no se pueden cargar ni a resolucion completa, asi que hay que
// partirlos ANTES, fuera de Unity (scripts/partir_tilesets.py). Se conserva el
// orden de la grilla original para que los grupos de tiles (una casa, un
// camino) queden juntos y reconocibles al pintar.
//
// Los Tiles se guardan como sub-assets de la paleta en vez de como archivos
// sueltos: un tileset son miles de tiles y otros tantos .meta harian pesar el
// proyecto sin necesidad.
namespace PokeRush.EditorTools
{
    public static class TilesetPaletteBuilder
    {
        private const string ChunkDir = "Assets/Sprites/Tilesets";
        private const string PaletteDir = "Assets/Palettes";
        private const int Tile = 32;

        [MenuItem("PokeRush/Armar paletas de tiles")]
        public static void BuildAll()
        {
            if (!Directory.Exists(ChunkDir))
            {
                Debug.LogError($"[Tiles] No existe {ChunkDir}. Corre antes scripts/partir_tilesets.py " +
                               "(los sheets originales superan el limite de textura de Unity).");
                return;
            }

            Directory.CreateDirectory(PaletteDir);
            string[] tilesets = Directory.GetDirectories(ChunkDir)
                .Select(Path.GetFileName).OrderBy(n => n).ToArray();
            if (tilesets.Length == 0) { Debug.LogError("[Tiles] No hay trozos que procesar."); return; }

            int totalTiles = 0;
            try
            {
                for (int i = 0; i < tilesets.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Paletas de tiles",
                        $"{tilesets[i]} ({i + 1}/{tilesets.Length})", (float)i / tilesets.Length);
                    totalTiles += BuildOne(tilesets[i]);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Tiles] {tilesets.Length} paletas en {PaletteDir}/ con {totalTiles} tiles en total. " +
                      "Abrilas con Window > 2D > Tile Palette.");
        }

        // Arma una sola paleta sin tocar las demas. BuildAll borra y rehace
        // TODAS las de Assets/Palettes, asi que si ya acomodaste tiles a mano
        // en alguna, correrlo entero te la pisa. Con esto se suma un tileset
        // nuevo dejando el resto como esta.
        [MenuItem("PokeRush/Armar paleta del tileset seleccionado")]
        public static void BuildSelected()
        {
            string[] tilesets = SeleccionDeTilesets();
            if (tilesets.Length == 0)
            {
                Debug.LogError($"[Tiles] Selecciona en el Project una carpeta de tileset dentro de {ChunkDir}.");
                return;
            }

            Directory.CreateDirectory(PaletteDir);
            int totalTiles = 0;
            try
            {
                for (int i = 0; i < tilesets.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Paletas de tiles",
                        $"{tilesets[i]} ({i + 1}/{tilesets.Length})", (float)i / tilesets.Length);
                    totalTiles += BuildOne(tilesets[i]);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Tiles] {string.Join(", ", tilesets)}: {totalTiles} tiles en {PaletteDir}/. " +
                      "Abrila con Window > 2D > Tile Palette.");
        }

        [MenuItem("PokeRush/Armar paleta del tileset seleccionado", true)]
        private static bool PuedeArmarSeleccionado() => SeleccionDeTilesets().Length > 0;

        // Arma solo los tilesets que todavia no tienen paleta. Es la via segura
        // cuando no hay como seleccionar en el Project (batchmode, MCP): BuildAll
        // rehace TODAS y pisa las que se acomodaron a mano.
        [MenuItem("PokeRush/Armar paletas faltantes")]
        public static void BuildMissing()
        {
            if (!Directory.Exists(ChunkDir))
            {
                Debug.LogError($"[Tiles] No existe {ChunkDir}.");
                return;
            }

            Directory.CreateDirectory(PaletteDir);
            string[] faltantes = Directory.GetDirectories(ChunkDir)
                .Select(Path.GetFileName)
                .Where(n => !File.Exists($"{PaletteDir}/{n}.prefab"))
                .OrderBy(n => n).ToArray();
            if (faltantes.Length == 0)
            {
                Debug.Log("[Tiles] Todos los tilesets ya tienen paleta.");
                return;
            }

            int totalTiles = 0;
            try
            {
                for (int i = 0; i < faltantes.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Paletas de tiles",
                        $"{faltantes[i]} ({i + 1}/{faltantes.Length})", (float)i / faltantes.Length);
                    totalTiles += BuildOne(faltantes[i]);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Tiles] {string.Join(", ", faltantes)}: {totalTiles} tiles en {PaletteDir}/. " +
                      "Abrilas con Window > 2D > Tile Palette.");
        }

        // Solo carpetas que sean hijas directas de ChunkDir: cada una es un
        // tileset y su nombre termina siendo el de la paleta.
        private static string[] SeleccionDeTilesets()
        {
            return Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == ChunkDir)
                .Select(Path.GetFileName)
                .Distinct().OrderBy(n => n).ToArray();
        }

        private static int BuildOne(string tileset)
        {
            string dir = $"{ChunkDir}/{tileset}";
            string[] chunks = Directory.GetFiles(dir, "*.png").OrderBy(p => p).ToArray();
            if (chunks.Length == 0) return 0;

            // ORDEN IMPORTANTE. Los tiles tienen que existir como asset ANTES
            // de que el Tilemap los referencie. Si se crean en memoria, se
            // arma el prefab y recien despues se hace AddObjectToAsset, las
            // referencias del Tilemap apuntan a objetos que no estan en ningun
            // asset y se pierden al reimportar: queda un prefab pesado y vacio.
            var contenedorPath = $"{PaletteDir}/{tileset}_Tiles.asset";
            AssetDatabase.DeleteAsset(contenedorPath);
            var contenedor = ScriptableObject.CreateInstance<TileContainer>();
            AssetDatabase.CreateAsset(contenedor, contenedorPath);

            var porPosicion = new List<KeyValuePair<Vector3Int, UnityEngine.Tilemaps.Tile>>();
            int filaBase = 0;

            foreach (string chunkPath in chunks)
            {
                string assetPath = chunkPath.Replace('\\', '/');
                Sprite[] sprites = SliceChunk(assetPath, out int columnas, out int filas);
                if (sprites == null) continue;

                foreach (Sprite sprite in sprites)
                {
                    int index = IndexFromName(sprite.name);
                    if (index < 0) continue;

                    var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                    tile.name = sprite.name;
                    tile.sprite = sprite;
                    tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
                    AssetDatabase.AddObjectToAsset(tile, contenedor);

                    // Y hacia abajo: la fila 0 del sheet es la de arriba, pero
                    // en Unity la Y crece hacia arriba.
                    porPosicion.Add(new KeyValuePair<Vector3Int, UnityEngine.Tilemaps.Tile>(
                        new Vector3Int(index % columnas, -(filaBase + index / columnas), 0), tile));
                }
                filaBase += filas;
            }

            // recien aca los tiles quedan persistidos y con referencia estable
            AssetDatabase.SaveAssets();

            var root = new GameObject(tileset);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var layerGo = new GameObject("Layer1");
            layerGo.transform.SetParent(root.transform, false);
            var tilemap = layerGo.AddComponent<Tilemap>();
            layerGo.AddComponent<TilemapRenderer>();
            foreach (var par in porPosicion) tilemap.SetTile(par.Key, par.Value);

            string palettePath = $"{PaletteDir}/{tileset}.prefab";
            AssetDatabase.DeleteAsset(palettePath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, palettePath);
            Object.DestroyImmediate(root);

            // El GridPalette es lo que hace que Unity lo reconozca como paleta
            var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
            gridPalette.name = "Palette Settings";
            gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
            AssetDatabase.AddObjectToAsset(gridPalette, prefab);
            AssetDatabase.SaveAssets();

            return porPosicion.Count;
        }

        // Recorta el trozo en grilla de 32 y devuelve solo los sprites con dibujo.
        private static Sprite[] SliceChunk(string assetPath, out int columnas, out int filas)
        {
            columnas = filas = 0;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;

            // Configurar y reimportar ANTES de medir: Texture2D devuelve el
            // tamaño ya reescalado por maxTextureSize, no el del archivo.
            bool needsConfig = importer.spriteImportMode != SpriteImportMode.Multiple
                               || !Mathf.Approximately(importer.spritePixelsPerUnit, Tile)
                               || importer.filterMode != FilterMode.Point
                               || !importer.isReadable
                               || importer.maxTextureSize < 16384;
            if (needsConfig)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = Tile;   // 1 tile = 1 celda de Grid
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.maxTextureSize = 16384;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) return null;
            columnas = texture.width / Tile;
            filas = texture.height / Tile;

            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            var rects = new List<SpriteRect>();
            for (int fila = 0; fila < filas; fila++)
            {
                for (int col = 0; col < columnas; col++)
                {
                    int index = fila * columnas + col;
                    var rect = new Rect(col * Tile, texture.height - (fila + 1) * Tile, Tile, Tile);
                    if (EsVacia(texture, rect)) continue;   // sin dibujo, no es un tile
                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{index:0000}",
                        rect = rect,
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = GUID.Generate()
                    });
                }
            }

            // OJO: NO usar importer.spritesheet. Esta deprecada y en Unity 6 el
            // que manda es el SpriteEditorDataProvider: al reimportar en modo
            // Multiple, Unity corre su detector automatico de manchas y pisa lo
            // que hayas puesto por la via vieja. Se nota porque devuelve rects
            // irregulares (199x447, 16x9...) en vez de la grilla pedida.
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider =
                factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            SpriteRect[] actuales = provider.GetSpriteRects();
            if (actuales == null || actuales.Length != rects.Count)
            {
                provider.SetSpriteRects(rects.ToArray());

                // la tabla nombre->id es lo que mantiene las referencias vivas
                // entre reimportaciones; sin esto los tiles quedan apuntando a
                // sprites huerfanos la proxima vez que se corra esto
                var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (nameIds != null)
                {
                    nameIds.SetNameFileIdPairs(rects.Select(r =>
                        new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray());
                }

                provider.Apply();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name, System.StringComparer.Ordinal)
                .ToArray();
        }

        private static bool EsVacia(Texture2D texture, Rect rect)
        {
            int x0 = (int)rect.x, y0 = (int)rect.y;
            for (int y = 0; y < rect.height; y += 2)
                for (int x = 0; x < rect.width; x += 2)
                    if (texture.GetPixel(x0 + x, y0 + y).a > 0.05f) return false;
            return true;
        }

        private static int IndexFromName(string spriteName)
        {
            int guion = spriteName.LastIndexOf('_');
            if (guion < 0) return -1;
            return int.TryParse(spriteName.Substring(guion + 1), out int n) ? n : -1;
        }
    }
}
