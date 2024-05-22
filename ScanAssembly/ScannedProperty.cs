using System.Reflection;

namespace ScanAssembly;

public class ScannedProperty : IComparable<ScannedProperty>, IChangeScanner<ScannedProperty>
{
    public ScannedProperty() { }

    internal ScannedProperty(PropertyInfo p, ScanContext ctx)
    {
        var idx = p.GetIndexParameters();
        if (idx.Any())
        {
            Name = p.Name
                   + "["
                   + string.Join(",", idx.Select(x => x.ParameterType.FullName ?? x.ParameterType.Name))
                   + "]";
        }
        else
        {
            Name = p.Name;
        }

        ctx.InfoMessage($"Found property: {Name}");

        var getter = p.GetGetMethod();
        var setter = p.GetSetMethod();

        TypeName       = p.PropertyType.FullName ?? p.PropertyType.Name;
        PublicRead     = getter?.IsPublic == true;
        PublicWrite    = setter?.IsPublic == true;
        ProtectedRead  = getter?.IsFamily == true;
        ProtectedWrite = setter?.IsFamily == true;
        IsStatic       = getter?.IsStatic == true || setter?.IsStatic == true;
        IsInit = setter?.ReturnParameter
                       .GetRequiredCustomModifiers()
                       .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) == true;
    }

    public string Name           { get; set; } = "";
    public string TypeName       { get; set; } = "";
    public bool   PublicRead     { get; set; }
    public bool   PublicWrite    { get; set; }
    public bool   ProtectedRead  { get; set; }
    public bool   ProtectedWrite { get; set; }
    public bool   IsStatic       { get; set; }
    public bool   IsInit         { get; set; }

    int IComparable<ScannedProperty>.CompareTo(ScannedProperty? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public IEnumerable<ScanChange> GetChangesFrom(ScannedProperty original)
    {
        if (!Name.Equals(original.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The original property name '{original.Name}' does not match the current property name '{Name}'."
            );
        }

        var tag = $"property {Name}";
        
        if (IsStatic != original.IsStatic)
        {
            if (!original.IsStatic)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a static property.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now an instance property.");
            }
        }

        if (!TypeName.Equals(original.TypeName, StringComparison.Ordinal))
        {
            yield return new ScanChange(
                ScanChangeSeverity.Major,
                $"{tag} has had the type changed from '{original.TypeName}' to '{TypeName}'."
            );
        }

        if (PublicRead != original.PublicRead)
        {
            if (!original.PublicRead)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now publicly readable.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer publicly readable.");
            }
        }

        if (PublicWrite != original.PublicWrite)
        {
            if (!original.PublicWrite)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now publicly writable.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer publicly writable.");
            }
        }

        if (ProtectedRead != original.ProtectedRead)
        {
            if (!original.ProtectedRead)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now protected read.");
            }
            else if (!PublicRead)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer protected read.");
            }
        }

        if (ProtectedWrite != original.ProtectedWrite)
        {
            if (!original.ProtectedWrite)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now protected write.");
            }
            else if (!PublicWrite)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer protected write.");
            }
        }

        if (IsInit != original.IsInit)
        {
            if (!original.IsInit)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now init-only.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is no longer init-only.");
            }
        }
    }
}
