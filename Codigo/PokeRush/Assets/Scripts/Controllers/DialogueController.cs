using System.Collections.Generic;
using UnityEngine;

// Maneja la caja de texto del mapa: lineas seguidas, preguntas de Si/No y
// listas de opciones (StartChoice).
//
// No lee input por su cuenta. El unico que lee teclado es PlayerInputController,
// que mientras 'IsOpen' este en true le manda todo aca en vez de mover al
// jugador. Asi no hay dos componentes peleandose por la misma tecla.
//
// Las lineas pueden traer quien las dice ("[TIXI] Hola.", "[PJ] ¿Qué?") y las
// marcas {PJ} e {INICIAL}; StartDialogue ademas resuelve lo que trae el asset
// (variantes segun la partida, objeto clave, pregunta con respuestas y el
// dialogo que sigue). Ver DialogueDefinition.
public class DialogueController : MonoBehaviour
{
    private enum Modo { Cerrado, Lineas, Pregunta }

    [Header("Referencias")]
    [SerializeField] private DialogueView dialogueView;

    // Mientras esto sea true, el jugador no se mueve.
    public bool IsOpen => _modo != Modo.Cerrado;

    private Modo _modo = Modo.Cerrado;

    private string _speaker;
    private Sprite _portrait;
    // El asset que se esta mostrando: de ahi salen los retratos de los que
    // hablan con [NOMBRE]. Null en los mensajes sueltos.
    private DialogueDefinition _definicion;
    private string[] _lineas;
    private int _lineaActual;
    private System.Action _onComplete;

    private string[] _opciones;
    private int _seleccion;
    private System.Action<int> _onEleccion;
    // Que opcion elige Escape. En las de Si/No es la segunda (el "No").
    private int _opcionAlCancelar;

    // Pasos seguidos de una cadena de dialogos sin que el jugador toque nada
    // (definiciones vacias que pasan de largo). Un 'siguiente' que se apunta a
    // si mismo sin lineas colgaria el juego: con esto se corta.
    private const int PasosVaciosMaximos = 32;

    private void Awake()
    {
        if (dialogueView == null) dialogueView = GetComponentInChildren<DialogueView>(true);
        // La caja arranca escondida pase lo que pase.
        if (dialogueView != null) dialogueView.Hide();
    }

    // ---- API ----

    // Lineas sueltas, sin necesidad de un DialogueDefinition.
    public void StartMessage(string speaker, Sprite portrait, string[] lineas,
        System.Action onComplete = null)
    {
        _definicion = null;
        MostrarLineas(speaker, portrait, lineas, onComplete);
    }

    // Un dialogo del asset con todo lo que trae: variantes, flag, objeto,
    // pregunta y lo que sigue. 'onComplete' se llama al final de TODA la cadena.
    public void StartDialogue(DialogueDefinition dialogue, StoryModel storyModel,
        System.Action onComplete = null)
    {
        Correr(dialogue, storyModel, onComplete, 0);
    }

    // Pregunta de dos opciones. El callback recibe true si eligio la primera.
    public void StartConfirm(string speaker, Sprite portrait, string pregunta,
        string opcionSi, string opcionNo, System.Action<bool> onResultado)
    {
        StartChoice(speaker, portrait, pregunta, new[] { opcionSi, opcionNo },
            eleccion => onResultado?.Invoke(eleccion == 0));
    }

    // Pregunta con una lista de opciones (los menus de los NPCs de la Quarcho
    // Cueva: comprar, viajar, charlar...). El callback recibe el indice
    // elegido. Escape elige 'opcionAlCancelar'; -1 = la ultima, que por
    // convencion es la de "nada".
    public void StartChoice(string speaker, Sprite portrait, string pregunta,
        string[] opciones, System.Action<int> onEleccion, int opcionAlCancelar = -1)
    {
        if (opciones == null || opciones.Length == 0) { onEleccion?.Invoke(-1); return; }

        _speaker = Marcas(speaker);
        _portrait = portrait;
        _opciones = new string[opciones.Length];
        for (int i = 0; i < opciones.Length; i++) _opciones[i] = Marcas(opciones[i]);
        _seleccion = 0;
        _onEleccion = onEleccion;
        _opcionAlCancelar = opcionAlCancelar >= 0 && opcionAlCancelar < opciones.Length
            ? opcionAlCancelar
            : opciones.Length - 1;
        _modo = Modo.Pregunta;

        dialogueView.Show();
        dialogueView.DisplayLine(_speaker, portrait, Marcas(pregunta));
        dialogueView.ShowChoices(_opciones, _seleccion);
    }

    // Cierra la caja SIN llamar a nadie: ni el callback de las lineas ni el de
    // la pregunta, ni el flag del final (en StartDialogue se aplica en el
    // callback, asi que tampoco corre). Para cuando lo que la abrio se cancela
    // entero (reiniciar la pelea de Julieta con un dialogo en pantalla): el
    // callback volveria a una pelea que ya no existe.
    public void Cancelar()
    {
        if (_modo == Modo.Cerrado) return;
        _onComplete = null;
        _onEleccion = null;
        Cerrar();
    }

    // ---- input, lo llama PlayerInputController ----

    public void OnNavigate(int delta)
    {
        if (_modo != Modo.Pregunta || delta == 0) return;

        _seleccion = Mathf.Clamp(_seleccion + delta, 0, _opciones.Length - 1);
        dialogueView.ShowChoices(_opciones, _seleccion);
    }

    public void OnConfirm()
    {
        if (_modo == Modo.Lineas) AvanzarLinea();
        else if (_modo == Modo.Pregunta) ResolverPregunta();
    }

    // Cancelar equivale a elegir la segunda opcion (el "No"), o la que haya
    // pedido la lista.
    public void OnCancel()
    {
        if (_modo != Modo.Pregunta) return;
        _seleccion = _opcionAlCancelar;
        ResolverPregunta();
    }

    // ---- la cadena de un DialogueDefinition ----

    // Muestra lo que le toca a 'def' en esta partida y, al cerrarse, aplica
    // flag y objeto y sigue con la pregunta o el siguiente.
    private void Correr(DialogueDefinition def, StoryModel story, System.Action alFinal, int pasosVacios)
    {
        if (def == null) { alFinal?.Invoke(); return; }

        System.Action despues = () =>
        {
            AplicarFinal(def, story);
            Seguir(def, story, alFinal, pasosVacios);
        };

        if (!def.TieneQueDecir(story))
        {
            if (pasosVacios >= PasosVaciosMaximos)
            {
                Debug.LogWarning($"[Dialogo] '{def.name}': demasiados dialogos vacios seguidos, se corta la cadena.");
                alFinal?.Invoke();
                return;
            }
            // Sin lineas no hay caja: se aplica lo del final y se sigue.
            AplicarFinal(def, story);
            Seguir(def, story, alFinal, pasosVacios + 1);
            return;
        }

        _definicion = def;
        MostrarLineas(def.speakerName, def.speakerPortrait, def.LineasPara(story), despues);
    }

    private void AplicarFinal(DialogueDefinition def, StoryModel story)
    {
        StoryFlag flag = def.FlagPara(story);
        if (flag != StoryFlag.None && story != null) story.ActivateFlag(flag);

        PlayerModel player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (def.otorgaObjeto != null && player != null && !player.Inventory.HasSpecialItem(def.otorgaObjeto))
        {
            player.Inventory.AddSpecialItem(def.otorgaObjeto, 1);
            Debug.Log($"[Dialogo] '{def.name}' entrego {def.otorgaObjeto.itemName}.");
        }
    }

    private void Seguir(DialogueDefinition def, StoryModel story, System.Action alFinal, int pasosVacios)
    {
        if (def.TienePregunta)
        {
            PlayerModel player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            List<DialogueDefinition.Respuesta> disponibles = def.RespuestasDisponibles(story, player);
            if (disponibles.Count > 0)
            {
                var textos = new string[disponibles.Count];
                for (int i = 0; i < textos.Length; i++) textos[i] = disponibles[i].opcion;

                // La que pidio el asset para Escape, si esta entre las que se ven.
                int alCancelar = -1;
                if (def.cancelarElige >= 0 && def.cancelarElige < def.respuestas.Length)
                    alCancelar = disponibles.IndexOf(def.respuestas[def.cancelarElige]);

                StartChoice(def.speakerName, def.speakerPortrait, def.pregunta, textos, eleccion =>
                {
                    if (eleccion < 0 || eleccion >= disponibles.Count) { alFinal?.Invoke(); return; }
                    DialogueDefinition.Respuesta r = disponibles[eleccion];
                    if (r.otorgaFlag != StoryFlag.None && story != null) story.ActivateFlag(r.otorgaFlag);
                    // Elegir es tocar algo: la cuenta de pasos vacios vuelve a cero.
                    Correr(r.siguiente, story, alFinal, 0);
                }, alCancelar);
                return;
            }
        }

        if (def.siguiente != null) { Correr(def.siguiente, story, alFinal, pasosVacios); return; }
        alFinal?.Invoke();
    }

    // ---- interno ----

    private void MostrarLineas(string speaker, Sprite portrait, string[] lineas, System.Action onComplete)
    {
        // Las vacias se saltean: una variante puede dejar renglones en blanco.
        var limpias = new List<string>();
        if (lineas != null)
            foreach (string l in lineas)
                if (!string.IsNullOrWhiteSpace(l)) limpias.Add(l);

        if (limpias.Count == 0) { onComplete?.Invoke(); return; }

        _speaker = speaker;
        _portrait = portrait;
        _lineas = limpias.ToArray();
        _lineaActual = 0;
        _onComplete = onComplete;
        _modo = Modo.Lineas;

        dialogueView.Show();
        dialogueView.HideChoices();
        MostrarLineaActual();
    }

    private void MostrarLineaActual()
    {
        string linea = _lineas[_lineaActual];
        string quien = _speaker;
        Sprite cara = _portrait;

        if (Partir(linea, out string nombre, out string texto))
        {
            linea = texto;
            if (EsElJugador(nombre))
            {
                quien = NombreDelJugador().ToUpperInvariant();
                cara = null;
            }
            else
            {
                quien = nombre;
                cara = nombre.Length == 0 ? null
                    : string.Equals(nombre, _speaker, System.StringComparison.OrdinalIgnoreCase) ? _portrait
                    : _definicion != null ? _definicion.RetratoDe(nombre) : null;
            }
        }

        dialogueView.DisplayLine(Marcas(quien), cara, Marcas(linea));
    }

    private void AvanzarLinea()
    {
        _lineaActual++;
        if (_lineaActual < _lineas.Length) { MostrarLineaActual(); return; }

        Cerrar();
        // El callback se llama DESPUES de cerrar: si abre otro dialogo (tipico
        // del "se unio al equipo"), no se lo come el Cerrar de este.
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    private void ResolverPregunta()
    {
        int eleccion = _seleccion;
        Cerrar();

        var cb = _onEleccion;
        _onEleccion = null;
        cb?.Invoke(eleccion);
    }

    private void Cerrar()
    {
        _modo = Modo.Cerrado;
        dialogueView.HideChoices();
        dialogueView.Hide();
    }

    // ---- marcas del texto (publicas para los tests y los carteles sin caja) ----

    // "[TIXI] Hola." -> nombre "TIXI", texto "Hola.". "[] Llueve." -> nombre
    // vacio (narrador). Sin corchete al principio devuelve false y no toca nada.
    public static bool Partir(string linea, out string nombre, out string texto)
    {
        nombre = null;
        texto = linea;
        if (string.IsNullOrEmpty(linea) || linea[0] != '[') return false;

        int cierre = linea.IndexOf(']');
        if (cierre < 0) return false;

        nombre = linea.Substring(1, cierre - 1).Trim();
        texto = linea.Substring(cierre + 1).TrimStart();
        return true;
    }

    public static bool EsElJugador(string nombre)
        => nombre == "PJ" || nombre == "{PJ}";

    // {PJ} = nombre del jugador, {INICIAL} = el primer Pokemon del equipo.
    public static string Marcas(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.IndexOf('{') < 0) return texto;
        return texto.Replace("{PJ}", NombreDelJugador()).Replace("{INICIAL}", NombreDelInicial());
    }

    // Para los carteles que no pasan por la caja (el MessageBox de respaldo):
    // "[TIXI] Hola." -> "TIXI: Hola.", con las marcas ya puestas.
    public static string ComoTextoPlano(string linea)
    {
        if (!Partir(linea, out string nombre, out string texto)) return Marcas(linea);
        if (EsElJugador(nombre)) nombre = NombreDelJugador().ToUpperInvariant();
        return Marcas(string.IsNullOrEmpty(nombre) ? texto : nombre + ": " + texto);
    }

    private static string NombreDelJugador()
    {
        PlayerModel p = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        return p != null && !string.IsNullOrEmpty(p.PlayerName) ? p.PlayerName : "PJ";
    }

    private static string NombreDelInicial()
    {
        PlayerModel p = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (p == null || p.Team.Count == 0 || p.Team[0] == null || p.Team[0].Definition == null)
            return "tu Pokémon";
        return p.Team[0].Definition.pokenName;
    }
}
