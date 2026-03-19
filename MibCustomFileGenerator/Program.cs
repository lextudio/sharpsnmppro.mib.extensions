using System.Text;
using Lextm.SharpSnmpPro.Mib;
using Lextm.SharpSnmpPro.Mib.Extensions;
using Lextm.SharpSnmpPro.Mib.Registry;
using Lextm.SharpSnmpPro.Mib.Validation;

try
{
    if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
    {
        ShowUsage();
        return 0;
    }

    var options = ParseArguments(args);
    var moduleName = Require(options, "--module");
    var mibsFolder = Path.GetFullPath(Require(options, "--mibs"));
    var versionText = options.TryGetValue("--version-text", out var versionValue) && !string.IsNullOrWhiteSpace(versionValue)
        ? versionValue
        : "MibCustomFileGenerator";
    var includeFileName = !options.TryGetValue("--include-file-name", out var includeFileNameValue)
        || !string.Equals(includeFileNameValue, "false", StringComparison.OrdinalIgnoreCase);
    var allowPending = !options.TryGetValue("--allow-pending", out var allowPendingValue)
        || !string.Equals(allowPendingValue, "false", StringComparison.OrdinalIgnoreCase);

    if (!Directory.Exists(mibsFolder))
    {
        Console.Error.WriteLine($"MIB folder does not exist: {mibsFolder}");
        return 1;
    }

    var outputPath = options.TryGetValue("--output", out var outputValue) && !string.IsNullOrWhiteSpace(outputValue)
        ? Path.GetFullPath(outputValue)
        : GetDefaultOutputPath(mibsFolder, moduleName);

    var registry = new ErrorRegistry();
    var modules = Directory
        .EnumerateFiles(mibsFolder, "*.txt", SearchOption.TopDirectoryOnly)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .SelectMany(path => Parser2.Compile(path, registry))
        .ToList();

    if (modules.Count == 0)
    {
        Console.Error.WriteLine($"No loadable MIB modules found under: {mibsFolder}");
        return 1;
    }

    var assembler = new Assembler(string.Empty);
    assembler.Tree.PendingModulesAllowed = allowPending;
    assembler.Assemble(modules, registry);

    var module = assembler.Tree.LoadedModules.FirstOrDefault(item => string.Equals(item.Name, moduleName, StringComparison.OrdinalIgnoreCase));
    if (module is null)
    {
        Console.Error.WriteLine($"Module '{moduleName}' was not found.");
        Console.Error.WriteLine("Loaded modules:");
        foreach (var loaded in assembler.Tree.LoadedModules.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"  {loaded.Name}");
        }

        return 1;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    var code = MibCodeGenerationUtility.GenerateModuleCode(module, versionText, false, includeFileName);
    File.WriteAllText(outputPath, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    Console.WriteLine($"Generated {module.Name} -> {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static Dictionary<string, string?> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        var current = args[index];
        if (!current.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument: {current}");
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            result[current] = args[index + 1];
            index++;
            continue;
        }

        result[current] = "true";
    }

    return result;
}

static string Require(IReadOnlyDictionary<string, string?> options, string key)
{
    if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing required argument: {key}");
    }

    return value;
}

static string GetDefaultOutputPath(string mibsFolder, string moduleName)
{
    var projectFolder = Directory.GetParent(mibsFolder)?.FullName
        ?? throw new InvalidOperationException($"Cannot determine project folder from MIB folder: {mibsFolder}");
    return Path.Combine(projectFolder, "Customized", $"{moduleName}.g.cs");
}

static void ShowUsage()
{
    Console.WriteLine("MibCustomFileGenerator");
    Console.WriteLine("Generates a customizable Module.g.cs file from a MIB folder.");
    Console.WriteLine();
    Console.WriteLine("Required:");
    Console.WriteLine("  --module <MODULE-NAME>   Module to export, for example IF-MIB");
    Console.WriteLine("  --mibs <FOLDER>          Folder containing the .txt MIB files");
    Console.WriteLine();
    Console.WriteLine("Optional:");
    Console.WriteLine("  --output <FILE>          Destination .g.cs path");
    Console.WriteLine("  --version-text <TEXT>    Header text written into the generated file");
    Console.WriteLine("  --include-file-name      true|false, default true");
    Console.WriteLine("  --allow-pending          true|false, default true");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run --project sharpsnmplib-samples/extensions/MibCustomFileGenerator/MibCustomFileGenerator.csproj -- \\");
    Console.WriteLine("    --module IF-MIB \\");
    Console.WriteLine("    --mibs sharpsnmplib-samples/Samples/snmpd/Mibs");
}
