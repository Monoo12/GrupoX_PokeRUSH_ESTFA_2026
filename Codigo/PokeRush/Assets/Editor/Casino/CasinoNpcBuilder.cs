using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone en el piso 1 del casino los dos personajes que lo hacen jugable de punta
// a punta: un apostador contra el que se pelea (paga 100 monedas) y una
// enfermera que cura, fiando si hace falta.
//
// Los dos salen de sprites que ya estan en el proyecto (Graphics/Characters) y
// se paran en los taburetes que dibujo el equipo de nivel, mirando a la maquina:
// desde el pasillo se leen como dos personas jugando.
//
// Es idempotente: borra los que puso antes y los vuelve a crear.
public static class CasinoNpcBuilder
{
    private const string Escena = "Assets/Scenes/Casino Nivel 1.unity";
    private const string Contenedor = "PersonajesCasino";
    private const string DefinitionFolder = "Assets/Resources/Definitions/Casino";

    private const string HojaApostador = "Assets/Graphics/Characters/Lider 1 bw.png";
    private const string HojaEnfermera = "Assets/Graphics/Characters/enfermera.png";
    private const string RetratoApostador = "Assets/Graphics/Trainers/JUGON.png";

    private const int CapaObstacles = 3;

    // Las hojas de personaje del pack son 4x4: cuatro poses por direccion, en
    // el orden abajo / izquierda / derecha / arriba.
    private static readonly string[] Direcciones = { "abajo", "izq", "der", "arriba" };

    // Taburetes elegidos: los dos pegados al pasillo de alfombra, uno de cada
    // lado, para que el jugador se los cruce si o si al entrar.
    private static readonly Vector2 PuestoApostador = new Vector2(-3.5f, 12.5f);
    private static readonly Vector2 PuestoEnfermera = new Vector2(4.5f, 12.5f);

    [MenuItem("PokeRush/Casino/Poner los personajes del piso 1")]
    public static void PonerTodos()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[CasinoNPC] Sali del Play Mode antes de correr esto.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Sprite[] apostador = CortarHoja(HojaApostador, 4, 4);
        Sprite[] enfermera = CortarHoja(HojaEnfermera, 4, 4);
        EnemyDefinition rival = ConstruirApostador();

        Scene scene = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        GameObject raiz = PrepararContenedor(scene);

        // Mira a la IZQUIERDA: la maquina le queda de ese lado.
        GameObject npcApostador = CrearPersonaje(raiz, "ApostadorCasino", PuestoApostador,
            Frame(apostador, 4, 1, 0));
        var trigger = npcApostador.AddComponent<InteractableTrigger>();
        ConfigurarApostador(trigger, rival);

        // Mira a la DERECHA, hacia su maquina.
        GameObject npcEnfermera = CrearPersonaje(raiz, "EnfermeraCasino", PuestoEnfermera,
            Frame(enfermera, 4, 2, 0));
        npcEnfermera.AddComponent<CasinoNurseInteractable>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[CasinoNPC] Apostador y enfermera puestos en el piso 1 del casino.");
    }

    // ---------- personajes ----------

    private static GameObject PrepararContenedor(Scene scene)
    {
        GameObject mapaRaiz = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "MapaRaiz") mapaRaiz = r;

        if (mapaRaiz == null)
        {
            Debug.LogError("[CasinoNPC] La escena no tiene MapaRaiz: corre primero " +
                           "PokeRush/Casino/Armar el rig de las escenas del casino.");
            mapaRaiz = new GameObject("MapaRaiz");
            SceneManager.MoveGameObjectToScene(mapaRaiz, scene);
        }

        Transform vieja = mapaRaiz.transform.Find(Contenedor);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var go = new GameObject(Contenedor);
        go.transform.SetParent(mapaRaiz.transform);
        return go;
    }

    private static GameObject CrearPersonaje(GameObject raiz, string nombre,
                                             Vector2 celda, Sprite sprite)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz.transform);
        go.transform.position = new Vector3(celda.x, celda.y, 0f);

        // Capa Obstacles: es la que mira el interactableMask del jugador, y de
        // paso hace que el personaje frene al que quiera atravesarlo.
        go.layer = CapaObstacles;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform);
        // El sprite tiene el pivote abajo, asi que se lo baja media celda para
        // que los pies queden apoyados en el piso del taburete.
        visual.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // Mismo sorting que el jugador: se tapan entre ellos segun la posicion,
        // y los dos quedan debajo de la capa de Techos.
        sr.sortingOrder = 15;

        return go;
    }

    private static void ConfigurarApostador(InteractableTrigger trigger, EnemyDefinition rival)
    {
        var so = new SerializedObject(trigger);
        so.FindProperty("titulo").stringValue = "APOSTADOR";
        so.FindProperty("combate").objectReferenceValue = rival;

        // Con el flag puesto, el trigger sabe que ya se peleo y no deja repetir
        // la pelea: si no, seria una maquina de imprimir 100 monedas. Lo activa
        // EventController al ganar, leyendo el defeatGrantsFlag del rival.
        so.FindProperty("otorgaFlag").enumValueIndex = (int)StoryFlag.Casino_ApostadorVencido;

        so.FindProperty("preguntaAntes").stringValue =
            "Eh, vos. Me quede sin fichas. Te juego los ultimos 100 a los Pokemon. Dale?";
        so.FindProperty("nombreQuePregunta").stringValue = "APOSTADOR";
        so.FindProperty("opcionSi").stringValue = "Dale";
        so.FindProperty("opcionNo").stringValue = "Paso";
        so.FindProperty("mensajeSiRechaza").stringValue = "Ma si. Andate.";
        so.FindProperty("mensajeExito").stringValue =
            "Tomalos... 100. Me voy a la maquina de al lado.";
        so.FindProperty("mensajeYaHecho").stringValue = "Ya me dejaste seco. Andate.";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- el rival ----------

    // El apostador como EnemyDefinition normal: el dinero lo paga el sistema de
    // recompensas que ya existe (EnemyModel.Money -> BattleOutcome.MoneyEarned ->
    // PlayerModel.AddMoney), no hace falta ni una linea nueva de economia.
    private static EnemyDefinition ConstruirApostador()
    {
        string path = DefinitionFolder + "/ApostadorCasino.asset";

        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<EnemyDefinition>();

        def.characterName = "Apostador";
        def.moneyReward = 100;
        def.defeatGrantsFlag = StoryFlag.Casino_ApostadorVencido;
        def.fleeIfWeak = false;

        def.pokenTeam = new[]
        {
            Entrada("Assets/Resources/Definitions/Pokens/0020_Raticate.asset", 12),
            Entrada("Assets/Resources/Definitions/Pokens/0064_Kadabra.asset", 12)
        };

        def.aiProfile = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>(
            "Assets/Resources/Definitions/AIProfiles/Trainer.asset");

        // JUGON es el retrato de apostador que ya trae el pack.
        Sprite retrato = AssetDatabase.LoadAssetAtPath<Sprite>(RetratoApostador);
        if (retrato == null)
        {
            AsegurarSpriteSimple(RetratoApostador);
            retrato = AssetDatabase.LoadAssetAtPath<Sprite>(RetratoApostador);
        }
        def.battleFrontSprite = retrato;

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static EnemyTeamEntry Entrada(string path, int nivel)
    {
        var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>(path);
        if (especie == null) Debug.LogWarning($"[CasinoNPC] No encontre {path}.");
        return new EnemyTeamEntry { species = especie, level = nivel };
    }

    // ---------- sprites ----------

    private static void AsegurarSpriteSimple(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[CasinoNPC] No existe {path}."); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    // Corta una hoja de personaje en su grilla y devuelve los sprites en orden
    // de lectura (fila 0 = mirando abajo).
    //
    // Igual que con la tira de la tragaperras: se corta con
    // SpriteEditorDataProvider porque en Unity 6 el campo 'spritesheet' del
    // importer se ignora.
    private static Sprite[] CortarHoja(string path, int columnas, int filas)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[CasinoNPC] No existe {path}."); return null; }

        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (textura == null) { Debug.LogError($"[CasinoNPC] No se pudo leer {path}."); return null; }

        int ancho = textura.width / columnas;
        int alto = textura.height / filas;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        // 32 pixeles por unidad, como los tiles: un personaje de 64px mide dos
        // celdas de alto, que es lo que miden los del resto del juego.
        importer.spritePixelsPerUnit = 32;
        importer.SaveAndReimport();

        string stem = System.IO.Path.GetFileNameWithoutExtension(path).Replace(" ", "_");

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        var pares = new List<SpriteNameFileIdPair>();

        for (int fila = 0; fila < filas; fila++)
        {
            for (int col = 0; col < columnas; col++)
            {
                string dir = fila < Direcciones.Length ? Direcciones[fila] : fila.ToString();
                var sr = new SpriteRect
                {
                    name = $"{stem}_{dir}_{col}",
                    spriteID = GUID.Generate(),
                    // El rect va en coordenadas de textura: la fila 0 de la hoja
                    // es la de ARRIBA, y ahi el origen esta abajo.
                    rect = new Rect(col * ancho, textura.height - (fila + 1) * alto, ancho, alto),
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f)
                };
                rects.Add(sr);
                pares.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        var nombres = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nombres != null) nombres.SetNameFileIdPairs(pares);
        provider.Apply();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var salida = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) salida.Add(s);

        // LoadAllAssetsAtPath no garantiza orden: se reordena por nombre segun
        // como se generaron.
        var porNombre = new Dictionary<string, Sprite>();
        foreach (Sprite s in salida) porNombre[s.name] = s;

        var ordenados = new Sprite[rects.Count];
        for (int i = 0; i < rects.Count; i++)
            porNombre.TryGetValue(rects[i].name, out ordenados[i]);
        return ordenados;
    }

    private static Sprite Frame(Sprite[] hoja, int columnas, int fila, int col)
    {
        if (hoja == null) return null;
        int i = fila * columnas + col;
        return i >= 0 && i < hoja.Length ? hoja[i] : null;
    }
}
