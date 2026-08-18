using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

// PokeRush > Reparar colisiones del mapa
//
// POR QUE EXISTE
// Los tiles de Interior1_Tiles.asset vienen con colliderType = None, asi que la
// capa 'Colisiones' se ve pero no bloquea. El arreglo es un override por celda
// (SetColliderType), que se guarda en la escena y no toca el asset compartido —
// no se puede tocar el asset porque 'Suelo' usa varios de los mismos tiles y se
// volveria solido el piso.
//
// El problema es que ese override es fragil: CUALQUIER edicion de tiles
// (pintar, borrar, RefreshAllTiles) hace que el Tilemap vuelva a leer el
// colliderType del asset y se pierda todo en silencio. El sintoma es siempre el
// mismo: el jugador atraviesa las paredes y el CompositeCollider2D tiene
// shapeCount = 0.
//
// Correr esto despues de tocar la capa 'Colisiones' y listo.
namespace PokeRush.EditorTools
{
    public static class ColisionesTilemapFixer
    {
        [MenuItem("PokeRush/Reparar colisiones del mapa")]
        public static void Reparar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int arregladas = 0;

            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var tc = tm.GetComponent<TilemapCollider2D>();
                if (tc == null) continue; // solo las capas que bloquean

                int celdas = ReparlarTilemap(tm, tc);
                arregladas++;
                Debug.Log($"[Colisiones] '{tm.name}': {celdas} celdas con collider, " +
                          $"capa '{LayerMask.LayerToName(tm.gameObject.layer)}', " +
                          $"shapes = {ShapeCount(tm)}");
            }

            if (arregladas == 0)
            {
                Debug.LogWarning("[Colisiones] No hay ningun Tilemap con TilemapCollider2D en la escena.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Colisiones] Listo. Acordate de guardar la escena.");
        }

        private static int ReparlarTilemap(Tilemap tm, TilemapCollider2D tc)
        {
            Undo.RecordObject(tm, "reparar colisiones");

            // 1) el override por celda, que es lo que se pierde
            int celdas = 0;
            foreach (var p in tm.cellBounds.allPositionsWithin)
            {
                if (tm.GetTile(p) == null) continue;
                tm.SetColliderType(p, Tile.ColliderType.Grid);
                celdas++;
            }

            // 2) la capa: el obstacleMask del PlayerController mira 'Obstacles'
            int obstacles = LayerMask.NameToLayer("Obstacles");
            if (obstacles >= 0 && tm.gameObject.layer != obstacles)
            {
                Undo.RecordObject(tm.gameObject, "capa Obstacles");
                tm.gameObject.layer = obstacles;
            }

            // 3) forzar la reconstruccion. El toggle de 'enabled' NO alcanza:
            //    hay que sacar y volver a poner el composite para que el
            //    TilemapCollider2D regenere su geometria de verdad.
            var cc = tm.GetComponent<CompositeCollider2D>();
            if (cc != null)
            {
                var previo = tc.compositeOperation;
                tc.compositeOperation = Collider2D.CompositeOperation.None;
                tc.enabled = false; tc.enabled = true;
                tc.compositeOperation = previo == Collider2D.CompositeOperation.None
                    ? Collider2D.CompositeOperation.Merge
                    : previo;
                cc.GenerateGeometry();
            }
            else
            {
                tc.enabled = false; tc.enabled = true;
            }

            Physics2D.SyncTransforms();
            return celdas;
        }

        private static int ShapeCount(Tilemap tm)
        {
            var cc = tm.GetComponent<CompositeCollider2D>();
            if (cc != null) return cc.shapeCount;
            var tc = tm.GetComponent<TilemapCollider2D>();
            return tc == null ? 0 : tc.shapeCount;
        }
    }
}
