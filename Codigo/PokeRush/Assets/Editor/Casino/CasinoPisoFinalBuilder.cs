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
//   - El jefe, parado atras de la mesa. Se le habla desde el frente de la mesa.
//
// TODO (equipo):
//   1. Los textos de los seis guardias son DE RELLENO. Los assets estan en
//      Assets/Resources/Definitions/Casino/Dialogos/Historia_PisoFinal_Guardia*.asset
//      y este builder NO los pisa si ya existen: se reescriben desde el
//      Inspector cuando esten los dialogos de historia.
//   2. La skin y el retrato del jefe son PROVISORIOS (Giovanni del pack de
//      Essentials). El personaje es Martin Reymar: cuando esten sus sprites se
//      cambian aca (HojaDelJefe / RetratoDelJefe) y se vuelve a correr.
public static class CasinoPisoFinalBuilder
{
    private const string Escena = "Assets/Scenes/Casino piso FINAL.unity";
    private const string Contenedor = "InteraccionesCasino";
    private const string Items = "Assets/Graphics/Items/";
    private const int CapaObstacles = 3;

    // Provisorios: ver el TODO de arriba.
    private const string HojaDelJefe = "giovanni";
    private const string RetratoDelJefe = "GIOVANNI1";
    private const string HojaDeLosGuardias = "rocketElite";

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

    // Donde va el jefe y donde se le habla. La mesa de cartas ocupa las celdas
    // (-1..1, 29..30) y el jefe queda parado atras, en (0, 31). Como la mesa
    // tapa, el collider con el que se lo agarra es un hijo apoyado sobre ella:
    // el jugador se para en (0, 28), mira arriba y habla por encima de la mesa.
    private static readonly Vector2 CeldaDelJefe = new Vector2(0.5f, 31.5f);
    private static readonly Vector2 CeldaDeLaMesa = new Vector2(0.5f, 29.5f);

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
                         $"({CasinoNpcBuilder.DialogosFolder}/Historia_PisoFinal_Guardia*.asset), y la " +
                         $"skin del jefe es provisoria ('{HojaDelJefe}'): va la de Martin Reymar.");
    }

    // ---------- el jefe en la escena ----------

    private static void PonerAlJefe(GameObject raiz, EnemyDefinition jefe, CasinoPrizeTable ruleta)
    {
        GameObject go = CasinoNpcBuilder.Personaje(raiz, "JefeFinal", CeldaDelJefe, HojaDelJefe, CasinoNpcBuilder.Abajo);

        // El collider de la mesa. Va como hijo porque PlayerController resuelve
        // con GetComponentInParent: el jugador toca la mesa y contesta el jefe.
        var mesa = new GameObject("MesaDePoker");
        mesa.transform.SetParent(go.transform, false);
        mesa.transform.localPosition = new Vector3(CeldaDeLaMesa.x - CeldaDelJefe.x,
                                                   CeldaDeLaMesa.y - CeldaDelJefe.y, 0f);
        mesa.layer = CapaObstacles;
        var colMesa = mesa.AddComponent<BoxCollider2D>();
        // Los tres anchos de la mesa. Trigger: las celdas de la mesa ya las
        // bloquea el tilemap de Colisiones, esto es solo para poder hablarle.
        colMesa.size = new Vector2(2.9f, 0.9f);
        colMesa.isTrigger = true;

        var npc = go.AddComponent<CasinoJefeFinalNpc>();
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = NombreDelJefe;
        so.FindProperty("retrato").objectReferenceValue = jefe != null ? jefe.battleFrontSprite : null;
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
