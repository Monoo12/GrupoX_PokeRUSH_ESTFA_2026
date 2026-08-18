using UnityEngine;
using UnityEngine.Tilemaps;

// Garantiza que la capa de colisiones bloquee de verdad, siempre.
//
// POR QUE EXISTE
// Los tiles de Interior1_Tiles.asset vienen con colliderType = None, asi que la
// capa 'Colisiones' se ve pero no choca. El arreglo es un override por celda
// (SetColliderType), pero ese override es FRAGIL: cualquier refresh del Tilemap
// —pintar, borrar, un reimport de assets, un domain reload— hace que el Tilemap
// vuelva a leer el colliderType del asset y se pierda todo EN SILENCIO. El
// sintoma es siempre el mismo: el jugador atraviesa las paredes.
//
// No se puede arreglar en el asset porque 'Suelo' usa varios de los mismos
// tiles y se volveria solido el piso.
//
// Este componente lo repone en Awake, asi el juego arranca bien pase lo que
// pase con lo guardado en la escena. Para el editor esta el menu
// 'PokeRush > Reparar colisiones del mapa'.
[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class ColisionesTilemapRuntimeFix : MonoBehaviour
{
    [Tooltip("Tipo de collider por celda. Grid usa la celda entera, que es lo " +
             "que se quiere para movimiento por grilla. Sprite no sirve: los " +
             "sprites de estos tiles tienen la physics shape vacia.")]
    [SerializeField] private Tile.ColliderType tipo = Tile.ColliderType.Grid;

    [Tooltip("Deja un log con cuantas celdas y cuantas formas quedaron.")]
    [SerializeField] private bool avisarPorConsola = true;

    private void Awake()
    {
        var tm = GetComponent<Tilemap>();
        var tc = GetComponent<TilemapCollider2D>();
        var cc = GetComponent<CompositeCollider2D>();

        int celdas = 0;
        foreach (var p in tm.cellBounds.allPositionsWithin)
        {
            if (tm.GetTile(p) == null) continue;
            tm.SetColliderType(p, tipo);
            celdas++;
        }

        // Reconstruccion: togglear 'enabled' NO alcanza. Hay que sacar y volver
        // a poner el composite para que el TilemapCollider2D regenere de verdad.
        var previo = tc.compositeOperation;
        tc.compositeOperation = Collider2D.CompositeOperation.None;
        tc.enabled = false;
        tc.enabled = true;
        tc.compositeOperation = previo == Collider2D.CompositeOperation.None
            ? Collider2D.CompositeOperation.Merge
            : previo;
        if (cc != null) cc.GenerateGeometry();

        Physics2D.SyncTransforms();

        if (avisarPorConsola)
        {
            int formas = cc != null ? cc.shapeCount : tc.shapeCount;
            Debug.Log($"[Colisiones] '{name}': {celdas} celdas, {formas} formas, " +
                      $"capa '{LayerMask.LayerToName(gameObject.layer)}'.");
            if (formas == 0)
                Debug.LogError($"[Colisiones] '{name}' quedo con 0 formas: el jugador " +
                               $"va a atravesar las paredes. Revisa la capa y los tiles.");
        }
    }
}
