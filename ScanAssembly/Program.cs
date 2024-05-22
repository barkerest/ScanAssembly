using System.Text.Json;

namespace ScanAssembly;

public class Program
{
    private static int Usage()
    {
        Console.WriteLine(
            "Usage: {0} assembly.dll [[out] assembly.json]",
            Path.GetFileName(typeof(Program).Assembly.Location)
        );
            
        Console.WriteLine();
        Console.WriteLine("  If only the first argument is provided, the scan of the assembly will be");
        Console.WriteLine("  written out as JSON and the program will exit with a code of 0.");
        Console.WriteLine();
        Console.WriteLine("  If all three arguments are provided, the scan of the assembly will be written");
        Console.WriteLine("  out to the provided JSON file and the program will exit with a code of 0.");
        Console.WriteLine();
        Console.WriteLine("  If two arguments are provided, the scan of the assembly will be compared to");
        Console.WriteLine("  the loaded JSON and any changes will be output.  The program will exit with");
        Console.WriteLine("  a code of 1-4 to suggest incrementing build(1), release(2), minor(3), or");
        Console.WriteLine("  major(4).");
        Console.WriteLine();
            
        return 8;
    }
    
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            return Usage();
        }

        Console.WriteLine($"Scanning assembly {args[0]}...");

        var asmFile = Path.GetFullPath(args[0]);
        if (!File.Exists(asmFile))
        {
            Console.WriteLine("File not found.");
            return 7;
        }
        
        var asmScan = new ScannedAssembly(asmFile);

        if (args.Length < 2)
        {
            Console.WriteLine();
            var json = JsonSerializer.Serialize(
                asmScan,
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }
            );
            Console.WriteLine(json);
            return 0;
        }
        
        if (args.Length > 2)
        {
            if (args[1] != "out")
            {
                return Usage();
            }
            Console.WriteLine($"Writing json {args[2]}...");
            var json = JsonSerializer.Serialize(
                asmScan,
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
            File.WriteAllText(Path.GetFullPath(args[2]), json);
            return 0;
        }
        else
        {
            Console.WriteLine($"Loading json {args[1]}...");
            var jsonFile = Path.GetFullPath(args[1]);
            if (!File.Exists(jsonFile))
            {
                Console.WriteLine("File not found.");
                return 6;
            }
            
            var json     = File.ReadAllText(jsonFile);
            var jsonScan = JsonSerializer.Deserialize<ScannedAssembly>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );

            if (jsonScan is null)
            {
                Console.WriteLine("No scan read from JSON.");
                return 5;
            }

            var negCount = 0;
            var minCount = 0;
            var majCount = 0;

            var bg = Console.BackgroundColor;
            var fg = Console.ForegroundColor;

            foreach (var chg in asmScan.GetChangesFrom(jsonScan))
            {
                switch (chg.Severity)
                {
                    case ScanChangeSeverity.Minor:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.BackgroundColor = ConsoleColor.Black;
                        minCount++;
                        break;
                    case ScanChangeSeverity.Major:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.BackgroundColor = ConsoleColor.Black;
                        majCount++;
                        break;
                    case ScanChangeSeverity.Negligible:
                        Console.ForegroundColor = fg;
                        Console.BackgroundColor = bg;
                        negCount++;
                        break;
                    default:
                        Console.ForegroundColor = fg;
                        Console.BackgroundColor = bg;
                        break;
                }

                Console.Write(chg.Severity.ToString().ToUpper());

                Console.ForegroundColor = fg;
                Console.BackgroundColor = bg;

                Console.Write(" ");
                Console.WriteLine(chg.Description);
            }

            // version changes already made.
            var md = asmScan.Version.Major    - jsonScan.Version.Major;
            var nd = asmScan.Version.Minor    - jsonScan.Version.Minor;
            var rd = asmScan.Version.Revision - jsonScan.Version.Revision;
            var bd = asmScan.Version.Build    - jsonScan.Version.Build;

            if (majCount > 0)
            {
                if (md > 0)
                {
                    Console.WriteLine("The assembly has had major changes, and the major version has increased.");
                    Console.WriteLine("No further version changes required.");
                    return 0;
                }

                Console.WriteLine("The assembly interface has had major changes, recommend incrementing the major version.");
                return 4;
            }
            
            if (minCount > 0)
            {
                 
                if (md > 0 || (md == 0 && nd > 0))
                {
                    var vc = md > 0 ? "major" : "minor";
                    Console.WriteLine($"The assembly has had minor changes, and the {vc} version has increased.");
                    Console.WriteLine("No further version changes required.");
                    return 0;
                }

                Console.WriteLine("The assembly interface has had minor changes, recommend incrementing the minor version.");
                return 3;
            }

            if (negCount > 0)
            {
                if (md > 0 || (md == 0 && nd > 0) || (md == 0 && nd == 0 && rd > 0))
                {
                    var vc = md > 0 ? "major" : nd > 0 ? "minor" : "revision";
                    Console.WriteLine($"The assembly has had negligible changes, and the {vc} version has increased.");
                    Console.WriteLine("No further version changes recommended.");
                    return 0;
                }

                Console.WriteLine("The assembly interface has had negligible changes, recommend incrementing the revision.");
                return 2;
            }

            if (asmScan.Hash.Equals(jsonScan.Hash))
            {
                Console.WriteLine("The assembly has not changed.  No version changes are recommended.");
                return 0;
            }

            Console.WriteLine("The assembly interface has not changed, but it has had content or code changes.");

            if (md > 0 || (md == 0 && nd > 0) || (md == 0 && nd == 0 && rd > 0) ||
                (md           == 0 && nd == 0             && rd == 0 && bd > 0))
            {
                var vc = md > 0 ? "major" : nd > 0 ? "minor" : rd > 0 ? "revision" : "build";
                Console.WriteLine($"The {vc} version has increased.");
                Console.WriteLine("No further version changes recommended.");
                return 0;
            }

            Console.WriteLine("Recommend updating the build version.");
            return 1;
        }
    }

}
