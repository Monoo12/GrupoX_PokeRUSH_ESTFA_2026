using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton: una sola instancia accesible desde cualquier lado con GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource; // se asignan en el Inspector
    [SerializeField] private AudioSource sfxSource;

    [Header("Equipo inicial")]
    // Se busca por pokenName en DefinitionRepository, asi que el nombre tiene
    // que coincidir con el campo pokenName del asset (no con el del archivo).
    // Gastly es Fantasma/Veneno: INMUNE a Normal, o sea que los STAB de
    // Raticate y Porygon-Z le hacen cero. Basculin aguanta a Braixen y
    // Combusken le pega a Melmetal y Porygon-Z con Lucha. Uno por amenaza.
    [SerializeField] private string[] equipoInicial = { "Gastly", "Basculin", "Combusken" };
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

    public void NewGame(string playerName)
    {
        Story = new StoryModel();
        Tower = new TowerProgressModel(Story);
        CurrentPlayer = new PlayerModel(playerName);

        ArmarEquipoInicial();

        Story.ActivateFlag(StoryFlag.GameStarted);
        Story.ActivateFlag(StoryFlag.ReceivedStarterPoken);
        ChangeState(GameState.Exploration);
    }

    // El equipo fijo con el que se entra a la torre 1. Si alguna especie no
    // esta en Resources avisamos fuerte: sin equipo el combate aborta con un
    // warning mudo en BattleController.BeginBattle.
    private void ArmarEquipoInicial()
    {
        for (int i = 0; i < equipoInicial.Length; i++)
        {
            PokenDefinition especie = DefinitionRepository.Instance.GetPoken(equipoInicial[i]);
            if (especie == null)
            {
                Debug.LogError($"[GameManager] No encuentro la especie '{equipoInicial[i]}' " +
                               "para el equipo inicial. Revisa que el pokenName coincida.");
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
    