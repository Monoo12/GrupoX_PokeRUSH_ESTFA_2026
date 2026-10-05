using UnityEditor;
using UnityEngine;

// Los assets del tutorial de la habitacion: lo que dice cada parte y el rival
// de la pelea de practica.
//
// Misma regla que la Quarcho Cueva y el casino: se crean una vez y NO se pisan
// nunca. Los textos son del equipo (estos son de relleno, pero ya explican los
// controles de verdad) y el rival es balance que se retoca en el Inspector:
// volver a correr el builder no borra nada que alguien haya cambiado.
//
// Los nombres que aparecen en los textos son los que ve el jugador: en el menu
// de pausa la mochila es "Bolsa" y el equipo "Pokemon"; en el submenu del
// equipo el resumen es "DATOS"; en el combate, LUCHAR / POKÉMON / MOCHILA / HUIR.
public static class TutorialAssets
{
    public const string Carpeta = "Assets/Resources/Definitions/Tutorial";
    public const string Dialogos = Carpeta + "/Dialogos";
    public const string RutaDelRival = Carpeta + "/RivalDelTutorial.asset";

    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Perfiles = "Assets/Resources/Definitions/AIProfiles/";
    public const string RutaPocion = "Assets/Resources/Definitions/Items/Pocion.asset";

    // El rival: la persona que esta en el cuarto. Nombre, hoja y retrato son
    // PROVISORIOS (del pack, ya versionados): los decide el equipo.
    public const string NombreDelRival = "COMPAÑERA";
    public const string HojaDelRival = "chica1";
    public const string RetratoDelRival = "Assets/Graphics/Trainers/CHICA.png";
    // Antes Interior_bg (un blanco liso): ver FondosDeCombateBuilder.
    private const string FondoDeLaPelea = FondosDeCombateBuilder.FondoDelRivalDelTutorial;

    // Un solo Pokemon, flojo: el inicial llega a Nv 20 desde la creacion de
    // personaje. Normal a proposito: a Lucario le pega doble lo de Lucha y a
    // Gengar no le hace nada, que es justo lo que explica la charla de antes.
    private const string EspecieDelRival = "0133_Eevee";
    private const int NivelDelRival = 10;
    private const int PlataDelRival = 100;

    // ---------- dialogos (texto del equipo: no se pisan) ----------

    public static DialogueDefinition Despertar() => Dialogo("Tutorial_Despertar", "", null,
        "Te despertás en tu habitación. Hoy empieza todo.",
        "Movete con las flechas o con WASD. Mantené Shift para correr.",
        "Parate al lado de algo y apretá X para usarlo. Con Z abrís el menú.",
        "Lo que brilla te enseña lo básico. Cuando termines con todo, la puerta se abre.");

    public static DialogueDefinition Mochila() => Dialogo("Tutorial_Mochila", "", null,
        "Tu mochila. Acá va todo lo que juntes: medicinas, objetos de combate y objetos clave.",
        "Una Poción le devuelve PS a un Pokémon: la elegís y después a quién dársela.",
        "En combate también sirve, pero usarla te gasta el turno.",
        "En el menú (Z) la vas a encontrar como Bolsa.");

    public static DialogueDefinition Equipo() => Dialogo("Tutorial_Equipo", "", null,
        "Tu equipo: hasta seis Pokémon. El primero de la lista es el que sale a pelear.",
        "Elegí un Pokémon y entrá a DATOS para ver sus stats, su tipo y sus movimientos.",
        "Desde DATOS también podés reordenar sus movimientos.",
        "En el menú (Z) el equipo está como Pokemon.");

    public static DialogueDefinition Pc() => Dialogo("Tutorial_Pc", "", null,
        "Tu PC. Guarda los Pokémon que no entran en el equipo: ocho cajas de treinta.",
        "Si ganás un Pokémon con el equipo lleno, viene directo acá.",
        "Hay un PC en cada nivel, así que podés rearmar el equipo cuando quieras.");

    public static DialogueDefinition Guardar() => Dialogo("Tutorial_Guardar", "", null,
        "Tu diario. Guardar la partida es anotar hasta dónde llegaste.",
        "Para guardar, abrí el menú con Z y elegí Guardar. Con F2 guardás en cualquier momento.",
        "Al volver a jugar, Continuar te deja en la Quarcho Cueva con todo lo guardado.");

    public static DialogueDefinition Opciones() => Dialogo("Tutorial_Opciones", "", null,
        "La tele. En Opciones cambiás el volumen de la música y de los efectos.",
        "También podés cambiar la tecla de cada acción, si las de siempre no te sirven.",
        "En el menú (Z) están como Opciones.");

    public static DialogueDefinition Pelea(Sprite retrato) => Dialogo("Tutorial_Pelea", NombreDelRival, retrato,
        "¡Por fin te despertaste! Antes de salir, una de práctica.",
        "LUCHAR: elegís un movimiento. Al lado de cada uno ves su tipo, sus PP, su potencia y su precisión.",
        "Y fijate la tabla de tipos: el Agua apaga el Fuego, el Fuego quema la Planta y la Planta absorbe el Agua.",
        "Si tu ataque le gana al tipo del rival pega el doble: es súper eficaz. Si pierde, pega la mitad.",
        "Y hay tipos que ni se tocan: lo Eléctrico no le hace nada a la Tierra. Pensá contra qué peleás antes de pegar.",
        "MOCHILA: usás un objeto, pero se te va el turno. POKÉMON: cambiás al que pelea. HUIR: solo contra salvajes.",
        "Si perdés no pasa nada: te curo y probamos de nuevo. ¡Vamos!");

    public static DialogueDefinition Cierre() => Dialogo("Tutorial_Cierre", "", null,
        "¡Listo! Ya sabés lo básico.",
        "Afuera te espera la Quarcho Cueva: desde ahí salís a cada nivel y volvés al terminarlo.",
        "La puerta ya abre: la flecha marca la salida. ¡Suerte!");

    private static DialogueDefinition Dialogo(string archivo, string hablante, Sprite retrato, params string[] lineas)
    {
        MenuUiKit.EnsureFolder(Dialogos);
        string path = $"{Dialogos}/{archivo}.asset";
        var def = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path);
        if (def != null) return def;

        def = ScriptableObject.CreateInstance<DialogueDefinition>();
        def.speakerName = hablante;
        def.speakerPortrait = retrato;
        def.lines = lineas;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // ---------- el rival (balance: se crea una vez) ----------

    public static EnemyDefinition Rival(Sprite retrato)
    {
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RutaDelRival);
        if (def != null) return def;

        MenuUiKit.EnsureFolder(Carpeta);
        def = ScriptableObject.CreateInstance<EnemyDefinition>();
        def.characterName = NombreDelRival;
        def.moneyReward = PlataDelRival;
        // Sin flag: la escena no tiene EventController. La parte de la pelea
        // activa Tutorial_Pelea por su cuenta al ganar.
        def.defeatGrantsFlag = StoryFlag.None;
        def.fleeIfWeak = false;

        var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{EspecieDelRival}.asset");
        if (especie == null) Debug.LogError($"[Tutorial] No existe la especie {EspecieDelRival}.");
        def.pokenTeam = new[] { new EnemyTeamEntry { species = especie, level = NivelDelRival } };

        // Wild: elige al azar. Es la de practica: no tiene que jugar fino.
        def.aiProfile = AssetDatabase.LoadAssetAtPath<AIProfileDefinition>($"{Perfiles}Wild.asset");
        def.battleFrontSprite = retrato;
        def.battleBackground = AssetDatabase.LoadAssetAtPath<Sprite>(FondoDeLaPelea);
        if (def.battleBackground == null)
            Debug.LogWarning($"[Tutorial] No se pudo cargar {FondoDeLaPelea}: la pelea usa el fondo por defecto.");

        AssetDatabase.CreateAsset(def, RutaDelRival);
        return def;
    }

    public static HealItemDefinition Pocion()
    {
        var pocion = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(RutaPocion);
        if (pocion == null) Debug.LogError($"[Tutorial] No existe {RutaPocion}: la mochila no da nada.");
        return pocion;
    }

    // Todos los dialogos, para los smoke tests.
    public static readonly string[] ArchivosDeDialogo =
    {
        "Tutorial_Despertar", "Tutorial_Mochila", "Tutorial_Equipo", "Tutorial_Pc",
        "Tutorial_Guardar", "Tutorial_Opciones", "Tutorial_Pelea", "Tutorial_Cierre"
    };
}
