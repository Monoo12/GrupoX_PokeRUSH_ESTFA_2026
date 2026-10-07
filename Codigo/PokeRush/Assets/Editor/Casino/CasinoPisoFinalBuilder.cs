using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Arma el ULTIMO piso del casino: el pasillo de guardias y el jefe que espera
// atras de la mesa de poker.
//
//   PokeRush > Casino > Poner el piso final (pasillo y jefe)
//
// Va aparte de CasinoNpcBuilder a proposito. Ese menu rehace los pisos 1 a 3 de
// cero cada vez que se corre, asi que meter el piso final ahi adentro obligaria
// a tocar los otros tres para cambiar una linea de este. Lo que SI se reusa son
// sus herramientas (abrir/validar/guardar la escena, cortar las hojas de
// personaje, crear rivales y dialogos): ver CasinoNpcBuilder.ArmarPiso.
//
// Lo que pone, de abajo hacia arriba:
//
//   - El ascensor del fondo del pasillo, que solo BAJA. El ultimo piso no
//     tiene enfermeria: sin esto, perder contra el jefe deja al jugador
//     encerrado con el equipo debilitado.
//   - La enfermera, enfrente del PC del fondo del pasillo (playtest del 4-oct).
//   - Seis guardias iguales, tres por lado, mirando a la alfombra.
//   - Martin Reymar, un busto sentado atras de la mesa, y la ZONA que hay
//     delante de la mesa: pisarla arranca la pelea. Al jefe no se le habla.
//     La mesa y el busto van agrandados (la sala se veia vacia).
//   - La charla de Reymar de a tramos: cuatro ZonaDeHistoria en el camino
//     hasta la mesa, y una mas atras de la mesa para despues de ganarle (la
//     computadora del laboratorio y el servidor 02).
//
// TODO (equipo): los textos de los seis guardias son DE RELLENO. Los assets
// estan en Assets/Resources/Definitions/Casino/Dialogos/Historia_PisoFinal_Guardia*.asset
// y este builder NO los pisa si ya existen: se reescriben desde el Inspector
// cuando esten los dialogos de historia.
public static class CasinoPisoFinalBuilder
{
    private const string Escena = "Assets/Scenes/Casino piso FINAL.unity";
    private const string Contenedor = "InteraccionesCasino";
    private const string Items = "Assets/Graphics/Items/";
    private const int CapaObstacles = 3;
    private const int CapaDefault = 0;

    private const string HojaDeLosGuardias = "rocketElite";

    // Martin Reymar. Es un BUSTO: se lo ve de la cintura para arriba, sentado
    // atras de la mesa. Lo prepara scripts/preparar_sprites_casino.py.
    private const string SpriteDeMartin = "Assets/Sprites/Casino/martin_sentado.png";
    private const string RetratoDelJefe = "MARTIN";

    private const string NombreDelJefe = "REYMAR";

    // Los seis guardias del pasillo: tres de cada lado, sobre las columnas que
    // quedan libres al costado de la alfombra (x = -3 y x = 3, o sea centros
    // -2,5 y 3,5). La alfombra (x = -2 a 2) y las dos columnas de los bordes
    // quedan libres para caminar. Hasta el 4-oct los de la izquierda estaban
    // en -3,5: contra la pared y no al lado de la alfombra.
    private static readonly (string nombre, Vector2 celda, int mira)[] Guardias =
    {
        ("GuardiaPasillo1", new Vector2(-2.5f, 7.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo2", new Vector2(3.5f, 7.5f), CasinoNpcBuilder.Izquierda),
        ("GuardiaPasillo3", new Vector2(-2.5f, 12.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo4", new Vector2(3.5f, 12.5f), CasinoNpcBuilder.Izquierda),
        ("GuardiaPasillo5", new Vector2(-2.5f, 17.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo6", new Vector2(3.5f, 17.5f), CasinoNpcBuilder.Izquierda)
    };

    // La enfermera, en la esquina de abajo a la derecha del pasillo, enfrente
    // del PC (-3,5; 4,5): las dos cosas al bajar del ascensor, como en los pisos
    // 2 y 3. Arriba del PC quedaba parada sobre el monitor. Antes este piso no
    // tenia y perder contra Reymar obligaba a bajar a curarse.
    private static readonly Vector2 CeldaDeLaEnfermera = new Vector2(4.5f, 4.5f);

    // Relleno hasta que llegue la historia. Todos dicen lo mismo a proposito:
    // son seis guardias iguales de un mismo cordon de seguridad.
    private static readonly string[] LineasDeRelleno =
    {
        "Arriba no se sube. Adentro no se entra. Vos ya estás adentro, así que seguí.",
        "El jefe te está esperando. No te hagas el vivo.",
        "Yo de vos me daba vuelta. Pero nadie me escucha."
    };

    // Al jefe NO se le habla: la pelea la dispara una ZONA delante de la mesa.
    // Por eso el objeto vive donde esta la hitbox y el dibujo va como hijo,
    // corrido hasta la mesa.
    //
    // La mesa de cartas agrandada ocupa las celdas (-2..2, 28..30). La zona
    // cubre las dos filas de adelante, que es por donde se llega caminando por
    // la alfombra; los costados quedan libres para ir atras de la mesa.
    private static readonly Vector2 CentroDeLaZona = new Vector2(0.5f, 27f);
    private static readonly Vector2 TamanoDeLaZona = new Vector2(5f, 2f);

    // La mesa: la del tileset (3x2 tiles en Colisiones) dibujada una vez y
    // media mas grande, como hijo del jefe. En el tilemap queda en su lugar el
    // tile invisible que frena, en todo lo que ocupa la mesa grande y en la
    // fila del asiento de Martin. Crece hacia ABAJO: el borde de arriba sigue
    // en y = 31 y el busto no se mete en la pared del fondo.
    private const string TilesetDelCasino = "2D_TopDown_Tileset_Casino_1024x512";
    private const string TileInvisible = "Assets/Palettes/ColisionInvisible.asset";
    private const float EscalaDeLaMesa = 1.5f;
    private static readonly string[,] TilesDeLaMesa =   // [fila desde abajo, columna]
    {
        { "0413", "0414", "0415" },
        { "0381", "0382", "0383" }
    };
    private static readonly Vector2 EsquinaDeLaMesa = new Vector2(-1.75f, 28f);
    private static readonly RectInt CeldasDeLaMesa = new RectInt(-2, 28, 5, 3);
    private static readonly RectInt CeldasDelAsiento = new RectInt(-1, 31, 3, 1);

    // Donde apoya el busto de Martin: metido en la mesa para que esta le tape
    // la base y se lea como sentado. Las celdas llegan a y = 31, pero los
    // tiles de arriba tienen 17 filas transparentes: el borde que se VE esta
    // en y ~ 30,2. Con la base en 30,45 se veia el piso entre el cuerpo y la
    // mesa (playtest del 5-oct: "el cuerpo no debe estar separado de la
    // mesa"); en 29,85 la mesa le tapa 0,35. Va 1,4 veces mas grande que un
    // personaje.
    private static readonly Vector2 ApoyoDeMartin = new Vector2(0.5f, 29.85f);
    private const float EscalaDeMartin = 1.4f;

    // Orden de dibujo. El busto va DETRAS de la mesa (12) y delante de las
    // paredes del tilemap Colisiones (10): agrandado, la cabeza pasa por
    // delante de la pared del fondo. Los personajes van en 15.
    private const int OrdenDetrasDeLaMesa = 11;
    private const int OrdenDeLaMesa = 12;

    // La charla de antes de pelear (historia.pdf) de a tramos, mientras el PJ
    // camina hacia la mesa: el final del pasillo, el paso a la sala y dos filas
    // de la sala. Lo que queda lo dice al pisar la zona de la pelea
    // (Casino_Reymar_Antes). Cada tramo pide el anterior y ninguno suena si
    // Reymar ya se presento (partidas de antes de esto).
    private static readonly (string nombre, string dialogo, Vector2 centro, Vector2 tamano, StoryFlag requiere)[] Tramos =
    {
        ("ZonaReymar1", "Casino_Reymar_Tramo1", new Vector2(0.5f, 19.5f), new Vector2(9f, 1f), StoryFlag.None),
        ("ZonaReymar2", "Casino_Reymar_Tramo2", new Vector2(0.5f, 21.5f), new Vector2(3f, 1f), StoryFlag.Historia_ReymarTramo1),
        ("ZonaReymar3", "Casino_Reymar_Tramo3", new Vector2(0.5f, 23.5f), new Vector2(15f, 1f), StoryFlag.Historia_ReymarTramo2),
        ("ZonaReymar4", "Casino_Reymar_Tramo4", new Vector2(0.5f, 25.5f), new Vector2(15f, 1f), StoryFlag.Historia_ReymarTramo3)
    };

    // Despues de ganarle: Reymar manda al PJ atras de la mesa (mensajeVictoria)
    // y ahi, frente al vidrio del laboratorio, pasa lo de la computadora y el
    // servidor 02 (Casino_Reymar_Servidor). Un brillo marca el lugar.
    private static readonly Vector2 CentroDelLaboratorio = new Vector2(0.5f, 32f);
    private static readonly Vector2 TamanoDelLaboratorio = new Vector2(15f, 2f);
    private static readonly Vector2 BrilloDelLaboratorio = new Vector2(-3.5f, 0.6f);   // relativo a la zona
    private const string MensajeDeVictoria =
        "Vení, atrás de la mesa. Desde la computadora del laboratorio te muestro tus datos.";

    // El ascensor: sobre la pared del fondo del pasillo, debajo de la alfombra.
    private static readonly Vector2 CeldaDelAscensor = new Vector2(0.5f, 3.5f);

    [MenuItem("PokeRush/Casino/Poner el piso final (pasillo y jefe)")]
    public static void Poner()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PisoFinal] Sali del Play Mode antes de correr esto.");
            return;
        }

        Scene abierta = SceneManager.GetSceneByPath(Escena);
        if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
        {
            Debug.LogError("[PisoFinal] La escena esta abierta con cambios sin guardar. " +
                           "Guardala o descartala y volve a correr esto: no se toco nada.");
            return;
        }

        System.IO.Directory.CreateDirectory(CasinoNpcBuilder.DialogosFolder);

        EnemyDefinition jefe = ArmarElJefe();
        CasinoPrizeTable ruleta = TablaDeLaRuletaDelJefe();
        AssetDatabase.SaveAssets();

        bool ok = CasinoNpcBuilder.ArmarPiso(Escena,
            raiz =>
            {
                foreach (var g in Guardias)
                {
                    GameObject guardia = CasinoNpcBuilder.Personaje(raiz, g.nombre, g.celda, HojaDeLosGuardias, g.mira);
                    CasinoNpcBuilder.ConfigurarQueHabla(
                        guardia.AddComponent<NpcQueHabla>(),
                        CasinoNpcBuilder.Dialogo("Historia_PisoFinal_" + g.nombre, "GUARDIA", LineasDeRelleno));
                }

                CasinoNpcBuilder.Personaje(raiz, "EnfermeraCasino", CeldaDeLaEnfermera, "enfermera", CasinoNpcBuilder.Izquierda)
                    .AddComponent<CasinoNurseInteractable>();

                PonerAlJefe(raiz, jefe, ruleta);
            },
            escena =>
            {
                PonerElAscensor(escena);
                PonerLaMesaEnElTilemap(escena);
                PonerLosTramos(escena);
            });

        if (!ok) return;

        Debug.Log($"[PisoFinal] Listo: {Guardias.Length} guardias, la enfermera, el jefe con su mesa, " +
                  $"{Tramos.Length} tramos de charla y el ascensor de bajada.");
        Debug.LogWarning("[PisoFinal] TODO equipo: los textos de los guardias son de relleno " +
                         $"({CasinoNpcBuilder.DialogosFolder}/Historia_PisoFinal_Guardia*.asset).");
    }

    // ---------- el jefe en la escena ----------

    private static void PonerAlJefe(GameObject raiz, EnemyDefinition jefe, CasinoPrizeTable ruleta)
    {
        // El objeto ES la zona: su Collider2D en 'Is Trigger' es la hitbox que
        // dispara la pelea al pisarla. No se usa Personaje() porque ese arma un
        // NPC con el que se habla, y a Martin no se le habla.
        var go = new GameObject("JefeFinal");
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(CentroDeLaZona.x, CentroDeLaZona.y, 0f);

        // OJO: la zona va en Default, NO en Obstacles como el resto de las
        // interacciones del casino, y eso NO es un descuido.
        //
        // El proyecto tiene 'Queries Hit Triggers' prendido, y el jugador
        // decide si puede pisar una celda con
        // Physics2D.OverlapCircle(destino, radio, obstacleMask), donde
        // obstacleMask es justamente Obstacles. O sea que un trigger puesto en
        // Obstacles FRENA al jugador igual que una pared: se quedaria parado en
        // el borde de la zona y la pelea no arrancaria nunca.
        //
        // En Default no lo frena, y el trigger igual le llega: el jugador esta
        // en Default, tiene Rigidbody2D cinematico y Default choca con Default.
        go.layer = CapaDefault;

        var zona = go.AddComponent<BoxCollider2D>();
        zona.size = TamanoDeLaZona;
        zona.isTrigger = true;

        // Martin sentado atras de la mesa. Va como HIJO y con su posicion puesta
        // en mundo (no relativa): la zona y el dibujo estan en lugares
        // distintos a proposito.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.position = new Vector3(ApoyoDeMartin.x, ApoyoDeMartin.y, 0f);
        visual.transform.localScale = Vector3.one * (CasinoNpcBuilder.EscalaDelVisual * EscalaDeMartin);

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CasinoNpcBuilder.SpriteDePersonaje(SpriteDeMartin);
        sr.sortingOrder = OrdenDetrasDeLaMesa;
        if (sr.sprite == null)
            Debug.LogError($"[PisoFinal] No se pudo cargar {SpriteDeMartin}. " +
                           "Corre scripts/preparar_sprites_casino.py.");

        DibujarLaMesa(go.transform);

        var npc = go.AddComponent<CasinoJefeFinalNpc>();
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = NombreDelJefe;
        so.FindProperty("retrato").objectReferenceValue =
            CasinoNpcBuilder.SpriteRetrato($"Assets/Graphics/Trainers/{RetratoDelJefe}.png");
        so.FindProperty("rival").objectReferenceValue = jefe;
        so.FindProperty("flagVencido").enumValueIndex = (int)StoryFlag.Casino_JefeFinalVencido;
        so.FindProperty("flagCasinoCompletado").enumValueIndex = (int)StoryFlag.Casino_Completado;
        so.FindProperty("ruletaDeCombate").objectReferenceValue = ruleta;
        so.FindProperty("turnosEntreGiros").intValue = 2;
        so.FindProperty("maximoDeRevanchas").intValue = 1;
        // La historia: lo que dice la primera vez y cuando se le apaga el chip.
        so.FindProperty("dialogoAntes").objectReferenceValue = HistoriaAssets.Dialogo("Casino/Dialogos/Casino_Reymar_Antes");
        so.FindProperty("dialogoDespues").objectReferenceValue = HistoriaAssets.Dialogo("Casino/Dialogos/Casino_Reymar_Despues");
        // Lo ultimo que dice despues de ganarle: manda al PJ atras de la mesa,
        // donde sigue la historia (ver PonerLosTramos).
        so.FindProperty("mensajeVictoria").stringValue = MensajeDeVictoria;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Los seis tiles de la mesa del tileset, agrandados. Van en mundo, no
    // relativos al jefe: la zona y la mesa estan en lugares distintos.
    private static void DibujarLaMesa(Transform jefe)
    {
        var mesa = new GameObject("Mesa");
        mesa.transform.SetParent(jefe, false);
        mesa.transform.position = Vector3.zero;

        for (int fila = 0; fila < TilesDeLaMesa.GetLength(0); fila++)
            for (int col = 0; col < TilesDeLaMesa.GetLength(1); col++)
            {
                Sprite sprite = TileDelCasino(TilesDeLaMesa[fila, col]);
                var pieza = new GameObject($"Mesa_{fila}{col}");
                pieza.transform.SetParent(mesa.transform, false);

                // Esquina de abajo a la izquierda de este tile, mas el pivote
                // del sprite (los tiles son de 1x1 unidad a 32 px).
                Vector2 pivote = sprite != null
                    ? new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height)
                    : new Vector2(0.5f, 0.5f);
                Vector2 esquina = EsquinaDeLaMesa + new Vector2(col, fila) * EscalaDeLaMesa;
                pieza.transform.position = esquina + pivote * EscalaDeLaMesa;
                pieza.transform.localScale = Vector3.one * EscalaDeLaMesa;

                var sr = pieza.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = OrdenDeLaMesa;
            }
    }

    // El tile (asset) de la mesa original en esa fila (desde abajo) y columna:
    // la mesa de Martin en la cueva usa los mismos (QuarchoCuevaBuilder).
    public static TileBase TileDeLaMesa(int fila, int col)
    {
        string nombre = $"{TilesetDelCasino}_{TilesDeLaMesa[fila, col]}";
        foreach (string guid in AssetDatabase.FindAssets($"{nombre} t:TileBase"))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null && tile.name == nombre) return tile;
        }
        Debug.LogError($"[PisoFinal] No se encontro el tile {nombre} del casino.");
        return null;
    }

    private static Sprite TileDelCasino(string numero)
    {
        string nombre = $"{TilesetDelCasino}_{numero}";
        foreach (string guid in AssetDatabase.FindAssets($"{TilesetDelCasino} t:Texture2D"))
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (o is Sprite s && s.name == nombre) return s;
        Debug.LogError($"[PisoFinal] No se encontro el tile {nombre} del casino.");
        return null;
    }

    // En el tilemap Colisiones la mesa grande y el asiento de Martin son el
    // tile invisible: frena sin dibujar (la mesa la dibuja DibujarLaMesa). Pisa
    // los tiles de la mesa original, que quedaban adentro.
    private static void PonerLaMesaEnElTilemap(Scene scene)
    {
        Tilemap colisiones = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
            .FirstOrDefault(t => t.name == "Colisiones");
        var invisible = AssetDatabase.LoadAssetAtPath<TileBase>(TileInvisible);
        if (colisiones == null || invisible == null)
        {
            Debug.LogError($"[PisoFinal] Falta el tilemap Colisiones o {TileInvisible}: la mesa no frena.");
            return;
        }

        foreach (RectInt zona in new[] { CeldasDeLaMesa, CeldasDelAsiento })
            foreach (Vector2Int c in zona.allPositionsWithin)
            {
                var celda = new Vector3Int(c.x, c.y, 0);
                colisiones.SetTile(celda, invisible);
                colisiones.SetColliderType(celda, Tile.ColliderType.Grid);
            }
    }

    // ---------- la charla de a tramos ----------

    // Van en su propio contenedor y no con los personajes: son zonas para pisar,
    // y la del paso a la sala esta sobre la alfombra (Decorado), donde el
    // tilemap Suelo no tiene tile y la verificacion de personajes la rechaza.
    private const string ContenedorDeLaCharla = "CharlaDeReymar";

    private static void PonerLosTramos(Scene scene)
    {
        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null) return;
        Transform vieja = mapaRaiz.transform.Find(ContenedorDeLaCharla);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
        var raiz = new GameObject(ContenedorDeLaCharla);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        foreach (var t in Tramos)
            ZonaDeCharla(raiz, t.nombre, t.centro, t.tamano, "Casino/Dialogos/" + t.dialogo,
                         t.requiere, StoryFlag.Historia_ReymarPresentado);

        GameObject laboratorio = ZonaDeCharla(raiz, "ZonaReymarServidor", CentroDelLaboratorio, TamanoDelLaboratorio,
                                              "Casino/Dialogos/Casino_Reymar_Servidor",
                                              StoryFlag.Casino_JefeFinalVencido, StoryFlag.None);

        // El brillo prende al ganarle y se apaga cuando paso lo del servidor.
        var brillo = new GameObject("Brillo");
        brillo.transform.SetParent(laboratorio.transform, false);
        brillo.transform.localPosition = BrilloDelLaboratorio;
        HistoriaAssets.Brillo(brillo, StoryFlag.Historia_ReymarServidor, StoryFlag.Casino_JefeFinalVencido,
                              new Color(0.45f, 0.85f, 1f, 0.55f));
    }

    // Una ZonaDeHistoria: en Default y en trigger (en Obstacles frenaria al
    // jugador, ver el comentario de PonerAlJefe).
    private static GameObject ZonaDeCharla(GameObject raiz, string nombre, Vector2 centro, Vector2 tamano,
                                           string dialogo, StoryFlag requiere, StoryFlag excluye)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(centro.x, centro.y, 0f);
        go.layer = CapaDefault;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = tamano;
        col.isTrigger = true;

        var zona = go.AddComponent<ZonaDeHistoria>();
        var so = new SerializedObject(zona);
        so.FindProperty("dialogo").objectReferenceValue = HistoriaAssets.Dialogo(dialogo);
        so.FindProperty("requiere").enumValueIndex = (int)requiere;
        so.FindProperty("excluye").enumValueIndex = (int)excluye;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // ---------- el ascensor ----------

    // Solo baja: es el ultimo piso, no hay a donde subir. Y tiene que estar, o
    // el que pierde contra el jefe se queda sin enfermeria y sin salida.
    private static void PonerElAscensor(Scene scene)
    {
        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null) return;

        Transform vieja = mapaRaiz.transform.Find(Contenedor);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var raiz = new GameObject(Contenedor);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        var go = new GameObject("Ascensor");
        go.transform.SetParent(raiz.transform);
        go.transform.position = new Vector3(CeldaDelAscensor.x, CeldaDelAscensor.y, 0f);
        go.layer = CapaObstacles;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(4f, 1f);
        col.isTrigger = true;

        var ascensor = go.AddComponent<CasinoElevatorInteractable>();
        var so = new SerializedObject(ascensor);
        // Vacio = este ascensor solo baja.
        so.FindProperty("escenaDestino").stringValue = "";
        so.FindProperty("requisito").enumValueIndex = (int)CasinoRequisitoTipo.Ninguno;
        so.FindProperty("otorgaFlag").enumValueIndex = (int)StoryFlag.None;
        // Al reves que en los pisos 1 a 3, aca las puertas estan en la pared de
        // ABAJO: la llegada por defecto (0, -1) deja al jugador afuera del mapa,
        // en negro y sin poder moverse. Se llega a la celda de arriba, sobre la
        // alfombra.
        so.FindProperty("llegada").vector2Value = Vector2.up;

        var abajo = new[]
        {
            ("Casino nivel 3", "PISO 3"),
            ("Casino Nivel 2", "PISO 2"),
            ("Casino Nivel 1", "PISO 1")
        };
        SerializedProperty pisos = so.FindProperty("pisosDeAbajo");
        pisos.arraySize = abajo.Length;
        for (int i = 0; i < abajo.Length; i++)
        {
            pisos.GetArrayElementAtIndex(i).FindPropertyRelative("escena").stringValue = abajo[i].Item1;
            pisos.GetArrayElementAtIndex(i).FindPropertyRelative("nombre").stringValue = abajo[i].Item2;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- assets ----------

    // El rival mas duro del casino, pero sin pasarse: seis Pokemon de nivel
    // 46-49, todos formas finales, contra los cuatro de nivel 48-49 del guardia
    // VIP (rebalanceo del 6-oct-2026; eran 24-27). Equipo de mafioso, todo
    // Siniestro, con Hydreigon de as: la linea de siempre (Absol, Drapion,
    // Bisharp ya Kingambit) mas Grimmsnarl y Tyranitar. En el simulador de
    // balance se le gana ~60% de las veces al primer intento. El perfil Elite
    // es un escalon arriba del GymLeader del guardia VIP y todavia no es el
    // FinalBoss, que queda para el jefe del juego.
    private static EnemyDefinition ArmarElJefe()
    {
        EnemyDefinition def = CasinoNpcBuilder.Enemigo(
            "JefeFinalCasino", "Reymar", 3000, StoryFlag.Casino_JefeFinalVencido,
            RetratoDelJefe, "Elite",
            ("0359_Absol", 46),
            ("0452_Drapion", 46),
            ("0861_Grimmsnarl", 47),
            ("0983_Kingambit", 47),
            ("0248_Tyranitar", 48),
            ("0635_Hydreigon", 49));

        if (def != null)
        {
            // Antes InteriorRojo (un degrade liso); desde el playtest del 5-oct
            // la arena grande, como el resto de los fondos (FondosDeCombateBuilder).
            def.battleBackground = CasinoNpcBuilder.SpriteSimple(FondosDeCombateBuilder.FondoDeReymar);
            EditorUtility.SetDirty(def);
        }
        return def;
    }

    // Los ocho gajos de la ruleta que aparece cada dos turnos en la pelea.
    //
    // Cuatro le pegan al jugador, dos lo ayudan, uno es moneda al aire y uno le
    // devuelve el giro. Suman 100 para que los pesos se lean como porcentajes,
    // aunque la ruleta los normaliza igual.
    //
    // Los iconos salen del pack de items de Essentials y son los que mejor se
    // leen con lo que hay: Ataque X para el buff, orbe toxico para el mordisco,
    // cola lenta para el turno perdido, placa en blanco para el cambio de tipo
    // y el monedero para el giro gratis.
    //
    // Se crea SOLO si no existe: los pesos son balance y el equipo los va a
    // querer tocar sin que un rebuild se los pise.
    private static CasinoPrizeTable TablaDeLaRuletaDelJefe()
    {
        string path = $"{CasinoNpcBuilder.DefinitionFolder}/RuletaDelJefeFinal.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = new[]
        {
            Gajo(EfectoDeRuleta.SubirAtaque, "Ataque", 15f, "XATTACK",
                 "La ruleta marca ATAQUE. La casa te da una mano."),
            Gajo(EfectoDeRuleta.BajarAtaque, "Ataque flojo", 13f, "BLACKSLUDGE",
                 "La ruleta marca ATAQUE FLOJO. Te aflojan el brazo."),
            Gajo(EfectoDeRuleta.BajarDefensa, "Defensa floja", 13f, "ABILITYSHIELD",
                 "La ruleta marca DEFENSA FLOJA. Te dejan al descubierto."),
            Gajo(EfectoDeRuleta.CambiarTipo, "Cambio de tipo", 12f, "BLANKPLATE",
                 "La ruleta marca CAMBIO DE TIPO. La casa te reparte otra carta."),
            Gajo(EfectoDeRuleta.SacarVida, "La casa cobra", 12f, "TOXICORB",
                 "La ruleta marca COMISION. La casa se cobra lo suyo."),
            Gajo(EfectoDeRuleta.DarPocion, "Poción de la casa", 14f, "SUPERPOTION",
                 "La ruleta marca POCIÓN. Invita la casa."),
            Gajo(EfectoDeRuleta.BloquearTurno, "Turno perdido", 11f, "LAGGINGTAIL",
                 "La ruleta marca TURNO PERDIDO. Se apagan las luces."),
            Gajo(EfectoDeRuleta.TirarDeNuevo, "Otro giro", 10f, "COINCASE",
                 "La ruleta marca OTRO GIRO. La ficha vuelve.")
        };

        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    private static Prize Gajo(EfectoDeRuleta efecto, string nombre, float peso, string icono, string cartel)
    {
        return new Prize
        {
            prizeName = nombre,
            article = "",
            amount = 1,
            probability = peso,
            icon = CasinoNpcBuilder.SpriteSimple($"{Items}{icono}.png"),
            efectoDeCombate = efecto,
            customMessage = cartel
        };
    }
}
