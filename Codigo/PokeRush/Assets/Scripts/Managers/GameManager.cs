using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton: una sola instancia accesible desde cualquier lado con GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource; // se asignan en el Inspector
    [SerializeField] private AudioSource sfxSource;

    [Header("Equipo inicial")]
    // Referencias DIRECTAS a los assets, no nombres.
    //
    // Antes esto era un string[] que se resolvia por pokenName contra el
    // DefinitionRepository. Buscar por nombre es fragil de una forma que no se
    // ve hasta que rompe: renombrar una especie, un acento o un espacio de mas
    // compilan igual y fallan recien en runtime, con el equipo vacio y el
    // combate abortando. Con referencias, Unity mantiene el link solo, el
    // Inspector no deja elegir algo que no exista, y si alguien borra el asset
    // el campo queda visiblemente en None.
    //
    // Eleccion del equipo (por si se retoca): Gastly es Fantasma/Veneno, INMUNE
    // a Normal, o sea que los STAB de Raticate y Porygon-Z le hacen cero.
    // Basculin aguanta a Braixen y Combusken le pega a Melmetal y Porygon-Z
    // con Lucha. Uno por amenaza.
    [Tooltip("Arrastra los PokenDefinition. El orden define el equipo.")]
    [SerializeField] private PokenDefinition[] equipoInicial;
    [Tooltip("Nivel de cada Poken, en el mismo orden. Si faltan, se usa 5.")]
    [SerializeField] private int[] nivelesIniciales = { 15, 15, 16 };

    public GameState CurrentState { get; private set; }
    public PlayerModel CurrentPlayer { get; private set; }
    public StoryModel Story { get; private set; }

    // Lectura del progreso de la torre 1. Se recrea junto con Story.
    public TowerProgressModel Tower { get; private set; }

    private void Awake()
    {
        // Si ya existe otro GameManager (ej: volviste a una escena que tiene uno),
        // este sobra: lo destruimos y cortamos antes de pisar la instancia buena.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // sobrevive los cambios de escena
        InitializeServices();
    }

    private void InitializeServices()
    {
        // Toca el singleton lazy de la BD: fuerza crear pokerush.db y sus tablas
        _ = DatabaseService.Instance;

        // Carga todos los ScriptableObjects desde Assets/Resources/Definitions/...
        DefinitionRepository.Instance.LoadAll();

        // Tabla de XP compartida: sin esto AddXP no sube de nivel
        PokenModel.DefaultXpTable = DefinitionRepository.Instance.ExperienceTable;

        // Conecta las fuentes de audio de la escena al servicio de audio
        AudioService.Instance.Initialize(musicSource, sfxSource);
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        // Mas adelante: disparar un evento aca para que la UI/otros sistemas reaccionen
    }

    // Partida nueva con el equipo fijo del Inspector. Se usa al entrar directo a
    // una escena por Play (SceneBootstrapper) y en las herramientas de prueba.
    public void NewGame(string playerName)
    {
        NewGame(playerName, Genero.Varon, null, 0);
    }

    // Partida nueva desde la creacion de personaje: el jugador eligio genero y
    // un inicial. Si 'inicial' viene en null se cae al equipo del Inspector, asi
    // el arranque suelto por Play sigue funcionando igual que antes.
    public void NewGame(string playerName, Genero genero, PokenDefinition inicial, int nivelInicial)
    {
        Story = new StoryModel();
        Tower = new TowerProgressModel(Story);
        CurrentPlayer = new PlayerModel(playerName, genero);

        if (inicial != null)
        {
            CurrentPlayer.Team.Add(new PokenModel(inicial, Mathf.Max(1, nivelInicial)));
            Debug.Log($"[GameManager] Partida nueva de '{playerName}' ({genero}) " +
                      $"con {inicial.pokenName} Nv{nivelInicial}.");
        }
        else
        {
            ArmarEquipoInicial();
        }

        Story.ActivateFlag(StoryFlag.GameStarted);
        Story.ActivateFlag(StoryFlag.ReceivedStarterPoken);
        ChangeState(GameState.Exploration);
    }

    // El equipo fijo con el que se entra a la torre 1. Un hueco sin asignar se
    // saltea con aviso: sin equipo el combate aborta con un warning mudo en
    // BattleController.BeginBattle y no se entiende por que.
    private void ArmarEquipoInicial()
    {
        if (equipoInicial == null || equipoInicial.Length == 0)
        {
            Debug.LogError("[GameManager] No hay especies asignadas en 'Equipo inicial'. " +
                           "Arrastra los PokenDefinition en el Inspector.");
            return;
        }

        for (int i = 0; i < equipoInicial.Length; i++)
        {
            PokenDefinition especie = equipoInicial[i];
            if (especie == null)
            {
                Debug.LogError($"[GameManager] El slot {i} del equipo inicial esta vacio.");
                continue;
            }

            int nivel = i < nivelesIniciales.Length ? nivelesIniciales[i] : 5;
            CurrentPlayer.Team.Add(new PokenModel(especie, nivel));
        }

        if (CurrentPlayer.Team.Count == 0)
            Debug.LogError("[GameManager] El equipo inicial quedo vacio: no se va a poder pelear.");
    }

    public void ContinueGame()
    {
        Story = new StoryModel();
        Tower = new TowerProgressModel(Story);
        CurrentPlayer = SaveService.Instance.LoadPlayer(Story); // null si no hay guardado

        if (CurrentPlayer == null) return; // no hay partida: la UI decide que hacer

        ChangeState(GameState.Exploration);
    }

    public void SaveGame()
    {
        if (CurrentPlayer == null) return;
        SaveService.Instance.SavePlayer(CurrentPlayer, Story);
    }

#if UNITY_EDITOR
    // Herramientas de prueba (solo editor): click derecho sobre el componente
    // GameManager en el Inspector -> aparecen estas opciones en el menu.
    [ContextMenu("Crear save de prueba")]
    private void CrearSaveDePrueba()
    {
        // En modo edicion nadie llamo a InitializeServices todavia: cargamos
        // las definiciones a mano (LoadAll es idempotente, no carga dos veces).
        DefinitionRepository.Instance.LoadAll();

        NewGame("SANTI");

        // Equipo de prueba con las definiciones de Resources/Definitions/Pokens
        string[] equipoDePrueba = { "Chispa", "Aquita", "Bulbi" };
        foreach (string nombre in equipoDePrueba)
        {
            PokenDefinition definicion = DefinitionRepository.Instance.GetPoken(nombre);
            if (definicion != null)
                CurrentPlayer.Team.Add(new PokenModel(definicion, Random.Range(5, 16)));
            else
                Debug.LogWarning($"[GameManager] No se encontro la definicion '{nombre}' en Resources/Definitions/Pokens.");
        }

        // Tres jefes vencidos = 3 medallas en la tarjeta del menu de saves
        Story.ActivateFlag(StoryFlag.BossDefeated_01);
        Story.ActivateFlag(StoryFlag.BossDefeated_02);
        Story.ActivateFlag(StoryFlag.BossDefeated_03);

        SaveGame();
        // Verificacion inmediata: si HasSaveData da False aca, el guardado fallo.
        bool ok = SaveService.Instance.HasSaveData();
        Debug.Log($"[GameManager] Save de prueba creado ({CurrentPlayer.Team.Count} pokens). HasSaveData: {ok}. " +
            (ok ? "Press Start ahora va al menu de saves." : "ALGO FALLO: el save no se encuentra."));
    }

    [ContextMenu("Borrar save")]
    private void BorrarSave()
    {
        SaveService.Instance.DeleteSave();
        Debug.Log("[GameManager] Save borrado. Press Start ahora va al juego nuevo.");
    }
#endif

}
    