using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// ANDRUSIANI > Generar assets
//
// Deja armado todo lo que no es codigo: el sprite marcador, el tile, la paleta
// para poder pintarlo y el prefab del bloque suelto. Es idempotente: correrlo de
// nuevo pisa lo generado y no toca las escenas, asi que sirve para recuperar los
// assets si alguien los borra o si se pierden en un merge.
namespace PokeRush.EditorTools
{
    public static class ANDRUSIANIBuilder
    {
        private const string Raiz = "Assets/ANDRUSIANI";
        private const string TilesDir = Raiz + "/Tiles";
        private const string PrefabsDir = Raiz + "/Prefabs";
        private const string PaletaDir = Raiz + "/Paleta";

        private const string MarcadorPath = TilesDir + "/Marcador_ParedInvisible.png";
        private const string TilePath = TilesDir + "/ParedInvisible.asset";
        private const string PaletaPath = PaletaDir + "/ANDRUSIANI.prefab";
        private const string BloquePath = PrefabsDir + "/BloqueInvisible.prefab";

        private const int Pixeles = 32;   // 1 tile = 32 px = 1 celda de Grid

        [MenuItem("ANDRUSIANI/Generar assets")]
        public static void GenerarTodo()
        {
            foreach (string dir in new[] { TilesDir, PrefabsDir, PaletaDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            Sprite marcador = GenerarMarcador();
            ParedInvisibleTile tile = GenerarTile(marcador);
            GenerarPaleta(tile);
            GenerarBloque();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ANDRUSIANI] Listo:\n" +
                      "  tile    " + TilePath + "\n" +
                      "  paleta  " + PaletaPath + "   (Window > 2D > Tile Palette)\n" +
                      "  prefab  " + BloquePath + "\n" +
                      "Para pintar: menu 'ANDRUSIANI > Preparar capa de paredes invisibles' " +
                      "sobre la escena abierta.");
        }

        // El marcador es blanco a proposito: el tinte lo pone el tile en
        // GetTileData, asi se puede cambiar el color sin regenerar el png.
        private static Sprite GenerarMarcador()
        {
            var tex = new Texture2D(Pixeles, Pixeles, TextureFormat.RGBA32, false);
            var px = new Color32[Pixeles * Pixeles];

            for (int y = 0; y < Pixeles; y++)
            {
                for (int x = 0; x < Pixeles; x++)
                {
                    bool borde = x == 0 || y == 0 || x == Pixeles - 1 || y == Pixeles - 1;
                    bool aspa = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - (Pixeles - 1)) <= 1;
                    byte alpha = borde ? (byte)255 : aspa ? (byte)190 : (byte)55;
                    px[y * Pixeles + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(MarcadorPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(MarcadorPath, ImportAssetOptions.ForceUpdate);

            // Sin PPU 32 el marcador no mide una celda y la pared se dibuja
            // corrida respecto de donde choca.
            var imp = (TextureImporter)AssetImporter.GetAtPath(MarcadorPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = Pixeles;
            imp.filterMode = FilterMode.Point;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(MarcadorPath);
        }

        private static ParedInvisibleTile GenerarTile(Sprite marcador)
        {
            var tile = AssetDatabase.LoadAssetAtPath<ParedInvisibleTile>(TilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<ParedInvisibleTile>();
                AssetDatabase.CreateAsset(tile, TilePath);
            }

            tile.marcador = marcador;
            tile.tipoDeCollider = Tile.ColliderType.Grid;
            tile.invisibleEnJuego = true;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();   // la paleta lo referencia: tiene que estar persistido
            return tile;
        }

        // Sin paleta el tile no se puede pintar: la Tile Palette solo muestra
        // tiles que viven dentro de un prefab con un GridPalette adentro.
        private static void GenerarPaleta(ParedInvisibleTile tile)
        {
            var root = new GameObject("ANDRUSIANI");
            root.AddComponent<Grid>().cellSize = Vector3.one;

            var capa = new GameObject("Layer1");
            capa.transform.SetParent(root.transform, false);
            var tilemap = capa.AddComponent<Tilemap>();
            capa.AddComponent<TilemapRenderer>();
            tilemap.SetTile(Vector3Int.zero, tile);

            AssetDatabase.DeleteAsset(PaletaPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PaletaPath);
            Object.DestroyImmediate(root);

            var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
            gridPalette.name = "Palette Settings";
            gridPalette.cellSizing = GridPalette.CellSizing.Manual;
            AssetDatabase.AddObjectToAsset(gridPalette, prefab);
            AssetDatabase.SaveAssets();
        }

        private static void GenerarBloque()
        {
            var go = new GameObject("BloqueInvisible");
            int capa = LayerMask.NameToLayer("Obstacles");
            if (capa >= 0) go.layer = capa;

            var caja = go.AddComponent<BoxCollider2D>();
            caja.size = Vector2.one;
            caja.offset = Vector2.zero;
            go.AddComponent<BloqueInvisible>();

            AssetDatabase.DeleteAsset(BloquePath);
            PrefabUtility.SaveAsPrefabAsset(go, BloquePath);
            Object.DestroyImmediate(go);
        }

        // ANDRUSIANI > Verificar que la pared choca
        //
        // Arma un tilemap de prueba en memoria, lo pinta y mira el shapeCount.
        // Es el chequeo que importa: un tile que se ve pero da 0 formas es
        // exactamente el bug que documenta Como_Armar_Un_Mapa_Nuevo.md.
        [MenuItem("ANDRUSIANI/Verificar que la pared choca")]
        public static void Verificar()
        {
            var tile = AssetDatabase.LoadAssetAtPath<ParedInvisibleTile>(TilePath);
            if (tile == null)
            {
                Debug.LogError("[ANDRUSIANI] No existe " + TilePath +
                               ". Corre 'ANDRUSIANI > Generar assets'.");
                return;
            }

            var root = new GameObject("ANDRUSIANI_test");
            root.transform.position = new Vector3(9000f, 9000f, 0f);   // lejos del mapa real
            root.AddComponent<Grid>().cellSize = Vector3.one;
            var capa = new GameObject("Colisiones");
            capa.transform.SetParent(root.transform, false);
            int capaObstacles = LayerMask.NameToLayer("Obstacles");
            if (capaObstacles >= 0) capa.layer = capaObstacles;

            var tilemap = capa.AddComponent<Tilemap>();
            capa.AddComponent<TilemapRenderer>();
            var tc = capa.AddComponent<TilemapCollider2D>();
            capa.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var cc = capa.AddComponent<CompositeCollider2D>();
            cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;

            for (int x = 0; x < 3; x++)
                tilemap.SetTile(new Vector3Int(x, 0, 0), tile);

            // La misma reconstruccion que hace ColisionesTilemapRuntimeFix: el
            // TilemapCollider2D no se entera solo de los tiles nuevos y hay que
            // sacarle y devolverle el composite para que regenere de verdad.
            tc.compositeOperation = Collider2D.CompositeOperation.None;
            tc.enabled = false;
            tc.enabled = true;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
            cc.GenerateGeometry();
            Physics2D.SyncTransforms();

            int formas = cc.shapeCount;
            var tipo = tilemap.GetColliderType(new Vector3Int(1, 0, 0));
            bool marcadorVisible = tilemap.GetSprite(new Vector3Int(1, 0, 0)) != null;

            // Lo que de verdad importa: el PlayerController no mira el collider,
            // mira este OverlapCircle contra la capa 'Obstacles'.
            Vector2 centro = root.transform.position + new Vector3(1.5f, 0.5f, 0f);
            bool frena = capaObstacles >= 0 &&
                         Physics2D.OverlapCircle(centro, 0.3f, 1 << capaObstacles) != null;
            bool dejaPasar = capaObstacles >= 0 &&
                             Physics2D.OverlapCircle(centro + new Vector2(0f, 3f), 0.3f,
                                                     1 << capaObstacles) == null;

            Object.DestroyImmediate(root);

            bool ok = formas > 0 && tipo == Tile.ColliderType.Grid && marcadorVisible && frena && dejaPasar;
            string detalle = "formas=" + formas + " colliderType=" + tipo +
                             " marcador=" + marcadorVisible + " frena=" + frena +
                             " dejaPasarDondeNoHayPared=" + dejaPasar;

            if (ok)
                Debug.Log("[ANDRUSIANI] OK: 3 celdas pintadas bloquean al jugador. " + detalle);
            else
                Debug.LogError("[ANDRUSIANI] MAL (esperaba formas>0, Grid, True, True, True): " + detalle);
        }
    }
}
