using UnityEngine;

// El Poken en la escena de combate. No dibuja nada por si mismo: delega en un
// PokenVisual, que puede ser un sprite (billboard) o un modelo 3D.
//
// Por que asi: el proyecto tiene sprites de ~971 especies importadas de PokeAPI
// pero modelos 3D de bastantes menos. En vez de elegir entre "todo 2.5D" y
// "todo 3D", cada especie usa lo mejor que tenga: si la PokenDefinition trae
// battleModel se dibuja el modelo, si no el sprito de siempre. Las dos formas
// conviven en el mismo combate y agregar un modelo nuevo no toca codigo.
//
// El resto del juego (BattleController, BattleFxView) habla solo con esta clase
// y nunca con un SpriteRenderer, asi los efectos funcionan igual en ambos casos.
public class PokenView : MonoBehaviour
{
    public enum VisualMode
    {
        // Modelo 3D si la especie lo tiene; si no, sprite. Es el modo normal.
        Auto,
        // Siempre sprite, aunque haya modelo. Deja el juego en 2.5D de un click.
        ForceSprite,
        // Siempre modelo. Las especies sin modelo no se dibujan (util para probar).
        ForceModel
    }

    [Header("Representacion")]
    [Tooltip("Como se elige entre sprite y modelo 3D.")]
    [SerializeField] private VisualMode visualMode = VisualMode.Auto;
    [SerializeField] private SpritePokenVisual spriteVisual;
    [SerializeField] private ModelPokenVisual modelVisual;

    private PokenVisual _active;

    // Alto real de lo que se esta dibujando. Los efectos lo usan para apuntar
    // los impactos al cuerpo y no a los pies.
    public float Height => _active != null ? _active.Height : 1f;

    // Punto donde revientan los golpes.
    public Vector3 HitPoint => _active != null
        ? _active.HitPoint
        : transform.position + Vector3.up * 0.5f;

    private void Reset()
    {
        spriteVisual = GetComponentInChildren<SpritePokenVisual>(includeInactive: true);
        modelVisual = GetComponentInChildren<ModelPokenVisual>(includeInactive: true);
    }

    private void Awake()
    {
        if (spriteVisual == null)
            spriteVisual = GetComponentInChildren<SpritePokenVisual>(includeInactive: true);
        if (modelVisual == null)
            modelVisual = GetComponentInChildren<ModelPokenVisual>(includeInactive: true);
    }

    public void SetPoken(PokenModel pokenModel, bool isPlayerSide)
    {
        if (pokenModel == null) return;
        PokenDefinition definition = pokenModel.Definition;

        PokenVisual chosen = ChooseVisual(definition);
        if (chosen == null)
        {
            Debug.LogWarning($"[PokeRush] {name}: no hay ningun PokenVisual asignado " +
                             $"para el modo {visualMode}.");
            return;
        }

        // Solo uno activo a la vez: si quedaran los dos se dibujarian el sprite
        // y el modelo superpuestos.
        if (spriteVisual != null) spriteVisual.gameObject.SetActive(spriteVisual == chosen);
        if (modelVisual != null) modelVisual.gameObject.SetActive(modelVisual == chosen);

        _active = chosen;
        _active.Bind(definition, isPlayerSide);

        // Limpio el tinte por si el Poken anterior quedo teñido o a medio
        // desvanecer (debilitado, envenenado) al reusar la vista.
        _active.SetTint(Color.white);
        _active.SetRendererVisible(true);
    }

    private PokenVisual ChooseVisual(PokenDefinition definition)
    {
        if (visualMode == VisualMode.ForceSprite) return spriteVisual;
        if (visualMode == VisualMode.ForceModel) return modelVisual;

        bool hasModel = definition != null && definition.battleModel != null;
        if (hasModel && modelVisual != null) return modelVisual;
        return spriteVisual;
    }

    public void Show() => gameObject.SetActive(true);

    public void Hide() => gameObject.SetActive(false);

    // Tinte multiplicativo con alpha. Color.white deja el Poken como estaba.
    public void SetTint(Color tint) => _active?.SetTint(tint);

    // Prende y apaga el dibujado sin desactivar el GameObject: apagarlo cortaria
    // las corrutinas de los efectos que estan corriendo sobre este Poken.
    public void SetRendererVisible(bool visible) => _active?.SetRendererVisible(visible);

    public void PlayHitFeedback()
    {
        // shake o flash al recibir daño — se implementa despues
    }
}
