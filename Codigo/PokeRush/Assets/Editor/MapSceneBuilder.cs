using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arma una escena vacia lista para pintar un mapa: Grid con las capas
// separadas y el orden de dibujado ya resuelto.
//
// Las capas y por que estan asi:
//   Suelo      (-10) pasto, caminos, agua. Se pinta primero y va al fondo.
//   Decorado   (0)   arbustos, flores, cosas sueltas encima del suelo.
//   Colisiones (10)  paredes, arboles, bordes. Lleva TilemapCollider2D, asi
//                    que lo que pintes aca bloquea al jugador.
//   Techos     (20)  copas de arbol y aleros, que tapan al jugador al pasar
//                    por debajo.
//
// El jugador va en sortingOrder 5: encima del decorado, debajo de los techos.
namespace PokeRush.EditorTools
{
    public static class MapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MapaNuevo.unity";

        [MenuItem("PokeRush/Armar escena de mapa")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // 7.5 celdas de media altura: se ve una porcion comoda para editar
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;   // 1 celda = 1 unidad = 32 px a PPU 32

            CreateLayer(gridGo.transform, "Suelo", -10, false);
            CreateLayer(gridGo.transform, "Decorado", 0, false);
            CreateLayer(gridGo.transform, "Colisiones", 10, true);
            CreateLayer(gridGo.transform, "Techos", 20, false);

            EditorSceneManager.MarkSceneDirty(scene);
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Mapa] Escena creada en {ScenePath}. Abri Window > 2D > Tile Palette, " +
                      "elegi una paleta y pinta seleccionando la capa en Active Tilemap.");
        }

        private static void CreateLayer(Transform parent, string name, int sortingOrder, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (!collider) return;
            go.AddComponent<TilemapCollider2D>();
            // El composite junta los tiles contiguos en un solo collider: sin
            // esto son miles de cajas sueltas y el jugador se traba en las
            // juntas al caminar pegado a una pared.
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            go.GetComponent<TilemapCollider2D>().compositeOperation =
                Collider2D.CompositeOperation.Merge;
        }
    }
}
