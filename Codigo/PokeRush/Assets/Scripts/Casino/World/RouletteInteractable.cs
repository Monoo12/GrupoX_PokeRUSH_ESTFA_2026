using UnityEngine;

// La mesa de ruleta del piso 2.
//
// Igual que la tragaperras: se cuelga del mueble que dibujo el equipo de nivel y
// abre la UI encima del piso, sin cambiar de escena.
[RequireComponent(typeof(Collider2D))]
public class RouletteInteractable : MonoBehaviour, IInteractable
{
    [Header("Mesa")]
    [Tooltip("Configuracion de esta mesa: casillas, pesos, premios y penalizaciones.")]
    [SerializeField] private RouletteTableDefinition mesa;
    [Tooltip("Titulo del cartel. Vacio = el nombre que trae la definicion.")]
    [SerializeField] private string titulo = "";

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, la mesa solo se puede usar teniendo ya este flag.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeBloqueada = "Esta mesa esta cerrada.";

    [Header("Al terminar")]
    [SerializeField] private bool mostrarResumen = true;
    [Tooltip("Cartel al cumplir el objetivo del piso.")]
    [SerializeField, TextArea(2, 3)] private string mensajeObjetivo =
        "El de seguridad te mira distinto. El ascensor ya te deja subir.";

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        if (requiereFlag != StoryFlag.None && (story == null || !story.HasFlag(requiereFlag)))
        {
            Avisar(mensajeBloqueada);
            return true;
        }

        if (mesa == null)
        {
            Debug.LogError($"[Casino] La mesa '{name}' no tiene definicion asignada.");
            Avisar("Esta mesa esta fuera de servicio.");
            return true;
        }

        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[Casino] No hay MenuManager: no se puede abrir la ruleta.");
            return true;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.Roulette,
            new RouletteScreenRequest { Mesa = mesa, Titulo = titulo },
            resultado => AlCerrar(resultado as RouletteScreenResult));

        return true;
    }

    private void AlCerrar(RouletteScreenResult resultado)
    {
        if (resultado == null || resultado.Giros <= 0) return;

        // Lo del objetivo se avisa siempre: es progresion, no un detalle.
        if (resultado.ObjetivoCumplido) { Avisar(mensajeObjetivo); return; }

        if (!mostrarResumen) return;

        string texto;
        if (resultado.Neto > 0) texto = $"Te fuiste con {resultado.Neto} de ganancia.";
        else if (resultado.Neto < 0) texto = $"La mesa se quedo con {-resultado.Neto}.";
        else texto = "Empataste con la mesa.";

        Avisar(texto);
    }

    private void Avisar(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Casino] {mensaje}"); return; }

        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje });
    }
}
