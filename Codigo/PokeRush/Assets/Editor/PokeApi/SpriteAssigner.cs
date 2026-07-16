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
// Los sprites del set se nombran por el nombre INGLES en mayusculas y sin
// simbolos (BULBASAUR, NIDORANF, MRMIME, HOOH...). El nombre ingles de cada
// especie sale del cache de PokeAPI (indexado por numero de Pokedex, que cada
// PokenDefinition ya guarda en dexNumber). El script:
//   1. Deriva el nombre de archivo del sprite desde el nombre ingles.
//   2. Copia el PNG necesario de Diseño/ a Assets/Sprites/Pokens/ (Unity solo
//      puede referenciar assets dentro de Assets/). Idempotente: no recopia.
//   3. Lo importa como Sprite y lo asigna al campo correspondiente.
// Reporta al final cuantos asigno y de cuales no encontro sprite.
namespace PokeRush.EditorTools.PokeApi
{
    public static class SpriteAssigner
    {
        private const string PokensFolder = "Assets/Resources/Definitions/Pokens";
        private const string DestRoot = "Assets/Sprites/Pokens";

        // Que carpeta del set alimenta que campo de PokenDefinition.
        private struct Slot
        {
            public string SourceSubfolder;   // carpeta en Diseño/Graphics/Pokemon
            public string Field;             // campo Sprite en PokenDefinition
        }

        private static readonly Slot[] Slots =
        {
            new Slot { SourceSubfolder = "Front",       Field = "battleFrontSprite" },
            new Slot { SourceSubfolder = "Back",        Field = "battleBackSprite" },
            new Slot { SourceSubfolder = "Front shiny", Field = "battleFrontShinySprite" },
            new Slot { SourceSubfolder = "Back shiny",  Field = "battleBackShinySprite" },
            new Slot { SourceSubfolder = "Icons",       Field = "inventorySprite" },
            new Slot { SourceSubfolder = "Icons shiny", Field = "inventoryShinySprite" }
        };

        [MenuItem("PokeRush/Asignar sprites a los Pokemon")]
        public static void Assign()
        {
            string designRoot = FindDesignPokemonFolder();
            if (designRoot == null)
            {
                EditorUtility.DisplayDialog("No encuentro los graficos",
                    "No pude ubicar la carpeta Diseño/Graphics/Pokemon subiendo desde el proyecto.\n" +
                    "Verifica que exista en la raiz del repositorio.", "OK");
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
            var newlyCopied = new List<string>();                 // assetPaths a configurar como Sprite
            var assignments = new List<(PokenDefinition poken, string field, string assetPath)>();
            var missing = new List<string>();                     // "Pikachu: Back shiny" no encontrados

            try
            {
                // --- Pasada 1: copiar los PNG necesarios y anotar las asignaciones ---
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
                            // Front/Back son los esenciales: solo esos se reportan como faltantes
                            if (slot.SourceSubfolder == "Front" || slot.SourceSubfolder == "Back")
                                missing.Add($"{poken.pokenName} ({fileName}): {slot.SourceSubfolder}");
                            continue;
                        }

                        string destDir = $"{DestRoot}/{slot.SourceSubfolder}";
                        string assetPath = $"{destDir}/{fileName}.png";
                        string destFull = Path.GetFullPath(assetPath);

                        if (!File.Exists(destFull))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destFull));
                            File.Copy(sourcePath, destFull);
                            newlyCopied.Add(assetPath);
                            copied++;
                        }
                        assignments.Add((poken, slot.Field, assetPath));
                    }
                }

                // --- Importar de una sola vez lo recien copiado ---
                EditorUtility.DisplayProgressBar("Importando sprites", "Actualizando la base de assets...", 0.9f);
                AssetDatabase.Refresh();

                // --- Configurar como Sprite (pixel art: sin filtro, sin compresion) ---
                foreach (string assetPath in newlyCopied)
                    ConfigureAsSprite(assetPath);

                // --- Pasada 2: asignar cada sprite a su campo ---
                foreach (var (poken, field, assetPath) in assignments)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null) continue;

                    var so = new SerializedObject(poken);
                    SerializedProperty prop = so.FindProperty(field);
                    if (prop != null && prop.objectReferenceValue != sprite)
                    {
                        prop.objectReferenceValue = sprite;
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

            string faltantes = missing.Count == 0
                ? ""
                : $"\n\nSin sprite ({missing.Count}, revisar a mano):\n- " +
                  string.Join("\n- ", missing.Take(25)) +
                  (missing.Count > 25 ? $"\n...y {missing.Count - 25} mas (ver Console)." : "");

            if (missing.Count > 0)
                Debug.LogWarning("[SpriteAssigner] Sin sprite:\n" + string.Join("\n", missing));

            string report = $"Sprites copiados a Assets/Sprites/Pokens: {copied}\n" +
                            $"Asignaciones hechas: {assigned}" + faltantes;
            Debug.Log("[SpriteAssigner] " + report);
            EditorUtility.DisplayDialog("Sprites asignados", report, "OK");
        }

        // ---------- nombre de archivo del sprite ----------

        // Nombre ingles de la especie (del cache PokeAPI por dexNumber) convertido
        // al formato del set: MAYUSCULAS sin simbolos. Fallback: el nombre del
        // propio asset (util para las especies de prueba sin cache).
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
            // Fallback: el nombre del asset (que suele coincidir con el ingles en Gen 1-2)
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

        private static void ConfigureAsSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        // Sube desde Assets/ hasta encontrar Diseño/Graphics/Pokemon en el repo.
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
