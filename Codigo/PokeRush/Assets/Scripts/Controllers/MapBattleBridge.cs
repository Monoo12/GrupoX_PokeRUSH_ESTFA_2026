using UnityEngine;

// Mapa -> Combate -> Mapa, en UNA SOLA ESCENA.
//
// La idea central: el combate NO es una escena aparte. Cambiar de escena
// destruye el mapa entero, y despues hay que reconstruirlo y restaurar la
// posicion del jugador, el estado de los NPC y los triggers ya usados. Nada de
// eso hace falta si la escena nunca se descarga: se esconde el mapa, se muestra
// el combate, y al terminar se hace al reves. El mapa queda intacto porque
// nunca dejo de existir.
//
// Lo unico que hay que cuidar es que desactivar un GameObject MATA sus
// corrutinas: por eso se alinea al jugador a la grilla antes de esconder el
// mapa (si no, queda entre dos tiles para siempre).
//
// USO
//   1. Poner este componente en un objeto suelto de la escena.
//   2. 'mapaRaiz'     -> el objeto que contiene Grid, jugador, NPCs y su camara.
//   3. 'combateRaiz'  -> el objeto que contiene el HUD, los PokenView, el fondo
//                        y la camara de combate. Arranca desactivado.
//   4. 'battleController' -> el BattleController que vive dentro de combateRaiz.
//
// Cualquier InteractableTrigger que dispare un combate ya funciona: el puente
// escucha los eventos del BattleController, no hace falta avisarle.
public class MapBattleBridge : MonoBehaviour
{
    [Header("Las dos mitades de la escena")]
    [Tooltip("Todo lo del mundo: Grid, jugador, NPCs, camara de exploracion.")]
    [SerializeField] private GameObject mapaRaiz;
    [Tooltip("Todo lo del combate: HUD, PokenViews, fondo, camara de combate. " +
             "Conviene dejarlo DESACTIVADO en el editor.")]
    [SerializeField] private GameObject combateRaiz;

    [Header("Referencias")]
    [SerializeField] private BattleController battleController;
    [Tooltip("Opcional. Si esta, se lo alinea a la grilla antes de esconder el mapa.")]
    [SerializeField] private PlayerController playerController;

    [Header("Ritmo")]
    [Tooltip("Segundos antes de devolver el control al mapa. Da aire a la " +
             "pantalla de resultado del combate.")]
    [SerializeField] private float esperaAlVolver = 0.4f;

    private void Awake()
    {
        // El combate arranca escondido pase lo que pase: si alguien lo dejo
        // activo en el editor, taparia el mapa desde el primer frame.
        if (combateRaiz != null) combateRaiz.SetActive(false);
        if (mapaRaiz != null) mapaRaiz.SetActive(true);
    }

    private void OnEnable()
    {
        if (battleController == null) return;
        battleController.BattleStarted += EsconderMapa;
        battleController.BattleEnded += VolverAlMapa;
    }

    private void OnDisable()
    {
        if (battleController == null) return;
        battleController.BattleStarted -= EsconderMapa;
        battleController.BattleEnded -= VolverAlMapa;
    }

    private void EsconderMapa()
    {
        // Antes de desactivar: cerrar el paso en curso. Desactivar el objeto
        // mata la corrutina de movimiento y el jugador quedaria entre tiles.
        if (playerController != null) playerController.SnapToGrid();

        if (combateRaiz != null) combateRaiz.SetActive(true);
        if (mapaRaiz != null) mapaRaiz.SetActive(false);
    }

    private void VolverAlMapa(BattleOutcome outcome)
    {
        StartCoroutine(VolverAlMapaRoutine());
    }

    private System.Collections.IEnumerator VolverAlMapaRoutine()
    {
        if (esperaAlVolver > 0f) yield return new WaitForSeconds(esperaAlVolver);

        // El mapa vuelve tal cual estaba: posiciones, flags y triggers nunca se
        // destruyeron. No hay nada que restaurar.
        if (mapaRaiz != null) mapaRaiz.SetActive(true);
        if (combateRaiz != null) combateRaiz.SetActive(false);
    }
}
