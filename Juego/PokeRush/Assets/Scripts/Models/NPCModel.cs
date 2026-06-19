public class NPCModel
{
    public NPCDefinition Definition { get; private set; }
    public bool HasInteracted { get; private set; }

    public NPCModel(NPCDefinition definition)
    {
        Definition = definition;
        HasInteracted = false;
        //HasInteracted permite saber si el jugador ya habló con este NPC en la sesión actual — útil para cambiar el diálogo después de la primera interacción.
    }

    public void RegisterInteraction()
    {
        HasInteracted = true;
    }
}