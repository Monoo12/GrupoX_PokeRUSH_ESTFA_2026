public class NurseModel : NPCModel
{
    public bool IsHealing { get; private set; }
    public NurseDefinition NurseDefinition { get; private set; }

    public NurseModel(NurseDefinition definition) : base(definition)
    {
        NurseDefinition = definition;
        IsHealing = false;
    }

    public void SetHealing(bool value)
    {
        IsHealing = value;
    }
}