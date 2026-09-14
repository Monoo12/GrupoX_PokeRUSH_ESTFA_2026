using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Pone los personajes de los pisos 1 a 3 del casino.
//
//   Piso 1: el guardia del ascensor (se le paga o se le gana; ganarle da la
//           ruleta de Pokemon), cuatro que pelean una vez (apostador,
//           malabarista, motorista y pokemaniaco), la enfermera, un anciano que
//           cuenta historia y ocho personas jugando a las tragaperras.
//   Piso 2: cuatro que pelean una vez (joven rica, modelo, medium y ladron), la
//           enfermera y una damisela que cuenta historia.
//   Piso 3: cuatro que pelean una vez (macarra, guay, veterano y karateka), la
//           enfermera y un empresario que cuenta historia.
//
// Los textos que ve el jugador van con tildes y enies: la caja de dialogo usa
// la fuente legacy de Unity, que las dibuja bien.
//
// Los que pelean una vez pueden dar la ruleta de premios de los NPCs (maximo dos
// por partida, ver CasinoRuletaDeNpcs). El ascensor del piso 1 pasa a pedir el
// pase del guardia en vez de plata juntada.
//
// Idempotente: borra el contenedor 'PersonajesCasino' de cada piso y lo vuelve a
// crear. Los assets que el equipo va a retocar (dialogos de historia y tablas de
// la ruleta) se crean SOLO si no existen: correrlo de nuevo no pisa los textos
// ni los porcentajes. Para regenerar uno, borralo y volve a correr esto.
//
// No toca la escena que este abierta con cambios: si alguno de los tres pisos
// tiene cambios sin guardar, no hace nada. Antes de guardar cada piso verifica
// que todos los interactuables se puedan alcanzar desde el spawn; si falla, no
// guarda ese piso.
public static class CasinoNpcBuilder
{
    private const string Piso1 = "Assets/Scenes/Casino Nivel 1.unity";
    private const string Piso2 = "Assets/Scenes/Casino Nivel 2.unity";
    private const string Piso3 = "Assets/Scenes/Casino nivel 3.unity";
    private static readonly string[] Escenas = { Piso1, Piso2, Piso3 };

    private const string Contenedor = "PersonajesCasino";
    private const string DefinitionFolder = "Assets/Resources/Definitions/Casino";
    private const string DialogosFolder = DefinitionFolder + "/Dialogos";
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Items = "Assets/Resources/Definitions/Items/";
    private const string Iconos = "Assets/Graphics/Ruleta/";
    private const string Hojas = "Assets/Graphics/Characters/";
    private const string Retratos = "Assets/Graphics/Trainers/";
    private const string PerfilesIA = "Assets/Resources/Definitions/AIProfiles/";

    private const int CapaObstacles = 3;

    // Las hojas de personaje del pack son 4x4: cuatro poses por direccion, en
    // el orden abajo / izquierda / derecha / arriba.
    private static readonly string[] Direcciones = { "abajo", "izq", "der", "arriba" };
    private const int Abajo = 0, Izquierda = 1, Derecha = 2, Arriba = 3;

    // Los NPCs que pueden dar la ruleta de premios al ganarles.
    private static readonly StoryFlag[] DanRuleta =
    {
        StoryFlag.Casino_ApostadorVencido,
        StoryFlag.Casino_MalabaristaVencido,
        StoryFlag.Casino_JovenRicaVencida,
        StoryFlag.Casino_ModeloVencida,
        StoryFlag.Casino_MacarraVencido,
        StoryFlag.Casino_GuayVencido,
        StoryFlag.Casino_MotoristaVencido,
        StoryFlag.Casino_PokemaniacoVencido,
        StoryFlag.Casino_MediumVencido,
        StoryFlag.Casino_LadronVencido,
        StoryFlag.Casino_VeteranoVencido,
        StoryFlag.Casino_KaratekaVencido
    };

    // Gente jugando a las tragaperras del piso 1: cada uno en un taburete,
    // mirando a su maquina. Nunca los dos taburetes de la misma maquina, y hay
    // maquinas sin nadie: todas se siguen pudiendo usar de frente o de costado.
    private static readonly (string nombre, Vector2 celda, string hoja, int mira, string hablante, string linea)[] Jugadores =
    {
        ("JugadorTragaperras1", new Vector2(-14.5f, 7.5f), "gordo", Derecha, "JUGADOR",
            "Shh... esta máquina está por pagar. La siento."),
        ("JugadorTragaperras2", new Vector2(-3.5f, 7.5f), "mujer1", Izquierda, "JUGADORA",
            "Llevo tres días en esta silla. Ya casi sale."),
        ("JugadorTragaperras3", new Vector2(15.5f, 7.5f), "guiri", Izquierda, "TURISTA",
            "Vine de vacaciones y no salí nunca del casino."),
        ("JugadorTragaperras4", new Vector2(-9.5f, 12.5f), "anciana2", Izquierda, "ABUELA",
            "Si sale triple, invito yo los caramelos."),
        ("JugadorTragaperras5", new Vector2(10.5f, 12.5f), "hombre1", Derecha, "JUGADOR",
            "No me mires, que me cortás la racha."),
        ("JugadorTragaperras6", new Vector2(-14.5f, 17.5f), "empresariaa", Derecha, "EMPRESARIA",
            "Esto es una inversión, no un vicio."),
        ("JugadorTragaperras7", new Vector2(4.5f, 17.5f), "mujer2", Derecha, "JUGADORA",
            "Perdí todo... pero la próxima sale, lo presiento."),
        ("JugadorTragaperras8", new Vector2(15.5f, 17.5f), "nobla", Izquierda, "SEÑORA",
            "Dicen que la del fondo está arreglada. Yo igual juego.")
    };

    private static readonly Dictionary<string, Sprite[]> _hojas = new Dictionary<string, Sprite[]>();

    [MenuItem("PokeRush/Casino/Poner los personajes de los pisos 1 a 3")]
    public static void PonerTodos()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[CasinoNPC] Sali del Play Mode antes de correr esto.");
            return;
        }

        foreach (string path in Escenas)
        {
            Scene abierta = SceneManager.GetSceneByPath(path);
            if (abierta.IsValid() && abierta.isLoaded && abierta.isDirty)
            {
                Debug.LogError($"[CasinoNPC] '{abierta.name}' esta abierta con cambios sin guardar. " +
                               "Guardala o descartala y volve a correr esto: no se toco nada.");
                return;
            }
        }

        _hojas.Clear();
        System.IO.Directory.CreateDirectory(DialogosFolder);

        // ---------- rivales ----------
        EnemyDefinition guardia = Enemigo("GuardiaAscensor", "Guardia", 300, StoryFlag.Casino_GuardiaVencido,
            "MATON", "GymLeader", ("0228_Houndour", 18), ("0552_Krokorok", 18), ("0093_Haunter", 19));
        EnemyDefinition apostador = Enemigo("ApostadorCasino", "Apostador", 100, StoryFlag.Casino_ApostadorVencido,
            "JUGON", "Trainer", ("0020_Raticate", 12), ("0064_Kadabra", 12));
        EnemyDefinition malabarista = Enemigo("MalabaristaCasino", "Malabarista", 120, StoryFlag.Casino_MalabaristaVencido,
            "MALABARISTA", "Trainer", ("0778_Mimikyu", 14), ("0707_Klefki", 14));
        EnemyDefinition jovenRica = Enemigo("JovenRicaCasino", "Joven rica", 150, StoryFlag.Casino_JovenRicaVencida,
            "JOVENRICA", "Trainer", ("0175_Togepi", 15), ("0302_Sableye", 16));
        EnemyDefinition modelo = Enemigo("ModeloCasino", "Modelo", 150, StoryFlag.Casino_ModeloVencida,
            "MODELO", "Trainer", ("0607_Litwick", 15), ("0353_Shuppet", 16));
        EnemyDefinition macarra = Enemigo("MacarraCasino", "Macarra", 180, StoryFlag.Casino_MacarraVencido,
            "MACARRA", "Trainer", ("0215_Sneasel", 17), ("0551_Sandile", 17));
        EnemyDefinition guay = Enemigo("GuayCasino", "Guay", 220, StoryFlag.Casino_GuayVencido,
            "GUAY", "Trainer", ("0207_Gligar", 17), ("0227_Skarmory", 18), ("0359_Absol", 18));
        EnemyDefinition motorista = Enemigo("MotoristaCasino", "Motorista", 110, StoryFlag.Casino_MotoristaVencido,
            "MOTORISTA", "Trainer", ("0240_Magby", 13), ("0111_Rhyhorn", 14));
        EnemyDefinition pokemaniaco = Enemigo("PokemaniacoCasino", "Pokemaníaco", 120, StoryFlag.Casino_PokemaniacoVencido,
            "POKEMANIACO", "Trainer", ("0239_Elekid", 13), ("0304_Aron", 14));
        EnemyDefinition medium = Enemigo("MediumCasino", "Médium", 160, StoryFlag.Casino_MediumVencido,
            "MEDIUM", "Trainer", ("0355_Duskull", 16), ("0092_Gastly", 16));
        EnemyDefinition ladron = Enemigo("LadronCasino", "Ladrón", 170, StoryFlag.Casino_LadronVencido,
            "LADRON", "Trainer", ("0624_Pawniard", 16), ("0303_Mawile", 17));
        EnemyDefinition veterano = Enemigo("VeteranoCasino", "Veterano", 200, StoryFlag.Casino_VeteranoVencido,
            "VETERANO", "Trainer", ("0232_Donphan", 18), ("0305_Lairon", 18));
        EnemyDefinition karateka = Enemigo("KaratekaCasino", "Karateka", 210, StoryFlag.Casino_KaratekaVencido,
            "KARATEKA", "Trainer", ("0057_Primeape", 18), ("0447_Riolu", 18));

        // ---------- ruletas y dialogos (solo si no existen) ----------
        CasinoPrizeTable ruletaNpcs = TablaDeLosNpcs();
        CasinoPrizeTable ruletaGuardia = TablaDelGuardia();

        DialogueDefinition anciano = Dialogo("Historia_Piso1_Anciano", "ANCIANO",
            "Llevo cuarenta años viniendo a este casino.",
            "Dicen que el dueño nunca baja de la oficina del último piso.",
            "Si llegás hasta arriba, después contame cómo es.");
        DialogueDefinition damisela = Dialogo("Historia_Piso2_Damisela", "DAMISELA",
            "La ruleta de este piso no es lo que parece.",
            "Vi gente entrar con todo su equipo y salir sin un solo Pokémon.",
            "Tené cuidado con lo que apostás.");
        DialogueDefinition empresario = Dialogo("Historia_Piso3_Empresario", "EMPRESARIO",
            "El blackjack es el único juego donde la casa no siempre gana.",
            "Por eso los de arriba vigilan cada mesa.",
            "Si ganás, hacelo sin llamar la atención.");

        AssetDatabase.SaveAssets();

        int ok = 0;

        if (ArmarPiso(Piso1, raiz =>
            {
                var g = Personaje(raiz, "GuardiaAscensor", new Vector2(3.5f, 19.5f), "maton", Abajo);
                ConfigurarGuardia(g.AddComponent<CasinoElevatorGuard>(), guardia, ruletaGuardia);

                var a = Personaje(raiz, "ApostadorCasino", new Vector2(-3.5f, 12.5f), "Lider 1 bw", Izquierda);
                ConfigurarEntrenador(a.AddComponent<CasinoTrainerNpc>(), "APOSTADOR", apostador,
                    StoryFlag.Casino_ApostadorVencido, ruletaNpcs,
                    "Eh, vos. Me quedé sin fichas. Te juego los últimos 100 a los Pokémon. ¿Dale?",
                    "Dale", "Paso", "Ma sí. Andate.",
                    "Tomalos... 100. Me voy a la máquina de al lado.",
                    "Ya me dejaste seco. Andate.");

                var m = Personaje(raiz, "MalabaristaCasino", new Vector2(9.5f, 15.5f), "malabarista", Abajo);
                ConfigurarEntrenador(m.AddComponent<CasinoTrainerNpc>(), "MALABARISTA", malabarista,
                    StoryFlag.Casino_MalabaristaVencido, ruletaNpcs,
                    "Mientras las máquinas giran, yo hago malabares con Pokémon. ¿Te animás a una pelea?",
                    "Pelear", "Ahora no", "Otra vez será.",
                    "Se me cayó todo... Bien ahí.",
                    "Ya me ganaste. Sigo practicando.");

                var mo2 = Personaje(raiz, "MotoristaCasino", new Vector2(-10.5f, 4.5f), "motorista", Abajo);
                ConfigurarEntrenador(mo2.AddComponent<CasinoTrainerNpc>(), "MOTORISTA", motorista,
                    StoryFlag.Casino_MotoristaVencido, ruletaNpcs,
                    "¿Viste la moto en la puerta? La gané en estas máquinas. ¿Peleamos?",
                    "Pelear", "Ahora no", "Otra vez será.",
                    "Me ganaste... Voy a tener que vender la moto.",
                    "Ya me ganaste. Voy a buscar la moto.");

                var pk = Personaje(raiz, "PokemaniacoCasino", new Vector2(-9.5f, 15.5f), "pokemaniaco", Abajo);
                ConfigurarEntrenador(pk.AddComponent<CasinoTrainerNpc>(), "POKEMANÍACO", pokemaniaco,
                    StoryFlag.Casino_PokemaniacoVencido, ruletaNpcs,
                    "¡Colecciono Pokémon raros! ¿Querés ver mi colección en combate?",
                    "¡Dale!", "No", "Te lo perdés.",
                    "Mi colección... ¡nooo!",
                    "Mi colección necesita un descanso.");

                Personaje(raiz, "EnfermeraCasino", new Vector2(4.5f, 12.5f), "enfermera", Derecha)
                    .AddComponent<CasinoNurseInteractable>();

                ConfigurarQueHabla(Personaje(raiz, "AncianoHistoria", new Vector2(5.5f, 4.5f), "anciano1", Abajo)
                    .AddComponent<NpcQueHabla>(), anciano);

                foreach (var j in Jugadores)
                {
                    var jugador = Personaje(raiz, j.nombre, j.celda, j.hoja, j.mira);
                    ConfigurarQueHabla(jugador.AddComponent<NpcQueHabla>(),
                        Dialogo(j.nombre, j.hablante, j.linea));
                    jugador.AddComponent<NpcJugandoMaquina>();
                }
            },
            PedirPaseEnElAscensor)) ok++;

        if (ArmarPiso(Piso2, raiz =>
            {
                var j = Personaje(raiz, "JovenRicaCasino", new Vector2(-10.5f, 13.5f), "jovenrica", Abajo);
                ConfigurarEntrenador(j.AddComponent<CasinoTrainerNpc>(), "JOVEN RICA", jovenRica,
                    StoryFlag.Casino_JovenRicaVencida, ruletaNpcs,
                    "Las fichas me aburren. Una pelea de verdad me vendría bien. ¿Aceptás?",
                    "Acepto", "No", "Qué decepción.",
                    "Qué forma tan poco elegante de ganar... pero ganaste.",
                    "No insistas, ya perdí una vez.");

                var mo = Personaje(raiz, "ModeloCasino", new Vector2(-5.5f, 6.5f), "modelo", Abajo);
                ConfigurarEntrenador(mo.AddComponent<CasinoTrainerNpc>(), "MODELO", modelo,
                    StoryFlag.Casino_ModeloVencida, ruletaNpcs,
                    "Si me ganás, te dejo sacarte una foto conmigo. ¿Peleamos?",
                    "Peleamos", "Paso", "Vos te lo perdés.",
                    "Hoy no era mi día.",
                    "Ya tuviste tu foto. Chau.");

                var me = Personaje(raiz, "MediumCasino", new Vector2(-10.5f, 18.5f), "medium", Abajo);
                ConfigurarEntrenador(me.AddComponent<CasinoTrainerNpc>(), "MÉDIUM", medium,
                    StoryFlag.Casino_MediumVencido, ruletaNpcs,
                    "Las cartas me dijeron que ibas a venir. También dicen que vas a perder. ¿Probamos?",
                    "Probemos", "No", "Ya lo sabía.",
                    "Esto no estaba en las cartas.",
                    "Las cartas no me hablan desde que me ganaste.");

                var la = Personaje(raiz, "LadronCasino", new Vector2(14.5f, 10.5f), "ladron", Izquierda);
                ConfigurarEntrenador(la.AddComponent<CasinoTrainerNpc>(), "LADRÓN", ladron,
                    StoryFlag.Casino_LadronVencido, ruletaNpcs,
                    "Shh... nadie vio nada. Si me ganás, no te toco las fichas. ¿Trato?",
                    "Trato", "Ni loco", "Cuidá tus bolsillos.",
                    "Está bien, está bien. Tus fichas están a salvo.",
                    "Yo no fui. Ni me mires.");

                Personaje(raiz, "EnfermeraCasino", new Vector2(-3.5f, 4.5f), "enfermera", Abajo)
                    .AddComponent<CasinoNurseInteractable>();

                ConfigurarQueHabla(Personaje(raiz, "DamiselaHistoria", new Vector2(-13.5f, 18.5f), "damisela", Abajo)
                    .AddComponent<NpcQueHabla>(), damisela);
            })) ok++;

        if (ArmarPiso(Piso3, raiz =>
            {
                var ma = Personaje(raiz, "MacarraCasino", new Vector2(4.5f, 10.5f), "macarra", Izquierda);
                ConfigurarEntrenador(ma.AddComponent<CasinoTrainerNpc>(), "MACARRA", macarra,
                    StoryFlag.Casino_MacarraVencido, ruletaNpcs,
                    "Este piso es mío. Si querés pasear por acá, primero me ganás.",
                    "Pelear", "Me voy", "Eso, andá.",
                    "Bueno, bueno... el piso es tuyo.",
                    "Ya está, ya está. Andá nomás.");

                var gu = Personaje(raiz, "GuayCasino", new Vector2(10.5f, 5.5f), "guay", Abajo);
                ConfigurarEntrenador(gu.AddComponent<CasinoTrainerNpc>(), "GUAY", guay,
                    StoryFlag.Casino_GuayVencido, ruletaNpcs,
                    "Llegar al tercer piso no es suerte. Mostrame que sabés pelear.",
                    "Pelear", "Todavía no", "Cuando quieras.",
                    "Tenés nivel. Nos vemos arriba.",
                    "Ya me ganaste. Respeto.");

                var ve = Personaje(raiz, "VeteranoCasino", new Vector2(-13.5f, 12.5f), "veterano", Abajo);
                ConfigurarEntrenador(ve.AddComponent<CasinoTrainerNpc>(), "VETERANO", veterano,
                    StoryFlag.Casino_VeteranoVencido, ruletaNpcs,
                    "Llevo treinta años en este casino. Vi caer a muchos como vos. ¿Te animás?",
                    "Me animo", "Todavía no", "Sabia decisión.",
                    "Hacía años que no perdía así. Bien jugado.",
                    "Ya me ganaste, pibe. Andá para arriba.");

                var ka = Personaje(raiz, "KaratekaCasino", new Vector2(6.5f, 4.5f), "luchador", Abajo);
                ConfigurarEntrenador(ka.AddComponent<CasinoTrainerNpc>(), "KARATEKA", karateka,
                    StoryFlag.Casino_KaratekaVencido, ruletaNpcs,
                    "En el tercer piso no se juega: se pelea. ¡En guardia!",
                    "¡En guardia!", "Paso", "Volvé cuando estés listo.",
                    "Tu técnica es superior. Me inclino.",
                    "Voy a seguir entrenando.");

                Personaje(raiz, "EnfermeraCasino", new Vector2(-5.5f, 4.5f), "enfermera", Abajo)
                    .AddComponent<CasinoNurseInteractable>();

                ConfigurarQueHabla(Personaje(raiz, "EmpresarioHistoria", new Vector2(-9.5f, 13.5f), "empresario", Abajo)
                    .AddComponent<NpcQueHabla>(), empresario);
            })) ok++;

        AssetDatabase.SaveAssets();

        if (ok == Escenas.Length)
            Debug.Log("[CasinoNPC] TERMINADO: personajes puestos en los tres pisos.");
        else
            Debug.LogError($"[CasinoNPC] Solo {ok} de {Escenas.Length} pisos quedaron guardados. Mira los errores de arriba.");
    }

    // ---------- escenas ----------

    private static bool ArmarPiso(string path, System.Action<GameObject> poner, System.Action<Scene> extra = null)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool yaAbierta = scene.IsValid() && scene.isLoaded;
        if (!yaAbierta) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null)
        {
            Debug.LogError($"[CasinoNPC] '{scene.name}' no tiene MapaRaiz: corre primero " +
                           "PokeRush/Casino/Armar el rig de las escenas del casino.");
            Cerrar(scene, yaAbierta, path);
            return false;
        }

        Transform vieja = mapaRaiz.transform.Find(Contenedor);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var raiz = new GameObject(Contenedor);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        poner(raiz);
        extra?.Invoke(scene);
        ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = Verificar(scene, raiz);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[CasinoNPC] {scene.name}: {p}");
            Debug.LogError($"[CasinoNPC] {scene.name}: NO se guarda.");
            Cerrar(scene, yaAbierta, path);
            return false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CasinoNPC] {scene.name}: {raiz.transform.childCount} personajes, todo alcanzable. Guardada.");
        if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
        return true;
    }

    // Abrir la escena en el editor pierde en silencio los colliderType = Grid
    // puestos por celda (en disco siguen, en memoria vuelven a los del asset), y
    // guardar en ese estado los pierde tambien en disco. Paso con los pisos 2 y 3
    // la primera vez que se corrio esto. En el juego no se nota porque
    // ColisionesTilemapRuntimeFix los repone en Awake, pero la escena tiene que
    // quedar bien guardada.
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

        if (repuestas > 0)
            Debug.Log($"[CasinoNPC] {scene.name}: {repuestas} celdas de Colisiones repuestas en Grid antes de guardar.");
    }

    // Descarta lo que se hizo en memoria.
    private static void Cerrar(Scene scene, bool yaAbierta, string path)
    {
        if (yaAbierta) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        else EditorSceneManager.CloseScene(scene, true);
    }

    // El ascensor del piso 1 ya no pide plata juntada: pide el pase del guardia.
    private static void PedirPaseEnElAscensor(Scene scene)
    {
        var ascensor = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<CasinoElevatorInteractable>(true))
            .FirstOrDefault();
        if (ascensor == null)
        {
            Debug.LogError("[CasinoNPC] El piso 1 no tiene CasinoElevatorInteractable.");
            return;
        }

        var so = new SerializedObject(ascensor);
        so.FindProperty("requisito").enumValueIndex = (int)CasinoRequisitoTipo.Flag;
        so.FindProperty("flagRequerido").enumValueIndex = (int)StoryFlag.Casino_Piso1_PaseAscensor;
        so.FindProperty("mensajeFaltaObjetivo").stringValue =
            "El guardia no te deja subir. Hablá con él: se le paga o se le gana.";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- verificacion ----------

    // Replica PlayerController con los datos de ESTA escena (no con Physics2D:
    // los tres pisos usan las mismas coordenadas, y si hay otro piso abierto sus
    // colliders se mezclarian con los de este).
    private static List<string> Verificar(Scene scene, GameObject personajes)
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

        var soJugador = new SerializedObject(jugador);
        float radioChoque = soJugador.FindProperty("collisionCheckRadius").floatValue;
        float radioInteraccion = soJugador.FindProperty("interactionRadius").floatValue;
        int mascaraInteraccion = soJugador.FindProperty("interactableMask").intValue;

        // Cada personaje nuevo parado en piso, sin pared ni otro personaje encima.
        var celdasDePersonajes = new HashSet<Vector3Int>();
        foreach (Transform t in personajes.transform)
        {
            Vector3Int celda = colisiones.WorldToCell(t.position);
            if (!suelo.HasTile(celda)) problemas.Add($"'{t.name}' esta fuera del piso ({celda.x},{celda.y}).");
            if (colisiones.HasTile(celda)) problemas.Add($"'{t.name}' esta sobre una pared o mueble ({celda.x},{celda.y}).");
            if (!celdasDePersonajes.Add(celda)) problemas.Add($"'{t.name}' comparte celda con otro personaje ({celda.x},{celda.y}).");
        }

        // Obstaculos solidos (NPCs y demas) de esta escena.
        var solidos = roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
                        c.gameObject.layer == CapaObstacles &&
                        !(c is TilemapCollider2D) && !(c is CompositeCollider2D))
            .Select(c => c.bounds).ToList();

        bool Bloqueada(Vector3Int c)
        {
            if (colisiones.HasTile(c)) return true;
            Vector3 centro = colisiones.GetCellCenterWorld(c);
            return solidos.Any(b => Toca(b, centro, radioChoque));
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
                if (alcanzables.Contains(n) || Mathf.Abs(n.x - spawn.x) > 60 || Mathf.Abs(n.y - spawn.y) > 60) continue;
                if (Bloqueada(n)) continue;
                alcanzables.Add(n);
                cola.Enqueue(n);
            }
        }

        // Cada interactuable tiene que poder agarrarse con X desde alguna celda
        // a la que se llega caminando.
        foreach (MonoBehaviour mb in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (!(mb is IInteractable) || !mb.gameObject.activeInHierarchy) continue;

            var suyos = mb.GetComponentsInChildren<Collider2D>(true)
                .Where(c => c.enabled && ((1 << c.gameObject.layer) & mascaraInteraccion) != 0)
                .Select(c => c.bounds).ToList();
            if (suyos.Count == 0)
            {
                problemas.Add($"'{mb.name}' ({mb.GetType().Name}) no tiene collider en una capa que el jugador pueda interactuar.");
                continue;
            }

            bool llega = alcanzables.Any(c => dirs.Any(d =>
            {
                Vector3 centro = colisiones.GetCellCenterWorld(c + d);
                return suyos.Any(b => Toca(b, centro, radioInteraccion));
            }));
            if (!llega) problemas.Add($"'{mb.name}' ({mb.GetType().Name}) no se puede alcanzar desde el spawn.");
        }

        return problemas;
    }

    private static bool Toca(Bounds b, Vector3 centro, float radio)
    {
        float x = Mathf.Clamp(centro.x, b.min.x, b.max.x);
        float y = Mathf.Clamp(centro.y, b.min.y, b.max.y);
        float dx = centro.x - x, dy = centro.y - y;
        return dx * dx + dy * dy < radio * radio;
    }

    // ---------- personajes ----------

    private static GameObject Personaje(GameObject raiz, string nombre, Vector2 celda, string hoja, int mira)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(celda.x, celda.y, 0f);

        // Capa Obstacles: es la que mira el interactableMask del jugador, y de
        // paso hace que el personaje frene al que quiera atravesarlo.
        go.layer = CapaObstacles;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        // El sprite tiene el pivote abajo: se lo baja media celda para que los
        // pies queden apoyados en el piso.
        visual.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = Frame(Hoja(hoja), mira);
        // Mismo sorting que el jugador: con el Renderer2D en Custom Axis (0,1,0)
        // se tapan entre ellos segun la altura.
        sr.sortingOrder = 15;

        if (sr.sprite == null) Debug.LogWarning($"[CasinoNPC] '{nombre}' quedo sin sprite ({hoja}).");
        return go;
    }

    private static void ConfigurarEntrenador(CasinoTrainerNpc npc, string nombre, EnemyDefinition rival,
        StoryFlag flagVencido, CasinoPrizeTable ruleta, string pregunta, string si, string no,
        string rechazo, string victoria, string yaVencido)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = nombre;
        so.FindProperty("retrato").objectReferenceValue = rival != null ? rival.battleFrontSprite : null;
        so.FindProperty("rival").objectReferenceValue = rival;
        so.FindProperty("flagVencido").enumValueIndex = (int)flagVencido;
        so.FindProperty("ruleta").objectReferenceValue = ruleta;
        so.FindProperty("pregunta").stringValue = pregunta;
        so.FindProperty("opcionSi").stringValue = si;
        so.FindProperty("opcionNo").stringValue = no;
        so.FindProperty("mensajeSiRechaza").stringValue = rechazo;
        so.FindProperty("mensajeVictoria").stringValue = victoria;
        so.FindProperty("mensajeYaVencido").stringValue = yaVencido;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarGuardia(CasinoElevatorGuard npc, EnemyDefinition rival, CasinoPrizeTable ruleta)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = "GUARDIA";
        so.FindProperty("retrato").objectReferenceValue = rival != null ? rival.battleFrontSprite : null;
        so.FindProperty("rival").objectReferenceValue = rival;
        so.FindProperty("ruletaDePokemon").objectReferenceValue = ruleta;
        so.FindProperty("precio").intValue = 500;
        so.FindProperty("flagPase").enumValueIndex = (int)StoryFlag.Casino_Piso1_PaseAscensor;
        so.FindProperty("flagVencido").enumValueIndex = (int)StoryFlag.Casino_GuardiaVencido;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarQueHabla(NpcQueHabla npc, DialogueDefinition dialogo)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("dialogo").objectReferenceValue = dialogo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- assets ----------

    // Los rivales se actualizan siempre (equipo, plata, flag): son balance del
    // builder, no texto del equipo.
    private static EnemyDefinition Enemigo(string archivo, string nombre, int plata, StoryFlag flag,
        string retrato, string perfil, params (string especie, int nivel)[] equipo)
    {
        string path = $"{DefinitionFolder}/{archivo}.asset";
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<EnemyDefinition>();

        def.characterName = nombre;
        def.moneyReward = plata;
        def.defeatGrantsFlag = flag;
        def.fleeIfWeak = false;
        def.pokenTeam = equipo.Select(e =>
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{e.especie}.asset");
            if (especie == null) Debug.LogError($"[CasinoNPC] No existe la especie {e.especie}.");
            return new EnemyTeamEntry { species = especie, level = e.nivel };
        }).ToArray();
        def.aiProfile = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>($"{PerfilesIA}{perfil}.asset");
        def.battleFrontSprite = SpriteRetrato($"{Retratos}{retrato}.png");

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static DialogueDefinition Dialogo(string archivo, string hablante, params string[] lineas)
    {
        string path = $"{DialogosFolder}/{archivo}.asset";
        var def = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path);
        if (def != null) return def; // el equipo lo reescribe: no se pisa

        def = ScriptableObject.CreateInstance<DialogueDefinition>();
        def.speakerName = hablante;
        def.lines = lineas;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // La ruleta de los NPCs: 35% Pokemon, 40% curativos, 25% plata.
    private static CasinoPrizeTable TablaDeLosNpcs()
    {
        string path = $"{DefinitionFolder}/RuletaDeLosNpcs.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla == null)
        {
            tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
            tabla.premios = new[]
            {
                PremioPokemon("0025_Pikachu", 15, 13, "icon_pokeball", 300),
                PremioObjeto("Pocion", "Poción", "Pociones", "una", 3, 17, "icon_pocion"),
                PremioPlata(150, 17),
                PremioPokemon("0133_Eevee", 15, 12, "icon_greatball", 300),
                PremioObjeto("SuperPocion", "Superpoción", "Superpociones", "una", 2, 15, "icon_superpocion"),
                PremioPokemon("0175_Togepi", 15, 10, "icon_pokeball", 300),
                PremioObjeto("HiperPocion", "Hiperpoción", "Hiperpociones", "una", 1, 8, "icon_hiperpocion"),
                PremioPlata(400, 8)
            };
            AssetDatabase.CreateAsset(tabla, path);
        }

        // La lista de quienes la dan si se mantiene al dia: depende de los NPCs
        // que pone este builder, no es balance.
        tabla.npcsQueLaPuedenDar = (StoryFlag[])DanRuleta.Clone();
        EditorUtility.SetDirty(tabla);
        return tabla;
    }

    // La ruleta del guardia: siempre un Pokemon. El icono marca la rareza.
    //   Poke Ball  (comun):    4 x 15% = 60%
    //   Great Ball (bueno):    2 x 12% = 24%
    //   Ultra Ball (raro):     1 x 12% = 12%
    //   Master Ball (muy raro): 1 x 4% =  4%
    private static CasinoPrizeTable TablaDelGuardia()
    {
        string path = $"{DefinitionFolder}/RuletaDelGuardia.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = new[]
        {
            PremioPokemon("0063_Abra", 15, 15, "icon_pokeball", 500),
            PremioPokemon("0133_Eevee", 16, 12, "icon_greatball", 500),
            PremioPokemon("0066_Machop", 15, 15, "icon_pokeball", 500),
            PremioPokemon("0123_Scyther", 17, 12, "icon_ultraball", 500),
            PremioPokemon("0081_Magnemite", 15, 15, "icon_pokeball", 500),
            PremioPokemon("0137_Porygon", 16, 12, "icon_greatball", 500),
            PremioPokemon("0228_Houndour", 15, 15, "icon_pokeball", 500),
            PremioPokemon("0147_Dratini", 18, 4, "icon_masterball", 500)
        };
        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    private static Prize PremioPokemon(string especie, int nivel, float peso, string icono, int siEquipoLleno)
    {
        var def = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{especie}.asset");
        if (def == null) Debug.LogError($"[CasinoNPC] No existe la especie {especie}.");
        return new Prize
        {
            prizeName = def != null ? def.pokenName : especie,
            article = "",
            amount = 1,
            probability = peso,
            icon = SpriteSimple($"{Iconos}{icono}.png"),
            poken = def,
            pokenLevel = nivel,
            moneyIfTeamFull = siEquipoLleno
        };
    }

    private static Prize PremioObjeto(string asset, string nombre, string plural, string articulo,
                                      int cantidad, float peso, string icono)
    {
        var item = AssetDatabase.LoadAssetAtPath<HealItemDefinition>($"{Items}{asset}.asset");
        if (item == null) Debug.LogError($"[CasinoNPC] No existe el objeto {asset}.");
        return new Prize
        {
            prizeName = nombre,
            prizePlural = plural,
            article = articulo,
            amount = cantidad,
            probability = peso,
            icon = SpriteSimple($"{Iconos}{icono}.png"),
            item = item
        };
    }

    private static Prize PremioPlata(int monedas, float peso)
    {
        return new Prize
        {
            prizeName = $"{monedas} monedas",
            article = "",
            amount = 1,
            probability = peso,
            icon = SpriteSimple($"{Iconos}icon_monedas.png"),
            money = monedas
        };
    }

    // ---------- sprites ----------

    // Los retratos de rival (Graphics/Trainers) se usan en la caja de dialogo y
    // en el combate. Vienen del pack con importaciones sueltas: algunos en modo
    // Multiple con el recorte automatico (MALABARISTA quedo partido en 4 y el
    // retrato mostraba un pedazo agrandado) y todos con filtro bilinear y
    // compresion, que los deja borrosos. Se fuerza imagen entera, Point y sin
    // compresion; solo se reimporta si algo no coincide.
    private static Sprite SpriteRetrato(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[CasinoNPC] No existe {path}."); return null; }

        bool bien = importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    !importer.mipmapEnabled;
        if (!bien)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[CasinoNPC] Retrato reimportado entero y sin filtro: {path}");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite SpriteSimple(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[CasinoNPC] No existe {path}."); return null; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] Hoja(string nombre)
    {
        if (_hojas.TryGetValue(nombre, out Sprite[] ya)) return ya;
        Sprite[] hoja = CortarHoja($"{Hojas}{nombre}.png", 4, 4);
        _hojas[nombre] = hoja;
        return hoja;
    }

    private static Sprite Frame(Sprite[] hoja, int fila)
    {
        if (hoja == null) return null;
        int i = fila * 4;
        return i >= 0 && i < hoja.Length ? hoja[i] : null;
    }

    // Corta una hoja de personaje en su grilla y devuelve los sprites en orden
    // de lectura (fila 0 = mirando abajo).
    //
    // Se corta con SpriteEditorDataProvider porque en Unity 6 el campo
    // 'spritesheet' del importer se ignora. Si la hoja ya estaba cortada con
    // estos nombres, se reusa sin reimportar.
    private static Sprite[] CortarHoja(string path, int columnas, int filas)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"[CasinoNPC] No existe {path}."); return null; }

        string stem = System.IO.Path.GetFileNameWithoutExtension(path).Replace(" ", "_");
        var nombres = new List<string>();
        for (int fila = 0; fila < filas; fila++)
            for (int col = 0; col < columnas; col++)
                nombres.Add($"{stem}_{Direcciones[fila]}_{col}");

        Sprite[] ordenar()
        {
            var porNombre = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
            return nombres.Select(n => porNombre.TryGetValue(n, out Sprite s) ? s : null).ToArray();
        }

        Sprite[] existentes = ordenar();
        if (importer.spriteImportMode == SpriteImportMode.Multiple && existentes.All(s => s != null))
            return existentes;

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
                var sr = new SpriteRect
                {
                    name = nombres[fila * columnas + col],
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
        var nombresProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nombresProvider != null) nombresProvider.SetNameFileIdPairs(pares);
        provider.Apply();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        return ordenar();
    }
}
