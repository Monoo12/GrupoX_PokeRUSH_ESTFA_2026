using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu de seleccion de partida guardada. Muestra una tarjeta con los datos del
// save (nombre, equipo, etc.) y tres botones: Opciones, Borrar partida y Volver.
// Tocar la tarjeta carga la partida y entra al juego.
public class SaveSlotMenuView : MonoBehaviour
{
    [Header("Tarjeta del save")]
    // La tarjeta entera es un boton: tocarla = continuar la partida.
    [SerializeField] private Button saveCardButton;
    [SerializeField] private Image playerSprite;
    [SerializeField] private Text nameText;
    [SerializeField] private Text medalsText;
    [SerializeField] private Text playtimeText;
    [SerializeField] private Text savedDateText;
    [SerializeField] private Text routeText;
    // Iconos del equipo (hasta 6). Se llenan con inventorySprite de cada Poken.
    [SerializeField] private Image[] pokenSlots;

    [Header("Botones")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button backButton;

    [Header("Opciones (opcional)")]
    [SerializeField] private GameObject optionsPanel;

    [Header("Escenas destino")]
    // Escena del juego a cargar al continuar la partida.
    [SerializeField] private string sceneJuego = "SampleScene";
    // Escena a la que vuelve el boton "Volver" (y tras borrar la partida).
    [SerializeField] private string sceneVolver = "Menu Principal";

    // Evita doble carga de escena por doble click.
    private bool _cargando;

    // Los primeros instantes tras cargar la escena se ignoran los clicks:
    // evita que un click "arrastrado" desde la escena anterior (click-through)
    // toque un boton de este menu sin querer (ej: Borrar partida).
    private const float GraciaInicial = 0.25f;

    // True si el click llego demasiado pronto o ya estamos cargando otra escena.
    private bool IgnorarClick => _cargando || Time.timeSinceLevelLoad < GraciaInicial;

    private void Awake()
    {
        if (saveCardButton != null) saveCardButton.onClick.AddListener(OnCardClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        if (saveCardButton != null) saveCardButton.onClick.RemoveListener(OnCardClicked);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OnOptionsClicked);
        if (deleteButton != null) deleteButton.onClick.RemoveListener(OnDeleteClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void Start()
    {
        RefreshCard();
        // Deja la tarjeta seleccionada al entrar: sin una seleccion inicial,
        // las flechas del teclado no tienen desde donde empezar a navegar.
        SeleccionarPrimero();
    }

    private void Update()
    {
        if (_cargando) return;

        // Si la seleccion se pierde (ej: click en el fondo la deselecciona),
        // volvemos a seleccionar la tarjeta para que las flechas siempre anden.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            SeleccionarPrimero();
    }

    // Selecciona la tarjeta del save como punto de partida de la navegacion.
    // Desde ahi: flecha abajo -> Opciones -> Borrar partida -> Volver.
    private void SeleccionarPrimero()
    {
        if (saveCardButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(saveCardButton.gameObject);
    }

    // Carga los datos del save y los vuelca en la tarjeta.
    private void RefreshCard()
    {
        // Carga "de lectura": solo para mostrar. La carga real la hace
        // GameManager.ContinueGame() cuando el jugador toca la tarjeta.
        var story = new StoryModel();
        PlayerModel player = SaveService.Instance.LoadPlayer(story);

        if (player == null)
        {
            // No hay partida (ej: se llego aca por error): volver al menu principal.
            SceneLoaderService.Instance.LoadScene(sceneVolver);
            return;
        }

        nameText.text = player.PlayerName;

        // Medallas = jefes derrotados segun los flags de historia.
        int medallas = story.ActiveFlags.Count(f => f.ToString().StartsWith("BossDefeated"));
        medalsText.text = $"Medallas: {medallas}";

        // TODO: tiempo de juego, fecha de guardado y ubicacion todavia no se
        // persisten en la base de datos. Cuando se agreguen columnas a PlayerRow
        // (ej: PlayTimeSeconds, SavedAt, CurrentRoute), reemplazar estos textos.
        playtimeText.text = "Tiempo de juego: --";
        savedDateText.text = "Guardado: --";
        routeText.text = "Ruta ---";

        // Iconos del equipo: uno por Poken, el resto de los slots queda vacio.
        for (int i = 0; i < pokenSlots.Length; i++)
        {
            bool tienePoken = i < player.Team.Count;
            pokenSlots[i].enabled = tienePoken; // slot vacio = no se dibuja
            if (tienePoken)
                pokenSlots[i].sprite = player.Team[i].IconSprite;
        }
    }

    // --- Handlers de los botones ---

    // Tocar la tarjeta: cargar la partida de verdad y entrar al juego.
    private void OnCardClicked()
    {
        if (IgnorarClick) return;
        _cargando = true;

        GameManager.Instance.ContinueGame();
        SceneLoaderService.Instance.LoadScene(sceneJuego);
    }

    private void OnOptionsClicked()
    {
        if (IgnorarClick) return;
        // Igual que en PauseMenuView: panel placeholder hasta que exista OptionsView.
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    private void OnDeleteClicked()
    {
        if (IgnorarClick) return;
        _cargando = true;

        SaveService.Instance.DeleteSave();
        // Sin save, el menu principal va a mandar a "juego nuevo" la proxima vez.
        SceneLoaderService.Instance.LoadScene(sceneVolver);
    }

    private void OnBackClicked()
    {
        if (IgnorarClick) return;
        _cargando = true;

        SceneLoaderService.Instance.LoadScene(sceneVolver);
    }
}
