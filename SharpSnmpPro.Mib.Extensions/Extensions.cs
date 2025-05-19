using System;
using System.IO;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpPro.Mib.Extensions;

namespace Lextm.SharpSnmpPro.Mib.Registry
{
    public static class ObjectTreeExtensions
    {
        public static void GenerateSourceFiles(this ObjectTree tree, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var project = Path.Combine(outputFolder, "Objects.csproj");
            if (!File.Exists(project))
            {
                File.WriteAllText(project, Properties.Resources.Objects);
            }

            foreach (var module in tree.LoadedModules)
            {
                if (module.Objects.Count == 0)
                {
                    continue;
                }

                module.Generate(outputFolder);
            }
        }
    }

    public static class ModuleExtensions
    {
        public static void Generate(this Module module, string folder)
        {
            if (module.Objects.Count == 0)
            {
                return;
            }

            var fileName = Path.Combine(folder, module.Name + ".Generated.cs");
            var customName = Path.Combine(folder, module.Name + ".cs");
            using (var custom = File.Exists(customName) ? null : new StreamWriter(customName, false))
            using (var generated = new StreamWriter(fileName, false))
            {
                // Write generated file
                var generatedContent = MibCodeGenerationUtility.GenerateModuleCode(module,
                    $"#SNMP MIB Compiler Pro {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}",
                    true);
                generated.Write(generatedContent);

                // Write custom file if needed
                if (custom != null)
                {
                    var customContent = MibCodeGenerationUtility.GenerateModuleCode(module,
                        $"#SNMP MIB Compiler Pro {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}",
                        false);
                    custom.Write(customContent);
                }
            }
        }
    }

    public static class ObjectTypeMacroExtensions
    {
        /// <summary>
        /// Generates code for an ObjectTypeMacro. This method is deprecated, use MibCodeGenerationUtility instead.
        /// </summary>
        public static void Generate(this ObjectTypeMacro macro, StreamWriter generated, StreamWriter custom)
        {
            if (macro.Status == EntityStatus.Deprecated || macro.Status == EntityStatus.Obsolete)
            {
                return;
            }

            if (macro.Type == DefinitionType.Entry)
            {
                return;
            }

            // Create temporary StringBuilders to hold the generated code
            var sbGenerated = new System.Text.StringBuilder();
            var sbCustom = custom != null ? new System.Text.StringBuilder() : null;

            // Generate code using the utility
            MibCodeGenerationUtility.GenerateObjectCode(sbGenerated, macro);
            if (sbCustom != null)
            {
                MibCodeGenerationUtility.GenerateCustomObjectCode(sbCustom, macro);
            }

            // Write to the provided StreamWriters
            generated.Write(sbGenerated.ToString());
            if (custom != null)
            {
                custom.Write(sbCustom.ToString());
            } 
        }

        /// <summary>
        /// Gets the default value for a type. This method is deprecated, use MibCodeGenerationUtility.Default instead.
        /// </summary>
        internal static string Default(ISmiType type)
        {
            return MibCodeGenerationUtility.Default(type);
        }

        /// <summary>
        /// Converts an Access enum to a string representation. This method is deprecated, use MibCodeGenerationUtility.AccessToString instead.
        /// </summary>
        private static string ToString(Access access)
        {
            return MibCodeGenerationUtility.AccessToString(access);
        }
    }
}
