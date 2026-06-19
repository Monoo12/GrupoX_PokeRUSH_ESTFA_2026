public class MoveModel
{
    public MoveDefinition Definition { get; private set; }
    public int CurrentPP { get; private set; }

    public bool HasPP => CurrentPP > 0;

    public MoveModel(MoveDefinition definition)
    {
        Definition = definition;
        CurrentPP = definition.maxPP;
    }
}