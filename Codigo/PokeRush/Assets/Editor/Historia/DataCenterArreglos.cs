using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arreglos del Data Center de Nestor que salieron del playtest del 4-oct-2026
// y del rebalanceo del 6-oct-2026.
//
//   PokeRush > Historia > Arreglar racks y liberados del Data Center
//
// 1. LOS RACKS: la mitad de arriba de cada rack estaba pintada en Techos, que
//    se dibuja ENCIMA de los personajes y no choca. Resultado: le cortaba la
//    cabeza a quien pasaba por delante y se podia pisar esa mitad. Toda celda
//    de Techos que este sobre piso libre y tenga un mueble de Colisiones justo
//    abajo pasa a Colisiones (se dibuja debajo del jugador y frena). Lo que si
//    va en Techos (el marco de una puerta, por donde se pasa por abajo) no tiene
//    Colisiones abajo y se queda donde esta.
//    EXCEPTO el tope de los ocho escritorios del salon (TopesDeLosEscritorios):
//    tambien tienen un mueble abajo, pero son una celda de piso con el borde
//    del escritorio en el cuarto de abajo. La primera pasada (5-oct) los bajo
//    con los racks y tapo la fila entre las dos hileras de escritorios (bug 3
//    del testing del 7-oct). Van en Techos: no frenan y el borde le tapa los
//    pies al que pasa por detras.
// 2. LOS LIBERADOS: los dos Pokemon encerrados que se suman al equipo con
//    LiberarPokenTrigger. Desde el rebalanceo del 6-oct-2026 vienen ya
//    evolucionados (el juego no tiene evolucion y es corto): el del agua del
//    laboratorio es Basculegion (era Basculin, el "raya blanca" del GDD) y el
//    de la celda, Blaziken (era Combusken: sigue siendo el Lucha que le pega a
//    Melmetal y a Porygon-Z). Este menu escribe especie, nivel y texto en los
//    prefabs y les pone la hoja de seguidor del pack, al mismo tamaño de pixel
//    que los personajes: Basculegion nadando de costado (de frente, desde
//    arriba, no se lee como un pez) y Blaziken parado de frente en la jaula.
//    Los objetos de la escena se siguen llamando NPC_Basculin y NPC_Combusken.
//
// Se puede volver a correr: si ya esta arreglado no cambia nada.
public static class DataCenterArreglos
{
    private const string Prefabs = "Assets/Prefabs/NPCs/";
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";

    private struct Liberado
    {
        public string objeto;     // prefab y nombre del objeto en la escena
        public string especie;
        public int nivel;
        public string nombre;
        public string hoja;       // hoja de seguidor del pack
        public int pose;          // CasinoNpcBuilder.Abajo/Izquierda...
    }

    // Basculegion temprano (la sala del tanque) y Blaziken un poco mas alto,
    // justo antes del nucleo: los dos por encima de Nestor (Nv 36).
    private static readonly Liberado[] Liberados =
    {
        new Liberado { objeto = "NPC_Basculin", especie = "0902_Basculegion", nivel = 42, nombre = "Basculegion",
                       hoja = "Followers/BASCULEGION", pose = CasinoNpcBuilder.Izquierda },
        new Liberado { objeto = "NPC_Combusken", especie = "0257_Blaziken", nivel = 44, nombre = "Blaziken",
                       hoja = "Followers/BLAZIKEN", pose = CasinoNpcBuilder.Abajo },
    };

    [MenuItem("PokeRush/Historia/Arreglar racks y liberados del Data Center")]
    public static void Arreglar()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("[DataCenter] Hay una escena con cambios sin guardar: guardala o descartala primero.");
                return;
            }

        foreach (Liberado l in Liberados) ActualizarElPrefab(l);

        Scene scene = EditorSceneManager.OpenScene(HistoriaDataCenterBuilder.Escena, OpenSceneMode.Single);
        int racks = BajarLosRacks(scene);
        DevolverLosTopesDeLosEscritorios(scene);
        string dibujos = string.Join(", ", Liberados.Select(l => $"{l.nombre} {(Dibujar(scene, l) ? "ok" : "NO ENCONTRADO")}"));

        HistoriaDataCenterBuilder.ReponerColisiones(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DataCenter] {racks} celdas de racks pasadas de Techos a Colisiones; liberados: {dibujos}. Guardada.");
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
            if (EsTopeDeEscritorio(p)) continue;

            colisiones.SetTile(p, techos.GetTile(p));
            colisiones.SetTransformMatrix(p, techos.GetTransformMatrix(p));
            colisiones.SetColliderType(p, Tile.ColliderType.Grid);
            techos.SetTile(p, null);
            movidas++;
        }
        return movidas;
    }

    // Las ocho terminales del salon, en dos hileras: el tope de las de arriba
    // (fila 16) y el de las de abajo (fila 13, el pasillo entre las hileras).
    private static readonly Vector2Int[] TopesDeLosEscritorios =
    {
        new Vector2Int(-8, 16), new Vector2Int(-4, 16), new Vector2Int(2, 16), new Vector2Int(6, 16),
        new Vector2Int(-8, 13), new Vector2Int(-4, 13), new Vector2Int(2, 13), new Vector2Int(6, 13),
    };

    private static bool EsTopeDeEscritorio(Vector3Int p)
        => System.Array.IndexOf(TopesDeLosEscritorios, new Vector2Int(p.x, p.y)) >= 0;

    // Deshace lo que BajarLosRacks hizo de mas el 5-oct. Devuelve cuantos movio.
    public static int DevolverLosTopesDeLosEscritorios(Scene scene)
    {
        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap techos = tilemaps.FirstOrDefault(t => t.name == "Techos");
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        if (techos == null || colisiones == null) return 0;

        int movidos = 0;
        foreach (Vector2Int c in TopesDeLosEscritorios)
        {
            var p = new Vector3Int(c.x, c.y, 0);
            if (!colisiones.HasTile(p) || techos.HasTile(p)) continue;

            techos.SetTile(p, colisiones.GetTile(p));
            techos.SetTransformMatrix(p, colisiones.GetTransformMatrix(p));
            colisiones.SetTile(p, null);
            movidos++;
        }
        if (movidos > 0) Debug.Log($"[DataCenter] {movidos} topes de escritorio devueltos de Colisiones a Techos.");
        return movidos;
    }

    // Especie, nivel y lo que dice la caja. Lo demas del prefab (radio, flags,
    // mensajes de equipo lleno) no se toca.
    private static void ActualizarElPrefab(Liberado l)
    {
        string ruta = $"{Prefabs}{l.objeto}.prefab";
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            var liberar = raiz.GetComponentInChildren<LiberarPokenTrigger>(true);
            if (liberar == null) { Debug.LogError($"[DataCenter] {ruta} no tiene LiberarPokenTrigger."); return; }

            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{l.especie}.asset");
            if (especie == null) { Debug.LogError($"[DataCenter] No existe la especie {l.especie}."); return; }

            var so = new SerializedObject(liberar);
            so.FindProperty("poken").objectReferenceValue = especie;
            so.FindProperty("nivel").intValue = l.nivel;
            so.FindProperty("nombreQueHabla").stringValue = l.nombre;
            so.FindProperty("pregunta").stringValue = $"{l.nombre} esta encerrado y te mira desde la jaula.\nLo liberas?";
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static bool Dibujar(Scene scene, Liberado l)
    {
        Transform objeto = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == l.objeto);
        SpriteRenderer sr = objeto != null ? objeto.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (sr == null) return false;

        Sprite[] poses = CasinoNpcBuilder.Poses(l.hoja);
        Sprite dibujo = poses != null && poses.Length > l.pose ? poses[l.pose] : null;
        if (dibujo == null) { Debug.LogError($"[DataCenter] No se pudo cortar {l.hoja}."); return false; }

        sr.sprite = dibujo;
        // El objeto del tanque esta girado 92 grados (asi se acomodo el icono
        // viejo): se endereza solo el dibujo, en mundo, y su collider no se
        // toca. Como un personaje: pivote media celda abajo del centro y el
        // mismo pixel que el jugador.
        sr.transform.rotation = Quaternion.identity;
        sr.transform.position = objeto.position + new Vector3(0f, -0.5f, 0f);
        sr.transform.localScale = Vector3.one * CasinoNpcBuilder.EscalaDelVisual;
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(sr.transform);
        return true;
    }
}
