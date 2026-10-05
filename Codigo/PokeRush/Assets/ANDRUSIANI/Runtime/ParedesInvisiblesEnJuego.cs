using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Las paredes invisibles tienen que serlo tambien en la build.
//
// EL PROBLEMA
// ParedInvisibleTile decide su color en GetTileData: rosa en el editor,
// transparente en Play. Pero el Tilemap guarda en la escena el sprite y el color
// con los que se pinto cada celda, y en una build NO le vuelve a preguntar al
// tile al cargar: dibuja lo guardado, o sea el aspa rosa. En el editor no se
// notaba porque entrar a Play refresca los tiles.
//
// EL ARREGLO
// Al cargar cada escena (en el juego, no en el editor) se refrescan las celdas
// que tienen una pared invisible. Refrescar es volver a llamar a GetTileData,
// que con el juego andando devuelve el color transparente. El collider no
// cambia: lo declara el tile (Grid).
public static class ParedesInvisiblesEnJuego
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => SceneManager.sceneLoaded -= AlCargar;

    // AfterSceneLoad: la primera escena no manda sceneLoaded cuando se entra a
    // Play sin recargar el dominio (opciones de Enter Play Mode del proyecto).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        Refrescar(SceneManager.GetActiveScene());
    }

    private static void AlCargar(Scene escena, LoadSceneMode modo) => Refrescar(escena);

    // Devuelve cuantas celdas refresco. Publico para los tests.
    public static int Refrescar(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return 0;

        int refrescadas = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (Tilemap tilemap in raiz.GetComponentsInChildren<Tilemap>(true))
        {
            if (!TienePared(tilemap)) continue;
            foreach (Vector3Int celda in tilemap.cellBounds.allPositionsWithin)
            {
                if (!(tilemap.GetTile(celda) is ParedInvisibleTile)) continue;
                tilemap.RefreshTile(celda);
                refrescadas++;
            }
        }
        return refrescadas;
    }

    private static bool TienePared(Tilemap tilemap)
    {
        int usados = tilemap.GetUsedTilesCount();
        if (usados == 0) return false;
        var tiles = new TileBase[usados];
        tilemap.GetUsedTilesNonAlloc(tiles);
        foreach (TileBase t in tiles)
            if (t is ParedInvisibleTile) return true;
        return false;
    }
}
