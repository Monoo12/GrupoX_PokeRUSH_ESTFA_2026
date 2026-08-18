using UnityEngine;

// Algo que bloquea el paso hasta que se activa un flag de la historia: los
// rayos que corta un boton, una reja que se abre con una tarjeta, etc.
//
// El objeto entero se apaga (visual + collider), asi que basta con ponerle este
// componente a la barrera y elegir el flag. Se apaga de dos formas:
//   - al vuelo, escuchando StoryModel.FlagActivated (el jugador toca el boton
//     y los rayos se cortan sin salir de la escena);
//   - al cargar la escena, si el flag ya estaba activo de antes.
//
// USO
//   1. Objeto con su sprite y un Collider2D en una capa del 'obstacleMask' del
//      jugador (en este proyecto, 'Obstacles').
//   2. Elegir 'flagQueLaApaga'.
//   3. El boton que la apaga es un InteractableTrigger con ese mismo flag en
//      'otorgaFlag'. No hace falta que se conozcan entre si.
public class BarreraPorFlag : MonoBehaviour
{
    [Header("Condicion")]
    [Tooltip("Cuando este flag se activa, la barrera desaparece.")]
    [SerializeField] private StoryFlag flagQueLaApaga = StoryFlag.None;
    [Tooltip("Al reves: arranca apagada y APARECE cuando se activa el flag.")]
    [SerializeField] private bool alReves = false;

    [Header("Que se apaga")]
    [Tooltip("Vacio = este mismo GameObject. Sirve para apagar varias piezas juntas.")]
    [SerializeField] private GameObject[] objetos;

    private StoryModel _story;

    // Se engancha en Update y no en Start A PROPOSITO: el StoryModel lo crea
    // SceneBootstrapper en SU Start, y el orden entre dos Start no esta
    // garantizado. Enganchandose en Start, la barrera se quedaba con un
    // GameManager.Story todavia en null y no se enteraba nunca del flag.
    private void Update()
    {
        if (_story != null) return; // ya enganchado, no hay nada que hacer

        var gm = GameManager.Instance;
        if (gm == null || gm.Story == null) return;

        _story = gm.Story;
        _story.FlagActivated += OnFlagActivada;
        Aplicar(_story.HasFlag(flagQueLaApaga)); // por si ya venia activo del guardado
    }

    private void OnDestroy()
    {
        if (_story != null) _story.FlagActivated -= OnFlagActivada;
    }

    private void OnFlagActivada(StoryFlag flag)
    {
        if (flag != flagQueLaApaga) return;
        Aplicar(true);
    }

    private void Aplicar(bool flagActivo)
    {
        // alReves invierte la logica: sin flag no se ve, con flag aparece.
        bool visible = alReves ? flagActivo : !flagActivo;

        if (objetos == null || objetos.Length == 0)
        {
            // NO se apaga el GameObject a si mismo. Update() no corre en un
            // objeto inactivo: si la escena quedara guardada con la barrera
            // desactivada, no podria volver a mostrarse nunca y el paso
            // quedaria abierto para siempre sin tocar el boton.
            // Apagando renderers y colliders el componente sigue vivo.
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = visible;
            return;
        }

        foreach (var o in objetos)
            if (o != null) o.SetActive(visible);
    }
}
