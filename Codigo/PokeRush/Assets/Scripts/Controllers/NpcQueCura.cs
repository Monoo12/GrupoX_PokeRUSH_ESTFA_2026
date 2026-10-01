using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Un NPC que cura al equipo: la enfermera del Data Center de Nestor.
//
// No trae sistemas propios. Lo que dice sale de DialogueDefinition (como
// NpcQueHabla, asi los textos se reescriben tocando el asset), la pregunta va
// por la caja de dialogo del mapa (DialogueController.StartConfirm) y la
// curacion es PlayerModel.CurarEquipo, la misma de la enfermera del casino y de
// la pelea del tutorial. A diferencia de la del casino, no cobra: es el primer
// nivel y no hay economia que cuidar.
//
//   1. 'saludo' (si tiene algo que decir en esta partida).
//   2. Si el equipo esta entero: 'yaEstaSano' y listo.
//   3. Si no, la pregunta. Si acepta: cura, la pantalla destella como en una
//      posta y dice 'alCurar'. Si no: 'alRechazar'.
//
// El destello es un velo blanco por DEBAJO de la caja de dialogo: se ve el
// mapa iluminarse mientras se lee el "listo".
[RequireComponent(typeof(Collider2D))]
public class NpcQueCura : MonoBehaviour, IInteractable
{
    [Header("Lo que dice (DialogueDefinition)")]
    [Tooltip("Antes de preguntar. Puede tener variantes por flag.")]
    [SerializeField] private DialogueDefinition saludo;
    [Tooltip("Despues de curar.")]
    [SerializeField] private DialogueDefinition alCurar;
    [Tooltip("Si no hay nada que curar.")]
    [SerializeField] private DialogueDefinition yaEstaSano;
    [Tooltip("Si el jugador dice que no.")]
    [SerializeField] private DialogueDefinition alRechazar;

    [Header("Pregunta")]
    [SerializeField] private string pregunta = "¿Querés que cure a tu equipo?";
    [Tooltip("Cortas: van en la ventanita del Sí/No, que con textos largos parte el renglón.")]
    [SerializeField] private string opcionSi = "Sí";
    [SerializeField] private string opcionNo = "No";

    [Header("Destello de la curacion")]
    [SerializeField] private Color colorDelDestello = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Cuantas veces destella, como la maquina de una posta.")]
    [SerializeField, Min(1)] private int destellos = 3;
    [SerializeField, Min(0.05f)] private float duracionDeCadaDestello = 0.28f;

    [Header("Referencias (opcional: se buscan solas)")]
    [SerializeField] private DialogueController dialogueController;

    // True desde que arranca la charla hasta que se cierra la ultima caja.
    private bool _ocupada;

    public DialogueDefinition Saludo => saludo;
    public bool Ocupada => _ocupada;

    private void Awake()
    {
        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
    }

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || dialogueController == null) return false;
        // Con la caja abierta la charla sigue en curso. Se mira la caja y no
        // solo '_ocupada' porque DialogueController.Cancelar cierra sin avisar.
        if (dialogueController.IsOpen) return true;

        _ocupada = true;
        dialogueController.StartDialogue(saludo, story, () => Ofrecer(player, story));
        return true;
    }

    private void Ofrecer(PlayerModel player, StoryModel story)
    {
        if (!player.EquipoNecesitaCuracion)
        {
            Terminar(yaEstaSano, story);
            return;
        }

        string quien = saludo != null ? saludo.speakerName : name;
        Sprite cara = saludo != null ? saludo.speakerPortrait : null;
        dialogueController.StartConfirm(quien, cara, pregunta, opcionSi, opcionNo, acepto =>
        {
            if (!acepto)
            {
                Terminar(alRechazar, story);
                return;
            }

            // Se cura ANTES de mostrar el "listo": si el juego se corta con la
            // caja abierta, la curacion ya esta hecha.
            player.CurarEquipo();
            Debug.Log($"[Enfermera] '{name}' curo al equipo ({player.Team.Count} Pokemon).");
            StartCoroutine(Destellar());
            Terminar(alCurar, story);
        });
    }

    private void Terminar(DialogueDefinition dialogo, StoryModel story)
    {
        dialogueController.StartDialogue(dialogo, story, () => _ocupada = false);
    }

    // ---------- destello ----------

    private IEnumerator Destellar()
    {
        Image velo = CrearVelo();
        if (velo == null) yield break;

        for (int i = 0; i < destellos; i++)
        {
            for (float t = 0f; t < duracionDeCadaDestello; t += Time.unscaledDeltaTime)
            {
                // Sube y baja (seno de medio ciclo): un pulso, no un parpadeo seco.
                float a = Mathf.Sin(t / duracionDeCadaDestello * Mathf.PI);
                velo.color = new Color(colorDelDestello.r, colorDelDestello.g, colorDelDestello.b,
                                       colorDelDestello.a * a);
                yield return null;
            }
        }
        Destroy(velo.canvas.gameObject);
    }

    // Un Canvas propio, de pantalla completa, justo debajo del de la caja de
    // dialogo: ilumina el mapa sin lavar el texto. No bloquea clics.
    private Image CrearVelo()
    {
        var go = new GameObject("[DestelloDeCuracion]");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas cajaDeDialogo = dialogueController != null ? dialogueController.GetComponentInParent<Canvas>() : null;
        canvas.sortingOrder = cajaDeDialogo != null ? cajaDeDialogo.sortingOrder - 1 : 0;

        var veloGo = new GameObject("Velo", typeof(RectTransform));
        veloGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)veloGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = veloGo.AddComponent<Image>();
        img.color = new Color(colorDelDestello.r, colorDelDestello.g, colorDelDestello.b, 0f);
        img.raycastTarget = false;
        return img;
    }

    // Si el NPC se apaga a mitad (el combate esconde el mapa), el velo no puede
    // quedar colgado en pantalla.
    private void OnDisable()
    {
        StopAllCoroutines();
        var velo = GameObject.Find("[DestelloDeCuracion]");
        if (velo != null) Destroy(velo);
    }
}
