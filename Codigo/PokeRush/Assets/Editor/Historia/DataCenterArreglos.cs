using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arreglos del Data Center de Nestor que salieron del playtest del 4-oct-2026.
//
//   PokeRush > Historia > Arreglar racks y Basculin del Data Center
//
// 1. LOS RACKS: la mitad de arriba de cada rack estaba pintada en Techos, que
//    se dibuja ENCIMA de los personajes y no choca. Resultado: le cortaba la
//    cabeza a quien pasaba por delante y se podia pisar esa mitad. Toda celda
//    de Techos que este sobre piso libre y tenga un mueble de Colisiones justo
//    abajo pasa a Colisiones (se dibuja debajo del jugador y frena). Lo que si
//    va en Techos (el marco de una puerta, por donde se pasa por abajo) no tiene
//    Colisiones abajo y se queda donde esta.
// 2. EL BASCULIN del agua del laboratorio usaba el icono chico del menu (36x30)
//    agrandado casi seis veces y con filtro bilineal: se veia borroso. Pasa a la
//    hoja de seguidor del pack (Followers/BASCULIN), al mismo tamaño de pixel
//    que los personajes, nadando de costado (de frente, desde arriba, no se lee
//    como un pez) y sin la rotacion de 90 grados que tenia el icono.
//
// Se puede volver a correr: si ya esta arreglado no cambia nada.
public static class DataCenterArreglos
{
    private const string HojaDelBasculin = "Followers/BASCULIN";

    [MenuItem("PokeRush/Historia/Arreglar racks y Basculin del Data Center")]
    public static void Arreglar()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("[DataCenter] Hay una escena con cambios sin guardar: guardala o descartala primero.");
                return;
            }

        Scene scene = EditorSceneManager.OpenScene(HistoriaDataCenterBuilder.Escena, OpenSceneMode.Single);
        int racks = BajarLosRacks(scene);
        bool basculin = ArreglarBasculin(scene);

        HistoriaDataCenterBuilder.ReponerColisiones(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DataCenter] {racks} celdas de racks pasadas de Techos a Colisiones; " +
                  $"Basculin {(basculin ? "con la hoja de seguidor" : "no encontrado")}. Guardada.");
    }

    public static int BajarLosRacks(Scene scene)
    {
        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap techos = tilemaps.FirstOrDefault(t => t.name == "Techos");
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        if (techos == null || colisiones == null) return 0;

        int movidas = 0;
        foreach (Vector3Int p in techos.cellBounds.allPositionsWithin)
        {
            if (!techos.HasTile(p) || colisiones.HasTile(p)) continue;
            if (!colisiones.HasTile(p + Vector3Int.down)) continue;   // un marco de puerta: se pasa por abajo

            colisiones.SetTile(p, techos.GetTile(p));
            colisiones.SetTransformMatrix(p, techos.GetTransformMatrix(p));
            colisiones.SetColliderType(p, Tile.ColliderType.Grid);
            techos.SetTile(p, null);
            movidas++;
        }
        return movidas;
    }

    private static bool ArreglarBasculin(Scene scene)
    {
        Transform basculin = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "NPC_Basculin");
        SpriteRenderer sr = basculin != null ? basculin.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (sr == null) return false;

        Sprite[] poses = CasinoNpcBuilder.Poses(HojaDelBasculin);
        Sprite nadando = poses != null && poses.Length > CasinoNpcBuilder.Izquierda ? poses[CasinoNpcBuilder.Izquierda] : null;
        if (nadando == null) { Debug.LogError($"[DataCenter] No se pudo cortar {HojaDelBasculin}."); return false; }

        sr.sprite = nadando;
        // El objeto de la liberacion esta girado 92 grados (asi se acomodo el
        // icono viejo): se endereza solo el dibujo, en mundo, y su collider no
        // se toca. Como un personaje: pivote media celda abajo del centro y el
        // mismo pixel que el jugador.
        sr.transform.rotation = Quaternion.identity;
        sr.transform.position = basculin.position + new Vector3(0f, -0.5f, 0f);
        sr.transform.localScale = Vector3.one * CasinoNpcBuilder.EscalaDelVisual;
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(sr.transform);
        return true;
    }
}
