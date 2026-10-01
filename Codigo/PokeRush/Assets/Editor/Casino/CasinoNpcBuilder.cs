using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Pone los personajes de los pisos 1 a 3 del casino. Son dos menus, para que
// rehacer un piso no toque el otro (el piso 1 quedo terminado a mano):
//
//   PokeRush > Casino > Poner los personajes del piso 1
//   PokeRush > Casino > Poner los personajes de los pisos 2 y 3
//
//   Piso 1: el guardia del ascensor (se le paga o se le gana; ganarle da la
//           ruleta de Pokemon), cuatro que pelean una vez (apostador,
//           malabarista, motorista y pokemaniaco), la enfermera, un anciano que
//           cuenta historia y ocho personas jugando a las tragaperras.
//   Piso 2: el guardia VIP del ascensor (se le muestra la Tarjeta VIP o se le
//           gana; ganarle da su ruleta exclusiva), el magnate que da la tarjeta
//           al ganarle, el revendedor que la vende por 1500, seis que pelean una
//           vez (joven rica, modelo, medium, ladron, tahur y bailarina), la
//           enfermera, dos que cuentan historia y seis jugando a la ruleta.
//   Piso 3: Tacconi (juega su Cosmog shiny al blackjack contra uno de tus
//           Pokemon), seis que pelean una vez (macarra, guay, veterano,
//           karateka, gangster y crupiera), la enfermera, dos que cuentan
//           historia y cuatro jugando al blackjack.
//
// Tambien configura los ascensores de los pisos 2 y 3 (que piden, a donde bajan)
// y las seis mesas de ruleta del piso 2 (la ruleta de premios por ganar al color).
//
// Los textos que ve el jugador van con tildes y enies: la caja de dialogo usa
// la fuente legacy de Unity, que las dibuja bien.
//
// Los que pelean una vez pueden dar la ruleta de premios de los NPCs (una cada
// dos combates, ver CasinoRuletaDeNpcs).
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
    public const string DefinitionFolder = "Assets/Resources/Definitions/Casino";
    public const string DialogosFolder = DefinitionFolder + "/Dialogos";
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Items = "Assets/Resources/Definitions/Items/";
    private const string Iconos = "Assets/Graphics/Ruleta/";
    private const string Hojas = "Assets/Graphics/Characters/";
    private const string Retratos = "Assets/Graphics/Trainers/";
    private const string PerfilesIA = "Assets/Resources/Definitions/AIProfiles/";

    private const int CapaObstacles = 3;

    // Cuanto hay que agrandar el sprite de un NPC para que su pixel de arte
    // mida lo MISMO que el del jugador.
    //
    // El jugador dibuja con PPU 100 y escala 4 (0,04 unidades por pixel); las
    // hojas de personaje se cortan a PPU 32 (0,03125). Sin corregirlo, un NPC
    // parado al lado del jugador se ve un 28% mas chico, que es exactamente lo
    // que se notaba en el pasillo del piso final.
    //
    // Es el cociente y no un numero a ojo: si alguien cambia el PPU del corte o
    // la escala del jugador, esto sigue estando bien.
    public const float PpuDeLasHojas = 32f;
    private const float PpuDelJugador = 100f;
    private const float EscalaDelJugador = 4f;
    public const float EscalaDelVisual = PpuDeLasHojas * EscalaDelJugador / PpuDelJugador;

    // Las hojas de personaje del pack son 4x4: cuatro poses por direccion, en
    // el orden abajo / izquierda / derecha / arriba.
    private static readonly string[] Direcciones = { "abajo", "izq", "der", "arriba" };
    public const int Abajo = 0, Izquierda = 1, Derecha = 2, Arriba = 3;

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
        StoryFlag.Casino_KaratekaVencido,
        StoryFlag.Casino_TahurVencido,
        StoryFlag.Casino_BailarinaVencida,
        StoryFlag.Casino_GangsterVencido,
        StoryFlag.Casino_CrupieraVencida
    };

    // La Tarjeta VIP: la pide el guardia del piso 2. Se gana venciendo al
    // magnate, sale en la ruleta de premios de los combates (~15%) o se compra.
    private const string TarjetaPath = "Assets/Resources/Definitions/Items/TarjetaVip.asset";
    private const int PrecioTarjeta = 1500;
    private const float PesoTarjetaEnLaRuleta = 15f;

    // Ganar MAS que esto al color (rojo o negro) en un giro abre la ruleta de premios.
    private const int UmbralRuletaPorColor = 200;

    // Lo que cobra el empresario del piso 3 por cada tiro de su ruleta.
    private const int PrecioRuletaDelEmpresario = 200;

    // El Pokemon de Tacconi, que pide el ascensor del piso 3.
    private const string EspecieDeTacconi = "0789_Cosmog";

    // El sprite de Tacconi: la pose de frente de la hoja que paso el equipo
    // (Assets/Tacconi/), recortada y con el fondo sacado. El retrato de la caja
    // de dialogo es el busto de esa misma pose, en Graphics/Trainers/TACCONI.png.
    //
    // Va mas alto que los demas (76 px de arte contra 58): su dibujo tiene
    // proporciones de unas 3 cabezas y los del pack son de 2,2, asi que igualar
    // por ALTURA lo deja como un palito con la cara hecha una mancha. Se iguala
    // por CABEZA, que es lo que se mira primero en un sprite chibi.
    private const string SpriteDeTacconi = "Assets/Sprites/Casino/tacconi_frente.png";

    // Gente jugando a la ruleta del piso 2: una persona en el FRENTE de cada mesa
    // (abajo, donde se ve el borde de la mesa y se apuesta), mirandola. Cada mesa
    // tiene 4 celdas de frente, asi que al jugador le quedan lugares para usarla.
    // Mesas: 01 y 02 abajo, 03 y 04 al medio, 05 y 06 arriba (las mas cerca del
    // ascensor). El que habla del guardia esta en la 05 y el que habla del
    // revendedor "de la esquina" en la 06, que es la de esa esquina.
    private static readonly (string nombre, Vector2 celda, string hoja, string hablante, string linea)[] JugadoresDeRuleta =
    {
        ("JugadorRuleta1", new Vector2(6.5f, 7.5f), "ricachona", "JUGADORA",
            "Rojo, rojo, rojo... ¡negro! Siempre sale negro cuando le voy al rojo."),
        ("JugadorRuleta2", new Vector2(11.5f, 7.5f), "presidente", "JUGADOR",
            "Dicen que si le ganás más de 200 al color, la casa te hace girar la ruleta de premios."),
        ("JugadorRuleta3", new Vector2(5.5f, 11.5f), "guaya", "JUGADORA",
            "Yo le voy al 17. Siempre al 17. Algún día va a salir."),
        ("JugadorRuleta4", new Vector2(12.5f, 11.5f), "chica1", "JUGADORA",
            "A mi hermana le salió una Tarjeta VIP en una ruleta de premios. Yo sigo esperando."),
        ("JugadorRuleta5", new Vector2(6.5f, 15.5f), "chaval2", "JUGADOR",
            "El guardia del ascensor no deja subir a nadie sin la Tarjeta VIP."),
        ("JugadorRuleta6", new Vector2(12.5f, 15.5f), "estudianta", "JUGADORA",
            "El revendedor de la esquina tiene tarjetas VIP... a precio de oro.")
    };

    // Gente jugando al blackjack del piso 3: parada justo debajo de los banquitos,
    // del lado curvo de la mesa, que es el de los jugadores. Del lado recto
    // (arriba) va el croupier: ahi parecian empleados y no clientes.
    private static readonly (string nombre, Vector2 celda, string hoja, string hablante, string linea)[] JugadoresDeBlackjack =
    {
        ("JugadorBlackjack1", new Vector2(-11.5f, 13.5f), "gabriel", "JUGADOR",
            "Diecinueve... me planto. No, pido. No, me planto."),
        ("JugadorBlackjack2", new Vector2(-5.5f, 13.5f), "sachiko", "JUGADORA",
            "Tacconi no pierde nunca. Dicen que tiene un Cosmog shiny y que lo apuesta."),
        ("JugadorBlackjack3", new Vector2(-12.5f, 7.5f), "guayo", "JUGADOR",
            "Al ascensor de este piso solo suben los que tienen un Pokémon shiny de verdad."),
        ("JugadorBlackjack4", new Vector2(-6.5f, 7.5f), "chica2", "JUGADORA",
            "¡Blackjack! ...ah, no, eran 22.")
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

    [MenuItem("PokeRush/Casino/Poner los personajes del piso 1")]
    public static void PonerPiso1() => Poner(piso1: true, pisos2y3: false);

    [MenuItem("PokeRush/Casino/Poner los personajes de los pisos 2 y 3")]
    public static void PonerPisos2y3() => Poner(piso1: false, pisos2y3: true);

    private static void Poner(bool piso1, bool pisos2y3)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[CasinoNPC] Sali del Play Mode antes de correr esto.");
            return;
        }

        string[] escenas = piso1 && pisos2y3 ? Escenas : piso1 ? new[] { Piso1 } : new[] { Piso2, Piso3 };
        foreach (string path in escenas)
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

        // Piso 2. El guardia VIP es mas dificil que el del piso 1 (4 Pokemon,
        // nivel 21-23, IA de lider); el magnate, "algo complicado" (3 Pokemon
        // de nivel 19-20 cuando el resto del piso anda por el 15-17).
        EnemyDefinition guardiaVip = Enemigo("GuardiaVipCasino", "Guardia VIP", 500, StoryFlag.Casino_GuardiaVipVencido,
            "ELITEROCKET", "GymLeader", ("0215_Sneasel", 21), ("0093_Haunter", 22), ("0552_Krokorok", 22), ("0082_Magneton", 23));
        EnemyDefinition magnate = Enemigo("MagnateCasino", "Magnate", 400, StoryFlag.Casino_MagnateVencido,
            "CABALLERO", "Trainer", ("0281_Kirlia", 19), ("0233_Porygon2", 20), ("0608_Lampent", 20));
        EnemyDefinition tahur = Enemigo("TahurCasino", "Tahúr", 170, StoryFlag.Casino_TahurVencido,
            "MAXIMO", "Trainer", ("0562_Yamask", 16), ("0679_Honedge", 17));
        EnemyDefinition bailarina = Enemigo("BailarinaCasino", "Bailarina", 160, StoryFlag.Casino_BailarinaVencida,
            "CHICA", "Trainer", ("0333_Swablu", 16), ("0315_Roselia", 17));
        // Piso 3.
        EnemyDefinition gangster = Enemigo("GangsterCasino", "Gángster", 230, StoryFlag.Casino_GangsterVencido,
            "ROCKETO", "Trainer", ("0228_Houndour", 18), ("0451_Skorupi", 19));
        EnemyDefinition crupiera = Enemigo("CrupieraCasino", "Crupiera", 220, StoryFlag.Casino_CrupieraVencida,
            "VETERANA", "Trainer", ("0137_Porygon", 18), ("0608_Lampent", 19));

        SpecialItemDefinition tarjeta = TarjetaVip();

        // ---------- ruletas y dialogos (solo si no existen) ----------
        // La ruleta de los NPCs de los pisos 1 y 3 NO da la Tarjeta VIP (solo
        // sirve para el ascensor del piso 2); la de los NPCs del piso 2 si (~15%).
        CasinoPrizeTable ruletaNpcs = TablaDeLosNpcs();
        CasinoPrizeTable ruletaNpcsPiso2 = TablaDeLosNpcsDelPiso2(tarjeta);
        CasinoPrizeTable ruletaGuardia = TablaDelGuardia();
        CasinoPrizeTable ruletaGuardiaVip = TablaDelGuardiaVip();
        CasinoPrizeTable ruletaMesa = TablaDeLaMesa();
        CasinoPrizeTable ruletaEmpresario = TablaDelEmpresario();

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
        // Relleno igual que los de arriba: se reescriben cuando llegue la historia.
        DialogueDefinition secretaria = Dialogo("Historia_Piso2_Secretaria", "SECRETARIA",
            "Trabajo para la administración del casino desde hace años.",
            "Arriba de todo hay alguien que decide quién gana y quién pierde.",
            "Pero eso no te lo dije yo.");
        DialogueDefinition jubilado = Dialogo("Historia_Piso3_Jubilado", "JUBILADO",
            "Yo antes tenía un equipo entero. Se lo aposté a Tacconi, uno por uno.",
            "Dicen que el dueño del casino le presta los Pokémon raros para que los apueste.",
            "Si le ganás, no le digas a nadie cómo lo hiciste.");

        AssetDatabase.SaveAssets();

        int ok = 0;

        if (piso1 && ArmarPiso(Piso1, raiz =>
            {
                var g = Personaje(raiz, "GuardiaAscensor", new Vector2(3.5f, 19.5f), "maton", Abajo);
                ConfigurarGuardia(g.AddComponent<CasinoElevatorGuard>(), "GUARDIA", guardia, ruletaGuardia,
                    StoryFlag.Casino_Piso1_PaseAscensor, StoryFlag.Casino_GuardiaVencido, 500, null);

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

        if (pisos2y3 && ArmarPiso(Piso2, raiz =>
            {
                var j = Personaje(raiz, "JovenRicaCasino", new Vector2(-10.5f, 10.5f), "jovenrica", Derecha);
                ConfigurarEntrenador(j.AddComponent<CasinoTrainerNpc>(), "JOVEN RICA", jovenRica,
                    StoryFlag.Casino_JovenRicaVencida, ruletaNpcsPiso2,
                    "Las fichas me aburren. Una pelea de verdad me vendría bien. ¿Aceptás?",
                    "Acepto", "No", "Qué decepción.",
                    "Qué forma tan poco elegante de ganar... pero ganaste.",
                    "No insistas, ya perdí una vez.");

                var mo = Personaje(raiz, "ModeloCasino", new Vector2(0.5f, 5.5f), "modelo", Abajo);
                ConfigurarEntrenador(mo.AddComponent<CasinoTrainerNpc>(), "MODELO", modelo,
                    StoryFlag.Casino_ModeloVencida, ruletaNpcsPiso2,
                    "Si me ganás, te dejo sacarte una foto conmigo. ¿Peleamos?",
                    "Peleamos", "Paso", "Vos te lo perdés.",
                    "Hoy no era mi día.",
                    "Ya tuviste tu foto. Chau.");

                var me = Personaje(raiz, "MediumCasino", new Vector2(-13.5f, 15.5f), "medium", Derecha);
                ConfigurarEntrenador(me.AddComponent<CasinoTrainerNpc>(), "MÉDIUM", medium,
                    StoryFlag.Casino_MediumVencido, ruletaNpcsPiso2,
                    "Las cartas me dijeron que ibas a venir. También dicen que vas a perder. ¿Probamos?",
                    "Probemos", "No", "Ya lo sabía.",
                    "Esto no estaba en las cartas.",
                    "Las cartas no me hablan desde que me ganaste.");

                var la = Personaje(raiz, "LadronCasino", new Vector2(13.5f, 11.5f), "ladron", Izquierda);
                ConfigurarEntrenador(la.AddComponent<CasinoTrainerNpc>(), "LADRÓN", ladron,
                    StoryFlag.Casino_LadronVencido, ruletaNpcsPiso2,
                    "Shh... nadie vio nada. Si me ganás, no te toco las fichas. ¿Trato?",
                    "Trato", "Ni loco", "Cuidá tus bolsillos.",
                    "Está bien, está bien. Tus fichas están a salvo.",
                    "Yo no fui. Ni me mires.");

                // Enfermera y PC juntos arriba a la izquierda, cerca del ascensor,
                // que es por donde se llega (ver PcEnLosNivelesBuilder).
                Personaje(raiz, "EnfermeraCasino", new Vector2(-13.5f, 19.5f), "enfermera", Abajo)
                    .AddComponent<CasinoNurseInteractable>();

                ConfigurarQueHabla(Personaje(raiz, "DamiselaHistoria", new Vector2(-8.5f, 10.5f), "damisela", Izquierda)
                    .AddComponent<NpcQueHabla>(), damisela);

                // ---- el guardia VIP y la Tarjeta VIP ----
                // El guardia y la secretaria flanquean la puerta del ascensor; el
                // magnate (que tiene una tarjeta) y el revendedor (que la vende)
                // quedan en la esquina de arriba a la derecha, al lado.
                var gv = Personaje(raiz, "GuardiaVip", new Vector2(2.5f, 19.5f), "rocketElite", Abajo);
                ConfigurarGuardia(gv.AddComponent<CasinoElevatorGuard>(), "GUARDIA VIP", guardiaVip, ruletaGuardiaVip,
                    StoryFlag.Casino_Piso2_PaseAscensor, StoryFlag.Casino_GuardiaVipVencido, 0, tarjeta);

                var mg = Personaje(raiz, "MagnateCasino", new Vector2(12.5f, 18.5f), "ricachon", Abajo);
                ConfigurarEntrenador(mg.AddComponent<CasinoTrainerNpc>(), "MAGNATE", magnate,
                    StoryFlag.Casino_MagnateVencido, null,
                    "¿La Tarjeta VIP? Tengo una de sobra. Si me ganás, es tuya. Pero no me gusta perder.",
                    "Pelear", "Ahora no", "Lo imaginaba.",
                    "Increíble... Un trato es un trato.",
                    "Ya tenés mi tarjeta. No me la gastes toda.",
                    tarjeta);

                var rv = Personaje(raiz, "RevendedorVip", new Vector2(15.5f, 19.5f), "hombrenegro", Izquierda);
                ConfigurarVendedor(rv.AddComponent<CasinoVendedorNpc>(), "REVENDEDOR", "ROCKETO", tarjeta, PrecioTarjeta);

                // ---- mas gente que pelea una vez ----
                var th = Personaje(raiz, "TahurCasino", new Vector2(-5.5f, 16.5f), "maximo", Izquierda);
                ConfigurarEntrenador(th.AddComponent<CasinoTrainerNpc>(), "TAHÚR", tahur,
                    StoryFlag.Casino_TahurVencido, ruletaNpcsPiso2,
                    "Las ruletas están arregladas, pero una pelea no. ¿Probamos suerte?",
                    "Probemos", "No", "Cobarde.",
                    "Me limpiaste. Ni en la ruleta pierdo tanto.",
                    "Ya me ganaste. Andá a apostar lo que me sacaste.");

                var bl = Personaje(raiz, "BailarinaCasino", new Vector2(-7.5f, 5.5f), "kimono", Abajo);
                ConfigurarEntrenador(bl.AddComponent<CasinoTrainerNpc>(), "BAILARINA", bailarina,
                    StoryFlag.Casino_BailarinaVencida, ruletaNpcsPiso2,
                    "Mi número empieza en diez minutos. ¿Me ayudás a calentar con una pelea?",
                    "Dale", "Ahora no", "Bueno, bailo sola.",
                    "¡Qué ritmo tenés! Me ganaste.",
                    "Ya me ganaste. Ahora, a bailar.");

                ConfigurarQueHabla(Personaje(raiz, "SecretariaHistoria", new Vector2(-1.5f, 19.5f), "secretaria", Abajo)
                    .AddComponent<NpcQueHabla>(), secretaria);

                foreach (var jr in JugadoresDeRuleta)
                {
                    var jugador = Personaje(raiz, jr.nombre, jr.celda, jr.hoja, Arriba);
                    ConfigurarQueHabla(jugador.AddComponent<NpcQueHabla>(), Dialogo(jr.nombre, jr.hablante, jr.linea));
                    jugador.AddComponent<NpcJugandoMaquina>();
                }
            },
            escena => ConfigurarPiso2(escena, ruletaMesa))) ok++;

        if (pisos2y3 && ArmarPiso(Piso3, raiz =>
            {
                var ma = Personaje(raiz, "MacarraCasino", new Vector2(3.5f, 12.5f), "macarra", Izquierda);
                ConfigurarEntrenador(ma.AddComponent<CasinoTrainerNpc>(), "MACARRA", macarra,
                    StoryFlag.Casino_MacarraVencido, ruletaNpcs,
                    "Este piso es mío. Si querés pasear por acá, primero me ganás.",
                    "Pelear", "Me voy", "Eso, andá.",
                    "Bueno, bueno... el piso es tuyo.",
                    "Ya está, ya está. Andá nomás.");

                var gu = Personaje(raiz, "GuayCasino", new Vector2(5.5f, 19.5f), "guay", Abajo);
                ConfigurarEntrenador(gu.AddComponent<CasinoTrainerNpc>(), "GUAY", guay,
                    StoryFlag.Casino_GuayVencido, ruletaNpcs,
                    "Llegar al tercer piso no es suerte. Mostrame que sabés pelear.",
                    "Pelear", "Todavía no", "Cuando quieras.",
                    "Tenés nivel. Nos vemos arriba.",
                    "Ya me ganaste. Respeto.");

                var ve = Personaje(raiz, "VeteranoCasino", new Vector2(-8.5f, 16.5f), "veterano", Derecha);
                ConfigurarEntrenador(ve.AddComponent<CasinoTrainerNpc>(), "VETERANO", veterano,
                    StoryFlag.Casino_VeteranoVencido, ruletaNpcs,
                    "Llevo treinta años en este casino. Vi caer a muchos como vos. ¿Te animás?",
                    "Me animo", "Todavía no", "Sabia decisión.",
                    "Hacía años que no perdía así. Bien jugado.",
                    "Ya me ganaste, pibe. Andá para arriba.");

                var ka = Personaje(raiz, "KaratekaCasino", new Vector2(4.5f, 5.5f), "luchador", Izquierda);
                ConfigurarEntrenador(ka.AddComponent<CasinoTrainerNpc>(), "KARATEKA", karateka,
                    StoryFlag.Casino_KaratekaVencido, ruletaNpcs,
                    "En el tercer piso no se juega: se pelea. ¡En guardia!",
                    "¡En guardia!", "Paso", "Volvé cuando estés listo.",
                    "Tu técnica es superior. Me inclino.",
                    "Voy a seguir entrenando.");

                // Enfermera y PC arriba a la izquierda, como en el piso 2.
                Personaje(raiz, "EnfermeraCasino", new Vector2(-13.5f, 19.5f), "enfermera", Abajo)
                    .AddComponent<CasinoNurseInteractable>();

                // El empresario vende tiros de su ruleta (siempre Pokemon); si le
                // dicen que no, cuenta lo suyo de historia.
                ConfigurarRuletaPaga(Personaje(raiz, "EmpresarioHistoria", new Vector2(11.5f, 14.5f), "empresario", Abajo)
                    .AddComponent<CasinoRuletaPagaNpc>(), ruletaEmpresario, PrecioRuletaDelEmpresario, empresario);

                // ---- Tacconi y su Cosmog shiny ----
                // Ya tiene sus sprites propios (los paso el equipo el 18-sep).
                // Va parado de frente y no se mueve, asi que es UN sprite suelto
                // y no una hoja de 4x4: ver PersonajeSuelto y SpriteDeTacconi.
                // Si el equipo pasa el resto de las direcciones, pasa a Personaje.
                //
                // Tiene SU mesa: la de arriba a la derecha (la mas cerca de la
                // alfombra). Esta del lado del croupier, porque es el que reparte;
                // su guardaespaldas se para al costado de la mesa mirando a la
                // alfombra y el veterano la mira desde el pasillo entre mesas.
                var tc = PersonajeSuelto(raiz, "Tacconi", new Vector2(-6.5f, 18.5f), SpriteDeTacconi);
                ConfigurarTacconi(tc.AddComponent<CasinoTacconiNpc>(), "TACCONI");

                // ---- mas gente que pelea una vez ----
                var gg = Personaje(raiz, "GangsterCasino", new Vector2(-3.5f, 16.5f), "rocketo", Derecha);
                ConfigurarEntrenador(gg.AddComponent<CasinoTrainerNpc>(), "GÁNGSTER", gangster,
                    StoryFlag.Casino_GangsterVencido, ruletaNpcs,
                    "Nadie se acerca a la mesa de Tacconi sin pasar por mí. ¿Peleamos?",
                    "Pelear", "Me voy", "Eso pensé.",
                    "Está bien, está bien. Tacconi te espera.",
                    "Ya me ganaste. Andá con Tacconi, pero cuidá tus Pokémon.");

                var cr = Personaje(raiz, "CrupieraCasino", new Vector2(5.5f, 16.5f), "veterana", Derecha);
                ConfigurarEntrenador(cr.AddComponent<CasinoTrainerNpc>(), "CRUPIERA", crupiera,
                    StoryFlag.Casino_CrupieraVencida, ruletaNpcs,
                    "Estoy en mi descanso. Si querés, repartimos Pokémon en vez de cartas.",
                    "Repartí", "No", "Otra vez será.",
                    "La casa perdió esta mano.",
                    "Ya me ganaste. Vuelvo a la mesa.");

                ConfigurarQueHabla(Personaje(raiz, "JubiladoHistoria", new Vector2(5.5f, 9.5f), "anciano2", Abajo)
                    .AddComponent<NpcQueHabla>(), jubilado);

                foreach (var j in JugadoresDeBlackjack)
                {
                    var jugador = Personaje(raiz, j.nombre, j.celda, j.hoja, Arriba);
                    ConfigurarQueHabla(jugador.AddComponent<NpcQueHabla>(), Dialogo(j.nombre, j.hablante, j.linea));
                    jugador.AddComponent<NpcJugandoMaquina>();
                }
            },
            ConfigurarPiso3)) ok++;

        AssetDatabase.SaveAssets();

        int esperados = (piso1 ? 1 : 0) + (pisos2y3 ? 2 : 0);
        if (ok == esperados)
            Debug.Log($"[CasinoNPC] TERMINADO: personajes puestos en {ok} piso(s).");
        else
            Debug.LogError($"[CasinoNPC] Solo {ok} de {esperados} pisos quedaron guardados. Mira los errores de arriba.");
    }

    // ---------- ascensores y mesas ----------

    private static IEnumerable<T> Componentes<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    // Piso 2: el ascensor pide el pase del guardia VIP y baja al piso 1; las seis
    // mesas abren la ruleta de premios al ganarle fuerte al color.
    private static void ConfigurarPiso2(Scene scene, CasinoPrizeTable ruletaMesa)
    {
        ConfigurarAscensor(scene, CasinoRequisitoTipo.Flag, StoryFlag.Casino_Piso2_PaseAscensor, null,
            "El guardia VIP no te deja subir. Hablá con él: con la Tarjeta VIP o ganándole.",
            ("Casino Nivel 1", "PISO 1"));

        int mesas = 0;
        foreach (RouletteTableInteractable mesa in Componentes<RouletteTableInteractable>(scene))
        {
            var so = new SerializedObject(mesa);
            so.FindProperty("ruletaPorColor").objectReferenceValue = ruletaMesa;
            so.FindProperty("umbralRuletaPorColor").intValue = UmbralRuletaPorColor;
            so.ApplyModifiedPropertiesWithoutUndo();
            mesas++;
        }

        if (mesas == 0) Debug.LogError("[CasinoNPC] El piso 2 no tiene mesas de ruleta (RouletteTableInteractable).");
        else Debug.Log($"[CasinoNPC] Piso 2: {mesas} mesas abren la ruleta de premios al ganar mas de {UmbralRuletaPorColor} al color.");
    }

    // Piso 3: el ascensor pide el Cosmog shiny de Tacconi y baja a los pisos 2 y 1.
    private static void ConfigurarPiso3(Scene scene)
    {
        var cosmog = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{EspecieDeTacconi}.asset");
        if (cosmog == null) Debug.LogError($"[CasinoNPC] No existe la especie {EspecieDeTacconi}.");

        ConfigurarAscensor(scene, CasinoRequisitoTipo.TenerPokemon, StoryFlag.None, cosmog,
            "Un lector de tarjetas parpadea en rojo: PISO 4, ACCESO RESTRINGIDO. La tarjeta la tiene Tacconi, y la apuesta junto con su {0} shiny.",
            ("Casino Nivel 2", "PISO 2"), ("Casino Nivel 1", "PISO 1"));
    }

    private static void ConfigurarAscensor(Scene scene, CasinoRequisitoTipo requisito, StoryFlag flag,
        PokenDefinition especie, string mensaje, params (string escena, string nombre)[] abajo)
    {
        CasinoElevatorInteractable ascensor = Componentes<CasinoElevatorInteractable>(scene).FirstOrDefault();
        if (ascensor == null)
        {
            Debug.LogError($"[CasinoNPC] '{scene.name}' no tiene CasinoElevatorInteractable.");
            return;
        }

        var so = new SerializedObject(ascensor);
        so.FindProperty("requisito").enumValueIndex = (int)requisito;
        so.FindProperty("flagRequerido").enumValueIndex = (int)flag;
        so.FindProperty("especieRequerida").objectReferenceValue = especie;
        so.FindProperty("debeSerShiny").boolValue = true;
        so.FindProperty(requisito == CasinoRequisitoTipo.TenerPokemon ? "mensajeFaltaPokemon" : "mensajeFaltaObjetivo")
            .stringValue = mensaje;

        SerializedProperty pisos = so.FindProperty("pisosDeAbajo");
        pisos.arraySize = abajo.Length;
        for (int i = 0; i < abajo.Length; i++)
        {
            SerializedProperty piso = pisos.GetArrayElementAtIndex(i);
            piso.FindPropertyRelative("escena").stringValue = abajo[i].escena;
            piso.FindPropertyRelative("nombre").stringValue = abajo[i].nombre;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- escenas ----------

    public static bool ArmarPiso(string path, System.Action<GameObject> poner, System.Action<Scene> extra = null)
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

    public static GameObject Personaje(GameObject raiz, string nombre, Vector2 celda, string hoja, int mira)
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
        // Y se agranda para dibujar con el MISMO pixel que el jugador (ver
        // EscalaDelVisual). Escala desde el pivote, que son los pies, asi que
        // no hay que reacomodar nada.
        visual.transform.localScale = Vector3.one * EscalaDelVisual;

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = Frame(Hoja(hoja), mira);
        // Mismo sorting que el jugador: con el Renderer2D en Custom Axis (0,1,0)
        // se tapan entre ellos segun la altura.
        sr.sortingOrder = 15;

        if (sr.sprite == null) Debug.LogWarning($"[CasinoNPC] '{nombre}' quedo sin sprite ({hoja}).");
        return go;
    }

    // Igual que Personaje, pero con UN sprite suelto en vez de una hoja de 4x4.
    //
    // Para los personajes que el equipo entrega dibujados a mano y que no se
    // mueven: no tienen las cuatro direcciones, asi que no hay hoja que cortar.
    // El primero fue Tacconi.
    public static GameObject PersonajeSuelto(GameObject raiz, string nombre, Vector2 celda, string pathDelSprite)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = new Vector3(celda.x, celda.y, 0f);

        go.layer = CapaObstacles;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        visual.transform.localScale = Vector3.one * EscalaDelVisual;

        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteDePersonaje(pathDelSprite);
        sr.sortingOrder = 15;

        if (sr.sprite == null) Debug.LogWarning($"[CasinoNPC] '{nombre}' quedo sin sprite ({pathDelSprite}).");
        return go;
    }

    // Importa un sprite suelto con las mismas medidas que las hojas cortadas:
    // 32 pixeles por unidad y pivote abajo al medio, que es lo que hace que los
    // pies queden apoyados en la celda y que mida lo mismo que el resto.
    public static Sprite SpriteDePersonaje(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) { Debug.LogError($"[CasinoNPC] No existe {path}."); return null; }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool bien = importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    !importer.mipmapEnabled &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, PpuDeLasHojas) &&
                    settings.spriteAlignment == (int)SpriteAlignment.BottomCenter;
        if (bien) return AssetDatabase.LoadAssetAtPath<Sprite>(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = PpuDeLasHojas;

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        Debug.Log($"[CasinoNPC] Sprite de personaje reimportado (PPU {PpuDeLasHojas}, pivote abajo): {path}");
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void ConfigurarEntrenador(CasinoTrainerNpc npc, string nombre, EnemyDefinition rival,
        StoryFlag flagVencido, CasinoPrizeTable ruleta, string pregunta, string si, string no,
        string rechazo, string victoria, string yaVencido, SpecialItemDefinition objetoAlGanar = null)
    {
        var soObjeto = new SerializedObject(npc);
        soObjeto.FindProperty("objetoAlGanar").objectReferenceValue = objetoAlGanar;
        soObjeto.ApplyModifiedPropertiesWithoutUndo();

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

    // Con 'objeto' puesto el guardia no cobra: pide ver el objeto (o que le ganes).
    private static void ConfigurarGuardia(CasinoElevatorGuard npc, string nombre, EnemyDefinition rival,
        CasinoPrizeTable ruleta, StoryFlag flagPase, StoryFlag flagVencido, int precio, SpecialItemDefinition objeto)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = nombre;
        so.FindProperty("retrato").objectReferenceValue = rival != null ? rival.battleFrontSprite : null;
        so.FindProperty("rival").objectReferenceValue = rival;
        so.FindProperty("ruletaDePokemon").objectReferenceValue = ruleta;
        so.FindProperty("precio").intValue = precio;
        so.FindProperty("objetoRequerido").objectReferenceValue = objeto;
        so.FindProperty("flagPase").enumValueIndex = (int)flagPase;
        so.FindProperty("flagVencido").enumValueIndex = (int)flagVencido;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarVendedor(CasinoVendedorNpc npc, string nombre, string retrato,
        SpecialItemDefinition objeto, int precio)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = nombre;
        so.FindProperty("retrato").objectReferenceValue = SpriteRetrato($"{Retratos}{retrato}.png");
        so.FindProperty("objeto").objectReferenceValue = objeto;
        so.FindProperty("precio").intValue = precio;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarTacconi(CasinoTacconiNpc npc, string retrato)
    {
        var cosmog = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{EspecieDeTacconi}.asset");
        if (cosmog == null) Debug.LogError($"[CasinoNPC] No existe la especie {EspecieDeTacconi}.");

        var so = new SerializedObject(npc);
        so.FindProperty("nombre").stringValue = "TACCONI";
        so.FindProperty("retrato").objectReferenceValue = SpriteRetrato($"{Retratos}{retrato}.png");
        so.FindProperty("especiePremio").objectReferenceValue = cosmog;
        so.FindProperty("nivelPremio").intValue = 15;
        so.FindProperty("premioShiny").boolValue = true;
        so.FindProperty("flagGanado").enumValueIndex = (int)StoryFlag.Casino_TacconiVencido;
        // La historia (historia.pdf): lo que sabe del registro de 2026 y la tarjeta del piso 4.
        so.FindProperty("pregunta").stringValue =
            "Una mano de blackjack: uno de tus Pokémon contra mi {0}. Si ganás, te llevás los dos: el shiny y la tarjeta. ¿Jugamos?";
        so.FindProperty("presentacion").objectReferenceValue = HistoriaAssets.Dialogo("Casino/Dialogos/Casino_Tacconi_Presentacion");
        so.FindProperty("despuesDeGanar").objectReferenceValue = HistoriaAssets.Dialogo("Casino/Dialogos/Casino_Tacconi_DespuesDeGanar");
        so.FindProperty("charlaYaGanado").objectReferenceValue = HistoriaAssets.Dialogo("Casino/Dialogos/Casino_Tacconi_YaGanado");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Ojo: volver a armar el piso vuelve el precio a PrecioRuletaDelEmpresario.
    private static void ConfigurarRuletaPaga(CasinoRuletaPagaNpc npc, CasinoPrizeTable ruleta, int precio,
                                             DialogueDefinition charla)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("ruleta").objectReferenceValue = ruleta;
        so.FindProperty("precio").intValue = precio;
        so.FindProperty("charla").objectReferenceValue = charla;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigurarQueHabla(NpcQueHabla npc, DialogueDefinition dialogo)
    {
        var so = new SerializedObject(npc);
        so.FindProperty("dialogo").objectReferenceValue = dialogo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- assets ----------

    // Los rivales se actualizan siempre (equipo, plata, flag): son balance del
    // builder, no texto del equipo.
    public static EnemyDefinition Enemigo(string archivo, string nombre, int plata, StoryFlag flag,
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

    public static DialogueDefinition Dialogo(string archivo, string hablante, params string[] lineas)
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

    // La Tarjeta VIP como asset. Se actualiza siempre (texto e icono): el nombre
    // es la clave con la que la guarda SaveService, asi que no hay que cambiarlo.
    private static SpecialItemDefinition TarjetaVip()
    {
        var def = AssetDatabase.LoadAssetAtPath<SpecialItemDefinition>(TarjetaPath);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<SpecialItemDefinition>();

        def.itemName = "Tarjeta VIP";
        def.description = "Tarjeta dorada del casino. El guardia del ascensor del piso 2 deja subir a quien la muestra.";
        def.effectDescription = "Abre el ascensor del piso 2.";
        def.buyPrice = PrecioTarjeta;
        def.icon = SpriteSimple("Assets/Sprites/Casino/tarjeta_vip.png");

        if (nueva) AssetDatabase.CreateAsset(def, TarjetaPath);
        EditorUtility.SetDirty(def);
        return def;
    }

    // La ruleta de los NPCs de los pisos 1 y 3: 35% Pokemon, 40% curativos y
    // 25% plata. SIN Tarjeta VIP: en el piso 1 todavia no sirve y en el 3 ya no.
    //
    // Si la tabla quedo con la tarjeta (se le metio el 14-sep y despues se
    // pidio que solo saliera en el piso 2), se vuelve UNA vez al reparto de antes.
    private static CasinoPrizeTable TablaDeLosNpcs()
    {
        string path = $"{DefinitionFolder}/RuletaDeLosNpcs.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla == null)
        {
            tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
            tabla.premios = PremiosDeLosNpcs();
            AssetDatabase.CreateAsset(tabla, path);
        }
        else if (tabla.premios != null && tabla.premios.Any(p => p != null && p.specialItem != null))
        {
            tabla.premios = PremiosDeLosNpcs();
            Debug.Log("[CasinoNPC] RuletaDeLosNpcs: se saco la Tarjeta VIP (ahora sale solo en la de los NPCs del piso 2).");
        }

        // La lista de quienes la dan si se mantiene al dia: depende de los NPCs
        // que pone este builder, no es balance.
        tabla.npcsQueLaPuedenDar = (StoryFlag[])DanRuleta.Clone();
        EditorUtility.SetDirty(tabla);
        return tabla;
    }

    // La ruleta de los NPCs del piso 2: ~33% Pokemon, 15% la Tarjeta VIP, 31%
    // curativos y 21% plata (lo pidio el enunciado: ~15% de tarjeta en los
    // combates del piso 2).
    private static CasinoPrizeTable TablaDeLosNpcsDelPiso2(SpecialItemDefinition tarjeta)
    {
        string path = $"{DefinitionFolder}/RuletaDeLosNpcsPiso2.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla == null)
        {
            tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
            tabla.premios = PremiosDeLosNpcsDelPiso2(tarjeta);
            AssetDatabase.CreateAsset(tabla, path);
        }

        // La MISMA lista que la de los pisos 1 y 3: la regla de "una cada dos
        // combates, hasta cuatro" cuenta a todos los NPCs juntos, sin importar
        // de que piso son ni de que tabla sacan el premio.
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

    private static Prize[] PremiosDeLosNpcs() => new[]
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

    private static Prize[] PremiosDeLosNpcsDelPiso2(SpecialItemDefinition tarjeta) => new[]
    {
        PremioPokemon("0025_Pikachu", 15, 12, "icon_pokeball", 300),
        PremioObjeto("Pocion", "Poción", "Pociones", "una", 3, 17, "icon_pocion"),
        PremioObjetoClave(tarjeta, PesoTarjetaEnLaRuleta),
        PremioPokemon("0133_Eevee", 15, 11, "icon_greatball", 300),
        PremioObjeto("SuperPocion", "Superpoción", "Superpociones", "una", 2, 14, "icon_superpocion"),
        PremioPlata(150, 13),
        PremioPokemon("0175_Togepi", 15, 10, "icon_pokeball", 300),
        PremioPlata(400, 8)
    };

    // La ruleta exclusiva del guardia VIP: siempre un Pokemon, mejores que los
    // del guardia del piso 1. El icono marca la rareza.
    //   Poke Ball  (comun):    4 x 15% = 60%
    //   Great Ball (bueno):    2 x 12% = 24%
    //   Ultra Ball (raro):     1 x 12% = 12%
    //   Master Ball (muy raro): 1 x 4% =  4%
    private static CasinoPrizeTable TablaDelGuardiaVip()
    {
        string path = $"{DefinitionFolder}/RuletaDelGuardiaVip.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = new[]
        {
            PremioPokemon("0403_Shinx", 21, 15, "icon_pokeball", 800),
            PremioPokemon("0610_Axew", 22, 12, "icon_greatball", 800),
            PremioPokemon("0529_Drilbur", 21, 15, "icon_pokeball", 800),
            PremioPokemon("0246_Larvitar", 22, 12, "icon_ultraball", 800),
            PremioPokemon("0361_Snorunt", 21, 15, "icon_pokeball", 800),
            PremioPokemon("0443_Gible", 22, 12, "icon_greatball", 800),
            PremioPokemon("0231_Phanpy", 21, 15, "icon_pokeball", 800),
            PremioPokemon("0371_Bagon", 23, 4, "icon_masterball", 800)
        };
        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    // La ruleta que vende el empresario del piso 3 (PrecioRuletaDelEmpresario
    // el tiro): siempre un Pokemon, un escalon arriba de la del guardia VIP.
    //   Poke Ball  (comun):    4 x 15% = 60%
    //   Great Ball (bueno):    2 x 12% = 24%
    //   Ultra Ball (raro):     1 x 12% = 12%
    //   Master Ball (muy raro): 1 x 4% =  4%
    private static CasinoPrizeTable TablaDelEmpresario()
    {
        string path = $"{DefinitionFolder}/RuletaDelEmpresario.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = new[]
        {
            PremioPokemon("0092_Gastly", 24, 15, "icon_pokeball", 200),
            PremioPokemon("0280_Ralts", 25, 12, "icon_greatball", 200),
            PremioPokemon("0304_Aron", 24, 15, "icon_pokeball", 200),
            PremioPokemon("0215_Sneasel", 25, 12, "icon_ultraball", 200),
            PremioPokemon("0302_Sableye", 24, 15, "icon_pokeball", 200),
            PremioPokemon("0374_Beldum", 25, 12, "icon_greatball", 200),
            PremioPokemon("0543_Venipede", 24, 15, "icon_pokeball", 200),
            PremioPokemon("0633_Deino", 26, 4, "icon_masterball", 200)
        };
        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    // La ruleta que abren las mesas del piso 2 al ganar mas de 200 al color:
    // 25% Pokemon, 40% curativos y 35% plata.
    private static CasinoPrizeTable TablaDeLaMesa()
    {
        string path = $"{DefinitionFolder}/RuletaDeLaMesa.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = new[]
        {
            PremioPokemon("0172_Pichu", 15, 13, "icon_pokeball", 300),
            PremioPlata(250, 16),
            PremioObjeto("Pocion", "Poción", "Pociones", "una", 2, 18, "icon_pocion"),
            PremioPokemon("0440_Happiny", 15, 12, "icon_greatball", 300),
            PremioPlata(500, 12),
            PremioObjeto("SuperPocion", "Superpoción", "Superpociones", "una", 1, 14, "icon_superpocion"),
            PremioObjeto("HiperPocion", "Hiperpoción", "Hiperpociones", "una", 1, 8, "icon_hiperpocion"),
            PremioPlata(1000, 7)
        };
        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    private static Prize PremioObjetoClave(SpecialItemDefinition objeto, float peso)
    {
        if (objeto == null) Debug.LogError("[CasinoNPC] Falta el objeto clave del premio.");
        return new Prize
        {
            prizeName = objeto != null ? objeto.itemName : "Objeto clave",
            article = "la",
            amount = 1,
            probability = peso,
            icon = SpriteSimple("Assets/Sprites/Casino/icon_tarjeta_vip.png"),
            specialItem = objeto,
            moneyIfOwned = 400
        };
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
    public static Sprite SpriteRetrato(string path)
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

    public static Sprite SpriteSimple(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        // Un PNG recien copiado puede no estar importado todavia.
        if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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

    // Las cuatro poses quietas de una hoja, en el orden Abajo / Izquierda /
    // Derecha / Arriba. Para los NPCs que se dan vuelta solos en el juego (el
    // guardia del Data Center que te ve entrar). Corta la hoja igual que
    // Personaje, asi que no cambia nada para las escenas que ya la usan.
    public static Sprite[] Poses(string hoja)
    {
        Sprite[] cortada = Hoja(hoja);
        return new[] { Frame(cortada, Abajo), Frame(cortada, Izquierda), Frame(cortada, Derecha), Frame(cortada, Arriba) };
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
        // 32 pixeles por unidad, como los tiles. El personaje termina de medir lo
        // que tiene que medir con EscalaDelVisual, que lo empareja con el jugador.
        importer.spritePixelsPerUnit = PpuDeLasHojas;
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
