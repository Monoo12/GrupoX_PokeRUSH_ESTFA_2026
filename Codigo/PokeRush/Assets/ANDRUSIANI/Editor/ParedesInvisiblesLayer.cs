using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// ANDRUSIANI > Preparar capa de paredes invisibles
//
// Agrega al Grid de la escena abierta una capa 'ParedesInvisibles' lista para
// pintar: collider + composite + rigidbody estatico + capa 'Obstacles'.
//
// POR QUE UN MENU Y NO "armalo a mano"
// Son cinco cosas y si falta una el jugador atraviesa la pared sin ningun
// error en consola. La que mas se olvida es la capa del GameObject: el
// PlayerController resuelve el paso con un OverlapCircle contra 'Obstacles',
// no con el collider, asi que una capa en 'Default' se ve perfecta y no frena
// a nadie. Ver Como_Armar_Un_Mapa_Nuevo.md, punto 2.
//
// Es idempotente: si la capa ya existe la reconfigura, no la duplica.
namespace PokeRush.EditorTools
{
    public static class ParedesInvisiblesLayer
    {
        private const string NombreCapa = "ParedesInvisibles";

        // Entre 'Colisiones' (10) y 'Techos' (20). En el juego no se ve, pero en
        // el editor conviene que el marcador quede arriba de los muebles.
        private const int Sorting = 11;

        [MenuItem("ANDRUSIANI/Preparar capa de paredes invisibles")]
        public static void Preparar()
        {
            var grid = BuscarGrid();
            if (grid == null)
            {
                Debug.LogError("[ANDRUSIANI] No hay ningun Grid en la escena abierta. " +
                               "Arma primero el mapa con 'PokeRush > Armar escena de mapa'.");
                return;
            }

            var existente = grid.transform.Find(NombreCapa);
            GameObject go;
            if (existente != null)
            {
                go = existente.gameObject;
            }
            else
            {
                go = new GameObject(NombreCapa);
                Undo.RegisterCreatedObjectUndo(go, "capa de paredes invisibles");
                go.transform.SetParent(grid.transform, false);
            }

            Undo.RecordObject(go, "configurar paredes invisibles");

            Componente<Tilemap>(go);
            Componente<TilemapRenderer>(go).sortingOrder = Sorting;

            var tc = Componente<TilemapCollider2D>(go);
            var rb = Componente<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Static;

            // El composite junta los tiles contiguos en un solo collider: sin el
            // son cajas sueltas y el jugador se traba en las juntas al caminar
            // pegado a la pared.
            var cc = Componente<CompositeCollider2D>(go);
            cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;

            // Seguro contra el bug clasico: el TilemapCollider2D a veces no se
            // entera de los tiles nuevos y el composite queda en shapeCount = 0.
            // Este componente rehace la geometria en Awake, asi el juego arranca
            // bien aunque la escena se haya guardado con las colisiones perdidas.
            Componente<ColisionesTilemapRuntimeFix>(go);

            int capa = LayerMask.NameToLayer("Obstacles");
            if (capa < 0)
            {
                Debug.LogError("[ANDRUSIANI] No existe la capa 'Obstacles' en este proyecto. " +
                               "Sin ella el jugador atraviesa todo.");
            }
            else
            {
                go.layer = capa;
            }

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;

            Debug.Log("[ANDRUSIANI] Capa '" + NombreCapa + "' lista bajo '" + grid.name + "'. " +
                      "Abri Window > 2D > Tile Palette, elegi la paleta 'ANDRUSIANI', " +
                      "ponela como Active Tilemap y pinta. Acordate de guardar la escena.", go);
        }

        private static T Componente<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        // Prioriza lo que este seleccionado: en escenas con mas de un Grid
        // (mapa + paleta abierta) elegir el equivocado es un dolor de cabeza.
        private static Grid BuscarGrid()
        {
            if (Selection.activeGameObject != null)
            {
                var deSeleccion = Selection.activeGameObject.GetComponentInParent<Grid>();
                if (deSeleccion != null) return deSeleccion;
            }

            return Object.FindFirstObjectByType<Grid>(FindObjectsInactive.Include);
        }
    }
}
