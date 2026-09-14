using UnityEngine;
using UnityEngine.UI;

// El PC de almacenamiento: las cajas y el equipo en una sola pantalla, con el
// arte de Graphics/UI/Storage (Pokemon Essentials).
//
// Como se usa (igual que en la saga):
//   - flechas: mover la mano por la caja. Arriba de todo se cambia de caja
//     (tambien con Pagina izq/der); abajo estan EQUIPO y SALIR;
//   - Aceptar sobre un Pokemon lo agarra; Aceptar sobre otro lugar lo suelta
//     ahi (si habia alguien, se intercambian);
//   - EQUIPO abre el panel del equipo, que se maneja igual;
//   - Volver suelta lo agarrado, cierra el panel del equipo o apaga el PC.
//
// La pantalla no mueve nada por su cuenta: le pide a PcReglas que haga el
// movimiento entero, que es donde viven las reglas (no dejar el equipo sin
// nadie que pelee, no pasar de 6). Agarrar es solo visual: hasta que se suelta
// el modelo no cambia, asi que cortar el juego con un Pokemon en la mano no
// pierde a nadie.
public class PcStorageScreen : MenuScreen
{
    private const int Columnas = PokenStorageModel.Columnas;
    private const int Filas = PokenStorageModel.Filas;
    // El lugar "VOLVER" del panel del equipo va despues de los seis huecos.
    private const int VolverDelEquipo = PlayerModel.EquipoMaximo;

    private enum Zona { Caja, Encabezado, Botones, Equipo }

    [Header("Caja")]
    [SerializeField] private Image boxBackground;
    [Tooltip("Un fondo por caja. Si hay mas cajas que fondos, se repiten.")]
    [SerializeField] private Sprite[] boxSprites = new Sprite[0];
    [SerializeField] private Text boxNameText;
    [Tooltip("Los 30 iconos de la caja, en orden de lectura.")]
    [SerializeField] private Image[] boxIcons = new Image[PokenStorageModel.LugaresPorCaja];
    [Tooltip("Donde apunta la mano para cambiar de caja.")]
    [SerializeField] private RectTransform boxHeader;
    [SerializeField] private Image arrowLeft;
    [SerializeField] private Image arrowRight;

    [Header("Equipo")]
    [SerializeField] private GameObject partyPanel;
    [SerializeField] private Image[] partyIcons = new Image[PlayerModel.EquipoMaximo];
    [SerializeField] private RectTransform partyBack;

    [Header("Botones de abajo")]
    [SerializeField] private Image partyButton;
    [SerializeField] private Image exitButton;

    [Header("Datos del Pokemon")]
    [SerializeField] private Text nameText;
    [SerializeField] private Image bigSprite;
    [SerializeField] private Text levelText;
    [SerializeField] private Text typeText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text shinyText;

    [Header("Cursor")]
    [SerializeField] private RectTransform cursor;
    [SerializeField] private Image cursorImage;
    [SerializeField] private Sprite cursorPoint;
    [SerializeField] private Sprite cursorGrab;
    [Tooltip("El Pokemon agarrado, que va colgando de la mano.")]
    [SerializeField] private Image heldIcon;
    [Tooltip("Donde queda la mano respecto del centro del lugar que apunta.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(14f, 22f);

    private Zona _zona = Zona.Caja;
    private int _caja;
    private int _celda;
    private int _boton;
    private int _slot;
    private bool _agarrando;
    private PcLugar _origen;
    private float _reloj;

    // Lecturas para los bancos de prueba.
    public int CajaActual => _caja;
    public bool Agarrando => _agarrando;

    private int Cajas => Player != null ? Player.Storage.Cajas : 1;

    // ---------------------------------------------------------------- abrir

    protected override void OnOpen(object payload)
    {
        _zona = Zona.Caja;
        _celda = 0;
        _boton = 0;
        _slot = 0;
        _agarrando = false;
        // La caja se recuerda entre una vez y otra: es donde el jugador estaba
        // acomodando.
        _caja = Mathf.Clamp(_caja, 0, Cajas - 1);

        if (partyPanel != null) partyPanel.SetActive(false);
        Refrescar();
    }

    protected override void OnClose()
    {
        _agarrando = false;
    }

    public override void OnFocusGained() => Refrescar();

    // ---------------------------------------------------------------- input

    public override void HandleAction(GameAction action)
    {
        switch (action)
        {
            case GameAction.Up: Mover(0, -1); break;
            case GameAction.Down: Mover(0, 1); break;
            case GameAction.Left: Mover(-1, 0); break;
            case GameAction.Right: Mover(1, 0); break;
            case GameAction.PageLeft: CambiarCaja(-1); break;
            case GameAction.PageRight: CambiarCaja(1); break;
            case GameAction.Confirm: Confirmar(); break;
            case GameAction.Cancel: Volver(); break;
        }
    }

    private void Mover(int dx, int dy)
    {
        switch (_zona)
        {
            case Zona.Caja:
            {
                int col = _celda % Columnas;
                int fila = _celda / Columnas;

                if (dy < 0 && fila == 0) { _zona = Zona.Encabezado; break; }
                if (dy > 0 && fila == Filas - 1)
                {
                    _zona = Zona.Botones;
                    _boton = col < Columnas / 2 ? 0 : 1;
                    break;
                }

                col = (col + dx + Columnas) % Columnas;
                fila += dy;
                _celda = fila * Columnas + col;
                break;
            }

            case Zona.Encabezado:
                if (dx != 0) CambiarCaja(dx);
                else if (dy > 0) { _zona = Zona.Caja; _celda %= Columnas; }
                break;

            case Zona.Botones:
                if (dx != 0) _boton = 1 - _boton;
                else if (dy < 0)
                {
                    _zona = Zona.Caja;
                    _celda = (Filas - 1) * Columnas + (_boton == 0 ? 1 : Columnas - 2);
                }
                break;

            case Zona.Equipo:
                MoverEnEquipo(dx, dy);
                break;
        }

        Refrescar();
    }

    // El panel del equipo tiene los seis huecos en dos columnas (0-1, 2-3, 4-5)
    // y VOLVER abajo de todo.
    private void MoverEnEquipo(int dx, int dy)
    {
        if (_slot == VolverDelEquipo)
        {
            if (dy < 0) _slot = PlayerModel.EquipoMaximo - 2;
            return;
        }

        int col = _slot % 2;
        int fila = _slot / 2;

        if (dx != 0) col = 1 - col;
        if (dy > 0)
        {
            if (fila == PlayerModel.EquipoMaximo / 2 - 1) { _slot = VolverDelEquipo; return; }
            fila++;
        }
        if (dy < 0 && fila > 0) fila--;

        _slot = fila * 2 + col;
    }

    private void Confirmar()
    {
        switch (_zona)
        {
            case Zona.Encabezado:
                CambiarCaja(1);
                return;

            case Zona.Botones:
                if (_boton == 0) AbrirEquipo();
                else Apagar();
                return;

            case Zona.Equipo:
                if (_slot == VolverDelEquipo) CerrarEquipo();
                else AgarrarOSoltar(PcLugar.DelEquipo(_slot));
                return;

            default:
                AgarrarOSoltar(PcLugar.DeCaja(_caja, _celda));
                return;
        }
    }

    private void AgarrarOSoltar(PcLugar lugar)
    {
        PlayerModel jugador = Player;
        if (jugador == null) return;

        if (!_agarrando)
        {
            if (PcReglas.Get(jugador, lugar) == null) return;
            _agarrando = true;
            _origen = lugar;
            Refrescar();
            return;
        }

        string error = PcReglas.Mover(jugador, _origen, lugar);
        if (error != null)
        {
            // Sigue en la mano: el jugador elige otro lugar o lo suelta con Volver.
            ShowMessage(error);
            return;
        }

        _agarrando = false;
        Refrescar();
    }

    private void Volver()
    {
        if (_agarrando)
        {
            _agarrando = false;
            Refrescar();
            return;
        }

        if (_zona == Zona.Equipo)
        {
            CerrarEquipo();
            return;
        }

        Apagar();
    }

    private void Apagar()
    {
        if (_agarrando)
        {
            ShowMessage("Soltá el Pokémon antes de apagar el PC.");
            return;
        }
        CloseSelf();
    }

    private void AbrirEquipo()
    {
        _zona = Zona.Equipo;
        _slot = 0;
        if (partyPanel != null) partyPanel.SetActive(true);
        Refrescar();
    }

    private void CerrarEquipo()
    {
        _zona = Zona.Botones;
        _boton = 0;
        if (partyPanel != null) partyPanel.SetActive(false);
        Refrescar();
    }

    private void CambiarCaja(int paso)
    {
        _caja = (_caja + paso + Cajas) % Cajas;
        Refrescar();
    }

    // ----------------------------------------------------------------- tick

    public override void OnTick(float unscaledDeltaTime)
    {
        // La mano sube y baja un par de pixeles: es lo que la hace ver viva.
        _reloj += unscaledDeltaTime;
        if (cursorImage != null)
            cursorImage.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Sin(_reloj * 5f) * 2f));
    }

    // --------------------------------------------------------------- pintar

    private static readonly Color Atenuado = new Color(0.72f, 0.74f, 0.8f, 1f);
    private static readonly Color Fantasma = new Color(1f, 1f, 1f, 0.35f);
    // Los botones vienen dibujados en el arte: encima va un brillo que se
    // prende solo en el elegido.
    private static readonly Color Brillo = new Color(1f, 0.86f, 0.32f, 0.45f);

    private void Refrescar()
    {
        PlayerModel jugador = Player;
        if (jugador == null) return;

        PokenStorageModel pc = jugador.Storage;
        _caja = Mathf.Clamp(_caja, 0, pc.Cajas - 1);

        if (boxBackground != null && boxSprites != null && boxSprites.Length > 0)
            boxBackground.sprite = boxSprites[_caja % boxSprites.Length];
        if (boxNameText != null) boxNameText.text = PokenStorageModel.NombreDeCaja(_caja);

        for (int i = 0; i < boxIcons.Length; i++)
            PintarIcono(boxIcons[i], pc.Get(_caja, i), EsElAgarrado(PcLugar.DeCaja(_caja, i)));

        for (int i = 0; i < partyIcons.Length; i++)
            PintarIcono(partyIcons[i], i < jugador.Team.Count ? jugador.Team[i] : null,
                        EsElAgarrado(PcLugar.DelEquipo(i)));

        Resaltar(partyButton, _zona == Zona.Botones && _boton == 0);
        Resaltar(exitButton, _zona == Zona.Botones && _boton == 1);
        if (arrowLeft != null) arrowLeft.color = _zona == Zona.Encabezado ? Color.white : Atenuado;
        if (arrowRight != null) arrowRight.color = _zona == Zona.Encabezado ? Color.white : Atenuado;

        PokenModel agarrado = _agarrando ? PcReglas.Get(jugador, _origen) : null;
        if (heldIcon != null)
        {
            heldIcon.gameObject.SetActive(agarrado != null);
            if (agarrado != null)
            {
                heldIcon.sprite = agarrado.IconSprite;
                heldIcon.preserveAspect = true;
            }
        }

        if (cursorImage != null)
        {
            Sprite mano = _agarrando ? cursorGrab : cursorPoint;
            if (mano != null) cursorImage.sprite = mano;
        }

        PintarDatos(agarrado ?? PokenBajoElCursor(jugador));
        UbicarCursor();
    }

    private bool EsElAgarrado(PcLugar lugar) => _agarrando && _origen.Igual(lugar);

    private static void PintarIcono(Image icono, PokenModel poken, bool agarrado)
    {
        if (icono == null) return;

        Sprite sprite = poken != null ? poken.IconSprite : null;
        icono.enabled = sprite != null;
        icono.sprite = sprite;
        icono.preserveAspect = true;
        // El que esta en la mano deja su lugar marcado a media luz, asi se ve
        // de donde salio.
        icono.color = agarrado ? Fantasma : Color.white;
    }

    private static void Resaltar(Image boton, bool elegido)
    {
        if (boton == null) return;
        boton.color = elegido ? Brillo : Color.clear;
    }

    private PokenModel PokenBajoElCursor(PlayerModel jugador)
    {
        switch (_zona)
        {
            case Zona.Caja: return jugador.Storage.Get(_caja, _celda);
            case Zona.Equipo: return _slot < jugador.Team.Count ? jugador.Team[_slot] : null;
            default: return null;
        }
    }

    private void PintarDatos(PokenModel poken)
    {
        bool hay = poken != null && poken.Definition != null;

        if (nameText != null) nameText.text = hay ? poken.Definition.pokenName : "";
        if (levelText != null) levelText.text = hay ? $"Nv. {poken.Level}" : "";
        if (hpText != null) hpText.text = hay ? $"PS {poken.CurrentHP}/{poken.MaxHP}" : "";
        if (typeText != null) typeText.text = hay ? Tipos(poken.Definition) : "";
        if (shinyText != null) shinyText.text = hay && poken.IsShiny ? "SHINY" : "";

        if (bigSprite != null)
        {
            Sprite sprite = hay ? (poken.FrontSprite != null ? poken.FrontSprite : poken.IconSprite) : null;
            bigSprite.sprite = sprite;
            bigSprite.enabled = sprite != null;
            bigSprite.preserveAspect = true;
        }
    }

    private void UbicarCursor()
    {
        if (cursor == null) return;

        RectTransform destino = null;
        switch (_zona)
        {
            case Zona.Caja:
                if (_celda < boxIcons.Length && boxIcons[_celda] != null) destino = boxIcons[_celda].rectTransform;
                break;
            case Zona.Encabezado:
                destino = boxHeader;
                break;
            case Zona.Botones:
                Image boton = _boton == 0 ? partyButton : exitButton;
                if (boton != null) destino = boton.rectTransform;
                break;
            case Zona.Equipo:
                if (_slot == VolverDelEquipo) destino = partyBack;
                else if (_slot < partyIcons.Length && partyIcons[_slot] != null) destino = partyIcons[_slot].rectTransform;
                break;
        }

        if (destino == null) return;

        // Por posicion de mundo: los iconos del equipo viven dentro del panel y
        // los de la caja no, asi que sus posiciones locales no se comparan.
        cursor.position = destino.position;
        cursor.anchoredPosition += cursorOffset;
        cursor.SetAsLastSibling();
    }

    private static string Tipos(PokenDefinition def)
    {
        string primero = NombreDeTipo(def.type);
        string segundo = def.type2 != PokenType.None && def.type2 != def.type ? NombreDeTipo(def.type2) : null;
        return segundo == null ? primero : $"{primero} / {segundo}";
    }

    private static string NombreDeTipo(PokenType tipo)
    {
        switch (tipo)
        {
            case PokenType.Normal: return "Normal";
            case PokenType.Fire: return "Fuego";
            case PokenType.Water: return "Agua";
            case PokenType.Grass: return "Planta";
            case PokenType.Electric: return "Eléctrico";
            case PokenType.Ice: return "Hielo";
            case PokenType.Fighting: return "Lucha";
            case PokenType.Poison: return "Veneno";
            case PokenType.Ground: return "Tierra";
            case PokenType.Flying: return "Volador";
            case PokenType.Psychic: return "Psíquico";
            case PokenType.Bug: return "Bicho";
            case PokenType.Rock: return "Roca";
            case PokenType.Ghost: return "Fantasma";
            case PokenType.Dragon: return "Dragón";
            case PokenType.Dark: return "Siniestro";
            case PokenType.Steel: return "Acero";
            case PokenType.Fairy: return "Hada";
            default: return "";
        }
    }
}
