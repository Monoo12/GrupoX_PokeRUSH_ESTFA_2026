using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Asigna a cada PokenDefinition sus sprites de batalla y de UI a partir del set
// de graficos del equipo (Diseño/Graphics/Pokemon/, fuera de Assets/).
//
//   PokeRush > Asignar sprites a los Pokemon
//
// Los PNG del set son TIRAS DE ANIMACION estilo Gen 5: N frames cuadrados en
// fila, donde el lado del frame es el alto del PNG (ej: BULBASAUR.png Front =
// 3762x38 = 99 frames de 38x38). Este script:
//   1. Deriva el nombre de archivo desde el nombre ingles (cache de PokeAPI,
//      indexado por dexNumber): MAYUSCULAS sin simbolos (NIDORANF, MRMIME...).
//   2. Copia el PNG a Assets/Sprites/Pokens/ (idempotente).
//   3. Lo RECORTA en frames (Multiple), con pivot bottom-center (los pies
//      apoyan en la plataforma), el PPU que le da su tamaño en combate (ver
//      PpuDeCombate) y maxTextureSize suficiente para que Unity no reescale
//      la tira (la causa del borroso).
//   4. Asigna: campo single = frame 0 (UI/compatibilidad) y el array de
//      frames completo para la animacion en batalla (PokenView los cicla).
namespace PokeRush.EditorTools.PokeApi
{
    public static class SpriteAssigner
    {
        private const string PokensFolder = "Assets/Resources/Definitions/Pokens";
        private const string DestRoot = "Assets/Sprites/Pokens";

        // Carpeta del set -> campo single + campo array de frames (null = sin array)
        private struct Slot
        {
            public string SourceSubfolder;
            public string Field;        // campo Sprite (recibe el frame 0)
            public string FramesField;  // campo Sprite[] (todos los frames) o null
            public bool Essential;      // si falta, se reporta
        }

        private static readonly Slot[] Slots =
        {
            new Slot { SourceSubfolder = "Front", Field = "battleFrontSprite", FramesField = "battleFrontFrames", Essential = true },
            new Slot { SourceSubfolder = "Back", Field = "battleBackSprite", FramesField = "battleBackFrames", Essential = true },
            new Slot { SourceSubfolder = "Front shiny", Field = "battleFrontShinySprite", FramesField = "battleFrontShinyFrames", Essential = false },
            new Slot { SourceSubfolder = "Back shiny", Field = "battleBackShinySprite", FramesField = "battleBackShinyFrames", Essential = false },
            new Slot { SourceSubfolder = "Icons", Field = "inventorySprite", FramesField = null, Essential = false },
            new Slot { SourceSubfolder = "Icons shiny", Field = "inventoryShinySprite", FramesField = null, Essential = false }
        };

        [MenuItem("PokeRush/Asignar sprites a los Pokemon")]
        public static void Assign()
        {
            string designRoot = FindDesignPokemonFolder();
            if (designRoot == null)
            {
                EditorUtility.DisplayDialog("No encuentro los graficos",
                    "No pude ubicar la carpeta Diseño/Graphics/Pokemon subiendo desde el proyecto.", "OK");
                return;
            }

            List<PokenDefinition> pokens = LoadAllPokens();
            if (pokens.Count == 0)
            {
                EditorUtility.DisplayDialog("No hay especies",
                    "No encontre PokenDefinition en Resources/Definitions/Pokens.", "OK");
                return;
            }

            int assigned = 0, copied = 0;
            var toConfigure = new List<string>();  // assetPaths a configurar/recortar
            var assignments = new List<(PokenDefinition poken, Slot slot, string assetPath)>();
            var missing = new List<string>();

            try
            {
                // --- Pasada 1: copiar los PNG y anotar las asignaciones ---
                for (int i = 0; i < pokens.Count; i++)
                {
                    PokenDefinition poken = pokens[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Copiando sprites",
                            $"{poken.pokenName} ({i + 1}/{pokens.Count})", (float)i / pokens.Count))
                        break;

                    string fileName = SpriteFileNameFor(poken);
                    if (string.IsNullOrEmpty(fileName)) { missing.Add($"{poken.pokenName}: sin nombre ingles"); continue; }

                    foreach (Slot slot in Slots)
                    {
                        string sourcePath = Path.Combine(designRoot, slot.SourceSubfolder, fileName + ".png");
                        if (!File.Exists(sourcePath))
                        {
                            if (slot.Essential)
                                missing.Add($"{poken.pokenName} ({fileName}): {slot.SourceSubfolder}");
                            continue;
                        }

                        string assetPath = $"{DestRoot}/{slot.SourceSubfolder}/{fileName}.png";
                        string destFull = Path.GetFullPath(assetPath);
                        if (!File.Exists(destFull))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destFull));
                            File.Copy(sourcePath, destFull);
                            copied++;
                        }
                        toConfigure.Add(assetPath);
                        assignments.Add((poken, slot, assetPath));
                    }
                }

                EditorUtility.DisplayProgressBar("Importando sprites", "Actualizando la base de assets...", 0f);
                AssetDatabase.Refresh();

                // --- Pasada 2: configurar y recortar cada tira (idempotente) ---
                var uniquePaths = toConfigure.Distinct().ToList();
                for (int i = 0; i < uniquePaths.Count; i++)
                {
                    if (i % 25 == 0 && EditorUtility.DisplayCancelableProgressBar("Recortando tiras de animacion",
                            $"{i + 1}/{uniquePaths.Count} (esto tarda unos minutos la primera vez)",
                            (float)i / uniquePaths.Count))
                        break;
                    ConfigureStrip(uniquePaths[i], EsTiraDeCombate(uniquePaths[i]));
                }

                // --- Pasada 3: asignar frame 0 + array de frames a cada especie ---
                for (int i = 0; i < assignments.Count; i++)
                {
                    var (poken, slot, assetPath) = assignments[i];
                    if (i % 50 == 0)
                        EditorUtility.DisplayProgressBar("Asignando sprites",
                            $"{i + 1}/{assignments.Count}", (float)i / assignments.Count);

                    Sprite[] frames = LoadFramesSorted(assetPath);
                    if (frames.Length == 0) continue;

                    var so = new SerializedObject(poken);
                    bool changed = false;

                    SerializedProperty single = so.FindProperty(slot.Field);
                    if (single != null && single.objectReferenceValue != frames[0])
                    {
                        single.objectReferenceValue = frames[0];
                        changed = true;
                    }

                    if (slot.FramesField != null)
                    {
                        SerializedProperty array = so.FindProperty(slot.FramesField);
                        if (array != null && array.arraySize != frames.Length)
                        {
                            array.arraySize = frames.Length;
                            for (int f = 0; f < frames.Length; f++)
                                array.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        assigned++;
                    }
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (missing.Count > 0)
                Debug.LogWarning("[SpriteAssigner] Sin sprite:\n" + string.Join("\n", missing));

            string report = $"PNG copiados: {copied}\nEspecies con sprites/frames asignados: {assigned}" +
                            (missing.Count > 0 ? $"\nSin sprite: {missing.Count} (ver Console)" : "");
            Debug.Log("[SpriteAssigner] " + report);
            EditorUtility.DisplayDialog("Sprites asignados", report, "OK");
        }

        // ---------- tamaño en combate ----------

        // Las tiras vienen recortadas al contenido, asi que el alto del frame ES
        // el tamaño del Pokemon: Aron mide 32 px y Noivern 182. Con PPU = alto
        // del frame (la regla vieja) todos median 1 unidad de alto y un Aron
        // quedaba del tamaño de un Haunter.
        //
        // Ahora un frame de AltoDeReferencia px mide 1 unidad (como antes) y el
        // resto en proporcion, con topes: los muy chicos no bajan de AltoMinimo
        // para que se vean, y los gigantes no pasan de AltoMaximo para que
        // entren en pantalla. Encima van las escalas de PokenView (1.1 y 1.7).
        public const int AltoDeReferencia = 80;   // la mediana de las tiras
        public const float AltoMinimo = 0.5f;
        public const float AltoMaximo = 1.5f;

        private static readonly string[] CarpetasDeCombate = { "Front", "Back", "Front shiny", "Back shiny" };

        // Alto en unidades de mundo de un frame de 'altoDelFrame' px.
        public static float AltoEnCombate(int altoDelFrame)
            => Mathf.Clamp(altoDelFrame / (float)AltoDeReferencia, AltoMinimo, AltoMaximo);

        public static float PpuDeCombate(int altoDelFrame)
            => altoDelFrame / AltoEnCombate(altoDelFrame);

        // Las tiras de batalla de Assets/Sprites/Pokens (no los iconos).
        public static bool EsTiraDeCombate(string assetPath)
        {
            string carpeta = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            foreach (string c in CarpetasDeCombate)
                if (carpeta == $"{DestRoot}/{c}") return true;
            return false;
        }

        // Reaplica el tamaño a las tiras ya importadas. Solo toca el PPU: no
        // copia, no recorta y no cambia las referencias de las especies.
        [MenuItem("PokeRush/Ajustar tamaño de los Pokémon en combate")]
        public static void AjustarTamanioEnCombate()
        {
            int cambiadas = 0, revisadas = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string c in CarpetasDeCombate)
                {
                    string carpeta = $"{DestRoot}/{c}";
                    if (!AssetDatabase.IsValidFolder(carpeta)) continue;

                    foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!EsTiraDeCombate(path)) continue;
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null) continue;
                        if (!TryReadPngSize(Path.GetFullPath(path), out _, out int alto)) continue;
                        revisadas++;

                        float ppu = PpuDeCombate(alto);
                        if (Mathf.Abs(importer.spritePixelsPerUnit - ppu) < 0.001f) continue;
                        importer.spritePixelsPerUnit = ppu;
                        importer.SaveAndReimport();
                        cambiadas++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[SpriteAssigner] Tamaño en combate: {cambiadas} tiras ajustadas de {revisadas} revisadas " +
                      $"({AltoDeReferencia} px = 1 unidad, entre {AltoMinimo} y {AltoMaximo}).");
        }

        // ---------- recorte de tiras ----------

        // Configura un PNG como tira de frames cuadrados: Multiple, pivot abajo,
        // sin filtro ni compresion, tamaño sin reescalar. El PPU es el de combate
        // para las tiras de batalla y el alto del frame para el resto: los
        // iconos y los entrenadores (BattleUiAssetImporter lo reusa para ellos).
        public static void ConfigureStrip(string assetPath, bool tamanioDeCombate = false)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            // Dimensiones REALES del PNG (texture.width mentiria si Unity ya lo reescalo)
            if (!TryReadPngSize(Path.GetFullPath(assetPath), out int width, out int height)) return;

            // Tira = frames cuadrados en fila; si no es exacto, queda como frame unico
            int frameCount = (height > 0 && width > height && width % height == 0) ? width / height : 1;
            int maxSize = NextPowerOfTwo(Mathf.Max(width, height));

            var metas = new List<SpriteMetaData>();
            for (int f = 0; f < frameCount; f++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"frame_{f:000}",
                    rect = new Rect(f * height, 0, frameCount > 1 ? height : width, height),
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f)
                });
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618
            importer.spritesheet = metas.ToArray();
#pragma warning restore 618
            importer.spritePixelsPerUnit = tamanioDeCombate ? PpuDeCombate(height) : height;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Mathf.Clamp(maxSize, 32, 16384);
            importer.SaveAndReimport();
        }

        // Frames ordenados por indice (frame_000, frame_001...)
        private static Sprite[] LoadFramesSorted(string assetPath)
            => AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();

        // Lee ancho/alto directo del encabezado PNG (bytes 16-23, big-endian)
        private static bool TryReadPngSize(string fullPath, out int width, out int height)
        {
            width = height = 0;
            try
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    var header = new byte[24];
                    if (stream.Read(header, 0, 24) < 24) return false;
                    width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return width > 0 && height > 0;
                }
            }
            catch { return false; }
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 32;
            while (result < value && result < 16384) result *= 2;
            return result;
        }

        // ---------- nombre de archivo del sprite ----------

        private static string SpriteFileNameFor(PokenDefinition poken)
        {
            string english = EnglishNameFor(poken);
            return string.IsNullOrEmpty(english) ? null : Sanitize(english);
        }

        private static string EnglishNameFor(PokenDefinition poken)
        {
            if (poken.dexNumber > 0)
            {
                try
                {
                    var species = PokeApiDownloader.GetDto<PokemonSpeciesDto>("pokemon-species", poken.dexNumber);
                    if (species != null && !string.IsNullOrEmpty(species.Name)) return species.Name;
                }
                catch { /* sin cache: usa el fallback */ }
            }
            return poken.pokenName;
        }

        private static string Sanitize(string name)
            => Regex.Replace(name.ToUpperInvariant(), "[^A-Z0-9]", "");

        // ---------- utilidades ----------

        private static List<PokenDefinition> LoadAllPokens()
            => AssetDatabase.FindAssets("t:PokenDefinition", new[] { PokensFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PokenDefinition>)
                .Where(p => p != null)
                .ToList();

        private static string FindDesignPokemonFolder()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int depth = 0; depth < 6 && dir != null; depth++)
            {
                string candidate = Path.Combine(dir.FullName, "Diseño", "Graphics", "Pokemon");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
