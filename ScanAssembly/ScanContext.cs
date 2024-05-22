using System.Reflection;
using System.Runtime.Loader;

namespace ScanAssembly;

public class ScanContext : IDisposable
{
    private static   void                Blackhole(string s) {}
    
    private readonly AssemblyLoadContext _loadContext;
    
    public string AssemblyFilePath { get; }

    private readonly string           _assemblyDir;
    private readonly string           _sdkDir;
    private          Assembly?        _assembly;
    private          ScannedAssembly? _scannedAssembly;

    public Assembly Assembly => _assembly ??= _loadContext.LoadFromAssemblyPath(AssemblyFilePath);

    public ScannedAssembly ScannedAssembly => _scannedAssembly ??= new ScannedAssembly(this);

    public Action<string> InfoMessage  { get; set; } = Blackhole;
    
    public ScanContext(string asmFilePath)
    {
        AssemblyFilePath = asmFilePath;
        _assemblyDir     = Path.GetDirectoryName(AssemblyFilePath) ?? "";
        _sdkDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared");
        if (!Directory.Exists(_sdkDir)) _sdkDir = "";
        _loadContext     = new AssemblyLoadContext("ScanContext", true);
        _loadContext.Resolving += LoadContextOnResolving;
    }

    private IEnumerable<string> GetCandidates(Version? version, string? name)
    {
        bool foundLocal = false;
        if (!string.IsNullOrEmpty(_assemblyDir))
        {
            foreach (var asmFile in Directory.GetFiles(_assemblyDir, "*.dll", SearchOption.AllDirectories))
            {
                var fn = Path.GetFileNameWithoutExtension(asmFile);
                if (fn.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    foundLocal = true;
                    InfoMessage($"Found candidate in assembly directory: {fn}");
                    yield return asmFile;
                }
            }
        }
        
        if (foundLocal) yield break;
        
        if (string.IsNullOrEmpty(_sdkDir)) yield break;
        
        // SDK structure: dotnet/shared/{SDK}/{VER}/{FILES}
        foreach (var sdk in Directory.GetDirectories(_sdkDir, "*", SearchOption.TopDirectoryOnly))
        {
            var sdkName = Path.GetFileName(sdk);
            foreach (var verDir in Directory.GetDirectories(sdk, "*", SearchOption.TopDirectoryOnly))
            {
                var ver = new Version(Path.GetFileName(verDir));
                if (version is null || (ver.Major == version.Major && ver.Minor == version.Minor))
                {
                    foreach (var asmFile in Directory.GetFiles(verDir, "*.dll", SearchOption.AllDirectories))
                    {
                        var fn = Path.GetFileNameWithoutExtension(asmFile);
                        if (fn.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            InfoMessage($"Found candidate in {sdkName} v{ver}: {fn}");
                            yield return asmFile;
                        }
                    }
                }
            }
        }
    }
    
    private Assembly? LoadContextOnResolving(AssemblyLoadContext ctx, AssemblyName asmName)
    {
        InfoMessage($"Attempting to resolve {asmName}.");
        var candidate = GetCandidates(asmName.Version, asmName.Name).MaxBy(x => x);
        if (string.IsNullOrEmpty(candidate)) return null;
        InfoMessage($"Using candidate: {candidate}");
        return ctx.LoadFromAssemblyPath(candidate);
    }

    public void Dispose()
    {
        _loadContext.Unload();
    }
}
