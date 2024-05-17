namespace ScanAssembly;

public class ScanChange
{
    public ScanChange(ScanChangeSeverity severity, string description)
    {
        Severity    = severity;
        Description = description;
    }
    
    public ScanChangeSeverity Severity { get; }

    public string Description { get; private set; }

    public ScanChange WithPrefix(string prefix)
    {
        Description = prefix + " " + Description;
        return this;
    }
}
