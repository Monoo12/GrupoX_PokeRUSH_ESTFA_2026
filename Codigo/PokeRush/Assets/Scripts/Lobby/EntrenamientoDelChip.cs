using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Entrenamiento intenso del chip: la torre de batalla de la Quarcho Cueva
// (playtest del 5-oct: "debe haber alguna fuente de dinero en la cueva aparte
// del poker, como una opcion llamada ENTRENAMIENTO INTENSO DEL CHIP, que
// funcione como una torre de batalla de los pokemons originales").
//
// Lo ofrece Tixi desde su menu, despues del casino (flagQueLoAbre): antes no,
// porque la economia del casino cuenta con que el jugador llega justo y tiene
// que jugar o pelear para el pase del ascensor. Las reglas, como la torre:
//
//   - El chip arma rivales virtuales, uno detras de otro, con tres Pokemon.
//   - Antes de cada combate el equipo se cura entero.
//   - Cada victoria paga (moneyReward del rival) y sube la racha. El premio
//     crece con la racha: 400, 500, 600... hasta 1200.
//   - Despues de cada victoria se elige seguir o retirarse con lo ganado.
//   - Perder corta la racha: la plata ganada ya quedo en el bolsillo.
//   - Al terminar, gane o pierda, el equipo vuelve curado.
//
// La dificultad sube con la racha: el nivel arranca 5 por debajo del Pokemon
// mas alto del equipo y sube uno por combate (hasta 5 por encima); de la
// quinta en adelante salen especies mas fuertes y desde la decima, los
// pseudolegendarios. La IA juega mejor en la cuarta y en la septima. Los
// rivales son todos evolucionados: a 3 niveles por debajo, un Espeon le gana
// de un golpe al Gastly del equipo de prueba (5-oct), por eso el margen de 5.
//
// Vive FUERA de MapaRaiz: el combate esconde el mapa (MapBattleBridge) y eso
// mataria las corrutinas de cualquier cosa que este adentro.
public class EntrenamientoDelChip : MonoBehaviour
{
    [Header("Referencias (opcional: se buscan solas)")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private DialogueController dialogueController;

    [Header("Cuando esta")]
    [Tooltip("None = siempre. Por defecto, recien despues del casino.")]
    [SerializeField] private StoryFlag flagQueLoAbre = StoryFlag.Casino_Completado;

    [Header("Quien lo presenta")]
    [SerializeField] private string nombre = "TIXI";
    [SerializeField] private Sprite retrato;

    [Header("Premios")]
    [Min(0)] [SerializeField] private int premioInicial = 400;
    [Min(0)] [SerializeField] private int premioPorRacha = 100;
    [Min(0)] [SerializeField] private int premioMaximo = 1200;

    [Header("Rivales")]
    [Range(1, 6)] [SerializeField] private int pokemonPorRival = 3;
    [Tooltip("El primer rival va estos niveles por debajo del Pokemon mas alto del equipo.")]
    [SerializeField] private int nivelesDeVentaja = 5;
    [Tooltip("Tope: nunca mas de estos niveles por encima del Pokemon mas alto.")]
    [SerializeField] private int nivelesPorEncimaMaximo = 5;
    [Tooltip("Desde esta racha salen las especies fuertes.")]
    [SerializeField] private int rachaDeLosFuertes = 5;
    [Tooltip("Desde esta racha se suman los pseudolegendarios.")]
    [SerializeField] private int rachaDeLaElite = 10;
    [Tooltip("Frente del entrenador virtual: uno al azar por combate.")]
    [SerializeField] private Sprite[] entrenadores = new Sprite[0];
    [SerializeField] private AIProfileDefinition iaDeLosPrimeros;   // racha 1-3
    [SerializeField] private AIProfileDefinition iaDelMedio;        // racha 4-6
    [SerializeField] private AIProfileDefinition iaDeLosUltimos;    // racha 7+

    [Header("Textos")]
    [SerializeField, TextArea(2, 4)] private string[] explicacion =
    {
        "El chip te conecta a una simulación: entrenadores virtuales, uno detrás de otro, con tres Pokémon cada uno.",
        "Antes de cada combate te curo el equipo. Cada victoria paga más que la anterior; si perdés, se corta la racha."
    };
    [Tooltip("{0} = lo que paga el primer combate.")]
    [SerializeField, TextArea(2, 3)] private string preguntaEntrar = "¿Te conecto? El primer combate paga {0} monedas.";
    [SerializeField] private string opcionEntrar = "Conectarme";
    [SerializeField] private string opcionNoEntrar = "Ahora no";
    [Tooltip("{0} = el numero de combate.")]
    [SerializeField, TextArea(2, 3)] private string antesDelCombate = "Simulación {0}: un entrenador virtual te desafía.";
    [Tooltip("{0} = la racha, {1} = lo que paga el proximo.")]
    [SerializeField, TextArea(2, 3)] private string preguntaSeguir = "¡Racha de {0}! El próximo combate paga {1}. ¿Seguís?";
    [SerializeField] private string opcionSeguir = "Seguir";
    [SerializeField] private string opcionRetirarse = "Retirarme";
    [Tooltip("{0} = la racha, {1} = lo ganado en total.")]
    [SerializeField, TextArea(2, 3)] private string mensajeRetiro =
        "Te desconecto con una racha de {0}. Ganaste {1} monedas en total y tu equipo quedó curado.";
    [Tooltip("{0} = el combate en que perdio, {1} = lo ganado en total.")]
    [SerializeField, TextArea(2, 3)] private string mensajeDerrota =
        "La simulación te ganó en el combate {0}. Te quedás con las {1} monedas que ganaste. Tu equipo quedó curado.";
    [SerializeField, TextArea(2, 3)] private string mensajeDerrotaSinPremio =
        "La simulación te ganó de entrada. Tu equipo quedó curado: probá de nuevo cuando quieras.";
    [SerializeField, TextArea(2, 3)] private string mensajeSinCombate =
        "El chip no puede armar la simulación ahora. Probá más tarde.";

    // ---------- los rivales ----------

    // Evolucionados conocidos, de poder parejo: el nivel hace la dificultad.
    public static readonly string[] Comunes =
    {
        "Raichu", "Primeape", "Machamp", "Magneton", "Rhydon", "Electabuzz", "Magmar", "Pinsir",
        "Heracross", "Houndoom", "Donphan", "Breloom", "Flygon", "Altaria", "Vaporeon", "Jolteon",
        "Flareon", "Espeon", "Steelix", "Skarmory", "Banette", "Dusclops", "Glalie", "Luxray",
        "Roserade", "Leafeon", "Glaceon", "Froslass", "Bisharp", "Krookodile", "Lycanroc", "Noivern",
        "Talonflame", "Toxtricity", "Corviknight", "Gliscor", "Yanmega"
    };

    // Los que pegan fuerte: iniciales finales y los favoritos de siempre.
    public static readonly string[] Fuertes =
    {
        "Charizard", "Blastoise", "Venusaur", "Typhlosion", "Feraligatr", "Meganium", "Sceptile",
        "Blaziken", "Swampert", "Torterra", "Infernape", "Empoleon", "Serperior", "Emboar", "Samurott",
        "Chesnaught", "Delphox", "Greninja", "Decidueye", "Incineroar", "Primarina", "Rillaboom",
        "Cinderace", "Inteleon", "Meowscarada", "Skeledirge", "Quaquaval", "Scizor", "Gengar",
        "Alakazam", "Lucario", "Gardevoir", "Gallade", "Umbreon", "Sylveon", "Togekiss", "Magnezone",
        "Rhyperior", "Electivire", "Magmortar", "Mamoswine", "Weavile", "Chandelure", "Haxorus",
        "Excadrill", "Conkeldurr", "Aegislash", "Mimikyu", "Hatterene", "Grimmsnarl", "Tinkaton",
        "Ceruledge", "Armarouge", "Kingambit", "Volcarona", "Snorlax", "Lapras", "Aerodactyl"
    };

    public static readonly string[] Elite =
    {
        "Dragonite", "Tyranitar", "Salamence", "Metagross", "Garchomp", "Hydreigon", "Goodra",
        "Kommo-o", "Dragapult", "Baxcalibur"
    };

    public bool EnCurso { get; private set; }
    public int Racha { get; private set; }
    public int GanadoEnLaSesion { get; private set; }
    public StoryFlag FlagQueLoAbre => flagQueLoAbre;

    private PlayerModel _jugador;
    private System.Action _alTerminar;
    private EnemyDefinition _rival;
    private readonly System.Random _rng = new System.Random();

    private void Awake()
    {
        if (battleController == null)
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    private void OnDestroy()
    {
        if (battleController != null) battleController.BattleEnded -= AlTerminarElCombate;
        BorrarRival();
    }

    public bool Disponible(StoryModel story)
        => battleController != null
           && (flagQueLoAbre == StoryFlag.None || (story != null && story.HasFlag(flagQueLoAbre)));

    // Lo que paga el combate numero 'combate' (1 = el primero).
    public int PremioDe(int combate)
        => Mathf.Min(premioMaximo, premioInicial + premioPorRacha * Mathf.Max(0, combate - 1));

    // Tixi elige la opcion: explica, pregunta y, si dice que si, arranca.
    public void Ofrecer(PlayerModel jugador, System.Action alTerminar = null)
    {
        if (EnCurso || jugador == null) { alTerminar?.Invoke(); return; }

        Decir(explicacion, () =>
            CasinoDialogo.Preguntar(dialogueController, nombre, retrato,
                string.Format(preguntaEntrar, PremioDe(1)), opcionEntrar, opcionNoEntrar,
                entrar =>
                {
                    if (entrar) Empezar(jugador, alTerminar);
                    else alTerminar?.Invoke();
                }));
    }

    // Sin cajas: lo usan Ofrecer y los tests.
    public void Empezar(PlayerModel jugador, System.Action alTerminar = null)
    {
        if (EnCurso || jugador == null) return;
        EnCurso = true;
        Racha = 0;
        GanadoEnLaSesion = 0;
        _jugador = jugador;
        _alTerminar = alTerminar;
        SiguienteCombate();
    }

    private void SiguienteCombate()
    {
        _jugador.CurarEquipo();
        int combate = Racha + 1;

        BorrarRival();
        _rival = ArmarRival(combate, NivelDelEquipo(_jugador), _rng.Next());
        if (_rival == null || _rival.pokenTeam == null || _rival.pokenTeam.Length == 0)
        {
            Decir(new[] { mensajeSinCombate }, () => Terminar(retirado: true));
            return;
        }

        Decir(new[] { string.Format(antesDelCombate, combate) }, () =>
        {
            battleController.BattleEnded += AlTerminarElCombate;
            battleController.StartBattle(_jugador, _rival);
            Debug.Log($"[Chip] Combate {combate}: {DescribirRival(_rival)} (paga {_rival.moneyReward}).");
        });
    }

    private void AlTerminarElCombate(BattleOutcome outcome)
    {
        battleController.BattleEnded -= AlTerminarElCombate;
        Debug.Log($"[Chip] Combate {Racha + 1} terminado: " +
                  (outcome == null ? "sin resultado" : outcome.PlayerVictory ? "gano" : outcome.Escaped ? "escapo" : "perdio") +
                  $" (paga {outcome?.MoneyEarned ?? 0}).");
        StartCoroutine(DespuesDelCombate(outcome));
    }

    private IEnumerator DespuesDelCombate(BattleOutcome outcome)
    {
        // Que vuelva el mapa (MapBattleBridge espera un poco) antes de hablar.
        PlayerController mapa = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        float limite = Time.unscaledTime + 5f;
        while (Time.unscaledTime < limite
               && (battleController.EnCurso || (mapa != null && !mapa.gameObject.activeInHierarchy)))
            yield return null;
        yield return null;

        if (outcome == null || !outcome.PlayerVictory)
        {
            Terminar(retirado: false);
            yield break;
        }

        Racha++;
        GanadoEnLaSesion += outcome.MoneyEarned;
        CasinoDialogo.Guardar();

        CasinoDialogo.Preguntar(dialogueController, nombre, retrato,
            string.Format(preguntaSeguir, Racha, PremioDe(Racha + 1)), opcionSeguir, opcionRetirarse,
            seguir =>
            {
                if (seguir) SiguienteCombate();
                else Terminar(retirado: true);
            });
    }

    private void Terminar(bool retirado)
    {
        if (_jugador != null) _jugador.CurarEquipo();
        CasinoDialogo.Guardar();
        BorrarRival();

        string mensaje = retirado ? string.Format(mensajeRetiro, Racha, GanadoEnLaSesion)
            : GanadoEnLaSesion > 0 ? string.Format(mensajeDerrota, Racha + 1, GanadoEnLaSesion)
            : mensajeDerrotaSinPremio;
        Debug.Log($"[Chip] Fin: racha {Racha}, {GanadoEnLaSesion} monedas ({(retirado ? "se retiro" : "perdio")}).");

        System.Action alTerminar = _alTerminar;
        _alTerminar = null;
        _jugador = null;
        EnCurso = false;
        Decir(new[] { mensaje }, alTerminar);
    }

    // ---------- armar el rival (sin escena: lo usan los tests) ----------

    // El nivel del Pokemon mas alto del equipo.
    public static int NivelDelEquipo(PlayerModel jugador)
    {
        int nivel = 1;
        if (jugador != null)
            foreach (PokenModel p in jugador.Team)
                if (p != null && p.Level > nivel) nivel = p.Level;
        return nivel;
    }

    public int NivelDelRival(int combate, int nivelDelEquipo)
    {
        int nivel = nivelDelEquipo - nivelesDeVentaja + Mathf.Max(0, combate - 1);
        nivel = Mathf.Min(nivel, nivelDelEquipo + nivelesPorEncimaMaximo);
        return Mathf.Clamp(nivel, 5, 100);
    }

    public string[] PozoDe(int combate)
    {
        if (combate >= rachaDeLaElite)
        {
            var todos = new List<string>(Fuertes);
            todos.AddRange(Elite);
            return todos.ToArray();
        }
        return combate >= rachaDeLosFuertes ? Fuertes : Comunes;
    }

    public AIProfileDefinition IaDe(int combate)
        => combate >= 7 && iaDeLosUltimos != null ? iaDeLosUltimos
         : combate >= 4 && iaDelMedio != null ? iaDelMedio
         : iaDeLosPrimeros;

    // Un EnemyDefinition en memoria: no es un asset, se borra al terminar.
    public EnemyDefinition ArmarRival(int combate, int nivelDelEquipo, int semilla)
    {
        var rng = new System.Random(semilla);
        int nivel = NivelDelRival(combate, nivelDelEquipo);

        var libres = new List<string>(PozoDe(combate));
        var equipo = new List<EnemyTeamEntry>();
        while (equipo.Count < pokemonPorRival && libres.Count > 0)
        {
            int i = rng.Next(libres.Count);
            PokenDefinition especie = DefinitionRepository.Instance.GetPoken(libres[i]);
            libres.RemoveAt(i);
            if (especie != null) equipo.Add(new EnemyTeamEntry { species = especie, level = nivel });
        }
        if (equipo.Count == 0) return null;

        var rival = ScriptableObject.CreateInstance<EnemyDefinition>();
        rival.name = "SimulacionDelChip";
        rival.hideFlags = HideFlags.DontSave;
        rival.characterName = "Entrenador virtual";
        rival.pokenTeam = equipo.ToArray();
        rival.moneyReward = PremioDe(combate);
        rival.aiProfile = IaDe(combate);
        rival.itemRewards = new HealItemDefinition[0];
        if (entrenadores != null && entrenadores.Length > 0)
            rival.battleFrontSprite = entrenadores[rng.Next(entrenadores.Length)];
        return rival;
    }

    private static string DescribirRival(EnemyDefinition rival)
    {
        var partes = new List<string>();
        foreach (EnemyTeamEntry e in rival.pokenTeam)
            partes.Add($"{e.species.pokenName} Nv {e.level}");
        return string.Join(", ", partes);
    }

    private void BorrarRival()
    {
        if (_rival == null) return;
        if (Application.isPlaying) Destroy(_rival);
        else DestroyImmediate(_rival);
        _rival = null;
    }

    private void Decir(string[] lineas, System.Action alCerrar)
        => CasinoDialogo.Decir(dialogueController, nombre, retrato, lineas, alCerrar);
}
