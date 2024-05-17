using System.Runtime.Loader;

namespace ScanAssembly;

public class ScannedAssembly : IComparable<ScannedAssembly>, IChangeScanner<ScannedAssembly>
{
    public ScannedAssembly() { }

    internal ScannedAssembly(string asmFileName)
    {
        var ctx = new AssemblyLoadContext("ScanContext", true);
        try
        {
            var asm = ctx.LoadFromAssemblyPath(asmFileName);
            Name = asm.GetName().Name ?? Path.GetFileName(asmFileName);
            Types = asm.GetExportedTypes()
                       .Select(x => new ScannedType(x))
                       .OrderBy(x => x.FullName)
                       .ToList();

            Resources = asm.GetManifestResourceNames()
                           .Select(n => new ScannedResource(n, asm.GetManifestResourceStream(n) ?? new MemoryStream()))
                           .OrderBy(x => x.Name)
                           .ToArray();
        }
        finally
        {
            ctx.Unload();
        }
    }

    public string Name { get; set; } = "";

    public ICollection<ScannedType>     Types     { get; set; } = new List<ScannedType>();
    public ICollection<ScannedResource> Resources { get; set; } = new List<ScannedResource>();

    public int CompareTo(ScannedAssembly? other)
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

    public IEnumerable<ScanChange> GetChangesFrom(ScannedAssembly original)
    {
        var missingTypes = original.Types
                              .Where(x => !Types.Any(y => y.FullName.Equals(x.FullName, StringComparison.Ordinal)))
                              .ToArray();

        foreach (var type in missingTypes)
        {
            yield return new ScanChange(ScanChangeSeverity.Major, $"The type '{type.FullName}' has been removed.");
        }

        foreach (var type in Types)
        {
            var origType =
                original.Types.FirstOrDefault(x => x.FullName.Equals(type.FullName, StringComparison.Ordinal));
            var tag = $"The type '{type.FullName}'";
            if (origType is null)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has been added.");
            }
            else
            {
                foreach (var chg in type.GetChangesFrom(origType))
                {
                    yield return chg.WithPrefix(tag);
                }
            }
        }

        var missingRes = original.Resources
                                 .Where(x => !Resources.Any(y => y.Name.Equals(x.Name, StringComparison.Ordinal)))
                                 .ToArray();
        foreach (var res in missingRes)
        {
            yield return new ScanChange(ScanChangeSeverity.Major, $"The resource '{res.Name}' has been removed.");
        }

        foreach (var res in Resources)
        {
            var origRes = original.Resources.FirstOrDefault(x => x.Name.Equals(res.Name, StringComparison.Ordinal));
            var tag     = $"The resource '{res.Name}'";
            if (origRes is null)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has been added.");
            }
            else
            {
                foreach (var chg in res.GetChangesFrom(origRes))
                {
                    yield return chg.WithPrefix(tag);
                }
            }
        }
    }
}
