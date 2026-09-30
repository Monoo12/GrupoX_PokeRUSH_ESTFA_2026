using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Prepara los sprites de Nestor y arma su PokenDefinition:
//   PokeRush > Importar sprites de Nestor
//
// POR QUE UN SCRIPT Y NO A MANO
// Los sprites de Nestor no son una grilla: la hoja original (Diseño/nestor)
// tiene poses de distinto tamaño repartidas irregularmente, asi que ya vienen
// recortados a PNG sueltos con alfa. Lo que falta es la parte que Unity no
// adivina y que a mano son 20 assets x 5 campos: filtro Point (si no, el pixel
// art sale borroso), sin compresion, y sobre todo el PPU.
//
// EL PPU ES LO QUE IMPORTA
// El combate asume que un Poken mide 1 unidad de mundo y tiene el pivot en los
// pies (ver BattleTestSceneBuilder: las posiciones Y son el piso de cada
// plataforma y la escala se aplica directa). Para eso el PPU de CADA sprite
// tiene que ser su propia altura en pixeles. Ponerlo fijo en 100 haria que
// Nestor midiera 3.7 unidades y tapara la pantalla.
namespace PokeRush.EditorTools
{
    public static class NestorSpriteImporter
    {
        private const string Carpeta = "Assets/Sprites/Nestor";
        private const string DefinicionPath = "Assets/Resources/Definitions/Pokens/NESTOR.asset";

        [MenuItem("PokeRush/Importar sprites de Nestor")]
        public static void Run()
        {
            List<string> paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            if (paths.Count == 0)
            {
                Debug.LogError($"[Nestor] No hay texturas en {Carpeta}.");
                return;
            }

            foreach (string path in paths) Configurar(path);
            AssetDatabase.Refresh();

            ArmarDefinicion();
            CablearBoss();
            Debug.Log($"[Nestor] {paths.Count} sprites configurados, NESTOR.asset actualizado " +
                      "y el boss cableado con su sprite de entrenador.");
        }

        private static void Configurar(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            // Alto REAL de la textura. Ojo: hay que leerlo del asset ya cargado,
            // no de los settings del importer, que mienten antes del reimport.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int alto = tex != null ? tex.height : 100;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;     // pixel art nitido
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = alto;        // 1 unidad de alto
            importer.spritePivot = new Vector2(0.5f, 0f); // pivot en los pies

            // spritePivot solo se respeta con el alignment en Custom
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        // Nestor tiene que entrar al combate como cualquier otro entrenador:
        // aparece el, dice su linea y RECIEN AHI saca su primer Poken. Eso ya lo
        // hace BattleController (TrainerSlideIn) siempre que el BossDefinition
        // tenga battleFrontSprite; lo que faltaba era que apuntara a Nestor y no
        // a un sprite de entrenador generico heredado.
        private static void CablearBoss()
        {
            Sprite retrato = Cargar("NESTOR_frente");
            if (retrato == null)
            {
                Debug.LogWarning("[Nestor] No encontre NESTOR_frente: el boss se queda con su sprite anterior.");
                return;
            }

            foreach (string ruta in new[]
                     {
                         "Assets/Resources/Definitions/Torre1/Nestor.asset",
                         "Assets/Resources/Definitions/Torre1/Nestor_SinMelmetal.asset"
                     })
            {
                var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(ruta);
                if (boss == null) continue;

                boss.battleFrontSprite = retrato;
                boss.battleIntroSprite = retrato;
                EditorUtility.SetDirty(boss);
                Debug.Log($"[Nestor] Sprite de entrenador asignado en {ruta}");
            }
            AssetDatabase.SaveAssets();
        }

        private static Sprite Cargar(string nombre)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{Carpeta}/{nombre}.png");

        // Nestor como Poken jugable/enfrentable. Stats altas a proposito: es el
        // boss de la torre 1. Los numeros se tocan despues en el Inspector.
        private static void ArmarDefinicion()
        {
            var def = AssetDatabase.LoadAssetAtPath<PokenDefinition>(DefinicionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<PokenDefinition>();
                AssetDatabase.CreateAsset(def, DefinicionPath);
            }

            def.pokenName = "Nestor";
            def.dexNumber = 0;                  // especie propia del juego
            def.type = PokenType.Electric;      // el chip
            def.type2 = PokenType.None;

            def.baseHP = 110;
            def.baseAttack = 95;
            def.baseDefense = 90;
            def.baseSpecialAttack = 105;
            def.baseSpecialDefense = 95;
            def.baseSpeed = 80;
            def.baseXpYield = 200;

            def.battleFrontSprite = Cargar("NESTOR_frente");
            def.battleBackSprite = Cargar("NESTOR_espalda");
            def.worldFrontSprite = Cargar("NESTOR_frente");
            def.worldBackSprite = Cargar("NESTOR_espalda");
            def.worldSideSprite = Cargar("NESTOR_lado");
            def.inventorySprite = Cargar("NESTOR_retrato_feliz");

            // SIN frames de batalla: PokenView toma battleFrontFrames como la
            // tira animada de un Pokemon y la cicla a 30 cuadros por segundo.
            // Con tres POSES distintas (frente, manos en la cadera, brazos
            // cruzados) Nestor parpadeaba entre las tres durante toda la fase de
            // liberacion. Se queda quieto en battleFrontSprite, como un entrenador.
            def.battleFrontFrames = new Sprite[0];

            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }
    }
}
