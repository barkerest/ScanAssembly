using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ScanAssembly;

public class ScannedAssembly : IComparable<ScannedAssembly>, IChangeScanner<ScannedAssembly>
{
    public ScannedAssembly() { }

    internal ScannedAssembly(string asmFileName)
    {
        var file  = new FileInfo(asmFileName);
        if (!file.Exists)
        {
            return;
        }
        
        using (var stream = file.OpenRead())
        {
            var hash = SHA256.Create().ComputeHash(stream);
            Hash = file.Length.ToString("x10") + string.Join("", hash.Select(x => x.ToString("x2")));
        }
    
        using (var ctx = new ScanContext(asmFileName))
        {
            var asm     = ctx.Assembly;
            var asmName = asm.GetName();
            Name    = asmName.Name    ?? Path.GetFileName(asmFileName);
            Version = asmName.Version ?? new Version();
            Types = asm.GetExportedTypes()
                       .Select(x => new ScannedType(x))
                       .OrderBy(x => x.FullName)
                       .ToList();

            Resources = asm.GetManifestResourceNames()
                           .Select(
                               n => new ScannedResource(n, asm.GetManifestResourceStream(n) ?? new MemoryStream())
                           )
                           .OrderBy(x => x.Name)
                           .ToArray();
        }
    }
    
    public string Name { get; set; } = "";

    public Version Version { get; set; } = new();

    public string Hash { get; set; } = "";
    
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
        // If the hash is set and has not changed, there should be no changes.
        if (!string.IsNullOrEmpty(Hash) && Hash.Equals(original.Hash, StringComparison.OrdinalIgnoreCase))
        {
            // informational only, we'll still check the exposed interface.
            yield return new ScanChange(ScanChangeSeverity.None, "The assembly file hash is the same.");
        }
        
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
