using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Empareja las hojas de Assets/Graphics/Animations con los MoveDefinition,
// las recorta en frames y arma un MoveAnimationDefinition por movimiento,
// dejandolo enganchado. El puente es MoveDefinition.englishName: las hojas
// vienen nombradas en ingles (con prefijos tipo "PRAS- ", "GEN8- ") y los
// assets guardan el nombre canonico de PokeAPI.
//
// OJO con el formato de las hojas. Hay tres familias y NO son intercambiables:
//   1. Secuencias simples (1-3 filas de 192x192): se leen de corrido. Son la
//      mayoria y las unicas que importamos automaticamente.
//   2. Bibliotecas de piezas (7+ filas): varias sub-animaciones distintas en
//      la misma hoja. Leerlas de corrido da un resultado incoherente, asi que
//      necesitan un Override que diga que tramo usar (ver PRAS- Fire.png).
//   3. Fondos de pantalla completa (celdas de 256x192, sufijo BG/FG): no van
//      sobre el objetivo sino detras de toda la escena. Todavia sin soporte.
namespace PokeRush.EditorTools
{
    public static class MoveAnimationImporter
    {
        private const string SheetDir = "Assets/Graphics/Animations";
        private const string AnimDir = "Assets/Resources/Definitions/MoveAnimations";
        private const string MovesDir = "Assets/Resources/Definitions/Moves";
        private const int Cell = 192;
        private const int MaxAutoRows = 3;      // mas filas que esto = biblioteca, no secuencia
        private const float BackgroundFill = 0.9f; // celda casi llena = fondo, no frame

        // Escala por defecto, en unidades de mundo (1 celda de 192px = 1 unidad).
        // Medido sobre CombatePrueba: la camara es ortografica con size 1.92, o
        // sea 3.84 unidades de alto visible, y un Poken ocupa ~1.1. Con 1.3 el
        // efecto cubre al objetivo con algo de margen sin tapar la pantalla;
        // 2.5 (el valor con el que arranque) daba 2.3 veces el Poken.
        private const float DefaultScale = 1.3f;
        private const float DefaultFps = 15f;

        // Tope de agrandado. Algunas hojas tienen dibujos de 13px sueltos en la
        // celda; normalizarlos a 1.3 unidades pide un 19x y sale un borron. Con
        // el tope se ven mas chicos que el resto, que es lo correcto: un
        // destellito es un destellito, no un efecto a pantalla completa.
        private const float MaxScale = 4f;

        // Ajustes finos para movimientos concretos. Sin entrada aca, un
        // movimiento usa la hoja que matchee por nombre y toda su secuencia.
        private class Override
        {
            public string Sheet;          // fuerza una hoja concreta
            public int FirstFrame = 0;    // tramo dentro de la hoja
            public int FrameCount = -1;   // -1 = hasta el final
            public float Fps = DefaultFps;
            public float Scale = DefaultScale;
            public bool AsProjectile;     // los frames viajan en vez de estallar
            public bool ScreenFlash;
        }

        // Los nombres son englishName de PokeAPI.
        private static readonly Dictionary<string, Override> Overrides =
            new Dictionary<string, Override>
        {
            // fire blast: 5x3, los 10 primeros son la estrella; la celda 10 es
            // un fondo naranja de pantalla completa que no va en la secuencia.
            ["fire-blast"] = new Override
            {
                // Llamarada es de las grandes: un poco por encima del default
                Sheet = "fire blast.png", FrameCount = 10,
                Fps = 15f, Scale = 1.6f, ScreenFlash = true
            },
            // PRAS- Fire.png es biblioteca (5x15). Sus 6 primeras celdas son un
            // chorro de llama que atraviesa la pantalla: justo un Lanzallamas.
            ["flamethrower"] = new Override
            {
                // el chorro viaja ~1.9 unidades entre los dos Pokens, asi que
                // conviene mas chico que un efecto que estalla sobre el objetivo
                Sheet = "PRAS- Fire.png", FirstFrame = 0, FrameCount = 6,
                Fps = 18f, Scale = 1.1f, AsProjectile = true
            }
        };

        [MenuItem("PokeRush/Importar animaciones de ataque")]
        public static void ImportMenu()
        {
            var moves = LoadMoves();
            if (moves.Count == 0)
            {
                Debug.LogError("[MoveAnim] No encontre MoveDefinition en " + MovesDir);
                return;
            }
            Directory.CreateDirectory(AnimDir);

            Dictionary<string, string> matches = MatchSheets(moves.Keys);
            int done = 0, skipped = 0;
            var reasons = new List<string>();

            try
            {
                int i = 0;
                foreach (var pair in matches)
                {
                    string english = pair.Key;
                    string sheet = pair.Value;
                    if (EditorUtility.DisplayCancelableProgressBar("Animaciones de ataque",
                        $"{english} ({i + 1}/{matches.Count})", (float)i / matches.Count)) break;
                    i++;

                    string reason;
                    if (ImportOne(english, sheet, moves[english], out reason)) done++;
                    else { skipped++; reasons.Add($"  {english}: {reason}"); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            int genericas = BuildTypeFallbacks(moves.Values);

            // Para medir hacia falta leer los pixeles; en el juego no, y una hoja
            // legible ocupa el doble de memoria (ver PreparacionDeRelease).
            PreparacionDeRelease.SoltarPixelesDeLasAnimaciones();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MoveAnim] {done} animaciones importadas, {skipped} salteadas " +
                      $"de {matches.Count} movimientos con hoja. " +
                      $"{genericas} genericas por tipo.");
            if (reasons.Count > 0)
                Debug.Log("[MoveAnim] Salteadas:\n" + string.Join("\n", reasons));
        }

        // ---------- genericas por tipo ----------

        // Movimiento donante de cada tipo: el que mejor lee como "golpe
        // elemental generico". Si alguno no quedo importado, se cae al que mas
        // frames tenga de ese tipo. Cambiar un donante es cambiar una linea.
        private static readonly Dictionary<PokenType, string> Donors =
            new Dictionary<PokenType, string>
        {
            [PokenType.Fire] = "magma-storm",       [PokenType.Water] = "hydro-pump",
            [PokenType.Grass] = "seed-bomb",        [PokenType.Electric] = "discharge",
            [PokenType.Ice] = "powder-snow",        [PokenType.Fighting] = "force-palm",
            [PokenType.Poison] = "sludge-bomb",     [PokenType.Ground] = "earth-power",
            [PokenType.Flying] = "aerial-ace",      [PokenType.Psychic] = "psybeam",
            [PokenType.Bug] = "x-scissor",          [PokenType.Rock] = "stone-axe",
            [PokenType.Ghost] = "shadow-ball",      [PokenType.Dragon] = "dragon-tail",
            [PokenType.Dark] = "dark-pulse",        [PokenType.Steel] = "meteor-mash",
            [PokenType.Fairy] = "fleur-cannon",     [PokenType.Normal] = "false-swipe"
        };

        private const string ByTypeDir = AnimDir + "/PorTipo";

        private static int BuildTypeFallbacks(IEnumerable<MoveDefinition> allMoves)
        {
            Directory.CreateDirectory(ByTypeDir);
            var conAnim = allMoves
                .Where(m => m.animationOverride != null
                            && m.animationOverride.impactFrames != null
                            && m.animationOverride.impactFrames.Length > 0)
                .ToList();

            int built = 0;
            foreach (PokenType type in System.Enum.GetValues(typeof(PokenType)))
            {
                if (type == PokenType.None) continue;

                MoveDefinition donor = null;
                if (Donors.TryGetValue(type, out string preferido))
                    donor = conAnim.FirstOrDefault(m => m.englishName == preferido);
                // sin el preferido, el del tipo con mas frames
                if (donor == null)
                    donor = conAnim.Where(m => m.type == type)
                                   .OrderByDescending(m => m.animationOverride.impactFrames.Length)
                                   .FirstOrDefault();
                if (donor == null) continue;

                string path = $"{ByTypeDir}/{type}.asset";
                var generic = AssetDatabase.LoadAssetAtPath<MoveAnimationDefinition>(path);
                bool isNew = generic == null;
                if (isNew) generic = ScriptableObject.CreateInstance<MoveAnimationDefinition>();

                MoveAnimationDefinition src = donor.animationOverride;
                generic.impactFrames = src.impactFrames;
                generic.impactFps = src.impactFps;
                generic.impactScale = src.impactScale;
                generic.tintImpactFrames = false;
                generic.screenFlash = false;   // el flash queda para los movimientos destacados

                if (isNew) AssetDatabase.CreateAsset(generic, path);
                EditorUtility.SetDirty(generic);
                built++;
            }
            return built;
        }

        // ---------- emparejado ----------

        private static Dictionary<string, MoveDefinition> LoadMoves()
        {
            var result = new Dictionary<string, MoveDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:MoveDefinition", new[] { MovesDir }))
            {
                var move = AssetDatabase.LoadAssetAtPath<MoveDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (move == null || string.IsNullOrEmpty(move.englishName)) continue;
                result[move.englishName.Trim().ToLowerInvariant()] = move;
            }
            return result;
        }

        // "GEN8- Aura Sphere.png" -> "aura-sphere"
        private static string Normalize(string fileName)
        {
            string n = Path.GetFileNameWithoutExtension(fileName);
            n = Regex.Replace(n, @"^(GEN\s*8-|GEN\s*9-|Gen\s*8\s*-|Gen\s*9\s*-|Custom_|PRAS\s*-)\s*",
                "", RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @"[_ ](bg|bg2|fg)\d*$", "", RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @"\s*\(\d+\)$", "");
            return Regex.Replace(n, @"[^A-Za-z0-9]+", "-").Trim('-').ToLowerInvariant();
        }

        private static Dictionary<string, string> MatchSheets(IEnumerable<string> englishNames)
        {
            var known = new HashSet<string>(englishNames);
            var result = new Dictionary<string, string>();

            foreach (string path in Directory.GetFiles(SheetDir, "*.png"))
            {
                string file = Path.GetFileName(path);
                // las hojas NNN-Nombre son las viejas de RPG Maker: piezas sueltas
                if (Regex.IsMatch(file, @"^\d{3}-")) continue;

                string key = Normalize(file);
                if (!known.Contains(key)) continue;

                // ante duplicados preferimos la hoja principal sobre la de fondo
                bool isLayer = Regex.IsMatch(file, @"[_ ](bg|fg)\d*\.png$", RegexOptions.IgnoreCase);
                if (result.ContainsKey(key) &&
                    !Regex.IsMatch(result[key], @"[_ ](bg|fg)\d*\.png$", RegexOptions.IgnoreCase))
                    continue;
                if (result.ContainsKey(key) && isLayer) continue;
                result[key] = file;
            }

            // los overrides mandan sobre lo que haya matcheado por nombre
            foreach (var kv in Overrides)
                if (!string.IsNullOrEmpty(kv.Value.Sheet)) result[kv.Key] = kv.Value.Sheet;

            return result;
        }

        // ---------- importacion ----------

        private static bool ImportOne(string english, string sheetFile, MoveDefinition move,
            out string reason)
        {
            reason = null;
            string sheetPath = $"{SheetDir}/{sheetFile}";
            var importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
            if (importer == null) { reason = "no pude cargar el importer"; return false; }

            // OJO con el orden: Unity trae maxTextureSize 2048 por defecto y
            // reescala las hojas altas al importarlas. Si midieramos antes de
            // configurar, Texture2D nos devolveria el tamaño YA REDUCIDO (una
            // hoja de 960x2880 se ve como 682x2048) y la descartariamos por no
            // ser grilla de 192. Configurar primero, medir despues.
            Configure(importer);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (texture == null) { reason = "no pude cargar la textura"; return false; }

            if (texture.width % Cell != 0 || texture.height % Cell != 0)
            {
                reason = $"no es grilla de {Cell} ({texture.width}x{texture.height})";
                return false;
            }

            Overrides.TryGetValue(english, out Override ov);
            int rows = texture.height / Cell;
            if (rows > MaxAutoRows && ov == null)
            {
                reason = $"{rows} filas: es biblioteca de piezas, necesita un Override con el tramo";
                return false;
            }

            Sprite[] frames = SliceAndPick(sheetPath, texture, ov);
            if (frames == null || frames.Length == 0) { reason = "no quedaron frames utiles"; return false; }

            string animPath = $"{AnimDir}/{move.name}.asset";
            var anim = AssetDatabase.LoadAssetAtPath<MoveAnimationDefinition>(animPath);
            bool isNew = anim == null;
            if (isNew) anim = ScriptableObject.CreateInstance<MoveAnimationDefinition>();

            float fps = ov != null ? ov.Fps : DefaultFps;
            float pedido = ov != null ? ov.Scale : DefaultScale;

            // La escala multiplica la CELDA de 192px, pero el dibujo casi nunca
            // la llena: hay hojas donde ocupa 60px y otras donde ocupa 190. Con
            // una escala fija el tamaño en pantalla sale distinto en cada
            // animacion. Normalizamos contra el recuadro real del dibujo para
            // que "1.3" signifique 1.3 unidades VISIBLES en todas.
            int extent = MeasureContentExtent(texture, frames);
            float scale = extent > 0
                ? Mathf.Min(pedido * Cell / extent, MaxScale)
                : pedido;

            if (ov != null && ov.AsProjectile)
            {
                anim.projectileFrames = frames;
                anim.projectileFps = fps;
                anim.projectileScale = scale;
                anim.impactFrames = new Sprite[0];
                // el chorro tarda en llegar: le damos viaje de verdad
                anim.travelTime = 0.42f;
            }
            else
            {
                anim.impactFrames = frames;
                anim.impactFps = fps;
                anim.impactScale = scale;
                anim.tintImpactFrames = false;
                anim.screenFlash = ov != null && ov.ScreenFlash;
            }

            if (isNew) AssetDatabase.CreateAsset(anim, animPath);
            EditorUtility.SetDirty(anim);

            move.animationOverride = anim;
            EditorUtility.SetDirty(move);
            return true;
        }

        // Recorta en grilla y devuelve los frames utiles: descarta celdas vacias
        // y celdas casi llenas (esas son fondos de pantalla, no cuadros).
        private static Sprite[] SliceAndPick(string assetPath, Texture2D texture, Override ov)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;

            int columns = texture.width / Cell;
            int rows = texture.height / Cell;

            var metas = new List<SpriteMetaData>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    metas.Add(new SpriteMetaData
                    {
                        name = $"{Path.GetFileNameWithoutExtension(assetPath)}_{row * columns + col:000}",
                        rect = new Rect(col * Cell, texture.height - (row + 1) * Cell, Cell, Cell),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
            }

#pragma warning disable 618 // spritesheet esta deprecada pero sigue siendo la via simple
            // el nombre importa: el orden de los frames sale de ordenarlos, asi
            // que un cambio en el formato obliga a recortar de nuevo
            bool sameSlicing = importer.spritesheet != null
                               && importer.spritesheet.Length == metas.Count
                               && importer.spritesheet[0].name == metas[0].name;
            if (!sameSlicing)
            {
                importer.spritesheet = metas.ToArray();
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
#pragma warning restore 618

            Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name, System.StringComparer.Ordinal)
                .ToArray();

            var useful = new List<Sprite>();
            foreach (Sprite sprite in all)
            {
                Coverage c = Measure(texture, sprite.rect);
                if (c == Coverage.Empty) continue;          // relleno
                if (c == Coverage.Full && useful.Count > 0) continue; // fondo pegado al final
                useful.Add(sprite);
            }

            if (ov != null)
            {
                if (ov.FirstFrame > 0) useful = useful.Skip(ov.FirstFrame).ToList();
                if (ov.FrameCount > 0) useful = useful.Take(ov.FrameCount).ToList();
            }
            return useful.ToArray();
        }

        // Deja la hoja lista para recortar y medir. maxTextureSize alto es lo
        // que evita que Unity reescale las hojas altas; isReadable es lo que
        // permite mirar los pixeles para descartar celdas vacias.
        private static void Configure(TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Multiple
                           || importer.filterMode != FilterMode.Point
                           || importer.textureCompression != TextureImporterCompression.Uncompressed
                           || !importer.isReadable
                           || importer.mipmapEnabled
                           || !Mathf.Approximately(importer.spritePixelsPerUnit, Cell)
                           || importer.maxTextureSize < 8192;
            if (!changed) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.spritePixelsPerUnit = Cell;   // 1 celda = 1 unidad de mundo
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
        }

        // Lado mayor (en pixeles) del recuadro que ocupa el dibujo a lo largo de
        // TODA la secuencia. Se mide sobre la union de los frames, no frame por
        // frame: si cada uno se normalizara por su cuenta, la animacion latiria
        // al cambiar de cuadro en vez de crecer como corresponde.
        private static int MeasureContentExtent(Texture2D texture, IEnumerable<Sprite> frames)
        {
            const int Stride = 2;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

            foreach (Sprite sprite in frames)
            {
                Rect r = sprite.rect;
                int x0 = (int)r.x, y0 = (int)r.y;
                for (int y = 0; y < r.height; y += Stride)
                {
                    for (int x = 0; x < r.width; x += Stride)
                    {
                        if (texture.GetPixel(x0 + x, y0 + y).a <= 0.05f) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0) return 0;
            return Mathf.Max(maxX - minX + 1, maxY - minY + 1);
        }

        private enum Coverage { Empty, Partial, Full }

        private static Coverage Measure(Texture2D texture, Rect rect)
        {
            const int Stride = 4; // muestreo: alcanza de sobra para decidir
            int opaque = 0, total = 0;
            int x0 = (int)rect.x, y0 = (int)rect.y;
            for (int y = 0; y < rect.height; y += Stride)
            {
                for (int x = 0; x < rect.width; x += Stride)
                {
                    total++;
                    if (texture.GetPixel(x0 + x, y0 + y).a > 0.05f) opaque++;
                }
            }
            if (opaque == 0) return Coverage.Empty;
            return opaque >= total * BackgroundFill ? Coverage.Full : Coverage.Partial;
        }
    }
}
