using System.Reflection;

namespace ScanAssembly;

public class ScannedMethod : IComparable<ScannedMethod>, IChangeScanner<ScannedMethod>
{
    public ScannedMethod() { }

    private ScannedMethod(string baseName, Type returnType, MethodBase m)
    {
        TypeName    = returnType.FullName ?? returnType.Name;
        IsStatic    = m.IsStatic;
        IsPublic    = m.IsPublic;
        IsProtected = m.IsFamily;
        Parameters = m.GetParameters()
                      .Select(x => new ScannedParam(x))
                      .OrderBy(x => x.Position)
                      .ToList();
        if (m.IsGenericMethod)
        {
            var cnt = m.GetGenericArguments().Length;
            baseName += $"`{cnt}";
        }
        if (IsStatic && (baseName == "op_Explicit" || baseName == "op_Implicit"))
        {
            baseName += "_" + TypeName;
        }
        Name = baseName
               + "("
               + string.Join(",", Parameters.Select(x => x.TypeName))
               + ")";
        
    }
    
    internal ScannedMethod(MethodInfo m) : this(m.Name, m.ReturnType, m)
    {
    }

    internal ScannedMethod(ConstructorInfo c) : this(".ctor", typeof(void), c)
    {
    }


    public string Name        { get; set; } = "";
    public string TypeName    { get; set; } = "";
    public bool   IsStatic    { get; set; }
    public bool   IsPublic    { get; set; }
    public bool   IsProtected { get; set; }

    public IList<ScannedParam> Parameters { get; set; } = new List<ScannedParam>();

    public int CompareTo(ScannedMethod? other)
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

    public IEnumerable<ScanChange> GetChangesFrom(ScannedMethod original)
    {
        if (!Name.Equals(original.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The original method name '{original.Name}' does not match the current method name '{Name}'."
            );
        }

        var tag = $"method {Name}";
        
        // this should be redundant since the method name contains the parameter types (and therefore count).
        if (Parameters.Count != original.Parameters.Count)
        {
            var diff = Parameters.Count - original.Parameters.Count;
            if (diff == 1)
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} has had 1 parameter added."
                );
            }
            else if (diff == -1)
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} has had 1 parameter removed."
                );
            }
            else if (diff > 0)
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} has had {diff} parameters added."
                );
            }
            else
            {
                yield return new ScanChange(
                    ScanChangeSeverity.Major,
                    $"{tag} has had {-diff} parameters removed."
                );
            }
        }

        if (IsStatic != original.IsStatic)
        {
            if (!original.IsStatic)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a static method.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now an instance method.");
            }
        }

        if (!TypeName.Equals(original.TypeName, StringComparison.Ordinal))
        {
            yield return new ScanChange(
                ScanChangeSeverity.Major,
                $"{tag} has had the return type changed from '{original.TypeName}' to '{TypeName}'."
            );
        }
        
        if (IsPublic != original.IsPublic)
        {
            if (!original.IsPublic)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now a public method.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer a public method.");
            }
        }

        if (IsProtected != original.IsProtected)
        {
            if (!original.IsProtected)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now a protected method.");
            }
            else if (!IsPublic) // => public is reported above.
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer a protected method.");
            }
        }

        for (var i = 0; i < Parameters.Count; i++)
        {
            var pc = Parameters[i];
            var po = original.Parameters[i];
            foreach (var chg in pc.GetChangesFrom(po))
            {
                yield return chg.WithPrefix(tag);
            }
        }
    }
}
