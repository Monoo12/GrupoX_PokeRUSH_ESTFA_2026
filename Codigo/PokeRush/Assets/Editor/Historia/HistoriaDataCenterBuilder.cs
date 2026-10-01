using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Pone la historia en el Data Center de Nestor (NestorGOD2), el primer nivel.
//
// Todo va adentro de MapaRaiz/HistoriaDataCenter, que se borra y se vuelve a
// armar entero: el mapa y lo que ya estaba (paneles, jaulas, Nestor) no se
// tocan, salvo los campos de historia del InteractableTrigger de Nestor.
//
//   NPCs        Personas del Data Center, cada una con su DialogueDefinition
//               (Torre1/Dialogos/DC_*). Cambian lo que dicen cuando cae el
//               servidor (variantes con Historia_Servidor01Desconectado).
//               La enfermera de la recepcion cura al equipo (NpcQueCura) y el
//               guardia de la sala de control te ve entrar (NpcQueTeDetecta).
//   Muebles     Sillas, banquetas y el sillon frenan: un tile invisible en
//               Colisiones en cada celda (no cambia nada de lo que se ve).
//   Cosas       Terminales, el memo de J., la foto de la mesa de Nestor y la
//               maquina expendedora: colliders sobre muebles que ya estan
//               dibujados, con un brillo celeste hasta que se leen.
//   Zonas       Momentos que pasan al PISAR un lugar (ZonaDeHistoria): las
//               pantallas del salon y el zumbido del pasillo de Nestor.
//   Nestor      Lo del guion antes y despues de la pelea, el Protocolo Nestor.
//   Apagon      Las luces bajan cuando el servidor se desconecta.
//   Salida      En la recepcion: con el nivel terminado se vuelve caminando a
//               la cueva (sin esto, VueltaAlLobby te sacaba apenas caia Nestor).
//
// Las celdas estan elegidas con sentido (la recepcionista atras del mostrador,
// la operaria en su escritorio, la biologa junto al tanque de Basculin...). Si
// el mapa cambia, se corren aca y se vuelve a correr el menu: verifica que cada
// uno este sobre piso libre y que se llegue caminando.
public static class HistoriaDataCenterBuilder
{
    private const string Escena = "Assets/Scenes/NestorGOD2.unity";
    private const string Contenedor = "HistoriaDataCenter";
    private const string T = "Torre1/Dialogos/";
    private const int CapaDefault = 0;
    private const int CapaObstacles = 3;

    // Techos dibuja en 20 (el sillon de la sala de descanso esta ahi) y los
    // personajes en 15. El que esta acostado va ARRIBA del mueble.
    private const int OrdenAcostado = 21;

    private static readonly Color Celeste = new Color(0.45f, 0.9f, 1f, 0.5f);
    private static readonly Color Verde = new Color(0.45f, 1f, 0.55f, 0.55f);

    private struct Npc
    {
        public string Nombre, Hoja, Dialogo;
        public Vector2Int Celda;
        public int Mira;
        // Celda de mueble desde la que tambien se le habla (el mostrador).
        public Vector2Int? Atencion;
        // Acostado sobre el mueble de su celda (el que duerme en el sillon). Es
        // el unico que puede estar arriba de un mueble: ver Verificar.
        public bool Acostado;
        // Cura al equipo (NpcQueCura) en vez de solo hablar. Sus textos son
        // Dialogo + _Curado / _Sano / _NoGracias.
        public bool Cura;
        // La sala (en celdas) donde te ve entrar y arranca a hablar solo
        // (NpcQueTeDetecta). Suena una vez: la marca YaPasoCon.
        public RectInt? Vigila;
        public StoryFlag YaPasoCon, NoVigilaCon;
    }

    private struct Cosa
    {
        public string Nombre, Dialogo;
        public Vector2Int Celda;
        public bool ConBrillo;
    }

    private struct Zona
    {
        public string Nombre, Dialogo;
        public Vector2Int Desde, Tamanio;
    }

    // Nadie va parado ARRIBA de un mueble. Las sillas y el sillon no frenan (no
    // estan en Colisiones), asi que para Verificar eran "piso libre", pero se
    // dibujan debajo de los personajes: la recepcionista y la operaria quedaban
    // subidas a su silla. Tampoco conviene un mueble justo al SUR: le queda
    // debajo de los pies (le paso al entusiasta con el respaldo de una silla).
    private static readonly Npc[] Npcs =
    {
        // Atras de su silla, entre el mostrador y la pared.
        new Npc { Nombre = "Recepcionista", Hoja = "asistenta", Celda = new Vector2Int(-4, 1),
                  Mira = CasinoNpcBuilder.Derecha, Dialogo = "DC_Recepcionista", Atencion = new Vector2Int(-3, 0) },
        // La enfermeria: en la recepcion, al lado del PC (enfermera y PC juntos,
        // como en el casino), mirando al que entra. Se le habla desde (4,0). Es
        // lo primero que se ve al llegar y queda de paso al volver del salon.
        new Npc { Nombre = "Enfermera", Hoja = "enfermera", Celda = new Vector2Int(4, 1),
                  Mira = CasinoNpcBuilder.Abajo, Dialogo = "DC_Enfermera", Cura = true },
        // Al lado de la silla de la primera terminal del salon, mirandola. En
        // (-2,15) estaba en el pasillo del medio, justo debajo del que sube a
        // Nestor: angostaba el camino principal del nivel.
        new Npc { Nombre = "Operaria", Hoja = "operaria", Celda = new Vector2Int(-6, 15),
                  Mira = CasinoNpcBuilder.Izquierda, Dialogo = "DC_Operaria" },
        // Ojo con las hojas: Personaje() las corta con nombres propios y cambia
        // los IDs de los sprites. Una hoja que otra escena ya usa cortada de otra
        // forma (la de 'tecnico', en EdificioNucleo) quedaria rota alla.
        // Al fondo del pasillo entre racks, revisando el ultimo. En (31,16)
        // cortaba ese pasillo (una sola celda de alto) por la mitad.
        new Npc { Nombre = "Tecnico", Hoja = "operario", Celda = new Vector2Int(36, 16),
                  Mira = CasinoNpcBuilder.Arriba, Dialogo = "DC_Tecnico" },
        // Delante de las mesas de la sala de descanso: tapa la silla de atras,
        // que es lo que corresponde en la vista 3/4.
        new Npc { Nombre = "Entusiasta", Hoja = "cerebrito", Celda = new Vector2Int(-35, 13),
                  Mira = CasinoNpcBuilder.Abajo, Dialogo = "DC_Entusiasta" },
        // Durmiendo en el sillon. Parado ahi lo tapaba el sillon (esta en
        // Techos, por encima de los personajes) y solo asomaba la coronilla.
        new Npc { Nombre = "EmpleadoDormido", Hoja = "chaval1", Celda = new Vector2Int(-45, 18),
                  Mira = CasinoNpcBuilder.Abajo, Dialogo = "DC_Dormido", Acostado = true },
        // De guardia al lado de la puerta de la sala de control (la del panel
        // de la energia auxiliar), mirando hacia ella. Te ve apenas pisas la
        // sala (x -46..-37, y 22..26): "!", se da vuelta y te habla solo. Una
        // vez: DC_Guardia_Cruz enciende GuardiaRecibioOrden al final. Con el
        // servidor caido ya no vigila (le hablas con X).
        new Npc { Nombre = "GuardiaDeLaLiga", Hoja = "maton", Celda = new Vector2Int(-40, 22),
                  Mira = CasinoNpcBuilder.Izquierda, Dialogo = "DC_Guardia",
                  Vigila = new RectInt(-46, 22, 10, 5),
                  YaPasoCon = StoryFlag.Historia_DC_GuardiaRecibioOrden,
                  NoVigilaCon = StoryFlag.Historia_Servidor01Desconectado },
        new Npc { Nombre = "Biologa", Hoja = "cientifica", Celda = new Vector2Int(-42, -4),
                  Mira = CasinoNpcBuilder.Izquierda, Dialogo = "DC_Biologa" },
        new Npc { Nombre = "PorygonConChip", Hoja = "Followers/PORYGON", Celda = new Vector2Int(25, 34),
                  Mira = CasinoNpcBuilder.Arriba, Dialogo = "DC_Porygon" },
    };

    private static readonly Cosa[] Cosas =
    {
        new Cosa { Nombre = "TerminalDelSalon", Celda = new Vector2Int(-4, 12), Dialogo = "DC_TerminalHall", ConBrillo = true },
        new Cosa { Nombre = "MemoDeJ", Celda = new Vector2Int(-41, 26), Dialogo = "DC_Memo", ConBrillo = true },
        new Cosa { Nombre = "FotoDeLaPromocion", Celda = new Vector2Int(0, 29), Dialogo = "DC_Foto", ConBrillo = true },
        new Cosa { Nombre = "MaquinaExpendedora", Celda = new Vector2Int(-29, 18), Dialogo = "DC_Maquina", ConBrillo = false },
    };

    private static readonly Zona[] Zonas =
    {
        // La entrada al salon de terminales, subiendo desde la recepcion.
        new Zona { Nombre = "ZonaPantallas", Desde = new Vector2Int(2, 8), Tamanio = new Vector2Int(2, 1), Dialogo = "DC_Zona_Pantallas" },
        // El pasillo que sube a la sala de Nestor.
        new Zona { Nombre = "ZonaGuardian", Desde = new Vector2Int(-2, 22), Tamanio = new Vector2Int(3, 1), Dialogo = "DC_Zona_Guardian" },
    };

    // La recepcion, donde aparece el jugador: "por donde entraste".
    private static readonly Vector2Int Salida = new Vector2Int(-1, -1);

    // Muebles que se dibujaban pero no frenaban: el jugador caminaba por
    // encima de las sillas y se metia adentro del sillon. Cada celda lleva un
    // tile INVISIBLE en Colisiones (ColisionInvisible): frena sin cambiar nada
    // de lo que se ve ni el orden de dibujo (mover la silla a Colisiones la
    // dibujaba encima de los escritorios y el sillon quedaba tapado por la
    // pared). Verificar comprueba que todo lo que se toca siga alcanzable.
    private static readonly (string que, Vector2Int[] celdas)[] MueblesQueFrenan =
    {
        ("las sillas de las ocho terminales del salon (dos celdas cada una)", new[]
        {
            new Vector2Int(-7, 11), new Vector2Int(-7, 12), new Vector2Int(-3, 11), new Vector2Int(-3, 12),
            new Vector2Int(1, 11), new Vector2Int(1, 12), new Vector2Int(5, 11), new Vector2Int(5, 12),
            new Vector2Int(-7, 14), new Vector2Int(-7, 15), new Vector2Int(-3, 14), new Vector2Int(-3, 15),
            new Vector2Int(1, 14), new Vector2Int(1, 15), new Vector2Int(5, 14), new Vector2Int(5, 15),
        }),
        ("las sillas de las mesas de la sala de descanso", new[]
        {
            new Vector2Int(-39, 14), new Vector2Int(-39, 16), new Vector2Int(-35, 14), new Vector2Int(-35, 16),
            new Vector2Int(-34, 14), new Vector2Int(-34, 16), new Vector2Int(-30, 14), new Vector2Int(-30, 16),
        }),
        ("las banquetas de la recepcion y la silla de la recepcionista", new[]
        {
            new Vector2Int(3, -4), new Vector2Int(4, -4), new Vector2Int(5, -2), new Vector2Int(5, -3),
            new Vector2Int(-4, 0),
        }),
        // El del medio no: lo ocupa el que duerme, que ya frena.
        ("los extremos del sillon de la sala de descanso", new[]
        {
            new Vector2Int(-46, 18), new Vector2Int(-44, 18),
        }),
        // Pintada en Suelo en vez de Colisiones (las de al lado estan bien).
        // Hoy no se llega porque el panel de rastreo tapa el paso, pero achicar
        // ese collider la volveria atravesable.
        ("la pared del fondo de la sala de generadores", new[]
        {
            new Vector2Int(27, 42), new Vector2Int(28, 42), new Vector2Int(27, 43), new Vector2Int(28, 43),
        }),
    };

    // Un Tile sin sprite y con collider Grid: frena y no dibuja nada.
    private const string TileInvisiblePath = "Assets/Palettes/ColisionInvisible.asset";

    // El "!" del guardia: el globo del pack de Essentials (ya cortado).
    private const string HojaDeExclamacion = "Assets/Graphics/Animations/Overworld exclaim.png";
    private const string SpriteDeExclamacion = "Overworld exclaim_0";

    [MenuItem("PokeRush/Historia/Poblar el Data Center de Nestor")]
    public static void Poblar()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene abierta = SceneManager.GetSceneAt(i);
            if (abierta.isDirty)
            {
                Debug.LogError($"[HistoriaDC] '{abierta.name}' tiene cambios sin guardar: guardala o descartala y volve a correr esto.");
                return;
            }
        }

        Scene scene = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null) { Debug.LogError("[HistoriaDC] NestorGOD2 no tiene MapaRaiz."); return; }

        Transform viejo = mapaRaiz.transform.Find(Contenedor);
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

        var raiz = new GameObject(Contenedor);
        raiz.transform.SetParent(mapaRaiz.transform, false);
        // Las luces bajan cuando cae el servidor (a mitad de la charla con Nestor).
        raiz.AddComponent<ApagonDelNivel>();

        var npcs = new GameObject("Personas");
        npcs.transform.SetParent(raiz.transform, false);
        foreach (Npc n in Npcs) PonerNpc(npcs, n);

        var cosas = new GameObject("Cosas");
        cosas.transform.SetParent(raiz.transform, false);
        foreach (Cosa c in Cosas) PonerCosa(cosas, c);

        var zonas = new GameObject("Zonas");
        zonas.transform.SetParent(raiz.transform, false);
        foreach (Zona z in Zonas) PonerZona(zonas, z);

        PonerSalida(raiz);
        bool nestorOk = ConfigurarNestor(scene);
        int frenan = PonerMueblesQueFrenan(scene);

        List<string> problemas = Verificar(scene, raiz);
        if (!nestorOk) problemas.Add("no se encontro el InteractableTrigger de Nestor.");
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[HistoriaDC] {p}");
            Debug.LogError("[HistoriaDC] NO se guarda: se descarta lo hecho.");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            return;
        }

        ReponerColisiones(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[HistoriaDC] Listo: {Npcs.Length} personas (enfermera y guardia que vigila incluidos), " +
                  $"{Cosas.Length} cosas, {Zonas.Length} zonas, {frenan} celdas de muebles que ahora frenan, " +
                  "salida en la recepcion, apagon y Nestor con su historia. Todo alcanzable. Guardada.");
    }

    // ---------- piezas ----------

    private static Vector3 Centro(Vector2Int celda) => new Vector3(celda.x + 0.5f, celda.y + 0.5f, 0f);

    private static void PonerNpc(GameObject raiz, Npc n)
    {
        GameObject go = CasinoNpcBuilder.Personaje(raiz, n.Nombre, Centro(n.Celda), n.Hoja, n.Mira);
        if (n.Acostado) Acostar(go);

        if (n.Cura)
        {
            PonerEnfermera(go, n);
        }
        else
        {
            var habla = go.AddComponent<NpcQueHabla>();
            var so = new SerializedObject(habla);
            so.FindProperty("dialogo").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (n.Vigila.HasValue) PonerVigia(go, n);

        if (n.Atencion.HasValue)
        {
            // El mostrador: apretar X frente a el habla con quien esta atras.
            var atencion = new GameObject("Atencion");
            atencion.transform.SetParent(go.transform, false);
            atencion.transform.position = Centro(n.Atencion.Value);
            atencion.layer = CapaObstacles;
            atencion.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
        }
    }

    // La enfermera: cura con PlayerModel.CurarEquipo (la misma curacion del
    // casino) y habla con sus cuatro dialogos.
    private static void PonerEnfermera(GameObject go, Npc n)
    {
        var cura = go.AddComponent<NpcQueCura>();
        var so = new SerializedObject(cura);
        so.FindProperty("saludo").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo);
        so.FindProperty("alCurar").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo + "_Curado");
        so.FindProperty("yaEstaSano").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo + "_Sano");
        so.FindProperty("alRechazar").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo + "_NoGracias");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // El que te ve entrar a su sala: la sala en el mundo, los flags, el "!" y
    // las cuatro poses de su hoja para darse vuelta hacia vos.
    private static void PonerVigia(GameObject go, Npc n)
    {
        RectInt sala = n.Vigila.Value;
        var vigia = go.AddComponent<NpcQueTeDetecta>();
        var so = new SerializedObject(vigia);
        so.FindProperty("centroDeLaSala").vector2Value = sala.center;
        so.FindProperty("tamanioDeLaSala").vector2Value = new Vector2(sala.width, sala.height);
        so.FindProperty("yaPasoConFlag").enumValueIndex = (int)n.YaPasoCon;
        so.FindProperty("excluye").enumValueIndex = (int)n.NoVigilaCon;
        so.FindProperty("signo").objectReferenceValue = SignoDeExclamacion();
        so.FindProperty("visual").objectReferenceValue = go.transform.Find("Visual").GetComponent<SpriteRenderer>();

        Sprite[] poses = CasinoNpcBuilder.Poses(n.Hoja);
        SerializedProperty miradas = so.FindProperty("miradas");
        miradas.arraySize = poses.Length;
        for (int i = 0; i < poses.Length; i++) miradas.GetArrayElementAtIndex(i).objectReferenceValue = poses[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // El globo del pack viene filtrado (Bilinear) y comprimido: en pixel art
    // se veia borroso al lado de los personajes. Se deja nitido y sin perder
    // el corte que ya tiene (6 globos).
    private static Sprite SignoDeExclamacion()
    {
        var importer = AssetImporter.GetAtPath(HojaDeExclamacion) as TextureImporter;
        if (importer != null && (importer.filterMode != FilterMode.Point ||
                                 importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Sprite signo = AssetDatabase.LoadAllAssetsAtPath(HojaDeExclamacion).OfType<Sprite>()
            .FirstOrDefault(s => s.name == SpriteDeExclamacion);
        if (signo == null) Debug.LogWarning($"[HistoriaDC] No encontre '{SpriteDeExclamacion}' en {HojaDeExclamacion}: el guardia te ve sin el '!'.");
        return signo;
    }

    // Pone el tile invisible en cada celda de MueblesQueFrenan que todavia no
    // frene. Devuelve cuantas puso. ReponerColisiones les da el Grid despues.
    private static int PonerMueblesQueFrenan(Scene scene)
    {
        Tilemap colisiones = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .FirstOrDefault(t => t.name == "Colisiones");
        if (colisiones == null) return 0;

        Tile invisible = TileInvisible();
        int puestas = 0;
        foreach (var (que, celdas) in MueblesQueFrenan)
            foreach (Vector2Int c in celdas)
            {
                var p = new Vector3Int(c.x, c.y, 0);
                if (colisiones.HasTile(p)) continue;
                colisiones.SetTile(p, invisible);
                colisiones.SetColliderType(p, Tile.ColliderType.Grid);
                puestas++;
            }
        return puestas;
    }

    private static Tile TileInvisible()
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(TileInvisiblePath);
        if (tile != null) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = null;
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(tile, TileInvisiblePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HistoriaDC] Creado {TileInvisiblePath}: tile sin sprite que solo frena.");
        return tile;
    }

    // Acuesta al personaje sobre el mueble de su celda. No hay hojas de gente
    // acostada en el pack: es el mismo sprite de frente girado 90 grados (la
    // cabeza a la izquierda), que en pixel art gira sin deformarse. El
    // collider no se mueve: se le habla desde la celda de abajo, como antes.
    private static void Acostar(GameObject npc)
    {
        Transform visual = npc.transform.Find("Visual");
        var sr = visual.GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        // El pivote son los pies: se corre medio cuerpo a la derecha para que
        // el cuerpo quede centrado en la celda y no sobre el apoyabrazos.
        float largo = sr.sprite.rect.height / sr.sprite.pixelsPerUnit * visual.localScale.y;
        visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
        visual.localPosition = new Vector3(largo * 0.5f, 0f, 0f);
        sr.sortingOrder = OrdenAcostado;
    }

    private static void PonerCosa(GameObject raiz, Cosa c)
    {
        var go = new GameObject(c.Nombre);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = Centro(c.Celda);
        // Obstacles: es la capa que mira el interactableMask del jugador. Va
        // sobre un mueble que ya frena, asi que no cambia por donde se camina.
        go.layer = CapaObstacles;
        go.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);

        DialogueDefinition dialogo = HistoriaAssets.Dialogo(T + c.Dialogo);
        var habla = go.AddComponent<NpcQueHabla>();
        var so = new SerializedObject(habla);
        so.FindProperty("dialogo").objectReferenceValue = dialogo;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (c.ConBrillo && dialogo != null)
            HistoriaAssets.Brillo(go, dialogo.grantsFlag, StoryFlag.None, Celeste);
    }

    private static void PonerZona(GameObject raiz, Zona z)
    {
        var go = new GameObject(z.Nombre);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(z.Desde.x + z.Tamanio.x * 0.5f, z.Desde.y + z.Tamanio.y * 0.5f, 0f);
        // Zona para PISAR: Default. En Obstacles el jugador frena en el borde.
        go.layer = CapaDefault;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(z.Tamanio.x - 0.2f, z.Tamanio.y - 0.2f);
        col.isTrigger = true;

        var zona = go.AddComponent<ZonaDeHistoria>();
        var so = new SerializedObject(zona);
        so.FindProperty("dialogo").objectReferenceValue = HistoriaAssets.Dialogo(T + z.Dialogo);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerSalida(GameObject raiz)
    {
        var go = new GameObject("SalidaDelNivel");
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = Centro(Salida);
        go.layer = CapaDefault;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 0.8f);
        col.isTrigger = true;
        go.AddComponent<SalidaDelNivel>();

        // El brillo va en un hijo y se prende recien con el nivel terminado (toma
        // el flag de la salida).
        var marca = new GameObject("Marca");
        marca.transform.SetParent(go.transform, false);
        HistoriaAssets.Brillo(marca, StoryFlag.None, StoryFlag.None, Verde);
    }

    // Nestor es una instancia del prefab NPC_Nestor: lo del guion se pone como
    // override de ESTA escena, sin tocar el prefab.
    private static bool ConfigurarNestor(Scene scene)
    {
        InteractableTrigger nestor = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<InteractableTrigger>(true))
            .FirstOrDefault(t => t.gameObject.name.StartsWith("NPC_Nestor"));
        if (nestor == null) return false;

        var so = new SerializedObject(nestor);
        so.FindProperty("dialogoAntes").objectReferenceValue = HistoriaAssets.Dialogo(T + "DC_Nestor_Antes");
        so.FindProperty("dialogoAntesRepetido").objectReferenceValue = HistoriaAssets.Dialogo(T + "DC_Nestor_AntesRepetido");
        so.FindProperty("dialogoAlGanar").objectReferenceValue = HistoriaAssets.Dialogo(T + "DC_Nestor_Despues");
        so.FindProperty("dialogoYaHecho").objectReferenceValue = HistoriaAssets.Dialogo(T + "DC_Nestor_YaHecho");
        // Sin esto, despues de ganarle volveria a ofrecer la pelea.
        so.FindProperty("hechoConFlag").enumValueIndex = (int)StoryFlag.Torre1_NestorLiberado;
        so.FindProperty("preguntaAntes").stringValue = "PROTOCOLO DE DEFENSA: LISTO.";
        so.FindProperty("nombreQuePregunta").stringValue = "SISTEMA";
        so.FindProperty("opcionSi").stringValue = "Pelear";
        so.FindProperty("opcionNo").stringValue = "Todavía no";
        so.FindProperty("mensajeSiRechaza").stringValue = "Protocolo en espera. No tardes: el sistema no espera.";
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(nestor);
        return true;
    }

    // Abrir la escena en el editor devuelve en memoria los colliderType del
    // asset (None) en vez de los Grid puestos por celda, y guardar en ese estado
    // los pierde tambien en disco (en el juego no se nota porque
    // ColisionesTilemapRuntimeFix los repone, pero la escena tiene que quedar
    // bien). Mismo arreglo que CasinoNpcBuilder: todo tile de Colisiones en Grid.
    private static void ReponerColisiones(Scene scene)
    {
        Tilemap colisiones = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .FirstOrDefault(t => t.name == "Colisiones");
        if (colisiones == null) return;

        int repuestas = 0;
        foreach (Vector3Int p in colisiones.cellBounds.allPositionsWithin)
        {
            if (!colisiones.HasTile(p) || colisiones.GetColliderType(p) == Tile.ColliderType.Grid) continue;
            colisiones.SetColliderType(p, Tile.ColliderType.Grid);
            repuestas++;
        }
        if (repuestas > 0) Debug.Log($"[HistoriaDC] {repuestas} celdas de Colisiones repuestas en Grid antes de guardar.");
    }

    // ---------- verificacion ----------

    // Cada persona sobre piso libre, cada cosa sobre un mueble, y todo se puede
    // agarrar con X desde una celda a la que se llega caminando desde el spawn.
    // Las barreras (los rayos) cuentan como abiertas: detras hay gente a la que
    // se llega apagandolas.
    private static List<string> Verificar(Scene scene, GameObject raiz)
    {
        var problemas = new List<string>();
        GameObject[] roots = scene.GetRootGameObjects();
        Tilemap colisiones = roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "Colisiones");
        Tilemap suelo = roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "Suelo");
        PlayerController jugador = roots.SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
        if (colisiones == null || suelo == null || jugador == null)
        {
            problemas.Add("faltan el tilemap Colisiones, el Suelo o el jugador.");
            return problemas;
        }

        var barreras = new HashSet<GameObject>(roots.SelectMany(r => r.GetComponentsInChildren<BarreraPorFlag>(true)).Select(b => b.gameObject));
        var solidos = roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == CapaObstacles &&
                        !(c is TilemapCollider2D) && !(c is CompositeCollider2D) &&
                        !barreras.Contains(c.gameObject))
            .Select(c => c.bounds).ToList();

        bool Bloqueada(Vector3Int c)
        {
            if (!suelo.HasTile(c) || colisiones.HasTile(c)) return true;
            Vector3 centro = colisiones.GetCellCenterWorld(c);
            return solidos.Any(b => b.Contains(new Vector3(centro.x, centro.y, b.center.z)));
        }

        // Los muebles que no frenan: sillas y mesas de Decorado, el sillon de
        // Techos. Todo tilemap que no sea el piso ni las colisiones.
        List<Tilemap> muebles = roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .Where(t => t != suelo && t != colisiones).ToList();

        foreach (Npc n in Npcs)
        {
            var c = new Vector3Int(n.Celda.x, n.Celda.y, 0);
            if (!suelo.HasTile(c) || colisiones.HasTile(c)) problemas.Add($"'{n.Nombre}' no esta sobre piso libre ({c.x},{c.y}).");

            Tilemap mueble = muebles.FirstOrDefault(t => t.HasTile(c));
            if (mueble != null && !n.Acostado)
                problemas.Add($"'{n.Nombre}' esta parado arriba de un mueble de {mueble.name} ({c.x},{c.y}): se lo ve subido a una silla.");
        }
        foreach (Cosa k in Cosas)
        {
            var c = new Vector3Int(k.Celda.x, k.Celda.y, 0);
            if (!colisiones.HasTile(c)) problemas.Add($"'{k.Nombre}' no esta sobre un mueble ({c.x},{c.y}): taparia el paso.");
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
                if (alcanzables.Contains(n) || Mathf.Abs(n.x - spawn.x) > 80 || Mathf.Abs(n.y - spawn.y) > 60) continue;
                if (Bloqueada(n)) continue;
                alcanzables.Add(n);
                cola.Enqueue(n);
            }
        }

        // TODO lo que se toca con X en la escena (no solo lo de la historia):
        // los muebles que ahora frenan no pueden dejar afuera un panel, una
        // jaula o el PC. Se mira cada celda que cubre su collider.
        foreach (MonoBehaviour m in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (!(m is IInteractable)) continue;
            var celdas = new List<Vector3Int>();
            foreach (Collider2D c in m.GetComponentsInChildren<Collider2D>(true))
            {
                Bounds b = c.bounds;
                for (int x = Mathf.FloorToInt(b.min.x + 0.05f); x <= Mathf.FloorToInt(b.max.x - 0.05f); x++)
                    for (int y = Mathf.FloorToInt(b.min.y + 0.05f); y <= Mathf.FloorToInt(b.max.y - 0.05f); y++)
                        celdas.Add(new Vector3Int(x, y, 0));
            }
            bool llega = celdas.Any(celda => dirs.Any(d => alcanzables.Contains(celda + d)));
            if (!llega) problemas.Add($"a '{m.name}' ({m.GetType().Name}) no se llega caminando.");
        }
        foreach (NpcQueHabla h in raiz.GetComponentsInChildren<NpcQueHabla>(true))
            if (h.Dialogo == null) problemas.Add($"'{h.name}' no tiene dialogo.");
        foreach (NpcQueCura e in raiz.GetComponentsInChildren<NpcQueCura>(true))
            if (e.Saludo == null) problemas.Add($"'{e.name}' cura pero no tiene saludo.");

        // El que vigila: parado adentro de su sala, y la sala se pisa.
        foreach (NpcQueTeDetecta v in raiz.GetComponentsInChildren<NpcQueTeDetecta>(true))
        {
            Rect sala = v.Sala;
            if (!sala.Contains((Vector2)v.transform.position)) problemas.Add($"'{v.name}' no esta adentro de la sala que vigila.");
            bool sePisa = alcanzables.Any(c => sala.Contains(new Vector2(c.x + 0.5f, c.y + 0.5f)));
            if (!sePisa) problemas.Add($"la sala que vigila '{v.name}' no se pisa.");
            if (v.YaPasoConFlag == StoryFlag.None) problemas.Add($"'{v.name}' vigila sin flag: se repetiria en cada partida cargada.");
        }

        // Un mueble que frena no puede caer donde hay alguien parado ni en el spawn.
        foreach (var (que, celdasDelMueble) in MueblesQueFrenan)
            foreach (Vector2Int c in celdasDelMueble)
            {
                var p = new Vector3Int(c.x, c.y, 0);
                if (p == spawn) problemas.Add($"un mueble que frena ({que}) cae en el spawn {p}.");
                if (Npcs.Any(n => n.Celda == c)) problemas.Add($"un mueble que frena ({que}) cae donde hay un NPC {p}.");
            }
        foreach (ZonaDeHistoria z in raiz.GetComponentsInChildren<ZonaDeHistoria>(true))
        {
            if (!alcanzables.Contains(colisiones.WorldToCell(z.transform.position))) problemas.Add($"la zona '{z.name}' no se pisa.");
            if (z.Dialogo == null) problemas.Add($"la zona '{z.name}' no tiene dialogo.");
        }
        SalidaDelNivel salida = raiz.GetComponentInChildren<SalidaDelNivel>(true);
        if (salida == null || !alcanzables.Contains(colisiones.WorldToCell(salida.transform.position)))
            problemas.Add("la salida no se pisa.");

        return problemas;
    }
}
