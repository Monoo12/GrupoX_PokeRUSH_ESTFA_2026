using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Menu de pausa del mundo: Pokemon / Bolsa / Guardar / Opciones / Salir.
//
// Las opciones NO estan en una lista del codigo: son los MenuOptionWidget que
// cuelgan del contenedor en el prefab, en el orden en que esten ahi. Agregar,
// sacar o reordenar una opcion es trabajo de Unity; el codigo solo mira el
// OptionId de cada una. Por eso no hay ni un solo indice hardcodeado aca.
public class PauseMenuScreen : MenuScreen
{
    // Ids que entiende esta pantalla. Un widget con otro id se ignora con aviso.
    public const string IdPokemon = "Pokemon";
    public const string IdBag = "Bolsa";
    public const string IdSave = "Guardar";
    public const string IdOptions = "Opciones";
    public const string IdExit = "Salir";

    [Header("Referencias")]
    [Tooltip("Padre de los MenuOptionWidget. El orden de los hijos es el orden del menu.")]
    [SerializeField] private Transform optionContainer;

    [Header("Cabecera (opcional)")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text moneyText;
    [SerializeField] private string moneyFormat = "$ {0}";

    [Header("Salir")]
    [Tooltip("Escena a la que vuelve la opcion Salir.")]
    [SerializeField] private string mainMenuScene = "Menu Principal";

    private readonly List<MenuOptionWidget> _options = new List<MenuOptionWidget>();
    private int _index;
    private bool _guardando;

    protected override void OnOpen(object payload)
    {
        CollectOptions();
        RefreshHeader();
        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _options.Count - 1));
        RefreshSelection();
    }

    public override void OnFocusGained()
    {
        // Volver de la mochila puede haber cambiado el dinero o el equipo.
        RefreshHeader();
        RefreshSelection();
    }

    private void CollectOptions()
    {
        _options.Clear();
        Transform padre = optionContainer != null ? optionContainer : transform;
        foreach (Transform hijo in padre)
        {
            if (!hijo.gameObject.activeSelf) continue;
            var widget = hijo.GetComponent<MenuOptionWidget>();
            if (widget != null) _options.Add(widget);
        }

        if (_options.Count == 0)
            Debug.LogWarning("[Menus] El menu de pausa no tiene ninguna opcion. " +
                             "Agrega MenuOptionWidget bajo 'Option Container'.");
    }

    private void RefreshHeader()
    {
        PlayerModel player = Player;
        if (playerNameText != null)
            playerNameText.text = player != null ? player.PlayerName : string.Empty;
        if (moneyText != null)
            moneyText.text = player != null ? string.Format(moneyFormat, player.Money) : string.Empty;
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < _options.Count; i++)
            _options[i].SetSelected(i == _index);
    }

    public override void HandleAction(GameAction action)
    {
        if (_guardando) return; // el guardado en curso no acepta teclas

        switch (action)
        {
            case GameAction.Up:
                Move(-1);
                break;
            case GameAction.Down:
                Move(1);
                break;
            case GameAction.Confirm:
                Activate();
                break;
            case GameAction.Cancel:
                CloseSelf();
                break;
        }
    }

    private void Move(int delta)
    {
        if (_options.Count == 0) return;
        _index = (_index + delta + _options.Count) % _options.Count;
        RefreshSelection();
    }

    private void Activate()
    {
        if (_options.Count == 0) return;
        string id = _options[_index].OptionId;

        switch (id)
        {
            case IdPokemon:
                AbrirEquipo();
                break;

            case IdBag:
                Manager.Open(MenuScreenId.Bag, new BagScreenRequest
                {
                    Context = MenuContextKind.Overworld
                });
                break;

            case IdSave:
                IniciarGuardado();
                break;

            case IdOptions:
                Manager.Open(MenuScreenId.Options);
                break;

            case IdExit:
                Salir();
                break;

            default:
                Debug.LogWarning($"[Menus] La opcion '{id}' del menu de pausa no esta conectada a nada.");
                break;
        }
    }

    private void AbrirEquipo()
    {
        if (Player == null || Player.Team.Count == 0)
        {
            ShowMessage("Todavia no tenes ningun Poken.");
            return;
        }

        Manager.Open(MenuScreenId.Party, new PartyScreenRequest
        {
            Mode = PartyScreenMode.Browse,
            Context = MenuContextKind.Overworld
        });
    }

    // ---------- guardado ----------

    // El flujo del enunciado: preguntar, avisar que se esta guardando, guardar
    // de verdad, verificar y recien ahi confirmar. La verificacion importa: sin
    // ella un guardado que falla se ve igual que uno que funciona.
    private void IniciarGuardado()
    {
        if (Player == null)
        {
            ShowMessage("No hay ninguna partida para guardar.");
            return;
        }

        Ask("¿Querés guardar la partida?", confirmado =>
        {
            if (confirmado) StartCoroutine(GuardarRoutine());
        });
    }

    private IEnumerator GuardarRoutine()
    {
        _guardando = true;

        // Caja no cerrable: mientras el guardado corre, ninguna tecla hace nada.
        MenuScreen aviso = Manager.Open(MenuScreenId.MessageBox, new MessageBoxRequest
        {
            Message = "Guardando...",
            ClosableByPlayer = false
        });

        // Un frame para que el cartel se llegue a dibujar antes de la escritura.
        yield return null;

        bool ok;
        string error = null;
        try
        {
            GameManager.Instance.SaveGame();
            ok = SaveService.Instance.HasSaveData();
        }
        catch (System.Exception e)
        {
            ok = false;
            error = e.Message;
            Debug.LogError($"[Menus] Fallo el guardado: {e}");
        }

        if (aviso != null) Manager.Close(aviso);
        _guardando = false;

        // Nunca se queda trabado: pase lo que pase se muestra un cartel que el
        // jugador puede cerrar y seguir jugando.
        ShowMessage(ok
            ? $"¡{Player.PlayerName} guardó la partida!"
            : $"No se pudo guardar la partida.{(error != null ? " (" + error + ")" : string.Empty)}");
    }

    // ---------- salir ----------

    private void Salir()
    {
        Ask("¿Salir al menú principal? Se perderá lo que no hayas guardado.", confirmado =>
        {
            if (!confirmado) return;

            // Cerrar TODA la pila antes de cambiar de escena: si no, el menu
            // (que sobrevive el cambio de escena) reaparece encima del menu
            // principal con el input tomado.
            Manager.CloseAll();
            Manager.SetContext(MenuContextKind.Overworld);
            SceneLoaderService.Instance.LoadScene(mainMenuScene);
        });
    }
}
