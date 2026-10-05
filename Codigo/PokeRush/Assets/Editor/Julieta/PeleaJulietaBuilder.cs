using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// La pelea contra Julieta: la escena de prueba y lo que hace falta para mudarla
// a la arena definitiva.
//
//   PokeRush > Jefe final (Julieta) > Armar la escena de prueba
//       Crea BossJulieta_Prueba: una sala chica con el piso de la arena
//       (JuliTile), Julieta al fondo, la zona que arranca la pelea y todo el
//       rig (jugador, camara, combate, dialogo, GameManager con un equipo de
//       fin de juego). Se regenera entera: no se trabaja sobre ella.
//   PokeRush > Jefe final (Julieta) > Mudar la pelea a la arena (BossFinal)
//       La arena de Santiago (ArenaJuli_Vacia, renombrada BossFinal): le pone
//       la pelea en la sala redonda, la historia de la llegada (las zonas y el
//       apagon), el rig, y la suma a Build Settings. Se puede volver a correr.
//   PokeRush > Jefe final (Julieta) > Preparar la escena abierta (después de pegar)
//       En la arena definitiva, con el objeto 'PeleaJulieta' ya pegado adentro
//       de MapaRaiz: agrega lo que falte (jugador, camara, combate, dialogo,
//       GameManager, puente mapa/combate) y verifica que se llegue a la zona.
//   PokeRush > Jefe final (Julieta) > Armar la escena de créditos
//       Crea CreditosFinales y la suma a Build Settings (el final la carga).
//
// Lo que se copia a la arena es UN objeto: 'PeleaJulieta'. Adentro estan
// Julieta (el NPC), la Zona y el Servidor; todo se busca solo al arrancar.
//
// Los assets (Julieta, el guion) los crea PeleaJulietaAssets y no se pisan.
public static class PeleaJulietaBuilder
{
    public const string EscenaDePrueba = "Assets/Scenes/BossJulieta_Prueba.unity";
    public const string EscenaDeCreditos = "Assets/Scenes/CreditosFinales.unity";
    public const string Raiz = "PeleaJulieta";

    // La arena definitiva: la de Santiago, con el nombre que espera el
    // recorrido (RecorridoDeLaHistoria) y el catalogo de audio.
    public const string EscenaDeLaArena = "Assets/Scenes/BossFinal.unity";
    // Como la subio Santiago (3-oct-2026, "primera version de la arena").
    public const string EscenaDeSantiago = "Assets/Scenes/ArenaJuli_Vacia.unity";
    private const string HistoriaDeLaSede = "HistoriaDeLaSede";
    private const string DialogosDeLaSede = PeleaJulietaAssets.Carpeta + "/Dialogos/";

    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string CombatePrefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string DialogoPrefab = "Assets/Prefabs/UI/DialogoCanvas.prefab";
    private const string TilesAsset = "Assets/Palettes/JuliTile_Tiles.asset";
    private const string Fuente = "Assets/TextMesh Pro/Fonts/power clear.ttf";
    private const string TextoDeCreditos = "Assets/Resources/Creditos/CreditosFinales.txt";
    private const string FuenteDeTitulos = "Assets/Fonts/power clear bold.ttf";
    private const string LogoDelGrupo = "Assets/Sprites/Logos/LevelUpCreators.png";

    // El reparto de los creditos: nombre y de donde sale su retrato.
    private static readonly (string nombre, string retrato)[] Reparto =
    {
        ("Tixi", "Assets/Graphics/Trainers/TIXI.png"),
        ("Néstor", "Assets/Sprites/Nestor/NESTOR_frente.png"),
        ("Tacconi", "Assets/Graphics/Trainers/TACCONI.png"),
        ("Martín Reymar", "Assets/Graphics/Trainers/MARTIN.png"),
        ("Julieta", "Assets/Graphics/Trainers/JULIETA.png"),
    };
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";

    private const int CapaObstacles = 3;
    private const int CapaDefault = 0;

    // El equipo de la partida DEMO (entrar directo por Play): uno de fin de
    // juego, con lo que el jugador puede tener a esta altura. En el juego de
    // verdad manda el equipo que viene de la partida.
    private static readonly (string especie, int nivel)[] EquipoDePrueba =
    {
        ("0448_Lucario", 36),
        ("0257_Blaziken", 35),
        ("0445_Garchomp", 36),
        ("0282_Gardevoir", 35),
        ("0094_Gengar", 35),
        ("0550_Basculin", 34),
    };

    // ------------------------------------------------------------ la sala de prueba

    // En celdas. Piso de x -6..6 e y -6..6, pared alrededor.
    private const int MinX = -6, MaxX = 6, MinY = -6, MaxY = 6;
    private static readonly Vector2 PosicionJulieta = new Vector2(0.5f, 4.5f);   // celda (0, 4)
    private static readonly Vector2 CentroDeLaZona = new Vector2(0.5f, 3f);      // celdas y 2..3
    private static readonly Vector2 TamanioDeLaZona = new Vector2(5f, 2f);       // x -2..2
    private static readonly Vector2 Spawn = new Vector2(0.5f, -3.5f);            // celda (0, -4)
    private static readonly Vector2 PosicionServidor = new Vector2(0.5f, 5.5f);   // el borde de abajo

    // ------------------------------------------------------------ la arena de Santiago

    // Afuera (y 0..1, el jugador aparece en (0,0)), la puerta en (1,4), el
    // edificio (y 5..10), el pasillo (x 0..1, y 11..16) y la sala redonda
    // (y 17..24) con la pokeball en el centro. Julieta va arriba de todo, con
    // el servidor atras: es un callejon, asi que puede tapar las dos columnas.
    private static readonly Vector2 ArenaJulieta = new Vector2(1f, 22.5f);
    private static readonly Vector2 ArenaServidor = new Vector2(1f, 23.5f);
    private static readonly Vector2 ArenaCentroDeLaZona = new Vector2(1f, 19.5f);   // fila 19, x -2..3
    private static readonly Vector2 ArenaTamanioDeLaZona = new Vector2(6f, 1f);

    // La historia de la llegada (historia.pdf, "Sede central de la Liga"):
    // una zona para pisar cada momento. Centro, tamaño, dialogo y efecto.
    private static readonly (string nombre, Vector2 centro, Vector2 tamanio, string dialogo,
                             string sonido, int pulsos)[] ZonasDeLaSede =
    {
        // Afuera, donde aparece: el jugador ya esta parado adentro al llegar.
        ("Llegada", new Vector2(0.5f, 1f), new Vector2(20f, 2f), "Final_Sede_Llegada", null, 0),
        // Recien entrado: la alarma.
        ("Intruso", new Vector2(1f, 5.5f), new Vector2(4f, 1f), "Final_Sede_Intruso", "alarma.ogg", 5),
        // La boca del pasillo que lleva a la sala del servidor.
        ("Puerta", new Vector2(1f, 11.5f), new Vector2(2f, 1f), "Final_Sede_Puerta", "puerta_se_abre.ogg", 0),
    };

    // Indices de la hoja JuliTile (ver JuliArenaSceneBuilder).
    private const int HojaCols = 48;
    private const int PisoArena = 169, PisoGris = 2, Pared = 21;
    private const int PokeballArriba = 3, PokeballAbajo = 34, PantallaGrande = 26;
    private const int BanderaRoja = 31, BanderaAzul = 33, PosteLuz = 262;

    // ------------------------------------------------------------ menus

    [MenuItem("PokeRush/Jefe final (Julieta)/Armar la escena de prueba")]
    public static void ArmarEscenaDePrueba()
    {
        if (!PuedeTocarEscenas()) return;

        // Los assets se leen DESPUES de abrir la escena nueva (ver
        // builder-assets-de-otra-escena): abrir en Single descarga lo que no usa
        // ninguna escena abierta.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject mapaRaiz = AsegurarRaiz(scene, "MapaRaiz");
        if (!ArmarSala(mapaRaiz)) return;

        PeleaJulieta pelea = ArmarPelea(mapaRaiz, PosicionJulieta, CentroDeLaZona, TamanioDeLaZona, PosicionServidor);
        if (pelea == null) return;

        if (!ArmarRig(scene, mapaRaiz, pelea, Spawn, conEquipoDePrueba: true, conBanco: true)) return;
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, Spawn);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Julieta] {p}");
            Debug.LogError("[Julieta] La escena de prueba NO se guardó.");
            return;
        }

        EditorSceneManager.SaveScene(scene, EscenaDePrueba);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Julieta] TERMINADO. Escena de prueba en {EscenaDePrueba}. Play, caminá hacia Julieta " +
                  "(o F9 para arrancar la pelea directo). Lo que se copia a la arena es MapaRaiz/PeleaJulieta.");
    }

    [MenuItem("PokeRush/Jefe final (Julieta)/Preparar la escena abierta (después de pegar)")]
    public static void PrepararEscenaAbierta()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Julieta] Salí del Play Mode antes de correr esto."); return; }

        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[Julieta] Guardá la escena primero."); return; }

        PeleaJulieta pelea = Buscar<PeleaJulieta>(scene);
        if (pelea == null)
        {
            Debug.LogError($"[Julieta] En esta escena no está la pelea. Copiá el objeto MapaRaiz/{Raiz} de " +
                           $"{EscenaDePrueba} y pegalo acá (adentro de MapaRaiz) antes de correr esto.");
            return;
        }

        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null)
        {
            Debug.LogError("[Julieta] La escena no tiene MapaRaiz (el objeto que se esconde durante la pelea). " +
                           "Ver Como_Armar_Un_Mapa_Nuevo.md.");
            return;
        }
        if (!pelea.transform.IsChildOf(mapaRaiz.transform)) pelea.transform.SetParent(mapaRaiz.transform, true);

        // Si ya hay jugador se respeta donde esta; si no, aparece abajo de la zona.
        Vector2 spawn = Buscar<PlayerController>(scene) is PlayerController pc
            ? (Vector2)pc.transform.position
            : (Vector2)pelea.transform.position + new Vector2(0f, -4f);
        if (!ArmarRig(scene, mapaRaiz, pelea, spawn, conEquipoDePrueba: false, conBanco: false)) return;
        ConfigurarPelea(pelea);
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, spawn);
        EditorSceneManager.MarkSceneDirty(scene);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Julieta] {p}");
            Debug.LogError("[Julieta] La escena quedó armada pero NO se guardó: acomodá lo de arriba y volvé a correr esto.");
            return;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Julieta] TERMINADO. '{scene.name}' tiene la pelea contra Julieta lista para jugar.");
    }

    [MenuItem("PokeRush/Jefe final (Julieta)/Mudar la pelea a la arena (BossFinal)")]
    public static void MudarALaArena()
    {
        if (!PuedeTocarEscenas()) return;

        // La escena de Santiago pasa a llamarse BossFinal. MoveAsset conserva
        // la GUID: nada de lo que la referencia se rompe.
        if (!System.IO.File.Exists(EscenaDeLaArena))
        {
            if (!System.IO.File.Exists(EscenaDeSantiago))
            {
                Debug.LogError($"[Julieta] No está la arena: ni {EscenaDeLaArena} ni {EscenaDeSantiago}.");
                return;
            }
            string error = AssetDatabase.MoveAsset(EscenaDeSantiago, EscenaDeLaArena);
            if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[Julieta] No se pudo renombrar la arena: {error}"); return; }
            Debug.Log($"[Julieta] {EscenaDeSantiago} -> {EscenaDeLaArena}.");
        }

        // Los assets se leen DESPUES de abrir la escena (ver builder-assets-de-otra-escena).
        Scene scene = EditorSceneManager.OpenScene(EscenaDeLaArena, OpenSceneMode.Single);
        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null)
        {
            Debug.LogError("[Julieta] La arena no tiene MapaRaiz (el objeto que se esconde durante la pelea).");
            return;
        }

        PeleaJulieta pelea = Buscar<PeleaJulieta>(scene);
        if (pelea == null)
            pelea = ArmarPelea(mapaRaiz, ArenaJulieta, ArenaCentroDeLaZona, ArenaTamanioDeLaZona, ArenaServidor);
        else
        {
            // El servidor se rehace siempre: es del builder (collider, brillo).
            ServidorPrincipal viejo = pelea.GetComponentInChildren<ServidorPrincipal>(true);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            ArmarServidor(pelea.gameObject, ArenaServidor);
        }
        if (pelea == null) return;

        ArmarHistoriaDeLaSede(mapaRaiz);
        AbrirPuertas(scene);

        Vector2 spawn = Buscar<PlayerController>(scene) is PlayerController pc
            ? (Vector2)pc.transform.position
            : new Vector2(0.5f, 0.5f);
        if (!ArmarRig(scene, mapaRaiz, pelea, spawn, conEquipoDePrueba: true, conBanco: false)) return;
        ConfigurarPelea(pelea);
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, spawn);
        EditorSceneManager.MarkSceneDirty(scene);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Julieta] {p}");
            Debug.LogError("[Julieta] La arena quedó armada pero NO se guardó.");
            return;
        }

        EditorSceneManager.SaveScene(scene);
        SumarABuildSettings(EscenaDeLaArena);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Julieta] TERMINADO. La pelea contra Julieta está en {EscenaDeLaArena} (y en Build Settings).");
    }

    // La llegada a la sede de la Liga (historia.pdf): afuera, la alarma al
    // entrar y la puerta del servidor, cada una en una zona para pisar; y el
    // apagon de la sala cuando el PJ destruye el servidor. Se rehace entera.
    private static void ArmarHistoriaDeLaSede(GameObject mapaRaiz)
    {
        Transform vieja = mapaRaiz.transform.Find(HistoriaDeLaSede);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var raiz = new GameObject(HistoriaDeLaSede);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        var apagon = raiz.AddComponent<ApagonDelNivel>();
        var soApagon = new SerializedObject(apagon);
        soApagon.FindProperty("flagQueApaga").enumValueIndex = (int)StoryFlag.Historia_ServidorPrincipalDestruido;
        soApagon.ApplyModifiedPropertiesWithoutUndo();

        foreach (var z in ZonasDeLaSede)
        {
            string ruta = DialogosDeLaSede + z.dialogo + ".asset";
            var dialogo = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(ruta);
            if (dialogo == null) Debug.LogWarning($"[Julieta] Falta el dialogo {ruta}: la zona '{z.nombre}' no va a decir nada.");

            var go = new GameObject("Zona_" + z.nombre);
            go.transform.SetParent(raiz.transform, false);
            go.transform.position = new Vector3(z.centro.x, z.centro.y, 0f);
            go.layer = CapaDefault;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = z.tamanio;

            var so = new SerializedObject(go.AddComponent<ZonaDeHistoria>());
            so.FindProperty("dialogo").objectReferenceValue = dialogo;
            if (z.sonido != null)
                so.FindProperty("sonido").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(PeleaJulietaAssets.CarpetaDeSonidos + z.sonido);
            so.FindProperty("pulsos").intValue = z.pulsos;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // En la primera version de la arena la puerta del edificio estaba en
    // Colisiones Y en Techos. Techos quiere decir "se pasa por abajo" (se
    // dibuja encima del jugador), asi que la copia de Colisiones sobraba y no
    // dejaba entrar. Toda celda que este en las dos capas pierde la de
    // Colisiones; el dibujo queda en Techos.
    private static void AbrirPuertas(Scene scene)
    {
        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        Tilemap techos = tilemaps.FirstOrDefault(t => t.name == "Techos");
        if (colisiones == null || techos == null) return;

        foreach (Vector3Int p in techos.cellBounds.allPositionsWithin)
        {
            if (!techos.HasTile(p) || !colisiones.HasTile(p)) continue;
            colisiones.SetTile(p, null);
            Debug.Log($"[Julieta] La celda {p} (puerta: está en Techos) ya no es pared.");
        }
    }

    [MenuItem("PokeRush/Jefe final (Julieta)/Armar la escena de créditos")]
    public static void ArmarCreditos()
    {
        if (!PuedeTocarEscenas()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        SceneManager.MoveGameObjectToScene(camGo, scene);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        var go = new GameObject("Creditos");
        SceneManager.MoveGameObjectToScene(go, scene);
        var creditos = go.AddComponent<CreditosFinales>();
        var so = new SerializedObject(creditos);
        so.FindProperty("texto").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(TextoDeCreditos);
        so.FindProperty("fuente").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(Fuente);
        so.FindProperty("fuenteDeTitulos").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FuenteDeTitulos);
        so.FindProperty("logoDelGrupo").objectReferenceValue = PrimerSprite(LogoDelGrupo);

        SerializedProperty reparto = so.FindProperty("reparto");
        reparto.ClearArray();
        foreach ((string nombre, string retrato) in Reparto)
        {
            Sprite s = PrimerSprite(retrato);
            if (s == null) Debug.LogWarning($"[Julieta] Sin retrato para {nombre} en {retrato}.");
            reparto.InsertArrayElementAtIndex(reparto.arraySize);
            SerializedProperty p = reparto.GetArrayElementAtIndex(reparto.arraySize - 1);
            p.FindPropertyRelative("nombre").stringValue = nombre;
            p.FindPropertyRelative("papel").stringValue = "";
            p.FindPropertyRelative("retrato").objectReferenceValue = s;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, EscenaDeCreditos);
        SumarABuildSettings(EscenaDeCreditos);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Julieta] Créditos en {EscenaDeCreditos} (y en Build Settings). El texto se edita en {TextoDeCreditos}.");
    }

    // El sprite de una imagen: el unico si es Single, el primero si es Multiple.
    private static Sprite PrimerSprite(string ruta)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (s != null) return s;
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
    }

    // ------------------------------------------------------------ la sala

    private static bool ArmarSala(GameObject mapaRaiz)
    {
        Dictionary<string, TileBase> tiles = AssetDatabase.LoadAllAssetsAtPath(TilesAsset)
            .OfType<TileBase>().ToDictionary(t => t.name, t => t);
        if (tiles.Count == 0)
        {
            Debug.LogError($"[Julieta] No hay tiles en {TilesAsset} (la paleta de la arena de Julieta).");
            return false;
        }
        TileBase Tile(int i) => tiles.TryGetValue($"JuliTile_00_{i:0000}", out TileBase t) ? t : null;

        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(mapaRaiz.transform, false);
        gridGo.AddComponent<Grid>().cellSize = Vector3.one;

        Tilemap suelo = Capa(gridGo, "Suelo", -10, false);
        Tilemap deco = Capa(gridGo, "Decorado", 0, false);
        Tilemap col = Capa(gridGo, "Colisiones", 10, true);

        for (int x = MinX - 1; x <= MaxX + 1; x++)
        for (int y = MinY - 1; y <= MaxY + 1; y++)
        {
            bool borde = x < MinX || x > MaxX || y < MinY || y > MaxY;
            if (borde) col.SetTile(new Vector3Int(x, y, 0), Tile(Pared));
            else suelo.SetTile(new Vector3Int(x, y, 0), Tile(Mathf.Abs(x) <= 4 && y >= -4 ? PisoArena : PisoGris));
        }

        void Poner(Tilemap tm, int x, int y, int idx, int w, int h)
        {
            for (int f = 0; f < h; f++)
            for (int c = 0; c < w; c++)
            {
                TileBase t = Tile(idx + f * HojaCols + c);
                if (t != null) tm.SetTile(new Vector3Int(x + c, y + (h - 1 - f), 0), t);
            }
        }

        // La pokeball del centro de la arena y las banderas. La pantalla de
        // arriba es el Servidor (un objeto de la pelea, no un tile).
        Poner(suelo, -1, 0, PokeballArriba, 2, 1);
        Poner(suelo, -1, -1, PokeballAbajo, 2, 1);
        Poner(col, -5, MaxY - 2, BanderaRoja, 1, 3);
        Poner(col, 5, MaxY - 2, BanderaAzul, 1, 3);
        col.SetTile(new Vector3Int(MinX, MinY, 0), Tile(PosteLuz));
        col.SetTile(new Vector3Int(MaxX, MinY, 0), Tile(PosteLuz));
        return true;
    }

    private static Tilemap Capa(GameObject grid, string nombre, int orden, bool conCollider)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(grid.transform, false);
        var tm = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = orden;
        if (!conCollider) return tm;

        go.layer = CapaObstacles;
        var tc = go.AddComponent<TilemapCollider2D>();
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        go.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        go.AddComponent<ColisionesTilemapRuntimeFix>();
        return tm;
    }

    // ------------------------------------------------------------ la pelea (lo que se copia)

    private static PeleaJulieta ArmarPelea(GameObject mapaRaiz, Vector2 posJulieta, Vector2 centroDeLaZona,
                                           Vector2 tamanioDeLaZona, Vector2 posServidor)
    {
        var raiz = new GameObject(Raiz);
        raiz.transform.SetParent(mapaRaiz.transform, false);
        var pelea = raiz.AddComponent<PeleaJulieta>();

        // Julieta: solida (capa Obstacles) para hablarle con X. El X lo atiende
        // PeleaJulieta, que esta en el padre (PlayerController busca con
        // GetComponentInParent).
        GameObject julieta = CasinoNpcBuilder.Personaje(raiz, "Julieta", posJulieta,
                                                        PeleaJulietaAssets.HojaDeJulieta, CasinoNpcBuilder.Abajo);
        // Parada entre dos columnas (el centro de la sala de la arena): tapa las
        // dos, asi se le puede hablar desde cualquiera.
        if (Mathf.Abs(Mathf.Repeat(posJulieta.x, 1f) - 0.5f) > 0.01f)
            julieta.GetComponent<BoxCollider2D>().size = new Vector2(1.9f, 0.9f);

        // La zona de delante: pisarla arranca el encuentro. Default + trigger.
        var zona = new GameObject("Zona");
        zona.transform.SetParent(raiz.transform, false);
        zona.transform.position = new Vector3(centroDeLaZona.x, centroDeLaZona.y, 0f);
        zona.layer = CapaDefault;
        var box = zona.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = tamanioDeLaZona;
        zona.AddComponent<ZonaDePeleaJulieta>();

        ArmarServidor(raiz, posServidor);

        if (!ConfigurarPelea(pelea)) return null;
        Debug.Log($"[Julieta] Julieta en {julieta.transform.position}, zona en {zona.transform.position}.");
        return pelea;
    }

    // El servidor principal, atras de Julieta: la pantalla grande de la hoja
    // de la arena (JuliTile, 4x2), armada con sprites sueltos para que se pueda
    // apagar sola al final (ServidorPrincipal). 'abajo' es el centro del borde
    // de abajo. Es solido: no se camina por detras.
    private static void ArmarServidor(GameObject raiz, Vector2 abajo)
    {
        Dictionary<string, TileBase> tiles = AssetDatabase.LoadAllAssetsAtPath(TilesAsset)
            .OfType<TileBase>().ToDictionary(t => t.name, t => t);

        var go = new GameObject("Servidor");
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(abajo.x, abajo.y, 0f);
        go.layer = CapaObstacles;
        // El collider arranca media celda mas arriba que el dibujo: asi la
        // fila de adelante queda libre y se lo puede tocar con X parado ahi
        // (el final se juega tocandolo).
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(4f, 1.5f);
        box.offset = new Vector2(0f, 1.25f);

        for (int f = 0; f < 2; f++)
        for (int c = 0; c < 4; c++)
        {
            tiles.TryGetValue($"JuliTile_00_{PantallaGrande + f * HojaCols + c:0000}", out TileBase t);
            Sprite sprite = (t as Tile)?.sprite;
            if (sprite == null) { Debug.LogWarning("[Julieta] Falta un pedazo de la pantalla del servidor (JuliTile)."); continue; }

            var parte = new GameObject($"Pantalla_{f}_{c}");
            parte.transform.SetParent(go.transform, false);
            parte.transform.localPosition = new Vector3(c - 1.5f, (1 - f) + 0.5f, 0f);
            // Un pixel del tile = un pixel de la grilla: la hoja va a 32 por celda.
            parte.transform.localScale = Vector3.one * (sprite.pixelsPerUnit / 32f);
            var sr = parte.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // Como los personajes: con el eje Y del Renderer2D, Julieta (mas
            // abajo) se dibuja adelante.
            sr.sortingOrder = 15;
        }

        go.AddComponent<ServidorPrincipal>();

        // Brilla mientras hay que ir a tocarlo: desde que Julieta cae hasta que
        // se destruye el nucleo.
        BrilloInteractuable brillo = HistoriaAssets.Brillo(go, StoryFlag.Historia_ServidorPrincipalDestruido,
                                                           StoryFlag.Julieta_Vencida, new Color(0.45f, 0.9f, 1f, 0.55f));
        var soBrillo = new SerializedObject(brillo);
        soBrillo.FindProperty("desplazamiento").vector2Value = new Vector2(0f, 1f);
        soBrillo.FindProperty("sortingOrder").intValue = 16;
        soBrillo.FindProperty("escalaGeneral").floatValue = 1.8f;
        soBrillo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Enchufa los assets al componente. Solo llena lo que esta vacio: si el
    // equipo cambio algo en el Inspector de la arena, no se pisa.
    private static bool ConfigurarPelea(PeleaJulieta pelea)
    {
        EnemyDefinition julieta = PeleaJulietaAssets.Julieta();
        GuionDeJulieta guion = PeleaJulietaAssets.Guion();
        CasinoPrizeTable ruleta = PeleaJulietaAssets.RuletaDelCasino();
        if (julieta == null || guion == null)
        {
            Debug.LogError("[Julieta] No se pudieron crear los assets de la pelea.");
            return false;
        }
        if (ruleta == null)
            Debug.LogWarning($"[Julieta] No está {PeleaJulietaAssets.RutaRuleta}: la pelea va a ir sin ruleta. " +
                             "Corré PokeRush > Casino > Poner el piso final para crearla.");

        var so = new SerializedObject(pelea);
        Llenar(so, "julieta", julieta);
        Llenar(so, "guion", guion);
        Llenar(so, "ruletaDelCasino", ruleta);
        Llenar(so, "perfilEstudiando", PeleaJulietaAssets.Perfil("Elite"));
        Llenar(so, "perfilEnSerio", PeleaJulietaAssets.Perfil("FinalBoss"));
        Llenar(so, "fuente", AssetDatabase.LoadAssetAtPath<Font>(Fuente));
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return true;
    }

    private static void Llenar(SerializedObject so, string campo, Object valor)
    {
        SerializedProperty p = so.FindProperty(campo);
        if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = valor;
    }

    // ------------------------------------------------------------ el rig

    // conEquipoDePrueba: el GameManager de la escena trae un equipo de fin de
    // juego para entrar por Play directo (viniendo de la cueva manda el de la
    // partida). conBanco: los atajos F9/F10/F11, solo en la escena de prueba.
    private static bool ArmarRig(Scene scene, GameObject mapaRaiz, PeleaJulieta pelea, Vector2 spawn,
                                 bool conEquipoDePrueba, bool conBanco)
    {
        if (mapaRaiz.GetComponentInChildren<Grid>(true) == null)
        {
            Debug.LogError("[Julieta] La escena no tiene Grid adentro de MapaRaiz: ¿es la arena?");
            return false;
        }

        GameObject player = AsegurarPlayer(scene, mapaRaiz, spawn);
        AsegurarCamara(mapaRaiz, player);
        AsegurarGameManager(scene, conEquipoDePrueba);
        AsegurarEventSystem(scene);
        GameObject combate = AsegurarCombate(scene);
        if (combate == null) return false;
        AsegurarSistema(scene, mapaRaiz, combate, player, conBanco: conBanco);
        AsegurarDialogo(scene);
        return true;
    }

    private static GameObject AsegurarPlayer(Scene scene, GameObject mapaRaiz, Vector2 spawn)
    {
        PlayerController existente = Buscar<PlayerController>(scene);
        GameObject player;
        if (existente == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapaRaiz.transform);
            player.name = "Player";
            player.transform.position = new Vector3(spawn.x, spawn.y, 0f);
        }
        else
        {
            player = existente.gameObject;
            if (!player.transform.IsChildOf(mapaRaiz.transform)) player.transform.SetParent(mapaRaiz.transform, true);
        }

        // Sin esto X no hace nada: el prefab viene con la mascara vacia.
        var so = new SerializedObject(player.GetComponent<PlayerController>());
        so.FindProperty("interactableMask").intValue |= 1 << CapaObstacles;
        so.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static void AsegurarCamara(GameObject mapaRaiz, GameObject player)
    {
        Camera cam = mapaRaiz.GetComponentsInChildren<Camera>(true).FirstOrDefault();
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.transform.SetParent(mapaRaiz.transform);
            cam = go.AddComponent<Camera>();
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
        }
        cam.gameObject.tag = "MainCamera";
        cam.orthographic = true;
        Asegurar<AudioListener>(cam.gameObject);

        var follow = Asegurar<CameraFollow>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
    }

    private static void AsegurarGameManager(Scene scene, bool conEquipoDePrueba)
    {
        GameManager existente = Buscar<GameManager>(scene);
        GameObject go = existente != null ? existente.gameObject : AsegurarRaiz(scene, "GameManager");
        var gm = Asegurar<GameManager>(go);
        var so = new SerializedObject(gm);

        if (conEquipoDePrueba)
        {
            var especies = new List<PokenDefinition>();
            var niveles = new List<int>();
            foreach (var (especie, nivel) in EquipoDePrueba)
            {
                var p = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{especie}.asset");
                if (p == null) { Debug.LogWarning($"[Julieta] No existe {especie} para el equipo de prueba."); continue; }
                especies.Add(p);
                niveles.Add(nivel);
            }
            SerializedProperty equipo = so.FindProperty("equipoInicial");
            SerializedProperty nivelesProp = so.FindProperty("nivelesIniciales");
            equipo.arraySize = especies.Count;
            nivelesProp.arraySize = niveles.Count;
            for (int i = 0; i < especies.Count; i++)
            {
                equipo.GetArrayElementAtIndex(i).objectReferenceValue = especies[i];
                nivelesProp.GetArrayElementAtIndex(i).intValue = niveles[i];
            }
        }

        Transform audio = go.transform.Find("Audio");
        if (audio == null)
        {
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(go.transform);
            audio = audioGo.transform;
            var musica = audioGo.AddComponent<AudioSource>();
            musica.playOnAwake = false;
            musica.loop = true;
            audioGo.AddComponent<AudioSource>().playOnAwake = false;
        }
        AudioSource[] fuentes = audio.GetComponents<AudioSource>();
        if (so.FindProperty("musicSource").objectReferenceValue == null)
            so.FindProperty("musicSource").objectReferenceValue = fuentes[0];
        if (so.FindProperty("sfxSource").objectReferenceValue == null)
            so.FindProperty("sfxSource").objectReferenceValue = fuentes.Length > 1 ? fuentes[1] : fuentes[0];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarEventSystem(Scene scene)
    {
        if (Buscar<EventSystem>(scene) != null) return;
        GameObject go = AsegurarRaiz(scene, "EventSystem");
        Asegurar<EventSystem>(go);
        Asegurar<InputSystemUIInputModule>(go);
    }

    private static GameObject AsegurarCombate(Scene scene)
    {
        BattleController existente = Buscar<BattleController>(scene);
        GameObject combate = existente != null ? existente.transform.root.gameObject : null;
        if (combate == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatePrefab);
            if (prefab == null) { Debug.LogError($"[Julieta] Falta {CombatePrefab}."); return null; }
            combate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            combate.name = "SistemaDeCombate";
        }
        // Va DESACTIVADO: lo prende MapBattleBridge cuando arranca la pelea.
        combate.SetActive(false);
        return combate;
    }

    private static void AsegurarSistema(Scene scene, GameObject mapaRaiz, GameObject combate, GameObject player,
                                        bool conBanco)
    {
        GameObject sistema = AsegurarRaiz(scene, "Sistema");
        var bridge = Asegurar<MapBattleBridge>(sistema);
        var boot = Asegurar<SceneBootstrapper>(sistema);
        if (conBanco) Asegurar<PeleaJulietaBancoDePrueba>(sistema);

        var playerController = player.GetComponent<PlayerController>();
        var soBridge = new SerializedObject(bridge);
        soBridge.FindProperty("mapaRaiz").objectReferenceValue = mapaRaiz;
        soBridge.FindProperty("combateRaiz").objectReferenceValue = combate;
        soBridge.FindProperty("battleController").objectReferenceValue = combate.GetComponentInChildren<BattleController>(true);
        soBridge.FindProperty("playerController").objectReferenceValue = playerController;
        soBridge.ApplyModifiedPropertiesWithoutUndo();

        var soBoot = new SerializedObject(boot);
        soBoot.FindProperty("playerController").objectReferenceValue = playerController;
        soBoot.FindProperty("crearPartidaSiFalta").boolValue = true;
        SerializedProperty nombre = soBoot.FindProperty("nombrePartidaDePrueba");
        if (string.IsNullOrEmpty(nombre.stringValue)) nombre.stringValue = "DEMO";
        soBoot.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsegurarDialogo(Scene scene)
    {
        if (Buscar<DialogueController>(scene) != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogoPrefab);
        if (prefab == null) { Debug.LogError($"[Julieta] Falta {DialogoPrefab}."); return; }
        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        canvas.name = "DialogoCanvas";
    }

    // ------------------------------------------------------------ verificar

    // Que se llegue caminando desde el spawn a la zona de la pelea y que la zona
    // no este en Obstacles (ahi frenaria al jugador en vez de dispararse).
    public static List<string> Verificar(Scene scene, Vector2 spawn)
    {
        var problemas = new List<string>();
        PeleaJulieta pelea = Buscar<PeleaJulieta>(scene);
        if (pelea == null) { problemas.Add("no hay PeleaJulieta en la escena."); return problemas; }

        ZonaDePeleaJulieta zona = pelea.GetComponentInChildren<ZonaDePeleaJulieta>(true);
        Collider2D colZona = zona != null ? zona.GetComponent<Collider2D>() : null;
        if (colZona == null) { problemas.Add("la pelea no tiene Zona (hijo con ZonaDePeleaJulieta y su collider)."); return problemas; }
        if (!colZona.isTrigger) problemas.Add("el collider de la Zona tiene que estar en Is Trigger.");
        if (zona.gameObject.layer == CapaObstacles)
            problemas.Add("la Zona está en la capa Obstacles: frena al jugador. Va en Default.");

        var tilemaps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).ToList();
        Tilemap suelo = tilemaps.FirstOrDefault(t => t.name == "Suelo");
        Tilemap colisiones = tilemaps.FirstOrDefault(t => t.name == "Colisiones");
        if (suelo == null || colisiones == null)
        {
            problemas.Add("faltan los tilemaps 'Suelo' o 'Colisiones' (ver Como_Armar_Un_Mapa_Nuevo.md).");
            return problemas;
        }

        var solidos = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
                        c.gameObject.layer == CapaObstacles &&
                        !(c is TilemapCollider2D) && !(c is CompositeCollider2D) &&
                        c.GetComponentInParent<PlayerController>() == null)
            .ToList();

        bool Bloqueada(Vector3Int c)
        {
            if (colisiones.HasTile(c)) return true;
            Vector3 centro = suelo.GetCellCenterWorld(c);
            return solidos.Any(s => s.bounds.Contains(new Vector3(centro.x, centro.y, s.bounds.center.z)));
        }

        Vector3Int inicio = suelo.WorldToCell(spawn);
        if (!suelo.HasTile(inicio) || Bloqueada(inicio))
        {
            problemas.Add($"donde aparece el jugador ({inicio}) no es piso libre.");
            return problemas;
        }

        var alcanzadas = new HashSet<Vector3Int> { inicio };
        var cola = new Queue<Vector3Int>();
        cola.Enqueue(inicio);
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (cola.Count > 0)
        {
            Vector3Int c = cola.Dequeue();
            foreach (Vector3Int d in dirs)
            {
                Vector3Int n = c + d;
                if (alcanzadas.Contains(n) || Mathf.Abs(n.x - inicio.x) > 60 || Mathf.Abs(n.y - inicio.y) > 60) continue;
                if (Bloqueada(n) || !suelo.HasTile(n)) continue;
                alcanzadas.Add(n);
                cola.Enqueue(n);
            }
        }

        Bounds b = colZona.bounds;
        bool llega = false;
        for (float x = b.min.x + 0.5f; x < b.max.x && !llega; x += 1f)
        for (float y = b.min.y + 0.5f; y < b.max.y && !llega; y += 1f)
            if (alcanzadas.Contains(suelo.WorldToCell(new Vector3(x, y, 0f)))) llega = true;
        if (!llega) problemas.Add("no se llega caminando a la zona de la pelea desde donde aparece el jugador.");

        Debug.Log($"[Julieta] Verificación: {alcanzadas.Count} celdas alcanzables, la zona {(llega ? "se alcanza" : "NO se alcanza")}.");
        return problemas;
    }

    // ------------------------------------------------------------ utilidades

    // Nunca abrir otra escena encima de una con cambios sin guardar: el
    // dialogo de "guardar?" deja colgado a quien corre esto por MCP.
    private static bool PuedeTocarEscenas()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Julieta] Salí del Play Mode antes de correr esto.");
            return false;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isDirty) continue;
            if (Application.isBatchMode) return true;
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
        return true;
    }

    public static void SumarABuildSettings(string path)
    {
        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EditorBuildSettingsScene existente = lista.FirstOrDefault(s => s.path == path);
        if (existente == null) lista.Add(new EditorBuildSettingsScene(path, true));
        else existente.enabled = true;
        EditorBuildSettings.scenes = lista.ToArray();
    }

    // Abrir una escena pierde los colliderType = Grid por celda, y guardarla asi
    // los pierde en disco (ver Como_Armar_Un_Mapa_Nuevo.md).
    private static void ReponerColisiones(Scene scene)
    {
        foreach (Tilemap t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (t.name != "Colisiones") continue;
            foreach (Vector3Int p in t.cellBounds.allPositionsWithin)
                if (t.HasTile(p) && t.GetColliderType(p) != Tile.ColliderType.Grid)
                    t.SetColliderType(p, Tile.ColliderType.Grid);
        }
    }

    private static GameObject AsegurarRaiz(Scene scene, string nombre)
    {
        GameObject existente = scene.GetRootGameObjects().FirstOrDefault(r => r.name == nombre);
        if (existente != null) return existente;
        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    public static T Buscar<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

    private static T Asegurar<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
