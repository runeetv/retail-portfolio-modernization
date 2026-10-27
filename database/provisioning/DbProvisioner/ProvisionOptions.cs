namespace DbProvisioner;

public sealed class ProvisionOptions
{
    public bool SchemaOnly { get; private set; }
    public bool SeedOnly { get; private set; }
    public bool Reset { get; private set; }

    public static ProvisionOptions Parse(string[] args)
    {
        var o = new ProvisionOptions();
        foreach (var a in args)
        {
            switch (a.Trim().ToLowerInvariant())
            {
                case "--schema-only": o.SchemaOnly = true; break;
                case "--seed-only": o.SeedOnly = true; break;
                case "--reset": o.Reset = true; break;
            }
        }
        return o;
    }
}
