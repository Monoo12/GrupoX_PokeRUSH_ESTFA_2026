using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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
//   - Seis guardias iguales, tres por lado, mirando a la alfombra.
//   - Martin Reymar, un busto sentado atras de la mesa, y la ZONA que hay
//     delante de la mesa: pisarla arranca la pelea. Al jefe no se le habla.
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
    // quedan libres al costado de la alfombra (x = -3 y x = 3). La alfombra
    // (x = -2 a 2) y las dos columnas de los bordes quedan libres para caminar.
    private static readonly (string nombre, Vector2 celda, int mira)[] Guardias =
    {
        ("GuardiaPasillo1", new Vector2(-3.5f, 7.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo2", new Vector2(3.5f, 7.5f), CasinoNpcBuilder.Izquierda),
        ("GuardiaPasillo3", new Vector2(-3.5f, 12.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo4", new Vector2(3.5f, 12.5f), CasinoNpcBuilder.Izquierda),
        ("GuardiaPasillo5", new Vector2(-3.5f, 17.5f), CasinoNpcBuilder.Derecha),
        ("GuardiaPasillo6", new Vector2(3.5f, 17.5f), CasinoNpcBuilder.Izquierda)
    };

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
    // La mesa de cartas ocupa las celdas (-1..1, 29..30). La zona cubre el
    // frente de la mesa, que es por donde se llega caminando por la alfombra.
    private static readonly Vector2 CentroDeLaZona = new Vector2(0.5f, 27.5f);
    private static readonly Vector2 TamanoDeLaZona = new Vector2(5f, 3f);

    // Donde apoya el busto de Martin. Va 0,4 celdas METIDO en la mesa para que
    // esta le tape la base y se lea como sentado; el borde de arriba de la mesa
    // esta en y = 31.
    private static readonly Vector2 ApoyoDeMartin = new Vector2(0.5f, 30.6f);

    // Orden de dibujo del busto. Los NPCs van en 15 y la mesa vive en el tilemap
    // 'Colisiones', que dibuja en 10: con 5 Martin queda DETRAS de la mesa y
    // delante del piso, que es lo que hace el efecto de estar sentado.
    private const int OrdenDetrasDeLaMesa = 5;

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

                PonerAlJefe(raiz, jefe, ruleta);
            },
            PonerElAscensor);

        if (!ok) return;

        Debug.Log($"[PisoFinal] Listo: {Guardias.Length} guardias, el jefe y el ascensor de bajada.");
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
        visual.transform.localScale = Vector3.one * CasinoNpcBuilder.EscalaDelVisual;

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CasinoNpcBuilder.SpriteDePersonaje(SpriteDeMartin);
        sr.sortingOrder = OrdenDetrasDeLaMesa;
        if (sr.sprite == null)
            Debug.LogError($"[PisoFinal] No se pudo cargar {SpriteDeMartin}. " +
                           "Corre scripts/preparar_sprites_casino.py.");

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
        so.ApplyModifiedPropertiesWithoutUndo();
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
    // 26-29 contra los cuatro de nivel 21-23 del guardia VIP, que era lo mas
    // dificil hasta ahora. Equipo de mafioso (siniestro y fantasma) con Gengar
    // de as. El perfil Elite es un escalon arriba del GymLeader del guardia VIP
    // y todavia no es el FinalBoss, que queda para el jefe del juego.
    private static EnemyDefinition ArmarElJefe()
    {
        EnemyDefinition def = CasinoNpcBuilder.Enemigo(
            "JefeFinalCasino", "Reymar", 3000, StoryFlag.Casino_JefeFinalVencido,
            RetratoDelJefe, "Elite",
            ("0359_Absol", 26),
            ("0452_Drapion", 26),
            ("0625_Bisharp", 27),
            ("0553_Krookodile", 27),
            ("0461_Weavile", 28),
            ("0094_Gengar", 29));

        if (def != null)
        {
            def.battleBackground = CasinoNpcBuilder.SpriteSimple("Assets/Sprites/Battlebacks/InteriorRojo_bg.png");
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
