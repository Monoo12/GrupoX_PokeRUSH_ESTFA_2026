public class VendorModel : NPCModel
{
    public VendorDefinition VendorDefinition { get; private set; }

    public VendorModel(VendorDefinition definition) : base(definition)
    {
        VendorDefinition = definition;
    }
}