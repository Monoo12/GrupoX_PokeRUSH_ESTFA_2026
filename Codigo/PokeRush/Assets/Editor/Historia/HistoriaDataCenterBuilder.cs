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
        // Parada al lado de su silla, mirando la terminal.
        new Npc { Nombre = "Operaria", Hoja = "operaria", Celda = new Vector2Int(-2, 15),
                  Mira = CasinoNpcBuilder.Izquierda, Dialogo = "DC_Operaria" },
        // Ojo con las hojas: Personaje() las corta con nombres propios y cambia
        // los IDs de los sprites. Una hoja que otra escena ya usa cortada de otra
        // forma (la de 'tecnico', en EdificioNucleo) quedaria rota alla.
        new Npc { Nombre = "Tecnico", Hoja = "operario", Celda = new Vector2Int(31, 16),
                  Mira = CasinoNpcBuilder.Arriba, Dialogo = "DC_Tecnico" },
        // Delante de las mesas de la sala de descanso: tapa la silla de atras,
        // que es lo que corresponde en la vista 3/4.
        new Npc { Nombre = "Entusiasta", Hoja = "cerebrito", Celda = new Vector2Int(-35, 13),
                  Mira = CasinoNpcBuilder.Abajo, Dialogo = "DC_Entusiasta" },
        // Durmiendo en el sillon. Parado ahi lo tapaba el sillon (esta en
        // Techos, por encima de los personajes) y solo asomaba la coronilla.
        new Npc { Nombre = "EmpleadoDormido", Hoja = "chaval1", Celda = new Vector2Int(-45, 18),
                  Mira = CasinoNpcBuilder.Abajo, Dialogo = "DC_Dormido", Acostado = true },
        new Npc { Nombre = "GuardiaDeLaLiga", Hoja = "maton", Celda = new Vector2Int(-40, 22),
                  Mira = CasinoNpcBuilder.Izquierda, Dialogo = "DC_Guardia" },
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
        Debug.Log($"[HistoriaDC] Listo: {Npcs.Length} personas, {Cosas.Length} cosas, {Zonas.Length} zonas, " +
                  "salida en la recepcion, apagon y Nestor con su historia. Todo alcanzable. Guardada.");
    }

    // ---------- piezas ----------

    private static Vector3 Centro(Vector2Int celda) => new Vector3(celda.x + 0.5f, celda.y + 0.5f, 0f);

    private static void PonerNpc(GameObject raiz, Npc n)
    {
        GameObject go = CasinoNpcBuilder.Personaje(raiz, n.Nombre, Centro(n.Celda), n.Hoja, n.Mira);
        if (n.Acostado) Acostar(go);
        var habla = go.AddComponent<NpcQueHabla>();
        var so = new SerializedObject(habla);
        so.FindProperty("dialogo").objectReferenceValue = HistoriaAssets.Dialogo(T + n.Dialogo);
        so.ApplyModifiedPropertiesWithoutUndo();

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

        foreach (NpcQueHabla h in raiz.GetComponentsInChildren<NpcQueHabla>(true))
        {
            var celdas = h.GetComponentsInChildren<Collider2D>(true)
                .Select(c => colisiones.WorldToCell(c.bounds.center)).ToList();
            bool llega = celdas.Any(celda => dirs.Any(d => alcanzables.Contains(celda + d)));
            if (!llega) problemas.Add($"a '{h.name}' no se llega caminando.");
            if (h.Dialogo == null) problemas.Add($"'{h.name}' no tiene dialogo.");
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
