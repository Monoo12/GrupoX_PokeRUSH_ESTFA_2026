using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Menu de Opciones: volumen, pantalla y controles.
//
// Las filas se arman por codigo a partir de GameInputService.RebindableActions:
// agregar una accion nueva al juego la hace aparecer aca sola, sin tocar el
// prefab ni esta pantalla.
public class OptionsScreen : MenuScreen
{
    private enum RowKind { Header, MusicVolume, SfxVolume, Resolution, ScreenMode, Rebind, Reset, Back }

    private class Row
    {
        public RowKind Kind;
        public string Label;
        public GameAction Action;
        public bool Selectable = true;
    }

    [Header("Filas")]
    [SerializeField] private OptionRowWidget rowPrefab;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private int visibleRows = 10;

    [Header("Textos")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text hintText;

    [Tooltip("Pasos del volumen entre 0 y el maximo.")]
    [SerializeField] private int volumeSteps = 10;

    private WidgetPool<OptionRowWidget> _rows;
    private readonly List<Row> _model = new List<Row>();

    private int _cursor;
    private int _scrollTop;

    // Escucha activa de "apreta una tecla". Se suelta si se cierra la pantalla.
    private GameAction _rebindTarget;
    private bool _waitingForKey;

    protected override void OnOpen(object payload)
    {
        if (_rows == null && rowPrefab != null)
            _rows = new WidgetPool<OptionRowWidget>(rowPrefab,
                rowContainer != null ? rowContainer : transform);

        if (titleText != null) titleText.text = "Opciones";

        BuildModel();
        _cursor = FirstSelectable(0, 1);
        _scrollTop = 0;
        Refresh();
    }

    protected override void OnClose()
    {
        CancelRebind();
    }

    private void BuildModel()
    {
        _model.Clear();
        _model.Add(new Row { Kind = RowKind.Header, Label = "Sonido", Selectable = false });
        _model.Add(new Row { Kind = RowKind.MusicVolume, Label = "Música" });
        _model.Add(new Row { Kind = RowKind.SfxVolume, Label = "Efectos" });
        _model.Add(new Row { Kind = RowKind.Header, Label = "Pantalla", Selectable = false });
        _model.Add(new Row { Kind = RowKind.Resolution, Label = "Resolución" });
        _model.Add(new Row { Kind = RowKind.ScreenMode, Label = "Modo" });
        _model.Add(new Row { Kind = RowKind.Header, Label = "Controles", Selectable = false });

        foreach (GameAction action in GameInputService.RebindableActions)
            _model.Add(new Row
            {
                Kind = RowKind.Rebind,
                Label = GameInputService.DisplayLabel(action),
                Action = action
            });

        _model.Add(new Row { Kind = RowKind.Header, Label = string.Empty, Selectable = false });
        _model.Add(new Row { Kind = RowKind.Reset, Label = "Restaurar valores" });
        _model.Add(new Row { Kind = RowKind.Back, Label = "Volver" });
    }

    // ---------- dibujo ----------

    private void Refresh()
    {
        if (_rows == null) return;

        ClampScroll();

        int cuantas = Mathf.Min(visibleRows, _model.Count);
        for (int fila = 0; fila < cuantas; fila++)
        {
            int indice = _scrollTop + fila;
            Row row = _model[indice];
            OptionRowWidget widget = _rows.Get(fila);

            switch (row.Kind)
            {
                case RowKind.Header:
                    widget.Bind(row.Label, null, -1f, false, true);
                    break;

                case RowKind.MusicVolume:
                    widget.Bind(row.Label, VolumeLabel(GameSettings.MusicVolume),
                        GameSettings.MusicVolume, true);
                    break;

                case RowKind.SfxVolume:
                    widget.Bind(row.Label, VolumeLabel(GameSettings.SfxVolume),
                        GameSettings.SfxVolume, true);
                    break;

                case RowKind.Resolution:
                    Vector2Int res = ConfiguracionDePantalla.ResolucionActual;
                    widget.Bind(row.Label, "< " + res.x + " x " + res.y + " >");
                    break;

                case RowKind.ScreenMode:
                    widget.Bind(row.Label, "< " + ConfiguracionDePantalla.NombreDelModo(ConfiguracionDePantalla.ModoActual) + " >");
                    break;

                case RowKind.Rebind:
                    bool esperando = _waitingForKey && _rebindTarget == row.Action;
                    widget.Bind(row.Label,
                        esperando ? "..." : GameInputService.Instance.GetDisplayName(row.Action));
                    break;

                default:
                    widget.Bind(row.Label, null);
                    break;
            }

            widget.SetSelected(indice == _cursor && !_waitingForKey);
        }
        _rows.HideFrom(cuantas);

        if (hintText != null)
            hintText.text = _waitingForKey
                ? "Apretá la tecla nueva (Esc para cancelar)"
                : "Izq/Der: ajustar   Aceptar: cambiar tecla   Volver: salir";
    }

    private string VolumeLabel(float value01)
        => Mathf.RoundToInt(value01 * volumeSteps) + " / " + volumeSteps;

    private void ClampScroll()
    {
        if (_model.Count <= visibleRows) { _scrollTop = 0; return; }
        if (_cursor < _scrollTop) _scrollTop = _cursor;
        else if (_cursor >= _scrollTop + visibleRows) _scrollTop = _cursor - visibleRows + 1;
        _scrollTop = Mathf.Clamp(_scrollTop, 0, _model.Count - visibleRows);
    }

    // ---------- input ----------

    public override void HandleAction(GameAction action)
    {
        // Mientras se espera una tecla, el servicio de input esta silenciado y
        // esto no se llama. Queda por las dudas.
        if (_waitingForKey) return;

        switch (action)
        {
            case GameAction.Up:
                _cursor = FirstSelectable(_cursor - 1, -1);
                Refresh();
                break;

            case GameAction.Down:
                _cursor = FirstSelectable(_cursor + 1, 1);
                Refresh();
                break;

            case GameAction.Left:
                Adjust(-1);
                break;

            case GameAction.Right:
                Adjust(1);
                break;

            case GameAction.Confirm:
                Activate();
                break;

            case GameAction.Cancel:
                CloseSelf();
                break;
        }
    }

    // Salta los titulos de seccion, que no son elegibles.
    private int FirstSelectable(int start, int step)
    {
        if (_model.Count == 0) return 0;
        int index = (start + _model.Count) % _model.Count;
        for (int i = 0; i < _model.Count; i++)
        {
            if (_model[index].Selectable) return index;
            index = (index + step + _model.Count) % _model.Count;
        }
        return 0;
    }

    private void Adjust(int delta)
    {
        Row row = _model[_cursor];
        float paso = 1f / Mathf.Max(1, volumeSteps);

        switch (row.Kind)
        {
            case RowKind.MusicVolume:
                GameSettings.MusicVolume = Mathf.Clamp01(GameSettings.MusicVolume + delta * paso);
                break;

            case RowKind.SfxVolume:
                GameSettings.SfxVolume = Mathf.Clamp01(GameSettings.SfxVolume + delta * paso);
                // Para escuchar como queda.
                Sonidos.Cursor();
                break;

            case RowKind.Resolution:
            {
                List<Vector2Int> lista = ConfiguracionDePantalla.Resoluciones();
                int i = lista.IndexOf(ConfiguracionDePantalla.ResolucionActual);
                i = Mathf.Clamp((i < 0 ? lista.Count - 1 : i) + delta, 0, lista.Count - 1);
                ConfiguracionDePantalla.Poner(lista[i], ConfiguracionDePantalla.ModoActual);
                break;
            }

            case RowKind.ScreenMode:
            {
                FullScreenMode[] modos = ConfiguracionDePantalla.Modos;
                int i = System.Array.IndexOf(modos, ConfiguracionDePantalla.ModoActual);
                i = ((i < 0 ? 0 : i) + delta + modos.Length) % modos.Length;
                ConfiguracionDePantalla.Poner(ConfiguracionDePantalla.ResolucionActual, modos[i]);
                break;
            }

            default:
                return;
        }

        Refresh();
    }

    private void Activate()
    {
        Row row = _model[_cursor];

        switch (row.Kind)
        {
            case RowKind.Rebind:
                StartRebind(row.Action);
                break;

            case RowKind.Reset:
                Ask("¿Restaurar la configuración por defecto?", confirmado =>
                {
                    if (!confirmado) return;
                    GameSettings.ResetToDefaults();
                    Refresh();
                });
                break;

            case RowKind.Back:
                CloseSelf();
                break;
        }
    }

    // ---------- reasignar teclas ----------

    private void StartRebind(GameAction action)
    {
        _rebindTarget = action;
        _waitingForKey = true;
        GameInputService.Instance.BeginListening();
        Refresh();
    }

    // Mientras se espera la tecla nueva, el servicio silencia todas las acciones
    // logicas: por eso la captura se hace aca, mirando el teclado directo.
    public override void OnTick(float unscaledDeltaTime)
    {
        if (!_waitingForKey) return;

        string path = GameInputService.Instance.PollPressedControl();
        if (string.IsNullOrEmpty(path)) return;

        _waitingForKey = false;
        GameInputService.Instance.CancelListening();

        // Escape cancela: dejar que se asigne haria imposible salir de un menu
        // si alguien la elige para todo.
        if (path.EndsWith("/escape")) { Refresh(); return; }

        // Los otros atajos fijos que abren el menu tampoco se pueden robar.
        if (GameInputService.IsMenuShortcut(path))
        {
            ShowMessage(GameInputService.DisplayNameForPath(path) +
                " está reservada para abrir el menú.", Refresh);
            return;
        }

        ResolveRebind(_rebindTarget, path);
    }

    // Una tecla no puede quedar en dos acciones a la vez: sin esto se podia
    // poner la misma en las diez filas y quedarse sin forma de navegar.
    // Si la tecla ya esta usada se pregunta antes de sacarsela a la otra, y si
    // es la unica que le queda ni se ofrece.
    private void ResolveRebind(GameAction target, string path)
    {
        GameInputService input = GameInputService.Instance;
        List<GameAction> choques = input.FindConflicts(path, target);

        if (choques.Count == 0)
        {
            input.SetPrimaryBinding(target, path);
            Refresh();
            return;
        }

        string tecla = GameInputService.DisplayNameForPath(path);

        foreach (GameAction otra in choques)
            if (input.BindingCount(otra) <= 1)
            {
                ShowMessage(tecla + " es la única tecla de " +
                    GameInputService.DisplayLabel(otra) + ". Elegí otra.", Refresh);
                return;
            }

        Ask(tecla + " ya la usa " + Listar(choques) + ". ¿Se la saco?", confirmado =>
        {
            if (confirmado)
            {
                foreach (GameAction otra in choques) input.RemoveBinding(otra, path);
                input.SetPrimaryBinding(target, path);
            }
            Refresh();
        });
    }

    // "Volver", "Volver y Menu", "Volver, Menu y Correr".
    private static string Listar(List<GameAction> acciones)
    {
        var nombres = new List<string>();
        foreach (GameAction a in acciones) nombres.Add(GameInputService.DisplayLabel(a));
        if (nombres.Count == 1) return nombres[0];

        string ultimo = nombres[nombres.Count - 1];
        nombres.RemoveAt(nombres.Count - 1);
        return string.Join(", ", nombres) + " y " + ultimo;
    }

    private void CancelRebind()
    {
        _waitingForKey = false;
        GameInputService.Instance.CancelListening();
    }
}
