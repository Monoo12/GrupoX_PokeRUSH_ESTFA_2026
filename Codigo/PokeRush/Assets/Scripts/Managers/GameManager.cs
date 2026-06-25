using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton: una sola instancia accesible desde cualquier lado con GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource; // se asignan en el Inspector
    [SerializeField] private AudioSource sfxSource;

    public GameState CurrentState { get; private set; }
    public PlayerModel CurrentPlayer { get; private set; }
    public StoryModel Story { get; private set; }

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
        CurrentPlayer = new PlayerModel(playerName);
        // (el equipo inicial de Pokens se asigna en otra tarea)
        ChangeState(GameState.Exploration);
    }

    public void ContinueGame()
    {
        Story = new StoryModel();
        CurrentPlayer = SaveService.Instance.LoadPlayer(Story); // null si no hay guardado

        if (CurrentPlayer == null) return; // no hay partida: la UI decide que hacer

        ChangeState(GameState.Exploration);
    }

    public void SaveGame()
    {
        if (CurrentPlayer == null) return;
        SaveService.Instance.SavePlayer(CurrentPlayer, Story);
    }

}
    