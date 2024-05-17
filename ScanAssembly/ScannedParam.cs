using System.Reflection;

namespace ScanAssembly;

public class ScannedParam : IComparable<ScannedParam>, IChangeScanner<ScannedParam>
{
    public ScannedParam()
    {
        
    }
    
    public ScannedParam(ParameterInfo p)
    {
        Position   = p.Position;
        Name       = p.Name                   ?? $"@[{p.Position}]";
        TypeName   = p.ParameterType.FullName ?? p.ParameterType.Name;
        IsRef      = p.ParameterType.IsByRef;
        IsOut      = p.IsOut;
        IsOptional = p.IsOptional;
        IsParams   = p.GetCustomAttributes<ParamArrayAttribute>().Any();
    }
    
    public int Position { get; set; }

    public string Name       { get; set; } = "";
    public string TypeName   { get; set; } = "";
    public bool   IsRef      { get; set; }
    public bool   IsOut      { get; set; }
    public bool   IsOptional { get; set; }
    public bool   IsParams   { get; set; }


    public int CompareTo(ScannedParam? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        return Position.CompareTo(other.Position);
    }

    public IEnumerable<ScanChange> GetChangesFrom(ScannedParam original)
    {
        if (Position != original.Position)
        {
            throw new ArgumentException(
                $"The original parameter position ({original.Position}) does not match the current parameter position ({Position})."
            );
        }

        var tag = $"parameter {(Position + 1)}";

        if (!Name.Equals(original.Name, StringComparison.Ordinal))
        {
            yield return new ScanChange(
                ScanChangeSeverity.Negligible,
                $"{tag} has had the name changed from '{original.Name}' to '{Name}'."
            );
        }

        // redundant, this should not actually get triggered since the types are part of the method name.
        var t1 = TypeName.EndsWith('&') ? TypeName[..^1] : TypeName;
        var t2 = original.TypeName.EndsWith('&') ? original.TypeName[..^1] : original.TypeName;
        if (!t1.Equals(t2, StringComparison.Ordinal))
        {
            yield return new ScanChange(
                ScanChangeSeverity.Major,
                $"{tag} has had the type changed from '{original.TypeName}' to '{TypeName}'."
            );
        }
        
        if (IsRef != original.IsRef)
        {
            if (!original.IsRef)
            {
                var type = IsOut ? "an out" : "a ref";
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} is now {type} parameter."
                );
            }
            else
            {
                var type = original.IsOut ? "an out" : "a ref";
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} is no longer {type} parameter."
                );
            }
        }
        else if (IsOut != original.IsOut)
        {
            if (!original.IsOut)
            {
                // ref => out is OK.
                yield return new ScanChange(
                    ScanChangeSeverity.Negligible,
                    $"{tag} went from a ref parameter to an out parameter."
                );
            }
            else
            {
                // out => ref now requires the parameter to be set before calling.
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} went from an out parameter to a ref parameter."
                );
            }
        }

        if (IsOptional != original.IsOptional)
        {
            if (!original.IsOptional)
            {
                // making a parameter optional is OK.
                yield return new ScanChange(
                    ScanChangeSeverity.Negligible,
                    $"{tag} is now optional."
                );
            }
            else
            {
                // making a parameter required is not OK.
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} is no longer optional."
                );
            }
        }

        if (IsParams != original.IsParams)
        {
            // this changes the signature, so it's never OK.
            if (!original.IsParams)
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} is now a variable argument collection."
                );
            }
            else
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} is no longer a variable argument collection."
                );
            }
        }
        
    }
}
