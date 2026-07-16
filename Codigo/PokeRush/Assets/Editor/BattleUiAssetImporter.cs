using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Copia los graficos de UI de batalla (Diseño/Graphics) a Assets/ y recorta
// las hojas de sprites (botones, iconos de tipo y de estado). Lo usa el
// builder de la escena de combate; tambien se puede correr solo desde
// PokeRush > Importar graficos de UI de batalla.
namespace PokeRush.EditorTools
{
    public static class BattleUiAssetImporter
    {
        private const string UiDest = "Assets/Sprites/UI/Battle";
        private const string BackgroundsDest = "Assets/Sprites/Battlebacks";

        // Mapa PokenType -> fila en cursor_fight.png / icon_types.png
        // (orden interno de Essentials: Normal=0, Fighting=1, Flying=2, Poison=3,
        //  Ground=4, Rock=5, Bug=6, Ghost=7, Steel=8, ???=9, Fire=10, Water=11,
        //  Grass=12, Electric=13, Psychic=14, Ice=15, Dragon=16, Dark=17, Fairy=18)
        public static readonly Dictionary<PokenType, int> TypeRow = new Dictionary<PokenType, int>
        {
            [PokenType.None] = 0, [PokenType.Normal] = 0, [PokenType.Fighting] = 1,
            [PokenType.Flying] = 2, [PokenType.Poison] = 3, [PokenType.Ground] = 4,
            [PokenType.Rock] = 5, [PokenType.Bug] = 6, [PokenType.Ghost] = 7,
            [PokenType.Steel] = 8, [PokenType.Fire] = 10, [PokenType.Water] = 11,
            [PokenType.Grass] = 12, [PokenType.Electric] = 13, [PokenType.Psychic] = 14,
            [PokenType.Ice] = 15, [PokenType.Dragon] = 16, [PokenType.Dark] = 17,
            [PokenType.Fairy] = 18
        };

        // Mapa StatusEffect -> fila en icon_statuses.png (orden: SLP,PSN,BRN,PAR,FRZ)
        public static readonly Dictionary<StatusEffect, int> StatusRow = new Dictionary<StatusEffect, int>
        {
            [StatusEffect.Asleep] = 0, [StatusEffect.Poisoned] = 1, [StatusEffect.Burned] = 2,
            [StatusEffect.Paralyzed] = 3, [StatusEffect.Frozen] = 4
        };

        // Filas de cursor_command.png que usamos
        public const int CommandRowFight = 0;
        public const int CommandRowPokemon = 1;
        public const int CommandRowBag = 2;
        public const int CommandRowFlee = 3;
        public const int CommandRowBack = 9; // ATRÁS

        [MenuItem("PokeRush/Importar graficos de UI de batalla")]
        public static void ImportMenu()
        {
            ImportAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[BattleUi] Graficos de UI de batalla importados y recortados.");
        }

        // Copia + recorta todo. Devuelve true si encontro la carpeta de diseño.
        public static bool ImportAll()
        {
            string designUi = FindDesignFolder("UI", "Battle");
            string designBacks = FindDesignFolder("Battlebacks");
            if (designUi == null || designBacks == null)
            {
                Debug.LogError("[BattleUi] No encuentro Diseño/Graphics/UI/Battle o /Battlebacks en el repo.");
                return false;
            }

            // --- imagenes simples (sprite unico) ---
            CopyPng(designUi, "databox_normal.png", UiDest);
            CopyPng(designUi, "databox_normal_foe.png", UiDest);
            CopyPng(designUi, "overlay_message.png", UiDest);
            CopyPng(designUi, "icon_ball.png", UiDest);

            // --- fondos de batalla (todos, para que sean intercambiables) ---
            int backgrounds = 0;
            foreach (string file in Directory.GetFiles(designBacks, "*_bg.png"))
            {
                if (CopyPng(designBacks, Path.GetFileName(file), BackgroundsDest)) backgrounds++;
            }

            AssetDatabase.Refresh();

            // --- configurar los sprites simples ---
            ConfigureSprite($"{UiDest}/databox_normal.png");
            ConfigureSprite($"{UiDest}/databox_normal_foe.png");
            ConfigureSprite($"{UiDest}/overlay_message.png");
            ConfigureSprite($"{UiDest}/icon_ball.png");
            foreach (string path in Directory.GetFiles(BackgroundsDest, "*_bg.png"))
                ConfigureSprite(path.Replace('\\', '/'));

            // --- hojas de sprites recortadas ---
            // cursor_command: 260x552 = 12 filas de 46, 2 columnas de 130 (normal | iluminado)
            CopyPng(designUi, "cursor_command.png", UiDest);
            // cursor_fight: 384x874 = 19 filas de 46, 2 columnas de 192 (una fila por tipo)
            CopyPng(designUi, "cursor_fight.png", UiDest);
            // icon_types: 96x608 = 19 filas de 32
            CopyPng(designUi, "icon_types.png", UiDest);
            // icon_statuses: 44x96 = 6 filas de 16
            CopyPng(designUi, "icon_statuses.png", UiDest);
            AssetDatabase.Refresh();

            SliceGrid($"{UiDest}/cursor_command.png", 130, 46, "command");
            SliceGrid($"{UiDest}/cursor_fight.png", 192, 46, "move");
            SliceGrid($"{UiDest}/icon_types.png", 96, 32, "type");
            SliceGrid($"{UiDest}/icon_statuses.png", 44, 16, "status");

            Debug.Log($"[BattleUi] UI copiada y {backgrounds} fondos importados en {BackgroundsDest}.");
            return true;
        }

        // ---------- acceso a los sprites ya importados ----------

        public static Sprite LoadSimple(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDest}/{fileName}");

        public static Sprite LoadBackground(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{BackgroundsDest}/{fileName}");

        // Sub-sprite de una hoja recortada: prefix_fila_col (col 0 normal, 1 iluminado)
        public static Sprite LoadSlice(string sheetFileName, string prefix, int row, int column)
        {
            string name = $"{prefix}_{row}_{column}";
            return AssetDatabase.LoadAllAssetsAtPath($"{UiDest}/{sheetFileName}")
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
        }

        // ---------- internos ----------

        private static bool CopyPng(string sourceDir, string fileName, string destAssetDir)
        {
            string source = Path.Combine(sourceDir, fileName);
            if (!File.Exists(source)) { Debug.LogWarning($"[BattleUi] Falta {fileName} en {sourceDir}"); return false; }

            string destFull = Path.GetFullPath($"{destAssetDir}/{fileName}");
            Directory.CreateDirectory(Path.GetDirectoryName(destFull));
            if (!File.Exists(destFull)) File.Copy(source, destFull);
            return true;
        }

        private static TextureImporter GetImporter(string assetPath)
            => AssetImporter.GetAtPath(assetPath) as TextureImporter;

        private static void ConfigureSprite(string assetPath)
        {
            TextureImporter importer = GetImporter(assetPath);
            if (importer == null) return;

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.filterMode != FilterMode.Point
                           || importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (changed) importer.SaveAndReimport();
        }

        // Recorta una hoja en una grilla de celdas fijas: prefix_fila_col.
        // La fila 0 es la de ARRIBA (los rects de Unity tienen origen abajo).
        private static void SliceGrid(string assetPath, int cellWidth, int cellHeight, string prefix)
        {
            TextureImporter importer = GetImporter(assetPath);
            if (importer == null) return;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            int width, height;
            if (texture != null) { width = texture.width; height = texture.height; }
            else return;

            int columns = width / cellWidth;
            int rows = height / cellHeight;

            var metas = new List<SpriteMetaData>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    metas.Add(new SpriteMetaData
                    {
                        name = $"{prefix}_{row}_{col}",
                        rect = new Rect(col * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
#pragma warning disable 618 // spritesheet esta deprecada pero sigue siendo la via simple
            importer.spritesheet = metas.ToArray();
#pragma warning restore 618
            importer.SaveAndReimport();
        }

        private static string FindDesignFolder(params string[] parts)
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int depth = 0; depth < 6 && dir != null; depth++)
            {
                string candidate = Path.Combine(dir.FullName, "Diseño", "Graphics", Path.Combine(parts));
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
