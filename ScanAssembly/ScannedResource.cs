using System.Security.Cryptography;

namespace ScanAssembly;

public class ScannedResource : IComparable<ScannedResource>, IChangeScanner<ScannedResource>
{
    public ScannedResource()
    {
        
    }

    public ScannedResource(string name, Stream stream, ScanContext ctx)
    {
        var buf = new byte[2048];
        long len = 0;
        ctx.InfoMessage($"Found resource: {name}");
        Name = name;

        var hash = SHA256.Create();
        
        var cnt = stream.Read(buf, 0, buf.Length);
        while (cnt > 0)
        {
            hash.TransformBlock(buf, 0, cnt, null, 0);
            len += cnt;
            cnt =  stream.Read(buf, 0, buf.Length);
        }

        hash.TransformFinalBlock(buf, 0, 0);
        Size      = len;
        Sha256Sum = string.Join("", hash.Hash!.Select(x => x.ToString("x2")));
    }
    
    public string Name      { get; set; } = "";
    public long   Size      { get; set; }
    public string Sha256Sum { get; set; } = "";


    public IEnumerable<ScanChange> GetChangesFrom(ScannedResource original)
    {
        if (!Name.Equals(original.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The original resource name '{original.Name}' does not match the current resource name '{Name}'."
            );
        }

        var tag = $"resource '{Name}'";
        
        if (Size != original.Size)
        {
            if (Size < original.Size)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has grown.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has shrunk.");
            }
        }
        else if (!Sha256Sum.Equals(original.Sha256Sum, StringComparison.OrdinalIgnoreCase))
        {
            yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} has changed.");
        }
    }

    public int CompareTo(ScannedResource? other)
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
}
