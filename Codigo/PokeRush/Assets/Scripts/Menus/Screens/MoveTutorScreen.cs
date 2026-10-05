using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// El tutor de MTs de Nestor en la Quarcho Cueva.
//
// Es el Recordador de movimientos de Pokemon Essentials (Graphics/UI/Move
// Reminder): la lista a la izquierda, de a cuatro, con el tipo, el nombre y el
// precio en el recuadro celeste; a la derecha el Pokemon arriba y el detalle
// del movimiento que se apunta (categoria, potencia, precision, PP y que hace).
//
// Dos pasos, en la misma pantalla:
//   1. Aprender: las MT que la especie puede aprender y todavia no sabe.
//   2. Olvidar:  si ya sabe cuatro, sus cuatro movimientos mas el nuevo. Elegir
//                uno de los cuatro lo reemplaza; elegir el nuevo (o volver)
//                vuelve al paso 1 sin cobrar nada.
// Siempre pregunta el precio antes de cobrar. Las reglas son de
// TutorDeMovimientos, no de esta pantalla.
//
// Tambien la usa el combate al subir de nivel (MoveTutorRequest.AprenderEste):
// arranca directo en el paso 2, sin precio, y elegir el nuevo o volver
// pregunta si deja de aprenderlo (como en la saga).
public class MoveTutorScreen : MenuScreen
{
    private enum Paso { Aprender, Olvidar }

    [Header("Lista")]
    [SerializeField] private TutorMoveRowWidget rowPrefab;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private int visibleRows = 4;
    [Tooltip("Flechas que se prenden cuando hay mas movimientos arriba/abajo.")]
    [SerializeField] private GameObject arrowUp;
    [SerializeField] private GameObject arrowDown;
    [SerializeField] private Text titleText;
    [SerializeField] private Text footerText;
    [SerializeField] private Text emptyText;

    [Header("Pokemon")]
    [SerializeField] private Image pokenIcon;
    [SerializeField] private Text pokenNameText;
    [SerializeField] private Image pokenType1;
    [SerializeField] private Image pokenType2;

    [Header("Detalle del movimiento")]
    [Tooltip("Graphics/UI/category.png: tres renglones (fisico, especial, estado).")]
    [SerializeField] private RawImage categoryIcon;
    [SerializeField] private Text powerText;
    [SerializeField] private Text accuracyText;
    [SerializeField] private Text ppText;
    [SerializeField] private Text descriptionText;

    [Header("Textos")]
    [Tooltip("{0} = el maestro.")]
    [SerializeField] private string tituloAprender = "MT DE {0}";
    [SerializeField] private string tituloOlvidar = "¿QUÉ OLVIDA?";
    [SerializeField] private string pieAprender = "$ {0}";
    [Tooltip("{0} = el movimiento nuevo, {1} = el precio.")]
    [SerializeField] private string pieOlvidar = "{0}: $ {1}";
    [Tooltip("{0} = el maestro, {1} = el Pokemon.")]
    [SerializeField] private string sinNadaParaEnsenar = "{0} no tiene nada para enseñarle a {1}.";
    [Tooltip("{0} = el movimiento, {1} = el Pokemon, {2} = el precio.")]
    [SerializeField] private string preguntaAprender = "¿Enseñarle {0} a {1} por $ {2}?";
    [Tooltip("{0} = el que olvida, {1} = el nuevo, {2} = el precio.")]
    [SerializeField] private string preguntaOlvidar = "¿Olvidar {0} y aprender {1} por $ {2}?";
    [Tooltip("{0} = el Pokemon, {1} = el nuevo.")]
    [SerializeField] private string aprendio = "¡{0} aprendió {1}!";
    [Tooltip("{0} = el Pokemon, {1} = el que olvida, {2} = el nuevo.")]
    [SerializeField] private string olvidoYAprendio = "1, 2 y... ¡Puf! {0} olvidó {1}... ¡y aprendió {2}!";
    [SerializeField] private string sinDescripcion = "Sin descripción.";

    [Header("Al subir de nivel")]
    [Tooltip("{0} = el movimiento nuevo.")]
    [SerializeField] private string pieSubirDeNivel = "Aprender {0}";
    [Tooltip("{0} = el que olvida, {1} = el nuevo.")]
    [SerializeField] private string preguntaOlvidarGratis = "¿Olvidar {0} para aprender {1}?";
    [Tooltip("{0} = el Pokemon, {1} = el nuevo.")]
    [SerializeField] private string preguntaDejarDeAprender = "¿Que {0} deje de aprender {1}?";
    [Tooltip("{0} = el Pokemon, {1} = el nuevo.")]
    [SerializeField] private string noAprendio = "{0} no aprendió {1}.";

    private WidgetPool<TutorMoveRowWidget> _rows;
    private readonly List<MoveDefinition> _ensenables = new List<MoveDefinition>();
    private readonly MoveTutorResult _result = new MoveTutorResult();

    private PokenModel _poken;
    private string _maestro = "NÉSTOR";
    private PreciosDelTutor _precios;

    private Paso _paso;
    private int _cursor;
    private int _scrollTop;
    // El paso 1 recuerda donde estaba el cursor al volver del paso 2.
    private int _cursorAprender;
    private MoveDefinition _nuevo;
    // Modo "subir de nivel": solo el paso 2, sin precio (ver AprenderEste).
    private bool _alSubirDeNivel;

    protected override void OnOpen(object payload)
    {
        var pedido = payload as MoveTutorRequest;
        _poken = pedido?.Poken;
        _maestro = pedido != null && !string.IsNullOrEmpty(pedido.Maestro) ? pedido.Maestro : "NÉSTOR";
        _precios = pedido?.Precios ?? new PreciosDelTutor();

        _result.Aprendido = null;
        _result.Olvidado = null;
        _result.Pagado = 0;
        Result = _result;

        if (_rows == null && rowPrefab != null)
            _rows = new WidgetPool<TutorMoveRowWidget>(rowPrefab, rowContainer != null ? rowContainer : transform);

        _ensenables.Clear();
        _alSubirDeNivel = pedido != null && pedido.AprenderEste != null && _poken != null;
        if (_alSubirDeNivel)
        {
            _nuevo = pedido.AprenderEste;
            _cursorAprender = 0;
            IrA(Paso.Olvidar);
            return;
        }
        _ensenables.AddRange(TutorDeMovimientos.Ensenables(_poken));

        _cursorAprender = 0;
        IrA(Paso.Aprender);
    }

    public override void OnFocusGained() => Refresh();

    private void IrA(Paso paso)
    {
        _paso = paso;
        _cursor = paso == Paso.Aprender ? _cursorAprender : 0;
        _scrollTop = 0;
        Refresh();
    }

    // ---------- lo que se lista ----------

    private int Filas => _paso == Paso.Aprender
        ? _ensenables.Count
        : (_poken != null ? _poken.Moves.Count : 0) + 1;

    // En el paso 2 la ultima fila es el movimiento nuevo.
    private bool EsLaFilaNueva(int fila) => _paso == Paso.Olvidar && fila == Filas - 1;

    private MoveDefinition MovimientoDeLaFila(int fila)
    {
        if (fila < 0 || fila >= Filas) return null;
        if (_paso == Paso.Aprender) return _ensenables[fila];
        if (EsLaFilaNueva(fila)) return _nuevo;
        MoveModel m = _poken.Moves[fila];
        return m != null ? m.Definition : null;
    }

    private int Precio(MoveDefinition move) => TutorDeMovimientos.Precio(move, _precios);

    // ---------- dibujo ----------

    private void Refresh()
    {
        ClampScroll();
        RefreshHeader();
        RefreshRows();
        RefreshDetail();
    }

    private void RefreshHeader()
    {
        if (titleText != null)
            titleText.text = _paso == Paso.Aprender ? string.Format(tituloAprender, _maestro) : tituloOlvidar;

        if (footerText != null)
            footerText.text = _alSubirDeNivel
                ? string.Format(pieSubirDeNivel, _nuevo != null ? _nuevo.moveName : "")
                : _paso == Paso.Aprender
                    ? string.Format(pieAprender, Player != null ? Player.Money : 0)
                    : string.Format(pieOlvidar, _nuevo != null ? _nuevo.moveName : "", Precio(_nuevo));

        if (pokenNameText != null)
            pokenNameText.text = _poken != null ? $"{_poken.Definition.pokenName}  Nv{_poken.Level}" : "";

        if (pokenIcon != null)
        {
            Sprite icono = _poken != null ? _poken.IconSprite : null;
            pokenIcon.sprite = icono;
            pokenIcon.enabled = icono != null;
            pokenIcon.preserveAspect = true;
        }

        PokenType t1 = _poken != null ? _poken.Definition.type : PokenType.None;
        PokenType t2 = _poken != null ? _poken.Definition.type2 : PokenType.None;
        MenuIconLibrary.PaintType(pokenType1, t1);
        MenuIconLibrary.PaintType(pokenType2, t2);

        if (emptyText != null)
        {
            bool vacio = _paso == Paso.Aprender && _ensenables.Count == 0;
            emptyText.enabled = vacio;
            if (vacio)
                emptyText.text = string.Format(sinNadaParaEnsenar, _maestro,
                    _poken != null ? _poken.Definition.pokenName : "este Pokémon");
        }
    }

    private void RefreshRows()
    {
        if (_rows == null) return;

        int plata = Player != null ? Player.Money : 0;
        int cuantas = Mathf.Min(visibleRows, Filas);
        for (int i = 0; i < cuantas; i++)
        {
            int fila = _scrollTop + i;
            MoveDefinition move = MovimientoDeLaFila(fila);
            TutorMoveRowWidget widget = _rows.Get(i);

            if (_paso == Paso.Aprender)
            {
                int precio = Precio(move);
                widget.Bind(move, $"$ {precio}", plata >= precio, false);
            }
            else
            {
                string pp = move != null ? $"PP {PpActual(fila, move)}/{move.maxPP}" : "";
                widget.Bind(move, pp, true, EsLaFilaNueva(fila));
            }
            widget.SetSelected(fila == _cursor);
        }
        _rows.HideFrom(cuantas);

        if (arrowUp != null) arrowUp.SetActive(_scrollTop > 0);
        if (arrowDown != null) arrowDown.SetActive(_scrollTop + visibleRows < Filas);
    }

    // Los PP que le quedan a un movimiento que ya sabe; el nuevo viene lleno.
    private int PpActual(int fila, MoveDefinition move)
    {
        if (!EsLaFilaNueva(fila) && _poken != null && fila < _poken.Moves.Count && _poken.Moves[fila] != null)
            return _poken.Moves[fila].CurrentPP;
        return move.maxPP;
    }

    private void RefreshDetail()
    {
        MoveDefinition move = MovimientoDeLaFila(_cursor);

        if (categoryIcon != null)
        {
            categoryIcon.enabled = move != null;
            if (move != null)
            {
                // category.png trae los tres de arriba abajo: fisico, especial,
                // estado. En UV el origen esta abajo, por eso se da vuelta.
                int fila = Mathf.Clamp((int)move.attackType, 0, 2);
                categoryIcon.uvRect = new Rect(0f, 1f - (fila + 1) / 3f, 1f, 1f / 3f);
            }
        }

        if (powerText != null)
            powerText.text = move == null ? "" : move.power > 0 ? move.power.ToString() : "---";
        if (accuracyText != null)
            accuracyText.text = move == null ? "" : move.alwaysHits || move.accuracy <= 0 ? "---" : move.accuracy.ToString();
        if (ppText != null)
            ppText.text = move == null ? "" : move.maxPP.ToString();
        if (descriptionText != null)
            descriptionText.text = move == null ? "" :
                string.IsNullOrEmpty(move.descripcion) ? sinDescripcion : move.descripcion;
    }

    private void ClampScroll()
    {
        if (Filas == 0) { _cursor = 0; _scrollTop = 0; return; }
        _cursor = Mathf.Clamp(_cursor, 0, Filas - 1);

        if (Filas <= visibleRows) { _scrollTop = 0; return; }
        _scrollTop = Mathf.Clamp(_scrollTop, 0, Filas - visibleRows);
        if (_cursor < _scrollTop) _scrollTop = _cursor;
        else if (_cursor >= _scrollTop + visibleRows) _scrollTop = _cursor - visibleRows + 1;
    }

    // ---------- input ----------

    public override void HandleAction(GameAction action)
    {
        switch (action)
        {
            case GameAction.Up:
                if (Filas > 0) { _cursor = (_cursor - 1 + Filas) % Filas; Refresh(); }
                break;
            case GameAction.Down:
                if (Filas > 0) { _cursor = (_cursor + 1) % Filas; Refresh(); }
                break;
            case GameAction.PageLeft:
            case GameAction.Left:
                if (Filas > 0) { _cursor = Mathf.Max(0, _cursor - visibleRows); Refresh(); }
                break;
            case GameAction.PageRight:
            case GameAction.Right:
                if (Filas > 0) { _cursor = Mathf.Min(Filas - 1, _cursor + visibleRows); Refresh(); }
                break;
            case GameAction.Confirm:
                if (_paso == Paso.Aprender) ElegirParaAprender();
                else ElegirParaOlvidar();
                break;
            case GameAction.Cancel:
                if (_alSubirDeNivel) PreguntarDejarDeAprender();
                else if (_paso == Paso.Olvidar) IrA(Paso.Aprender);
                else CloseSelf();
                break;
        }
    }

    private void ElegirParaAprender()
    {
        if (_ensenables.Count == 0 || _poken == null) { CloseSelf(); return; }

        MoveDefinition move = MovimientoDeLaFila(_cursor);
        int precio = Precio(move);

        // Primero la plata: no tiene sentido elegir que olvidar si no alcanza.
        if (Player == null || Player.Money < precio)
        {
            ShowMessage($"Son $ {precio} y tenés $ {(Player != null ? Player.Money : 0)}.");
            return;
        }

        if (_poken.Moves.Count >= TutorDeMovimientos.MovimientosMaximos)
        {
            _cursorAprender = _cursor;
            _nuevo = move;
            IrA(Paso.Olvidar);
            return;
        }

        Ask(string.Format(preguntaAprender, move.moveName, _poken.Definition.pokenName, precio), si =>
        {
            if (!si) return;
            Ensenar(move, -1, precio);
        });
    }

    private void ElegirParaOlvidar()
    {
        if (_alSubirDeNivel) { ElegirAlSubirDeNivel(); return; }
        if (EsLaFilaNueva(_cursor)) { IrA(Paso.Aprender); return; }

        MoveDefinition viejo = MovimientoDeLaFila(_cursor);
        int indice = _cursor;
        int precio = Precio(_nuevo);

        Ask(string.Format(preguntaOlvidar, viejo != null ? viejo.moveName : "-", _nuevo.moveName, precio), si =>
        {
            if (!si) return;
            Ensenar(_nuevo, indice, precio, viejo);
        });
    }

    // ---------- subir de nivel ----------

    private void ElegirAlSubirDeNivel()
    {
        if (EsLaFilaNueva(_cursor)) { PreguntarDejarDeAprender(); return; }

        MoveDefinition viejo = MovimientoDeLaFila(_cursor);
        int indice = _cursor;
        Ask(string.Format(preguntaOlvidarGratis, viejo != null ? viejo.moveName : "-", _nuevo.moveName), si =>
        {
            if (!si) return;
            if (_poken == null || !_poken.ReplaceMove(indice, _nuevo))
            {
                ShowMessage("No se pudo aprender.");
                return;
            }

            _result.Aprendido = _nuevo;
            _result.Olvidado = viejo;
            _result.Pagado = 0;
            Refresh();
            ShowMessage(string.Format(olvidoYAprendio, _poken.Definition.pokenName,
                viejo != null ? viejo.moveName : "-", _nuevo.moveName), CloseSelf);
        });
    }

    private void PreguntarDejarDeAprender()
    {
        string nombre = _poken != null ? _poken.Definition.pokenName : "";
        Ask(string.Format(preguntaDejarDeAprender, nombre, _nuevo != null ? _nuevo.moveName : ""), si =>
        {
            if (!si) return;
            _result.Aprendido = null;
            _result.Olvidado = null;
            ShowMessage(string.Format(noAprendio, nombre, _nuevo != null ? _nuevo.moveName : ""), CloseSelf);
        });
    }

    private void Ensenar(MoveDefinition move, int reemplazar, int precio, MoveDefinition viejo = null)
    {
        string motivo = TutorDeMovimientos.MotivoParaNoEnsenar(Player, _poken, move, reemplazar, precio);
        if (motivo != null || !TutorDeMovimientos.Ensenar(Player, _poken, move, reemplazar, precio))
        {
            ShowMessage(motivo ?? "No se pudo enseñar.");
            return;
        }

        _result.Aprendido = move;
        _result.Olvidado = viejo;
        _result.Pagado = precio;

        string nombre = _poken.Definition.pokenName;
        string cartel = viejo != null
            ? string.Format(olvidoYAprendio, nombre, viejo.moveName, move.moveName)
            : string.Format(aprendio, nombre, move.moveName);

        Refresh();
        ShowMessage(cartel, CloseSelf);
    }
}
