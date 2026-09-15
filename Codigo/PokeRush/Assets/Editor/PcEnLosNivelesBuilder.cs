using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Pone el PC de almacenamiento en cada nivel jugable.
//
//   PokeRush > Poner el PC en todos los niveles
//
// Un PC por nivel, en una celda elegida mirando cada mapa: cerca de la entrada o
// de la enfermera, sin tapar pasillos. Idempotente: borra el contenedor
// 'PcDelNivel' de cada escena y lo vuelve a crear.
//
// Antes de guardar cada escena verifica que la celda sea piso, que no haya pared
// ni otro objeto solido, y que se pueda llegar caminando desde el spawn hasta al
// lado del PC. Si algo falla, esa escena no se guarda. Tampoco toca una escena
// que este abierta con cambios sin guardar.
public static class PcEnLosNivelesBuilder
{
    private const string Contenedor = "PcDelNivel";
    private const string SpritePath = "Assets/Sprites/Mundo/pc.png";
    private const int CapaObstacles = 3;

    // Centro de la celda de cada PC (las celdas miden 1 y su centro cae en .5).
    private static readonly (string escena, Vector2 celda)[] Niveles =
    {
        // Torre 1: la sala del arranque, en la esquina de arriba a la derecha.
        ("Assets/Scenes/NestorGOD2.unity", new Vector2(5.5f, 1.5f)),
        // Casino, piso 1: el pasillo de la entrada, a la izquierda del spawn.
        ("Assets/Scenes/Casino Nivel 1.unity", new Vector2(-2.5f, -1.5f)),
        // Piso 2: arriba a la izquierda, al lado de la enfermera y cerca del
        // ascensor, que es por donde se llega.
        ("Assets/Scenes/Casino Nivel 2.unity", new Vector2(-14.5f, 19.5f)),
        // Piso 3: igual que en el piso 2.
        ("Assets/Scenes/Casino nivel 3.unity", new Vector2(-14.5f, 19.5f)),
        // Piso final: al lado del spawn.
        ("Assets/Scenes/Casino piso FINAL.unity", new Vector2(-3.5f, 4.5f))
    };

    [MenuItem("PokeRush/Poner el PC en todos los niveles")]
    public static void PonerEnTodos()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PC] Sali del Play Mode antes de correr esto.");
            return;
        }

        foreach (var nivel in Niveles)
        {
            Scene abierta = SceneManager.GetSceneByPath(nivel.escena);
            if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
            {
                Debug.LogError($"[PC] '{abierta.name}' esta abierta con cambios sin guardar. " +
                               "Guardala o descartala y volve a correr esto: no se toco nada.");
                return;
            }
        }

        Sprite sprite = SpriteDelPc();
        int ok = 0;
        foreach (var nivel in Niveles)
            if (Poner(nivel.escena, nivel.celda, sprite)) ok++;

        if (ok == Niveles.Length) Debug.Log($"[PC] TERMINADO: PC puesto en los {ok} niveles.");
        else Debug.LogError($"[PC] Solo {ok} de {Niveles.Length} niveles quedaron con PC. Mira los errores de arriba.");
    }

    private static bool Poner(string path, Vector2 celda, Sprite sprite)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[PC] No existe {path}, se saltea.");
            return false;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool yaAbierta = scene.IsValid() && scene.isLoaded;
        if (!yaAbierta) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        GameObject[] roots = scene.GetRootGameObjects();
        GameObject mapaRaiz = roots.FirstOrDefault(r => r.name == "MapaRaiz");

        // El PC va dentro de MapaRaiz, que el combate apaga: un PC encima de una
        // pelea no tiene sentido.
        Transform padre = mapaRaiz != null ? mapaRaiz.transform : null;
        GameObject viejo = padre != null
            ? padre.Find(Contenedor)?.gameObject
            : roots.FirstOrDefault(r => r.name == Contenedor);
        if (viejo != null) Object.DestroyImmediate(viejo);

        var contenedor = new GameObject(Contenedor);
        if (padre != null) contenedor.transform.SetParent(padre, false);
        else SceneManager.MoveGameObjectToScene(contenedor, scene);

        var pc = new GameObject("PC");
        pc.transform.SetParent(contenedor.transform, false);
        pc.transform.position = new Vector3(celda.x, celda.y, 0f);
        pc.layer = CapaObstacles;
        pc.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
        pc.AddComponent<PcInteractable>();

        var visual = new GameObject("Visual");
        visual.transform.SetParent(pc.transform, false);
        // Pivote abajo: se baja media celda para que el mueble apoye en el piso.
        visual.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 15;

        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        string problema = Verificar(scene, pc);
        if (problema != null)
        {
            Debug.LogError($"[PC] {scene.name}: {problema} NO se guarda.");
            if (yaAbierta) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            else EditorSceneManager.CloseScene(scene, true);
            return false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PC] {scene.name}: PC en ({celda.x}, {celda.y}). Guardada.");
        if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
        return true;
    }

    // Abrir una escena en el editor pierde en silencio los colliderType = Grid
    // puestos por celda, y guardarla en ese estado los pierde tambien en disco
    // (ver Documentacion/Como_Armar_Un_Mapa_Nuevo.md). Se reponen antes de guardar.
    private static void ReponerColisiones(Scene scene)
    {
        Tilemap colisiones = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .FirstOrDefault(t => t.name == "Colisiones");
        if (colisiones == null) return;

        foreach (Vector3Int p in colisiones.cellBounds.allPositionsWithin)
            if (colisiones.HasTile(p) && colisiones.GetColliderType(p) != Tile.ColliderType.Grid)
                colisiones.SetColliderType(p, Tile.ColliderType.Grid);
    }

    // Devuelve null si el PC esta bien puesto, o que tiene de malo.
    private static string Verificar(Scene scene, GameObject pc)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        var tilemaps = roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        Tilemap suelo = tilemaps.FirstOrDefault(t => t.name == "Suelo");
        PlayerController jugador = roots.SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
        if (colisiones == null || suelo == null || jugador == null)
            return "faltan el tilemap Colisiones, el Suelo o el jugador.";

        var soJugador = new SerializedObject(jugador);
        float radioChoque = soJugador.FindProperty("collisionCheckRadius")?.floatValue ?? 0.3f;
        float radioInteraccion = soJugador.FindProperty("interactionRadius")?.floatValue ?? 0.45f;

        Vector3Int celda = colisiones.WorldToCell(pc.transform.position);
        if (!suelo.HasTile(celda)) return $"la celda ({celda.x},{celda.y}) no es piso.";
        if (colisiones.HasTile(celda)) return $"la celda ({celda.x},{celda.y}) es pared o mueble.";

        Collider2D propio = pc.GetComponent<Collider2D>();
        var solidos = roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c != propio && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
                        c.gameObject.layer == CapaObstacles &&
                        !(c is TilemapCollider2D) && !(c is CompositeCollider2D))
            .ToList();

        Vector3 centroPc = colisiones.GetCellCenterWorld(celda);
        Collider2D encima = solidos.FirstOrDefault(c => Toca(c.bounds, centroPc, radioChoque));
        if (encima != null) return $"la celda ({celda.x},{celda.y}) ya la ocupa '{encima.name}'.";

        var bloqueos = solidos.Select(c => c.bounds).ToList();
        bloqueos.Add(propio.bounds);

        bool Bloqueada(Vector3Int c)
        {
            if (colisiones.HasTile(c)) return true;
            Vector3 centro = colisiones.GetCellCenterWorld(c);
            return bloqueos.Any(b => Toca(b, centro, radioChoque));
        }

        Vector3Int spawn = colisiones.WorldToCell(jugador.transform.position);
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        var alcanzables = new HashSet<Vector3Int> { spawn };
        var cola = new Queue<Vector3Int>();
        cola.Enqueue(spawn);
        while (cola.Count > 0)
        {
            Vector3Int c = cola.Dequeue();
            foreach (Vector3Int d in dirs)
            {
                Vector3Int n = c + d;
                if (alcanzables.Contains(n) || Mathf.Abs(n.x - spawn.x) > 80 || Mathf.Abs(n.y - spawn.y) > 80) continue;
                if (Bloqueada(n)) continue;
                alcanzables.Add(n);
                cola.Enqueue(n);
            }
        }

        // Alguna celda alcanzable de al lado, mirando al PC, lo agarra con X.
        bool llega = dirs.Any(d => alcanzables.Contains(celda - d) &&
                                   Toca(propio.bounds, colisiones.GetCellCenterWorld(celda), radioInteraccion));
        return llega ? null : $"no se llega caminando desde el spawn hasta al lado del PC ({celda.x},{celda.y}).";
    }

    private static bool Toca(Bounds b, Vector3 centro, float radio)
    {
        float x = Mathf.Clamp(centro.x, b.min.x, b.max.x);
        float y = Mathf.Clamp(centro.y, b.min.y, b.max.y);
        float dx = centro.x - x, dy = centro.y - y;
        return dx * dx + dy * dy < radio * radio;
    }

    // pc.png es arte propio de 32x48: a 32 pixeles por unidad mide una celda de
    // ancho y una y media de alto, como los muebles del tileset. Pivote abajo.
    private static Sprite SpriteDelPc()
    {
        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        }
        if (importer == null)
        {
            Debug.LogError($"[PC] No existe {SpritePath}.");
            return null;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool bien = importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 32f) &&
                    settings.spriteAlignment == (int)SpriteAlignment.BottomCenter;
        if (!bien)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 32f;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }
}
