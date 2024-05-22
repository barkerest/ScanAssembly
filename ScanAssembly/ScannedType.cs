using System.Reflection;

namespace ScanAssembly;

public class ScannedType : IComparable<ScannedType>, IChangeScanner<ScannedType>
{
    public ScannedType() { }

    internal ScannedType(Type t)
    {
        FullName    = t.FullName ?? t.Name;
        IsInterface = t.IsInterface;
        IsStruct    = t.IsValueType;
        IsAbstract  = t.IsAbstract;
        IsSealed    = t.IsSealed;

        Properties = t.GetProperties(
                          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy
                      )
                      .Select(x => new ScannedProperty(x))
                      .Where(x => x.PublicRead || x.ProtectedRead || x.PublicWrite || x.ProtectedWrite)
                      .OrderBy(x => x.Name)
                      .ToList();

        Fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                  .Select(x => new ScannedField(x))
                  .Where(x => x.IsPublic || x.IsProtected)
                  .OrderBy(x => x.Name)
                  .ToList();

        Methods = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                   .Select(x => new ScannedMethod(x))
                   .Concat(
                       t.GetMethods(
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy
                        )
                        .Select(x => new ScannedMethod(x))
                   )
                   .Where(x => x.IsPublic || x.IsProtected)
                   .OrderBy(x => x.Name)
                   .ToList();
    }

    public string FullName { get; set; } = "";

    public bool IsStruct    { get; set; }
    public bool IsInterface { get; set; }

    public bool IsAbstract { get; set; }
    public bool IsSealed   { get; set; }

    // no need to check for IsGeneric.  The full name takes care of that for us.
    
    public ICollection<ScannedProperty> Properties { get; set; } = new List<ScannedProperty>();
    public ICollection<ScannedField>    Fields     { get; set; } = new List<ScannedField>();
    public ICollection<ScannedMethod>   Methods    { get; set; } = new List<ScannedMethod>();

    public int CompareTo(ScannedType? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        return string.Compare(FullName, other.FullName, StringComparison.Ordinal);
    }

    public IEnumerable<ScanChange> GetChangesFrom(ScannedType original)
    {
        if (!FullName.Equals(original.FullName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The original type name '{original.FullName}' does not match the current type name '{FullName}'."
            );
        }

        var tag = $"Type '{FullName}'";
        
        if (IsStruct != original.IsStruct)
        {
            if (!original.IsStruct)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a value type.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a reference type.");
            }
        }

        if (IsInterface != original.IsInterface)
        {
            if (!original.IsInterface)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now an interface.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer an interface.");
            }
        }

        if (IsAbstract != original.IsAbstract)
        {
            if (!original.IsAbstract)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now an abstract type.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is no longer an abstract type.");
            }
        }

        if (IsSealed != original.IsSealed)
        {
            if (!original.IsSealed)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a sealed type.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is no longer a sealed type.");
            }
        }

        var missingFields = original.Fields
                                    .Where(x => !Fields.Any(y => y.Name.Equals(x.Name, StringComparison.Ordinal)))
                                    .ToArray();
        foreach (var field in missingFields)
        {
            yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is missing field {field.Name}.");
        }

        foreach (var field in Fields)
        {
            var origField = original.Fields.FirstOrDefault(x => x.Name.Equals(field.Name, StringComparison.Ordinal));
            if (origField is null)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has a new field {field.Name}.");
            }
            else
            {
                foreach (var chg in field.GetChangesFrom(origField))
                {
                    yield return chg.WithPrefix(tag);
                }
            }
        }

        var missingProps = original.Properties
                                   .Where(x => !Properties.Any(y => y.Name.Equals(x.Name, StringComparison.Ordinal)))
                                   .ToArray();
        foreach (var prop in missingProps)
        {
            yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is missing property {prop.Name}.");
        }

        foreach (var prop in Properties)
        {
            var origProp = original.Properties.FirstOrDefault(x => x.Name.Equals(prop.Name, StringComparison.Ordinal));
            if (origProp is null)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has a new property {prop.Name}.");
            }
            else
            {
                foreach (var chg in prop.GetChangesFrom(origProp))
                {
                    yield return chg.WithPrefix(tag);
                }
            }
        }

        var missingMeths = original.Methods
                                   .Where(x => !Methods.Any(y => y.Name.Equals(x.Name, StringComparison.Ordinal)))
                                   .ToArray();
        foreach (var meth in missingMeths)
        {
            yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is missing method {meth.Name}.");
        }

        foreach (var meth in Methods)
        {
            var origMeth = original.Methods.FirstOrDefault(x => x.Name.Equals(meth.Name, StringComparison.Ordinal));
            if (origMeth is null)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has a new method {meth.Name}.");
            }
            else
            {
                foreach (var chg in meth.GetChangesFrom(origMeth))
                {
                    yield return chg.WithPrefix(tag);
                }
            }
        }
    }
}
