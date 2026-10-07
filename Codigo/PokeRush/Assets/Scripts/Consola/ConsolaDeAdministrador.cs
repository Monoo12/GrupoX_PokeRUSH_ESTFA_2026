using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// La consola de administrador: comandos y botones para probar la build sin
// jugar todo de nuevo (ir a cualquier punto de la historia, dar Pokemon,
// ganar una pelea, atravesar paredes...). Se prende en Opciones -> Pruebas y
// se abre y cierra con la tecla de la izquierda del 1 (la | en el teclado
// latinoamericano; se lee por posicion, asi anda en cualquier distribucion).
// Lo que hace cada comando esta en ComandosDeLaConsola; aca solo se dibuja.
//
// Por que IMGUI y no un Canvas: tiene que andar arriba de cualquier escena,
// tenga o no EventSystem (varias no tienen), sin prefab que mantener y sin
// meterse en la navegacion de los menus. Es lo mismo que usa el banco de
// prueba de Julieta. No es UI del juego: no la ve el que no la prende.
//
// Mientras esta abierta:
//   - el teclado es de la consola (GameInputService.OcuparTeclado): no se
//     mueve el personaje, no avanza el dialogo, el 1-4 del combate es texto;
//   - los EventSystem de la escena se apagan, si no el Enter que ejecuta un
//     comando ademas apretaria el boton seleccionado ("Borrar partida").
//
// Se crea sola al arrancar (no hay que agregarla a ninguna escena). Va antes
// que todos los scripts (DefaultExecutionOrder) para que el frame en que se
// abre ya nadie lea la tecla como propia.
[DefaultExecutionOrder(-1000)]
public class ConsolaDeAdministrador : MonoBehaviour
{
    public static ConsolaDeAdministrador Instancia { get; private set; }
    public static bool Abierta => Instancia != null && Instancia._abierta;

    private const string NombreDelCampo = "ConsolaLinea";
    private const int MaximoDeLineas = 300;
    // Se dibuja para 720 de alto y se escala: igual de legible en cualquier
    // resolucion.
    private const float AltoDeReferencia = 720f;
    private const float AnchoDeBotones = 300f;

    // Lo que escribe la tecla de la consola en cada distribucion de teclado
    // (| ° ¬ en la latinoamericana, º ª en la española, ` ~ en la de EE.UU.):
    // no se usa en ningun comando y no tiene que quedar en la linea.
    private const string CaracteresDeLaTecla = "|°¬º`~ª";

    private readonly ComandosDeLaConsola _comandos = new ComandosDeLaConsola();
    private readonly List<ComandosDeLaConsola.Linea> _registro = new List<ComandosDeLaConsola.Linea>();
    private readonly List<string> _historial = new List<string>();
    private readonly List<EventSystem> _eventSystemsApagados = new List<EventSystem>();

    private bool _abierta;
    // Si algun comando corrio: solo entonces apagar la consola apaga los
    // trucos. Los bancos de prueba ponen su propia velocidad y su autoguardado
    // y no hay que pisarselos.
    private bool _tocoTrucos;
    private bool _enfocar;
    private bool _bajarScroll;
    private bool _cursorAlFinal;
    private int _posHistorial = -1;
    private string _linea = string.Empty;
    private Vector2 _scroll;

    private string _especieRapida = string.Empty;
    private string _nivelRapido = string.Empty;

    // Las sugerencias se calculan una vez por cambio de linea, no en cada
    // pasada de OnGUI (son varias por frame y hay 445 especies).
    private string _lineaSugerida;
    private string _ayudaDeLaLinea = string.Empty;

    private float _fps;
    private float _fpsTiempo;
    private int _fpsCuadros;

    private GUIStyle _fondo, _texto, _ok, _error, _eco, _titulo, _seccion, _campo, _boton, _botonPrendido, _aviso,
                     _pista, _pistaDerecha;

    // ---------- vida ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("ConsolaDeAdministrador");
        DontDestroyOnLoad(go);
        go.AddComponent<ConsolaDeAdministrador>();
    }

    // El proyecto no recarga el dominio al entrar a Play: los trucos de la
    // sesion anterior no pueden seguir prendidos en la siguiente.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        Instancia = null;
        ApagarTrucos();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
        _comandos.Limpiar = () => _registro.Clear();
        Escribir(new ComandosDeLaConsola.Linea(
            "Consola de administrador. 'ayuda' lista los comandos, Tab completa, las flechas recorren lo escrito.",
            ComandosDeLaConsola.Tipo.Titulo));
    }

    private void OnDestroy()
    {
        if (Instancia != this) return;
        Cerrar();
        Instancia = null;
    }

    private void Update()
    {
        ContarFps();

        // Apagada desde Opciones: se cierra y se lleva sus trucos, que no
        // queden prendidos sin forma de verlos ni de apagarlos.
        if (!GameSettings.ConsolaHabilitada)
        {
            Cerrar();
            if (_tocoTrucos)
            {
                ApagarTrucos();
                _tocoTrucos = false;
            }
            return;
        }

        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

        // Mientras Opciones espera una tecla nueva, esa tecla es de Opciones.
        if (GameInputService.Instance.IsRebinding) return;

        if (teclado.backquoteKey.wasPressedThisFrame)
        {
            if (_abierta) Cerrar(); else Abrir();
            return;
        }

        if (!_abierta) return;
        if (teclado.escapeKey.wasPressedThisFrame)
        {
            Cerrar();
            return;
        }

        // Una escena recien cargada (o la pelea, que arma uno si falta) trae
        // su EventSystem prendido.
        ApagarEventSystems();
    }

    public void Abrir()
    {
        if (_abierta) return;
        _abierta = true;
        _enfocar = true;
        _bajarScroll = true;
        GameInputService.OcuparTeclado(true);
        ApagarEventSystems();
    }

    public void Cerrar()
    {
        if (!_abierta) return;
        _abierta = false;
        GameInputService.OcuparTeclado(false);
        foreach (EventSystem es in _eventSystemsApagados)
            if (es != null) es.enabled = true;
        _eventSystemsApagados.Clear();
    }

    private void ApagarEventSystems()
    {
        // current es el primero prendido: apagarlo deja ver al siguiente.
        for (int i = 0; i < 8 && EventSystem.current != null; i++)
        {
            EventSystem es = EventSystem.current;
            es.enabled = false;
            _eventSystemsApagados.Add(es);
        }
    }

    private static void ApagarTrucos()
    {
        PokenModel.NoRecibeDanio = null;
        PlayerController.AtravesarParedes = false;
        CasinoDialogo.GuardadoAutomatico = true;
        Time.timeScale = 1f;
    }

    // ---------- comandos ----------

    public void Ejecutar(string linea)
    {
        linea = (linea ?? string.Empty).Trim();
        if (linea.Length == 0) return;

        if (_historial.Count == 0 || _historial[_historial.Count - 1] != linea) _historial.Add(linea);
        _posHistorial = -1;
        _tocoTrucos = true;

        // Al Player.log tambien: si alguien reporta un bug, se ve que toco.
        Debug.Log("[Consola] " + linea);
        foreach (ComandosDeLaConsola.Linea salida in _comandos.Ejecutar(linea)) Escribir(salida);
        // Al fondo ya en el proximo dibujo (y otra vez despues de medir las
        // lineas nuevas, por si alguna ocupa dos renglones).
        _scroll.y = float.MaxValue;
        _bajarScroll = true;
        _lineaSugerida = null;
    }

    private void Escribir(ComandosDeLaConsola.Linea linea)
    {
        _registro.Add(linea);
        if (_registro.Count > MaximoDeLineas) _registro.RemoveRange(0, _registro.Count - MaximoDeLineas);
    }

    private void RecorrerHistorial(int paso)
    {
        if (_historial.Count == 0) return;
        if (_posHistorial < 0) _posHistorial = _historial.Count;
        _posHistorial = Mathf.Clamp(_posHistorial + paso, 0, _historial.Count);
        _linea = _posHistorial < _historial.Count ? _historial[_posHistorial] : string.Empty;
        _cursorAlFinal = true;
    }

    private void ContarFps()
    {
        _fpsCuadros++;
        _fpsTiempo += Time.unscaledDeltaTime;
        if (_fpsTiempo < 0.5f) return;
        _fps = _fpsCuadros / _fpsTiempo;
        _fpsCuadros = 0;
        _fpsTiempo = 0f;
    }

    // ---------- dibujo ----------

    private void OnGUI()
    {
        bool habilitada = GameSettings.ConsolaHabilitada;
        if (!habilitada && !_abierta) return;

        PrepararEstilos();
        float escala = Mathf.Max(0.75f, Screen.height / AltoDeReferencia);
        Matrix4x4 antes = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(escala, escala, 1f));
        float ancho = Screen.width / escala;
        float alto = Screen.height / escala;

        if (_abierta) DibujarConsola(ancho, alto);
        else DibujarAvisos(ancho);

        GUI.matrix = antes;
    }

    // Con la consola cerrada, que no se olvide que hay trucos prendidos.
    private void DibujarAvisos(float ancho)
    {
        var partes = new List<string>();
        if (PokenModel.NoRecibeDanio != null) partes.Add("DIOS");
        if (PlayerController.AtravesarParedes) partes.Add("ATRAVESAR PAREDES");
        if (!Mathf.Approximately(Time.timeScale, 1f)) partes.Add($"x{Time.timeScale:0.##}");
        if (!CasinoDialogo.GuardadoAutomatico) partes.Add("SIN AUTOGUARDADO");
        if (partes.Count > 0)
        {
            var texto = new GUIContent("PRUEBAS: " + string.Join(" · ", partes));
            GUI.Label(new Rect(6f, 4f, _aviso.CalcSize(texto).x, 20f), texto, _aviso);
        }
        if (_comandos.MostrarFps)
        {
            var fps = new GUIContent($"FPS {_fps:0}");
            float w = _aviso.CalcSize(fps).x;
            GUI.Label(new Rect(ancho - w - 6f, 4f, w, 20f), fps, _aviso);
        }
    }

    private void DibujarConsola(float ancho, float alto)
    {
        const float margen = 8f;
        Rect panel = new Rect(margen, margen, ancho - margen * 2f, Mathf.Min(alto - margen * 2f, 480f));
        GUI.Box(panel, GUIContent.none, _fondo);

        Rect izquierda = new Rect(panel.x + 10f, panel.y + 8f, panel.width - AnchoDeBotones - 30f, panel.height - 16f);
        Rect derecha = new Rect(izquierda.xMax + 10f, panel.y + 8f, AnchoDeBotones, panel.height - 16f);

        AtenderTeclas();
        DibujarLinea(izquierda);
        DibujarBotones(derecha);
    }

    private void DibujarLinea(Rect area)
    {
        string modos = $"{(_comandos.ModoDios ? "  · DIOS" : "")}{(PlayerController.AtravesarParedes ? "  · PAREDES" : "")}" +
                       $"{(!Mathf.Approximately(Time.timeScale, 1f) ? $"  · x{Time.timeScale:0.##}" : "")}" +
                       $"{(_comandos.MostrarFps ? $"  · FPS {_fps:0}" : "")}";
        GUI.Label(new Rect(area.x, area.y, area.width, 20f), "CONSOLA DE ADMINISTRADOR" + modos, _titulo);
        GUI.Label(new Rect(area.x, area.y, area.width, 20f), "| abre y cierra · Esc cierra · Tab completa", _pistaDerecha);

        const float altoCampo = 26f, altoAyuda = 18f;
        Rect registro = new Rect(area.x, area.y + 24f, area.width, area.height - 24f - altoCampo - altoAyuda - 8f);
        Rect ayuda = new Rect(area.x, registro.yMax + 2f, area.width, altoAyuda);
        Rect campo = new Rect(area.x, ayuda.yMax + 4f, area.width, altoCampo);

        GUILayout.BeginArea(registro);
        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (ComandosDeLaConsola.Linea l in _registro) GUILayout.Label(l.Texto, Estilo(l.Tipo));
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        if (_bajarScroll && Event.current.type == EventType.Repaint)
        {
            _scroll.y = float.MaxValue;
            _bajarScroll = false;
        }

        ActualizarAyuda();
        GUI.Label(ayuda, _ayudaDeLaLinea, _pista);

        GUI.SetNextControlName(NombreDelCampo);
        _linea = GUI.TextField(campo, _linea, 200, _campo);
        _linea = SinLaTeclaDeLaConsola(_linea);

        if (_enfocar)
        {
            GUI.FocusControl(NombreDelCampo);
            _enfocar = false;
        }
        if (_cursorAlFinal && GUI.GetNameOfFocusedControl() == NombreDelCampo)
        {
            var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            editor.text = _linea;
            editor.MoveTextEnd();
            _cursorAlFinal = false;
        }
    }

    // Enter, Tab y las flechas son de la consola, no del campo de texto: se
    // atienden antes de dibujarlo y se marcan como usadas.
    private void AtenderTeclas()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        string foco = GUI.GetNameOfFocusedControl();

        // En los campos de "Dar Pokemon", Enter da el Pokemon.
        if (foco == "ConsolaEspecie" || foco == "ConsolaNivel")
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                EjecutarDesdeBoton(("dar " + _especieRapida + " " + _nivelRapido).Trim());
                e.Use();
            }
            else if (e.character == '\n' || e.character == '\t') e.Use();
            return;
        }
        if (foco != NombreDelCampo) return;

        switch (e.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Ejecutar(_linea);
                _linea = string.Empty;
                e.Use();
                return;
            case KeyCode.UpArrow:
                RecorrerHistorial(-1);
                e.Use();
                return;
            case KeyCode.DownArrow:
                RecorrerHistorial(1);
                e.Use();
                return;
            case KeyCode.Tab:
                _linea = _comandos.Completar(_linea);
                _cursorAlFinal = true;
                e.Use();
                return;
        }

        // El caracter de Enter y de Tab llega aparte, como otro evento.
        if (e.character == '\n' || e.character == '\t') e.Use();
    }

    private void ActualizarAyuda()
    {
        if (_lineaSugerida == _linea) return;
        _lineaSugerida = _linea;

        string escrito = _linea.TrimStart();
        if (escrito.Length == 0)
        {
            _ayudaDeLaLinea = "Probá: ayuda · ir casino2 · dar blaziken 50 · curar · ganar · dios · fantasma";
            return;
        }

        int espacio = escrito.IndexOf(' ');
        ComandosDeLaConsola.Comando comando = espacio > 0 ? _comandos.Buscar(escrito.Substring(0, espacio)) : null;
        List<string> opciones = _comandos.Sugerencias(escrito, 8);
        string lista = opciones.Count > 0 ? string.Join("  ", opciones) : "";
        _ayudaDeLaLinea = comando != null
            ? comando.Uso + (lista.Length > 0 ? "   →  " + lista : "")
            : lista;
    }

    private void DibujarBotones(Rect area)
    {
        GUILayout.BeginArea(area);

        Seccion("Equipo");
        GUILayout.BeginHorizontal();
        Boton("Curar", "curar");
        Boton("+5 niveles", "subir 5");
        Boton("Kit de prueba", "kit");
        Boton("Ver", "equipo");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("ConsolaEspecie");
        _especieRapida = SinLaTeclaDeLaConsola(GUILayout.TextField(_especieRapida, 40, _campo, GUILayout.Width(150f)));
        GUI.SetNextControlName("ConsolaNivel");
        _nivelRapido = SinLaTeclaDeLaConsola(GUILayout.TextField(_nivelRapido, 3, _campo, GUILayout.Width(44f)));
        if (GUILayout.Button("Dar Pokémon", _boton))
            Ejecutar(("dar " + _especieRapida + " " + _nivelRapido).Trim());
        GUILayout.EndHorizontal();
        GUILayout.Label("especie (o número) y nivel", _pista);

        Seccion("Combate");
        GUILayout.BeginHorizontal();
        Boton("Ganar", "ganar");
        Boton("Rival a 1 PS", "rival1");
        Boton("Perder", "perder");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Interruptor("Modo dios", _comandos.ModoDios, "dios");
        Boton("Fase (Julieta)", "fase");
        GUILayout.EndHorizontal();

        Seccion("Mundo");
        GUILayout.BeginHorizontal();
        Interruptor("Atravesar paredes", PlayerController.AtravesarParedes, "fantasma");
        Interruptor("x1", Mathf.Approximately(Time.timeScale, 1f), "velocidad 1");
        Interruptor("x2", Mathf.Approximately(Time.timeScale, 2f), "velocidad 2");
        Interruptor("x4", Mathf.Approximately(Time.timeScale, 4f), "velocidad 4");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Boton("+5000 plata", "plata 5000");
        Boton("Guardar", "guardar");
        Interruptor("Autoguardado", CasinoDialogo.GuardadoAutomatico, "autoguardado");
        GUILayout.EndHorizontal();

        Seccion("Ir a (con la historia hasta ahí)");
        DestinosDeLaConsola.Destino[] destinos = DestinosDeLaConsola.Todos;
        for (int i = 0; i < destinos.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            Boton(destinos[i].Nombre, "ir " + destinos[i].Clave);
            if (i + 1 < destinos.Length) Boton(destinos[i + 1].Nombre, "ir " + destinos[i + 1].Clave);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    private void Seccion(string titulo)
    {
        GUILayout.Space(4f);
        GUILayout.Label(titulo.ToUpperInvariant(), _seccion);
    }

    // Cada boton escribe su comando: mismo camino que tipearlo, y queda en el
    // registro para aprenderlo.
    private void Boton(string texto, string comando)
    {
        if (GUILayout.Button(texto, _boton)) EjecutarDesdeBoton(comando);
    }

    private void Interruptor(string texto, bool prendido, string comando)
    {
        if (GUILayout.Button(texto, prendido ? _botonPrendido : _boton)) EjecutarDesdeBoton(comando);
    }

    private void EjecutarDesdeBoton(string comando)
    {
        Ejecutar(comando);
        _enfocar = true;
    }

    private static string SinLaTeclaDeLaConsola(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.IndexOfAny(CaracteresDeLaTecla.ToCharArray()) < 0) return texto;
        var limpio = new System.Text.StringBuilder(texto.Length);
        foreach (char c in texto)
            if (CaracteresDeLaTecla.IndexOf(c) < 0) limpio.Append(c);
        return limpio.ToString();
    }

    // ---------- estilos ----------

    private GUIStyle Estilo(ComandosDeLaConsola.Tipo tipo)
    {
        switch (tipo)
        {
            case ComandosDeLaConsola.Tipo.Ok: return _ok;
            case ComandosDeLaConsola.Tipo.Error: return _error;
            case ComandosDeLaConsola.Tipo.Eco: return _eco;
            case ComandosDeLaConsola.Tipo.Titulo: return _titulo;
            default: return _texto;
        }
    }

    private void PrepararEstilos()
    {
        if (_fondo != null) return;

        _fondo = new GUIStyle(GUI.skin.box) { normal = { background = Textura(new Color(0.05f, 0.06f, 0.09f, 0.94f)) } };

        _texto = Linea(new Color(0.88f, 0.89f, 0.92f));
        _ok = Linea(new Color(0.55f, 0.92f, 0.55f));
        _error = Linea(new Color(1f, 0.5f, 0.45f));
        _eco = Linea(new Color(0.58f, 0.64f, 0.78f));
        _titulo = Linea(new Color(1f, 0.82f, 0.35f));
        _titulo.fontStyle = FontStyle.Bold;

        _seccion = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 2, 1),
            normal = { textColor = new Color(0.55f, 0.68f, 0.9f) }
        };
        _pista = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, clipping = TextClipping.Clip, margin = new RectOffset(2, 2, 0, 0),
            normal = { textColor = new Color(0.6f, 0.63f, 0.7f) }
        };
        _pistaDerecha = new GUIStyle(_pista) { alignment = TextAnchor.MiddleRight };

        Texture2D campo = Textura(new Color(0.14f, 0.16f, 0.22f, 1f));
        _campo = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 15, padding = new RectOffset(6, 6, 4, 4),
            normal = { background = campo, textColor = Color.white },
            hover = { background = campo, textColor = Color.white },
            focused = { background = Textura(new Color(0.18f, 0.21f, 0.3f, 1f)), textColor = Color.white }
        };

        _boton = Boton(new Color(0.2f, 0.23f, 0.31f), new Color(0.28f, 0.33f, 0.45f), new Color(0.36f, 0.42f, 0.56f));
        _botonPrendido = Boton(new Color(0.16f, 0.45f, 0.26f), new Color(0.2f, 0.55f, 0.32f), new Color(0.24f, 0.62f, 0.37f));

        _aviso = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(6, 6, 2, 2),
            normal = { background = Textura(new Color(0f, 0f, 0f, 0.6f)), textColor = new Color(1f, 0.82f, 0.35f) }
        };
        _aviso.stretchWidth = false;
    }

    private static GUIStyle Linea(Color color) => new GUIStyle(GUI.skin.label)
    {
        fontSize = 13, wordWrap = true, richText = false,
        margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(2, 2, 1, 1),
        normal = { textColor = color }
    };

    private static GUIStyle Boton(Color normal, Color hover, Color apretado) => new GUIStyle(GUI.skin.button)
    {
        fontSize = 12, padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(2, 2, 2, 2),
        normal = { background = Textura(normal), textColor = Color.white },
        hover = { background = Textura(hover), textColor = Color.white },
        active = { background = Textura(apretado), textColor = Color.white }
    };

    private static Texture2D Textura(Color color)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }
}
