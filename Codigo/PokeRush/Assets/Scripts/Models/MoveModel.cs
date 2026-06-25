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

    public void ConsumePP()
    {
        if (CurrentPP > 0)
        {
            CurrentPP--;
        }
        else
        {
            throw new System.InvalidOperationException($"No PP left for move {Definition.moveName}");
        }
    }

    public void SetCurrentPP(int pp)
    {
        CurrentPP = pp;
        if (CurrentPP < 0) CurrentPP = 0;
        if (CurrentPP > Definition.maxPP) CurrentPP = Definition.maxPP;
    }
}
