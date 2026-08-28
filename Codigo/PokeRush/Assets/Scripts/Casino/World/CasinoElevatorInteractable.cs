using UnityEngine;

// Que hace falta para poder usar el ascensor.
public enum CasinoRequisitoTipo
{
    // Sube siempre. Para probar, o para un piso sin condicion.
    Ninguno,
    // Hay que tener juntada cierta plata (piso 1: las tragaperras).
    DineroMinimo,
    // Hay que haber cumplido un objetivo que activo su propia mecanica
    // (piso 2: la ruleta; piso 3: ganar el blackjack apostando un Poken).
    Flag
}

// El ascensor que conecta los pisos del casino.
//
// Es el unico que decide si se puede subir: la condicion no vive en la puerta
// dibujada ni en la UI, asi el equipo de nivel puede mover, cambiar o rehacer la
// puerta sin tocar la progresion. Se cuelga de un objeto con Collider2D pegado a
// las puertas, en una capa que este dentro del interactableMask del jugador.
//
// Cuando la condicion se cumple una vez, queda registrada en un StoryFlag (que
// SaveService ya guarda): el jugador no tiene que volver a juntarla si baja y
// sube, ni si cierra el juego.
[RequireComponent(typeof(Collider2D))]
public class CasinoElevatorInteractable : MonoBehaviour, IInteractable
{
    [Header("A donde lleva")]
    [Tooltip("Nombre EXACTO de la escena del piso siguiente (tiene que estar en Build Settings).")]
    [SerializeField] private string escenaDestino = "";
    [Tooltip("Nombre del piso, para los carteles. Ej: 'piso 2'.")]
    [SerializeField] private string nombreDelDestino = "el piso siguiente";

    [Header("Condicion para subir")]
    [SerializeField] private CasinoRequisitoTipo requisito = CasinoRequisitoTipo.DineroMinimo;
    [Tooltip("Plata que hay que tener juntada, con requisito = DineroMinimo.")]
    [Min(0)] [SerializeField] private int dineroRequerido = 500;
    [Tooltip("Flag que hay que tener, con requisito = Flag.")]
    [SerializeField] private StoryFlag flagRequerido = StoryFlag.None;

    [Header("Progreso")]
    [Tooltip("Flag que se activa al cumplir la condicion por primera vez. " +
             "Con esto el piso queda superado para siempre.")]
    [SerializeField] private StoryFlag otorgaFlag = StoryFlag.None;

    [Header("Deuda")]
    [Tooltip("Con una deuda abierta con la enfermeria, el ascensor no sube. " +
             "Es lo que le da peso a que te curen fiado.")]
    [SerializeField] private bool bloqueaConDeuda = true;
    [Tooltip("Cartel cuando hay deuda. {0} = lo que se debe.")]
    [SerializeField, TextArea(2, 3)] private string mensajeConDeuda =
        "El de seguridad te frena: primero pagale las {0} monedas a la enfermeria.";

    [Header("Textos")]
    [SerializeField] private string mensajeSubiendo = "El ascensor se abre.";
    [Tooltip("Cartel cuando falta plata. {0} = lo que falta, {1} = lo pedido.")]
    [SerializeField, TextArea(2, 3)] private string mensajeFaltaDinero =
        "Necesitas conseguir mas dinero. Te faltan {0}.";
    [SerializeField, TextArea(2, 3)] private string mensajeFaltaObjetivo =
        "El ascensor no se abre todavia. Te falta algo en este piso.";
    [Tooltip("Preguntar antes de subir. Con esto en false sube de una.")]
    [SerializeField] private bool preguntarAntesDeSubir = true;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        // La deuda frena SIEMPRE, incluso si el piso ya estaba superado: si no,
        // bastaria con subir una vez para no pagarla nunca mas.
        if (bloqueaConDeuda && player.HasDebt)
        {
            Avisar(string.Format(mensajeConDeuda, player.Debt));
            return true;
        }

        bool yaLoPaso = otorgaFlag != StoryFlag.None && story != null && story.HasFlag(otorgaFlag);

        if (!yaLoPaso && !CumpleRequisito(player, story, out string faltante))
        {
            Avisar(faltante);
            return true;
        }

        // La primera vez que cumple, queda anotado.
        if (!yaLoPaso && otorgaFlag != StoryFlag.None && story != null)
            story.ActivateFlag(otorgaFlag);

        if (!preguntarAntesDeSubir) { Subir(); return true; }

        Preguntar($"Subir a {nombreDelDestino}?", acepto => { if (acepto) Subir(); });
        return true;
    }

    private bool CumpleRequisito(PlayerModel player, StoryModel story, out string mensaje)
    {
        mensaje = null;

        switch (requisito)
        {
            case CasinoRequisitoTipo.Ninguno:
                return true;

            case CasinoRequisitoTipo.DineroMinimo:
                if (player.Money >= dineroRequerido) return true;
                mensaje = string.Format(mensajeFaltaDinero,
                    dineroRequerido - player.Money, dineroRequerido);
                return false;

            case CasinoRequisitoTipo.Flag:
                if (flagRequerido == StoryFlag.None) return true;
                if (story != null && story.HasFlag(flagRequerido)) return true;
                mensaje = mensajeFaltaObjetivo;
                return false;
        }

        return true;
    }

    private void Subir()
    {
        if (string.IsNullOrEmpty(escenaDestino))
        {
            Debug.LogError($"[Casino] El ascensor '{name}' no tiene escena de destino.");
            return;
        }

        // El cambio de piso SI cambia de escena: son cuatro mapas distintos y es
        // lo que hace el resto del juego. Lo que no cambia de escena son los
        // minijuegos, que se abren encima del piso.
        //
        // El progreso viaja: GameManager es DontDestroyOnLoad, asi que el
        // dinero, el equipo y los flags cruzan solos.
        Avisar(mensajeSubiendo);
        SceneLoaderService.Instance.LoadScene(escenaDestino);
    }

    // Los carteles salen por la caja de mensajes del sistema de menus, la misma
    // que usa el resto del juego.
    private void Avisar(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Casino] {mensaje}"); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje });
    }

    private void Preguntar(string pregunta, System.Action<bool> respuesta)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null) { respuesta?.Invoke(true); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = pregunta, IsQuestion = true },
            resultado => respuesta?.Invoke(resultado is MessageBoxResult r && r.Confirmed));
    }
}
