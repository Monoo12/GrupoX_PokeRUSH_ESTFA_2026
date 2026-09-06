using UnityEngine;

// La maquina de la ruleta de premios puesta en el mapa.
//
// Mismo patron que la tragaperras y la mesa de ruleta: se cuelga del mueble que
// dibujo el equipo de nivel y abre la UI encima del piso, sin cambiar de
// escena. La diferencia es que esta NO oscurece el mapa (lo decide la propia
// pantalla con DimsBackground), asi que el jugador ve la ruleta apoyada sobre
// el escenario.
[RequireComponent(typeof(Collider2D))]
public class PrizeWheelInteractable : MonoBehaviour, IInteractable
{
    [Header("Premios")]
    [Tooltip("Premios de esta maquina. Vacio = los que trae cargados el prefab " +
             "de la pantalla, que ya vienen con los ocho por defecto.")]
    [SerializeField] private Prize[] premios;

    [Header("Condiciones")]
    [Tooltip("Si esta puesto, la ruleta solo se puede usar teniendo ya este flag.")]
    [SerializeField] private StoryFlag requiereFlag = StoryFlag.None;
    [SerializeField] private string mensajeBloqueada = "La ruleta esta apagada.";

    [Header("Un solo uso (opcional)")]
    [Tooltip("Con esto prendido la ruleta se puede girar una sola vez en toda " +
             "la partida. Apagado = se puede volver cuantas veces quiera.")]
    [SerializeField] private bool unSoloUso = false;
    [SerializeField] private string mensajeYaUsada = "Ya giraste hoy. Volve manana.";

    [Header("Textos")]
    [Tooltip("Lo que dice antes de abrir. Vacio = abre de una.")]
    [SerializeField, TextArea(2, 3)] private string mensajeDeBienvenida = "";

    private bool _yaUsada;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        if (requiereFlag != StoryFlag.None && (story == null || !story.HasFlag(requiereFlag)))
        {
            Avisar(mensajeBloqueada);
            return true;
        }

        if (unSoloUso && _yaUsada)
        {
            Avisar(mensajeYaUsada);
            return true;
        }

        if (string.IsNullOrEmpty(mensajeDeBienvenida)) Abrir();
        else Avisar(mensajeDeBienvenida, Abrir);

        return true;
    }

    private void Abrir()
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            Debug.LogError("[Ruleta] No hay MenuManager: no se puede abrir la ruleta.");
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.PrizeWheel,
            new PrizeWheelRequest { Prizes = premios },
            resultado => AlCerrar(resultado as PrizeWheelResult));
    }

    private void AlCerrar(PrizeWheelResult resultado)
    {
        if (resultado != null && resultado.Giros > 0) _yaUsada = true;
    }

    private void Avisar(string mensaje, System.Action alCerrar = null)
    {
        if (string.IsNullOrEmpty(mensaje)) { alCerrar?.Invoke(); return; }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[Ruleta] {mensaje}"); alCerrar?.Invoke(); return; }

        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = mensaje },
            _ => alCerrar?.Invoke());
    }
}
