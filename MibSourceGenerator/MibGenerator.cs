using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Lextm.SharpSnmpPro.Mib;
using Lextm.SharpSnmpPro.Mib.Registry;
using Lextm.SharpSnmpPro.Mib.Validation;
using Lextm.SharpSnmpPro.Mib.Extensions;

namespace MibSourceGenerator
{
    [Generator]
    public class MibGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // No configuration attributes needed; nothing to register.
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                // Add this line for source generator debugging
                #if DEBUG
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    LogMessage(context, "Attempting to launch debugger for source generator");
                    //System.Diagnostics.Debugger.Launch();
                }
                #endif

                LogMessage(context, $"Source Generator executing in process {System.Diagnostics.Process.GetCurrentProcess().Id}");

                // Only use AdditionalFiles for configuration; no attributes or config files.

                // Load the list of customized modules (which should only have Generated.g.cs files)
                var customizedModules = LoadCustomizedModules(context);

                // Find all .txt files in AdditionalFiles (potential MIB documents)
                var mibTxtFiles = context.AdditionalFiles
                    .Where(f => Path.GetExtension(f.Path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!mibTxtFiles.Any())
                {
                    LogWarning(context, "No .txt MIB files were found in the project. MIB code generation skipped.");
                    return;
                }

                // Find .mibs files in AdditionalFiles
                var mibsFiles = FindMibsFiles(context);
                if (!mibsFiles.Any())
                {
                    LogWarning(context, "No .mibs files were found in the project. MIB code generation skipped.");
                    return;
                }

                // Build a set of all .txt file paths that are listed in any .mibs file
                var mibsTxtSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mibsFilePath in mibsFiles)
                {
                    var mibsAdditional = context.AdditionalFiles.FirstOrDefault(f => f.Path == mibsFilePath);
                    if (mibsAdditional == null)
                        continue;
                    var content = mibsAdditional.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
                    foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                            continue;
                        var mibPath = line.Trim();
                        // If the path is not absolute, make it relative to the .mibs file
                        if (!Path.IsPathRooted(mibPath))
                        {
                            mibPath = Path.Combine(Path.GetDirectoryName(mibsFilePath), mibPath);
                        }
                        mibsTxtSet.Add(mibPath);
                    }
                }

                // Compile all .txt files as MIB documents
                var registry = new ErrorRegistry();
                registry.ErrorAdded += (sender, args) => LogError(context, $"MIB Compiler: {args}");
                registry.WarningAdded += (sender, args) => LogWarning(context, $"MIB Compiler: {args}");

                var allModules = new List<Module>();
                foreach (var txtFile in mibTxtFiles)
                {
                    try
                    {
                        LogMessage(context, $"Compiling MIB document: {txtFile.Path}");
                        var modules = Parser2.Compile(txtFile.Path, registry);
                        allModules.AddRange(modules);
                    }
                    catch (Exception ex)
                    {
                        LogError(context, $"Error compiling MIB file {txtFile.Path}: {ex.Message}");
                    }
                }

                if (!allModules.Any())
                {
                    LogMessage(context, "No MIB modules found for compilation");
                    return;
                }

                // Only generate C# files for modules whose source .txt file is listed in any .mibs file
                var assembler = new Assembler("");
                assembler.Assemble(allModules, registry);

                var processedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var module in assembler.Tree.LoadedModules)
                {
                    // Only generate if the module's FileName is in mibsTxtSet
                    if (module.Objects.Count == 0 || processedModules.Contains(module.Name))
                        continue;
                    if (!mibsTxtSet.Contains(module.FileName))
                        continue;

                    processedModules.Add(module.Name);
                    LogMessage(context, $"Generating source for {module.Name}");

                    // Always generate the base implementation
                    var generatedCode = GenerateModuleCode(module);
                    context.AddSource($"{module.Name}.Generated.g.cs", SourceText.From(generatedCode, Encoding.UTF8));

                    // Only generate and add customizable implementation if this module is not in the customized list
                    if (!customizedModules.Contains(module.Name))
                    {
                        var customCode = GenerateCustomModuleCode(module);
                        TryCreatePhysicalCustomFile(context, module, customCode);
                        context.AddSource($"{module.Name}.g.cs", SourceText.From(customCode, Encoding.UTF8));
                    }
                    else
                    {
                        LogMessage(context, $"Skipping customizable code generation for {module.Name} (found in .customized file)");
                    }
                }

                LogMessage(context, "MIB compilation completed successfully");
            }
            catch (Exception ex)
            {
                LogError(context, $"An error occurred during MIB code generation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private List<string> FindMibsFiles(GeneratorExecutionContext context)
        {
            // Look for .mibs files in the AdditionalFiles
            return context.AdditionalFiles
                .Where(file => Path.GetExtension(file.Path).Equals(".mibs", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Path)
                .ToList();
        }

        private void ProcessMibsFile(GeneratorExecutionContext context, ErrorRegistry registry, List<Module> modules, string mibsFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(mibsFilePath))
                {
                    LogError(context, ".mibs file path is empty. Skipping.");
                    return;
                }
                LogMessage(context, $"Processing .mibs file: {mibsFilePath}");

                string mibsFileDirectory = string.Empty;
                try
                {
                    mibsFileDirectory = Path.GetDirectoryName(mibsFilePath);
                }
                catch (Exception ex)
                {
                    LogError(context, $"Failed to get directory name for .mibs file '{mibsFilePath}': {ex.Message}");
                    return;
                }

                string content = string.Empty;
                // Use AdditionalFiles mechanism for file access only (no direct file IO)
                var additionalFile = context.AdditionalFiles.FirstOrDefault(f => f.Path == mibsFilePath);
                if (additionalFile != null)
                {
                    content = additionalFile.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
                }
                else
                {
                    LogError(context, $".mibs file {mibsFilePath} not found in AdditionalFiles (file IO not allowed in analyzers)");
                    return;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    LogWarning(context, $".mibs file {mibsFilePath} is empty. Skipping.");
                    return;
                }

                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                LogMessage(context, $"Found {lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//"))} MIB files in .mibs file");

                // Directory listing removed for analyzer compliance.

                // Process each MIB file specified in the .mibs file
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                        continue; // Skip empty lines and comments

                    var mibFilePathForLog = line.Trim();
                    if (string.IsNullOrEmpty(mibFilePathForLog))
                    {
                        LogWarning(context, "Encountered empty MIB file path in .mibs file. Skipping.");
                        continue;
                    }
                    if (!Path.IsPathRooted(mibFilePathForLog) && !string.IsNullOrEmpty(mibsFileDirectory))
                    {
                        mibFilePathForLog = Path.Combine(mibsFileDirectory, mibFilePathForLog);
                    }

                    // File.Exists not allowed in analyzers; skip existence check.

                    try
                    {
                        LogMessage(context, $"Compiling MIB file: {mibFilePathForLog}");
                        var existingModuleNames = new HashSet<string>(modules.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
                        var compiledModules = Parser2.Compile(mibFilePathForLog, registry);
                        foreach (var compiledModule in compiledModules)
                        {
                            LogMessage(context, $"Module {compiledModule.Name} has {compiledModule.Imports.Count} imports:");
                            foreach (var import in compiledModule.Imports)
                            {
                                var resolvedStatus = import.Module == null ? "UNRESOLVED" : "resolved";
                                LogMessage(context, $"  - Import: {import.Name} from module: {import.Module} ({resolvedStatus})");
                            }
                        }
                        foreach (var compiledModule in compiledModules)
                        {
                            if (!existingModuleNames.Contains(compiledModule.Name))
                            {
                                modules.Add(compiledModule);
                                existingModuleNames.Add(compiledModule.Name);
                                LogMessage(context, $"Added module: {compiledModule.Name} from {mibFilePathForLog}");
                            }
                            else
                            {
                                LogMessage(context, $"Skipping duplicate module: {compiledModule.Name} from {mibFilePathForLog}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(context, $"Error compiling MIB file {mibFilePathForLog}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(context, $"Error processing .mibs file {mibsFilePath}: {ex.Message}");
            }
        }

        private void ProcessMibFile(GeneratorExecutionContext context, ErrorRegistry registry, List<Module> modules, string mibFilePath)
        {
            // File IO not allowed in analyzers; this method should not be used.
            LogError(context, "Direct file IO is not allowed in analyzers. Use AdditionalFiles only.");
        }

        private void ProcessMibsFile(GeneratorExecutionContext context, AdditionalText mibsFile)
        {
            // Read the content of the .mibs file
            var content = mibsFile.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }
            // Parse the .mibs file to get MIB file paths (no file IO, so just collect the lines)
            var mibFilePaths = new List<string>();
            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                {
                    continue; // Skip empty lines and comments
                }
                var mibPath = line.Trim();
                // If the path is not absolute, make it relative to the .mibs file
                if (!Path.IsPathRooted(mibPath))
                {
                    mibPath = Path.Combine(Path.GetDirectoryName(mibsFile.Path), mibPath);
                }
                // Do not check File.Exists; just add the path
                mibFilePaths.Add(mibPath);
            }
            if (!mibFilePaths.Any())
            {
                return;
            }
            // Compile MIB files and generate code
            var registry = new ErrorRegistry();
            registry.ErrorAdded += (sender, args) => ReportCompileError(context, args.ToString());
            registry.WarningAdded += (sender, args) => ReportCompileWarning(context, args.ToString());
            try
            {
                // Compile each MIB file
                var modules = new List<Module>();
                foreach (var mibPath in mibFilePaths)
                {
                    try
                    {
                        var compiledModules = Parser2.Compile(mibPath, registry);
                        modules.AddRange(compiledModules);
                    }
                    catch (Exception ex)
                    {
                        ReportCompileError(context, $"Failed to compile {mibPath}: {ex.Message}");
                    }
                }
                // Assemble the modules (no temp dir/file IO allowed, so pass null or empty string)
                var assembler = new Assembler("");
                assembler.Assemble(modules, registry);
                // Generate C# code for each module
                foreach (var module in modules)
                {
                    if (module.Objects.Count == 0)
                    {
                        continue;
                    }
                    string generatedCode = GenerateModuleCode(module);
                    string fileName = $"{module.Name}.Generated.cs";
                    // Add the generated code to the compilation
                    context.AddSource(fileName, SourceText.From(generatedCode, Encoding.UTF8));
                }
            }
            catch (Exception ex)
            {
                ReportCompileError(context, $"Unexpected error during MIB compilation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generates code for a Module in the format expected for generated files.
        /// Uses the shared MibCodeGenerationUtility class.
        /// </summary>
        /// <param name="module">The Module to generate code for.</param>
        /// <returns>A string containing the generated code.</returns>
        private string GenerateModuleCode(Module module)
        {
            return MibCodeGenerationUtility.GenerateModuleCode(module, $"MibSourceGenerator {GetType().Assembly.GetName().Version}", true);
        }

        /// <summary>
        /// Generates code for a Module in the format expected for customizable files.
        /// Uses the shared MibCodeGenerationUtility class.
        /// </summary>
        /// <param name="module">The Module to generate code for.</param>
        /// <returns>A string containing the generated code.</returns>
        private string GenerateCustomModuleCode(Module module)
        {
            return MibCodeGenerationUtility.GenerateModuleCode(module, $"MibSourceGenerator {GetType().Assembly.GetName().Version}", false);
        }
        private void LogMessage(GeneratorExecutionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "MSG001",
                    "MibGenerator Info",
                    message,
                    "MibGenerator",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None));
        }

        private bool TryCreatePhysicalCustomFile(GeneratorExecutionContext context, Module module, string customCode)
        {
            // File IO not allowed in analyzers/source generators. Always return false.
            return false;
        }

        private void LogWarning(GeneratorExecutionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "WARN001",
                    "MibGenerator Warning",
                    message,
                    "MibGenerator",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None));
        }

        private void LogError(GeneratorExecutionContext context, string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "ERR001",
                    "MibGenerator Error",
                    message,
                    "MibGenerator",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None));
        }

        private void ReportCompileError(GeneratorExecutionContext context, string message)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "MIBGEN004",
                title: "MIB compilation error",
                messageFormat: "{0}",
                category: "MibGenerator",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, message));
        }
        
        private void ReportCompileWarning(GeneratorExecutionContext context, string message)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "MIBGEN005",
                title: "MIB compilation warning",
                messageFormat: "{0}",
                category: "MibGenerator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, message));
        }

        private HashSet<string> LoadCustomizedModules(GeneratorExecutionContext context)
        {
            // Look for a .customized file in AdditionalFiles (if present)
            var customizedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var customizedFile = context.AdditionalFiles.FirstOrDefault(f => Path.GetExtension(f.Path).Equals(".customized", StringComparison.OrdinalIgnoreCase));
            if (customizedFile == null)
            {
                // No .customized file: treat as "no customizations"
                return customizedModules;
            }
            string content = null;
            try
            {
                content = customizedFile.GetText(context.CancellationToken)?.ToString();
            }
            catch (Exception ex)
            {
                LogError(context, $"Error reading .customized file: {ex.Message}");
            }
            if (string.IsNullOrEmpty(content))
            {
                // Empty .customized file: treat as "no customizations"
                return customizedModules;
            }
            try
            {
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                        continue;
                    var moduleName = line.Trim();
                    customizedModules.Add(moduleName);
                    LogMessage(context, $"Added module to customized list: {moduleName}");
                }
                LogMessage(context, $"Loaded {customizedModules.Count} customized module(s): {string.Join(", ", customizedModules)}");
            }
            catch (Exception ex)
            {
                LogError(context, $"Error parsing .customized file: {ex.Message}");
            }
            return customizedModules;
        }
    }
}
