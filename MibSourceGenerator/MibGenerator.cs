using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class MibGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Debugger.Launch();
            // 1. Get all .txt files from AdditionalFiles
            var txtFiles = context.AdditionalTextsProvider
                .Where(f => f.Path != null && f.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .Collect();

            // 2. Get all .mibs files and parse them into a set of .txt file paths to generate
            var mibsTxtSet = context.AdditionalTextsProvider
                .Where(f => f.Path != null && f.Path.EndsWith(".mibs", StringComparison.OrdinalIgnoreCase))
                .Collect()
                .Select((mibsFiles, ct) => {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var mibsFile in mibsFiles)
                    {
                        var content = mibsFile.GetText(ct)?.ToString() ?? string.Empty;
                        var folder = Path.GetDirectoryName(mibsFile.Path);
                        foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//")) continue;
                            set.Add(Path.Combine(folder, line.Trim()));
                        }
                    }
                    return set;
                });

            // 3. Get all .customized files and parse them into a set of module names to suppress customizable file generation
            var customizedModules = context.AdditionalTextsProvider
                .Where(f => f.Path != null && f.Path.EndsWith(".customized", StringComparison.OrdinalIgnoreCase))
                .Collect()
                .Select((customizedFiles, ct) => {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var customizedFile in customizedFiles)
                    {
                        var content = customizedFile.GetText(ct)?.ToString() ?? string.Empty;
                        foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//")) continue;
                            set.Add(line.Trim());
                        }
                    }
                    return set;
                });

            // 4. Combine all three sources
            var allInputs = txtFiles.Combine(mibsTxtSet).Combine(customizedModules);

            context.RegisterSourceOutput(allInputs, (spc, tuple) =>
            {
                var txtFilesList = tuple.Left.Left;
                var mibsTxtSet = tuple.Left.Right;
                var customizedModules = tuple.Right;

                // 2. Use Parser2 and Assembler to compile all .txt files to memory as loaded modules
                var registry = new ErrorRegistry();
                registry.ErrorAdded += (sender, args) => { };
                registry.WarningAdded += (sender, args) => { };
                var allModules = new List<Module>();
                foreach (var txtFile in txtFilesList)
                {
                    try
                    {
                        var modules = Parser2.Compile(txtFile.Path, registry);
                        allModules.AddRange(modules);
                    }
                    catch { }
                }
                if (!allModules.Any())
                    return;
                var assembler = new Assembler("");
                assembler.Assemble(allModules, registry);

                // 3. Find all .txt files specified in .mibs, as they should be generated to C#
                foreach (var module in assembler.Tree.LoadedModules)
                {
                    if (module.Objects.Count == 0)
                        continue;
                    if (!mibsTxtSet.Contains(module.FileName))
                        continue;

                    var generatedCode = GenerateModuleCode(module);
                    spc.AddSource($"{module.Name}.Generated.g.cs", SourceText.From(generatedCode, Encoding.UTF8));

                    // 4. Learn which modules from .customized shouldn't be generated with the customizable file
                    if (!customizedModules.Contains(module.Name))
                    {
                        var customCode = GenerateCustomModuleCode(module);
                        spc.AddSource($"{module.Name}.g.cs", SourceText.From(customCode, Encoding.UTF8));
                    }
                }
            });
        }


        // No longer needed in incremental generator


        // No longer needed in incremental generator


        // No longer needed in incremental generator


        // No longer needed in incremental generator
        
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
