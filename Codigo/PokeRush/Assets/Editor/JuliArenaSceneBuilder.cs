using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arma la arena de batalla con la paleta JuliTile, en dos sabores:
//   - el mapa completo, pintado por codigo (el render de referencia);
//   - una escena vacia con el rig listo para pintarla a mano.
//
// Los tiles se nombran JuliTile_00_NNNN, donde NNNN es la celda dentro de la
// hoja de 48 columnas que genera Codigo/Herramientas/juli_tileset.py. Un elemento de varias
// celdas ocupa indices consecutivos: base + fila*48 + columna. Por eso alcanza
// con guardar el indice de arriba-izquierda y el tamaño.
//
// OJO: los indices de abajo son la foto de la hoja actual. Si se vuelve a
// correr juli_tileset.py con otros parametros, el empaquetado cambia y hay que
// rehacerlos. Por eso el builder verifica la cantidad de tiles antes de pintar.
namespace PokeRush.EditorTools
{
    public static class JuliArenaSceneBuilder
    {
        private const string TilesAsset = "Assets/Palettes/JuliTile_Tiles.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
        private const string EscenaCompleta = "Assets/Scenes/ArenaJuli.unity";
        private const string EscenaVacia = "Assets/Scenes/ArenaJuli_Vacia.unity";

        private const int HojaCols = 48;      // ancho de la hoja, en tiles
        private const int TilesEsperados = 338;
        private const int W = 48, H = 36;     // tamaño del mapa

        // --- indices de la hoja -------------------------------------------------
        private const int PisoClaro = 0, PisoGris = 2, PisoOscuro = 9;
        private const int PisoArena = 169, PisoRojo = 172, PisoAzul = 173;
        private const int PokeballArriba = 3, PokeballAbajo = 34;   // 2x1 cada mitad
        private const int Pasto = 279, Pasto2 = 280, Agua = 281;
        private const int Pared = 21, ParedLogo = 43, ParedVentana = 40;   // 43 y 40 son 2x2
        private const int Arco = 498, Arco2 = 680;                          // 2x2
        private const int BanderaRoja = 31, BanderaAzul = 33;               // 1x3
        private const int PantallaGrande = 26;                              // 4x2
        private const int Arbol = 166, Arbol2 = 247, Farol = 167, Farol2 = 168;
        private const int BancoMadera = 248, BancoOscuro = 264;             // 2x1
        private const int Maceta = 188;
        private const int Baranda = 270, BarandaLuz = 338;                  // 2x1
        private const int Poste = 272, PosteLuz = 262;
        private const int Escalera = 378;                                   // 2x2
        private const int Cascada = 365;                                    // 3x3
        private const int Estanque = 480;                                   // 2x2
        private const int SenalArena = 503, SenalEntrenamiento = 520;
        private const int SenalTienda = 686, SenalSalida = 689, SenalSolo = 701;

        private static Dictionary<string, TileBase> _tiles;

        [MenuItem("PokeRush/Arena Juli/Armar mapa completo")]
        public static void ArmarCompleta()
        {
            if (!CargarTiles()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene escena = NuevaEscena(out var capas, out var player);
            Pintar(capas);
            player.transform.position = CentroDeCelda(24, 6);
            ResolverColisiones(capas["Colisiones"]);
            Guardar(escena, EscenaCompleta);

            Debug.Log($"[ArenaJuli] Mapa completo en {EscenaCompleta}. " +
                      "El jugador arranca en la alfombra, al sur de la arena.");
        }

        [MenuItem("PokeRush/Arena Juli/Armar escena vacia para pintar")]
        public static void ArmarVacia()
        {
            if (!CargarTiles()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene escena = NuevaEscena(out var capas, out var player);
            player.transform.position = CentroDeCelda(0, 0);
            Guardar(escena, EscenaVacia);

            Debug.Log($"[ArenaJuli] Escena vacia en {EscenaVacia}. Abri Window > 2D > Tile Palette, " +
                      "elegi JuliTile y pinta eligiendo la capa en Active Tilemap. " +
                      "Cuando termines de pintar Colisiones corre PokeRush > Reparar colisiones del mapa.");
        }

        // --- rig ----------------------------------------------------------------

        private static Scene NuevaEscena(out Dictionary<string, Tilemap> capas, out GameObject player)
        {
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Todo el mundo cuelga de MapaRaiz: es lo que MapBattleBridge apaga
            // durante un combate. Lo que quede afuera se ve encima de la pelea.
            var raiz = new GameObject("MapaRaiz");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(raiz.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(raiz.transform, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            capas = new Dictionary<string, Tilemap>
            {
                ["Suelo"] = Capa(gridGo.transform, "Suelo", -10, false),
                ["Decorado"] = Capa(gridGo.transform, "Decorado", 0, false),
                ["Colisiones"] = Capa(gridGo.transform, "Colisiones", 10, true),
                ["Techos"] = Capa(gridGo.transform, "Techos", 20, false),
            };

            player = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            player.transform.SetParent(raiz.transform, false);

            // target es un SerializeField privado de CameraFollow
            var follow = camGo.AddComponent<CameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return escena;
        }

        private static Tilemap Capa(Transform padre, string nombre, int orden, bool conCollider)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var tm = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = orden;
            if (!conCollider) return tm;

            // Sin esto el jugador atraviesa todo: el obstacleMask del
            // PlayerController mira la capa Obstacles, no basta con el collider.
            go.layer = LayerMask.NameToLayer("Obstacles");
            var tc = go.AddComponent<TilemapCollider2D>();
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            go.AddComponent<CompositeCollider2D>().geometryType =
                CompositeCollider2D.GeometryType.Polygons;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
            go.AddComponent<ColisionesTilemapRuntimeFix>();
            return tm;
        }

        // --- el mapa ------------------------------------------------------------

        private static void Pintar(Dictionary<string, Tilemap> capas)
        {
            Tilemap suelo = capas["Suelo"], deco = capas["Decorado"], col = capas["Colisiones"];

            Rellenar(suelo, 0, 0, W, H, PisoGris);
            Borde(col, 0, 0, W, H, Pared);

            Arena(suelo, deco, col);
            Norte(deco, col);
            Edificios(suelo, deco, col);
            Jardines(suelo, deco, col);

            // alfombra roja: la entrada a la arena desde el sur
            Rellenar(suelo, 23, 4, 3, 8, PisoRojo);
            Poner(deco, 23, 10, Escalera, 2, 2);
            Poner(deco, 20, 2, SenalArena, 3, 1);
            Poner(deco, 26, 2, SenalSalida, 3, 1);
        }

        private const int ArenaCx = 24, ArenaCy = 18, ArenaR = 7;

        private static bool DentroDeArena(int x, int y)
        {
            int dx = Mathf.Abs(x - ArenaCx), dy = Mathf.Abs(y - ArenaCy);
            return dx <= ArenaR && dy <= ArenaR && dx + dy <= ArenaR + 3;
        }

        private static void Arena(Tilemap suelo, Tilemap deco, Tilemap col)
        {
            for (int y = ArenaCy - ArenaR; y <= ArenaCy + ArenaR; y++)
                for (int x = ArenaCx - ArenaR; x <= ArenaCx + ArenaR; x++)
                    if (DentroDeArena(x, y))
                        suelo.SetTile(new Vector3Int(x, y, 0), Tile(PisoArena));

            // la pokebola del centro son dos mitades sueltas de 2x1
            Poner(suelo, 23, 18, PokeballArriba, 2, 1);
            Poner(suelo, 23, 17, PokeballAbajo, 2, 1);

            // Anillo de baranda: las celdas de afuera que tocan la arena. Se deja
            // un hueco al sur para poder entrar.
            for (int y = ArenaCy - ArenaR - 1; y <= ArenaCy + ArenaR + 1; y++)
                for (int x = ArenaCx - ArenaR - 1; x <= ArenaCx + ArenaR + 1; x++)
                {
                    if (DentroDeArena(x, y)) continue;
                    if (!Vecina(x, y)) continue;
                    if (y < ArenaCy && x >= 23 && x <= 25) continue;   // entrada

                    bool esquina = Mathf.Abs(x - ArenaCx) > 3 && Mathf.Abs(y - ArenaCy) > 3;
                    col.SetTile(new Vector3Int(x, y, 0), Tile(esquina ? PosteLuz : Poste));
                }

            // barandas largas en los tramos rectos de arriba y abajo
            for (int x = 20; x <= 28; x += 2)
            {
                Poner(col, x, ArenaCy + ArenaR + 1, Baranda, 2, 1);
                if (x < 22 || x > 25) Poner(col, x, ArenaCy - ArenaR - 1, BarandaLuz, 2, 1);
            }
        }

        private static bool Vecina(int x, int y)
        {
            return DentroDeArena(x + 1, y) || DentroDeArena(x - 1, y)
                || DentroDeArena(x, y + 1) || DentroDeArena(x, y - 1);
        }

        private static void Norte(Tilemap deco, Tilemap col)
        {
            Poner(deco, 22, 32, PantallaGrande, 4, 2);
            Poner(col, 18, 31, BanderaRoja, 1, 3);
            Poner(col, 29, 31, BanderaRoja, 1, 3);
            Poner(col, 20, 31, BanderaAzul, 1, 3);
            Poner(col, 27, 31, BanderaAzul, 1, 3);
            Poner(col, 15, 31, Farol, 1, 1);
            Poner(col, 32, 31, Farol, 1, 1);
        }

        private static void Edificios(Tilemap suelo, Tilemap deco, Tilemap col)
        {
            Edificio(suelo, deco, col, 2, 28, 10, 6, SenalSalida);          // recepcion
            Edificio(suelo, deco, col, 36, 28, 10, 6, SenalTienda);         // tienda
            Edificio(suelo, deco, col, 2, 14, 10, 8, SenalEntrenamiento);   // entrenamiento
            Edificio(suelo, deco, col, 36, 14, 10, 8, SenalSolo);           // sala VIP
        }

        private static void Edificio(Tilemap suelo, Tilemap deco, Tilemap col,
                                     int x, int y, int w, int h, int senal)
        {
            Rellenar(suelo, x, y, w, h, PisoOscuro);
            Borde(col, x, y, w, h, Pared);
            Poner(col, x + 2, y + 1, ParedVentana, 2, 2);
            Poner(col, x + w - 4, y + 1, ParedLogo, 2, 2);

            // el arco va al sur, mirando al centro del mapa
            int arcoX = x + w / 2 - 1;
            Poner(col, arcoX, y, Arco, 2, 2);
            Poner(deco, arcoX - 1, y - 2, senal, 3, 1);
        }

        private static void Jardines(Tilemap suelo, Tilemap deco, Tilemap col)
        {
            Rellenar(suelo, 2, 3, 12, 9, Pasto);
            Rellenar(suelo, 34, 3, 12, 9, Pasto2);

            Poner(col, 3, 8, Cascada, 3, 3);
            Poner(col, 7, 4, Estanque, 2, 2);
            Poner(col, 2, 6, Arbol, 1, 1);
            Poner(col, 5, 6, Arbol2, 1, 1);
            Poner(col, 11, 9, Arbol, 1, 1);
            Poner(col, 12, 4, Farol2, 1, 1);
            Poner(col, 10, 3, BancoMadera, 2, 1);
            Poner(deco, 4, 4, Maceta, 1, 1);

            Poner(col, 36, 9, Arbol, 1, 1);
            Poner(col, 39, 6, Arbol2, 1, 1);
            Poner(col, 43, 8, Arbol, 1, 1);
            Poner(col, 45, 4, Farol2, 1, 1);
            Poner(col, 36, 3, BancoOscuro, 2, 1);
            Poner(deco, 41, 4, Maceta, 1, 1);

            // pasillos de piso duro entre los jardines y la arena. Los tramos
            // azules van por afuera del anillo: pisarian el piso de la arena.
            Rellenar(suelo, 14, 3, 9, 9, PisoClaro);
            Rellenar(suelo, 26, 3, 8, 9, PisoClaro);
            Rellenar(suelo, 12, 12, 5, 2, PisoAzul);
            Rellenar(suelo, 32, 12, 4, 2, PisoAzul);
        }

        // --- helpers de pintado -------------------------------------------------

        private static void Rellenar(Tilemap tm, int x, int y, int w, int h, int idx)
        {
            TileBase t = Tile(idx);
            if (t == null) return;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    tm.SetTile(new Vector3Int(x + i, y + j, 0), t);
        }

        private static void Borde(Tilemap tm, int x, int y, int w, int h, int idx)
        {
            TileBase t = Tile(idx);
            if (t == null) return;
            for (int i = 0; i < w; i++)
            {
                tm.SetTile(new Vector3Int(x + i, y, 0), t);
                tm.SetTile(new Vector3Int(x + i, y + h - 1, 0), t);
            }
            for (int j = 0; j < h; j++)
            {
                tm.SetTile(new Vector3Int(x, y + j, 0), t);
                tm.SetTile(new Vector3Int(x + w - 1, y + j, 0), t);
            }
        }

        // Coloca un elemento de varias celdas. En la hoja la fila 0 es la de
        // arriba; en Unity la Y crece hacia arriba, asi que se da vuelta.
        private static void Poner(Tilemap tm, int x, int y, int idxBase, int w, int h)
        {
            for (int f = 0; f < h; f++)
                for (int c = 0; c < w; c++)
                {
                    TileBase t = Tile(idxBase + f * HojaCols + c);
                    if (t == null) continue;   // celda vacia del elemento
                    tm.SetTile(new Vector3Int(x + c, y + (h - 1 - f), 0), t);
                }
        }

        private static TileBase Tile(int idx) =>
            _tiles.TryGetValue($"JuliTile_00_{idx:0000}", out TileBase t) ? t : null;

        private static Vector3 CentroDeCelda(int x, int y) => new Vector3(x + 0.5f, y + 0.5f, 0f);

        // --- colisiones ---------------------------------------------------------

        // Pintar tiles no alcanza: vienen con colliderType None y el composite
        // queda en shapeCount 0. Va override por celda, y el toggle del enabled
        // es obligatorio para que el collider se entere.
        private static void ResolverColisiones(Tilemap tm)
        {
            foreach (Vector3Int p in tm.cellBounds.allPositionsWithin)
                if (tm.GetTile(p) != null)
                    tm.SetColliderType(p, UnityEngine.Tilemaps.Tile.ColliderType.Grid);

            var tc = tm.GetComponent<TilemapCollider2D>();
            tc.enabled = false;
            tc.enabled = true;
            tm.GetComponent<CompositeCollider2D>().GenerateGeometry();
        }

        // --- carga --------------------------------------------------------------

        private static bool CargarTiles()
        {
            _tiles = AssetDatabase.LoadAllAssetsAtPath(TilesAsset)
                .OfType<TileBase>()
                .ToDictionary(t => t.name, t => t);

            if (_tiles.Count == 0)
            {
                Debug.LogError($"[ArenaJuli] No hay tiles en {TilesAsset}. Corre antes " +
                               "Codigo/Herramientas/juli_tileset.py y PokeRush > Armar paletas faltantes.");
                return false;
            }
            if (_tiles.Count != TilesEsperados)
            {
                Debug.LogWarning($"[ArenaJuli] La paleta tiene {_tiles.Count} tiles y se esperaban " +
                                 $"{TilesEsperados}. Si se regenero la hoja, los indices del builder " +
                                 "quedaron viejos y el mapa va a salir con piezas cambiadas.");
            }
            return true;
        }

        private static void Guardar(Scene escena, string ruta)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(escena, ruta);
            AssetDatabase.Refresh();
        }
    }
}
