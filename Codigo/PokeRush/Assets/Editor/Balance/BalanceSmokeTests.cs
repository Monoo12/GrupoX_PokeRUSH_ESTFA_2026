using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Lo que el rebalanceo del 6-oct-2026 garantiza y no se tiene que perder al
// tocar un equipo o una ruleta:
//   - los rivales del recorrido pelean a Nv 36 o mas, casi todos con formas finales;
//   - todo lo que se le entrega al jugador (inicial, liberados, ruletas, Mew)
//     es forma final y a la altura de su etapa;
//   - los pozos de las ruletas y del entrenamiento del chip son formas finales
//     que existen en el proyecto.
//
//   PokeRush > Balance > Correr smoke tests del balance
//
// Para la DIFICULTAD (cuanto se gana cada pelea) esta el simulador:
// PokeRush > Balance > Simular la partida.
public static class BalanceSmokeTests
{
    private static int _passed, _failed;

    private const string Defs = "Assets/Resources/Definitions/";

    // Los rivales del recorrido de la historia, en orden.
    public static readonly string[] RivalesDelRecorrido =
    {
        "Tutorial/RivalDelTutorial",
        "Torre1/Nestor", "Torre1/Nestor_SinMelmetal",
        "Casino/ApostadorCasino", "Casino/MalabaristaCasino", "Casino/MotoristaCasino", "Casino/PokemaniacoCasino",
        "Casino/GuardiaAscensor",
        "Casino/BailarinaCasino", "Casino/JovenRicaCasino", "Casino/LadronCasino", "Casino/MagnateCasino",
        "Casino/MediumCasino", "Casino/ModeloCasino", "Casino/TahurCasino", "Casino/GuardiaVipCasino",
        "Casino/CrupieraCasino", "Casino/GangsterCasino", "Casino/GuayCasino", "Casino/KaratekaCasino",
        "Casino/MacarraCasino", "Casino/VeteranoCasino",
        "Casino/JefeFinalCasino",
        "Julieta/Julieta"
    };

    // Las ruletas que dan Pokemon y el nivel minimo de sus gajos.
    private static readonly (string tabla, int nivelMinimo)[] Ruletas =
    {
        ("Casino/RuletaDeLosNpcs", 44), ("Casino/RuletaDeLosNpcsPiso2", 44), ("Casino/RuletaDelGuardia", 44),
        ("Casino/RuletaDelGuardiaVip", 44), ("Casino/RuletaDeLaMesa", 44), ("Casino/RuletaDelEmpresario", 44),
        ("Lobby/RuletaDeMartin_Pokemon", 48)
    };

    // Las especies del proyecto que evolucionan en otra que TAMBIEN esta en el
    // proyecto (formas no finales). El juego no tiene evolucion: si una de
    // estas le toca al jugador, se queda asi toda la partida.
    public static readonly HashSet<string> NoFinales = new HashSet<string>
    {
        "Bulbasaur", "Ivysaur", "Charmander", "Charmeleon", "Squirtle", "Wartortle", "Rattata", "Pikachu", "Pichu",
        "Mankey", "Primeape", "Abra", "Kadabra", "Machop", "Machoke", "Magnemite", "Magneton", "Gastly", "Haunter",
        "Onix", "Rhyhorn", "Rhydon", "Chansey", "Happiny", "Scyther", "Electabuzz", "Elekid", "Magmar", "Magby",
        "Eevee", "Porygon", "Porygon2", "Dratini", "Dragonair", "Chikorita", "Bayleef", "Cyndaquil", "Quilava",
        "Totodile", "Croconaw", "Togepi", "Togetic", "Yanma", "Gligar", "Sneasel", "Swinub", "Piloswine",
        "Houndour", "Phanpy", "Larvitar", "Pupitar", "Treecko", "Grovyle", "Torchic", "Combusken", "Mudkip",
        "Marshtomp", "Ralts", "Kirlia", "Shroomish", "Aron", "Lairon", "Budew", "Roselia", "Trapinch", "Vibrava",
        "Swablu", "Feebas", "Shuppet", "Duskull", "Dusclops", "Snorunt", "Bagon", "Shelgon", "Beldum", "Metang",
        "Turtwig", "Grotle", "Chimchar", "Monferno", "Piplup", "Prinplup", "Shinx", "Luxio", "Drifloon", "Gible",
        "Gabite", "Munchlax", "Riolu", "Hippopotas", "Skorupi", "Snivy", "Servine", "Tepig", "Pignite", "Oshawott",
        "Dewott", "Drilbur", "Timburr", "Gurdurr", "Venipede", "Whirlipede", "Basculin", "Sandile", "Krokorok",
        "Darumaka", "Yamask", "Solosis", "Duosion", "Karrablast", "Foongus", "Ferroseed", "Litwick", "Lampent",
        "Axew", "Fraxure", "Pawniard", "Bisharp", "Deino", "Zweilous", "Larvesta", "Chespin", "Quilladin",
        "Fennekin", "Braixen", "Froakie", "Frogadier", "Fletchling", "Fletchinder", "Honedge", "Doublade", "Skrelp",
        "Goomy", "Sliggoo", "Phantump", "Pumpkaboo", "Noibat", "Rowlet", "Dartrix", "Litten", "Torracat", "Popplio",
        "Brionne", "Grubbin", "Charjabug", "Rockruff", "Mareanie", "Mudbray", "Bounsweet", "Steenee", "Wimpod",
        "Jangmo-o", "Hakamo-o", "Cosmog", "Cosmoem", "Meltan", "Grookey", "Thwackey", "Scorbunny", "Raboot",
        "Sobble", "Drizzile", "Rookidee", "Corvisquire", "Chewtle", "Toxel", "Hatenna", "Hattrem", "Impidimp",
        "Morgrem", "Cufant", "Dreepy", "Drakloak", "Kubfu", "Sprigatito", "Floragato", "Fuecoco", "Crocalor",
        "Quaxly", "Quaxwell", "Nacli", "Naclstack", "Charcadet", "Tinkatink", "Tinkatuff", "Frigibax", "Arctibax",
        "Código Cero"
    };

    [MenuItem("PokeRush/Balance/Correr smoke tests del balance")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        TestRivalesDelRecorrido();
        TestLoQueRecibeElJugador();
        TestRuletas();
        TestPozos();

        if (_failed == 0) Debug.Log($"[BalanceSmokeTests] TODOS OK: {_passed} checks pasaron.");
        else Debug.LogError($"[BalanceSmokeTests] {_failed} FALLARON, {_passed} pasaron. Revisar errores arriba.");
    }

    private static void Check(bool ok, string nombre)
    {
        if (ok) { _passed++; return; }
        _failed++;
        Debug.LogError($"[BalanceSmokeTests] FALLO: {nombre}");
    }

    private static EnemyTeamEntry[] EquipoDe(NPCDefinition def)
        => def is EnemyDefinition e ? e.pokenTeam : def is BossDefinition b ? b.pokenTeam : null;

    private static void TestRivalesDelRecorrido()
    {
        int total = 0, finales = 0;
        foreach (string ruta in RivalesDelRecorrido)
        {
            var def = AssetDatabase.LoadAssetAtPath<NPCDefinition>($"{Defs}{ruta}.asset");
            EnemyTeamEntry[] equipo = def != null ? EquipoDe(def) : null;
            Check(equipo != null && equipo.Length > 0 && equipo.All(e => e != null && e.species != null),
                  $"Rival {ruta}: existe y tiene equipo");
            if (equipo == null) continue;

            int minimo = equipo.Min(e => e.level);
            Check(minimo >= 36, $"Rival {ruta}: todos a Nv 36 o mas (el mas bajo es Nv {minimo})");
            foreach (EnemyTeamEntry e in equipo.Where(e => e?.species != null))
            {
                total++;
                if (!NoFinales.Contains(e.species.pokenName)) finales++;
            }
        }
        Check(total > 0 && finales >= total * 0.9f,
              $"Rivales: al menos el 90% de sus Pokemon son formas finales ({finales} de {total})");

        // Los jefes, un escalon arriba de lo que los rodea.
        int Maximo(string r) => EquipoDe(AssetDatabase.LoadAssetAtPath<NPCDefinition>($"{Defs}{r}.asset"))?.Max(e => e.level) ?? 0;
        Check(Maximo("Julieta/Julieta") > Maximo("Casino/JefeFinalCasino"), "Julieta tiene el Pokemon de nivel mas alto que Reymar");
        Check(Maximo("Casino/JefeFinalCasino") >= Maximo("Casino/VeteranoCasino"), "Reymar no queda por debajo de los NPC de su ultimo piso");
    }

    private static void TestLoQueRecibeElJugador()
    {
        int inicial = SimuladorDeBalance.NivelDeLosIniciales();
        Check(inicial >= 40, $"Inicial: Nv 40 o mas desde la creacion de personaje (es Nv {inicial})");

        foreach (var (prefab, especie, nivelMinimo) in new[] { ("NPC_Basculin", "Basculegion", 40), ("NPC_Combusken", "Blaziken", 40) })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/NPCs/{prefab}.prefab");
            var lib = go != null ? go.GetComponentInChildren<LiberarPokenTrigger>(true) : null;
            var so = lib != null ? new SerializedObject(lib) : null;
            var poken = so?.FindProperty("poken").objectReferenceValue as PokenDefinition;
            int nivel = so?.FindProperty("nivel").intValue ?? 0;
            string pregunta = so?.FindProperty("pregunta").stringValue ?? "";
            Check(poken != null && poken.pokenName == especie && nivel >= nivelMinimo,
                  $"Liberado {prefab}: {especie} a Nv {nivelMinimo}+ (es {poken?.pokenName} Nv {nivel})");
            Check(pregunta.StartsWith(especie), $"Liberado {prefab}: la pregunta nombra a {especie}");
        }

        int mew = SimuladorDeBalance.NivelDelMewDeTacconi();
        Check(mew >= 48, $"El Mew de Tacconi llega a la altura del final (Nv {mew})");
    }

    private static void TestRuletas()
    {
        foreach (var (ruta, nivelMinimo) in Ruletas)
        {
            var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>($"{Defs}{ruta}.asset");
            var pokemon = tabla?.premios?.Where(p => p != null && p.poken != null).ToList();
            Check(pokemon != null && pokemon.Count > 0, $"Ruleta {ruta}: existe y tiene gajos de Pokemon");
            if (pokemon == null || pokemon.Count == 0) continue;
            int minimo = pokemon.Min(p => p.pokenLevel);
            Check(minimo >= nivelMinimo, $"Ruleta {ruta}: sus Pokemon salen a Nv {nivelMinimo}+ (el mas bajo, Nv {minimo})");
        }
    }

    private static void TestPozos()
    {
        var indice = Resources.Load<IndiceDeDefiniciones>(IndiceDeDefiniciones.RutaEnResources);
        var nombres = new HashSet<string>(indice != null ? indice.pokens.Select(e => e.nombre) : new string[0]);

        var pozos = new Dictionary<string, string[]>
        {
            { "ruletas: Poke Ball", VariedadDeLasRuletas.Comunes }, { "ruletas: Super Ball", VariedadDeLasRuletas.Buenas },
            { "ruletas: Ultra Ball", VariedadDeLasRuletas.Raras }, { "ruletas: Master Ball", VariedadDeLasRuletas.MuyRaras },
            { "chip: comunes", EntrenamientoDelChip.Comunes }, { "chip: fuertes", EntrenamientoDelChip.Fuertes },
            { "chip: elite", EntrenamientoDelChip.Elite }
        };
        foreach (var pozo in pozos)
        {
            var faltan = pozo.Value.Where(n => !nombres.Contains(n)).ToList();
            var noFinales = pozo.Value.Where(n => NoFinales.Contains(n)).ToList();
            Check(faltan.Count == 0, $"Pozo {pozo.Key}: todas las especies existen ({string.Join(", ", faltan)})");
            Check(noFinales.Count == 0, $"Pozo {pozo.Key}: solo formas finales ({string.Join(", ", noFinales)})");
        }
    }
}
