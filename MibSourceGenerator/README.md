# MIB Source Generator

This source generator automatically compiles MIB documents to C# code during the build process. It reads `.mibs` files in your project and generates the corresponding C# classes for use in SNMP agents.

## How it Works

1. The source generator scans your project for `.mibs` files, which contain lists of MIB documents to compile.
2. For each MIB document, it uses the SharpSnmpPro.Mib library to parse and compile the MIB.
3. The generator creates two files for each MIB module:
   - `ModuleName.Generated.g.cs` - Contains auto-generated code with OID definitions and metadata
   - `ModuleName.g.cs` - Contains customizable partial classes
4. The generated code is included in the compilation process, so you can immediately use the generated classes in your SNMP agent.

## How to Use

### Step 1: Add the Source Generator to Your Project

Add a reference to the MibSourceGenerator project in your .csproj file:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\..\MibSourceGenerator\MibSourceGenerator.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### Step 2: Create Supporting Files

Create a `Mibs` folder in your project that contains all the needed MIB documents and a `.mibs` file in it that lists the MIB documents you want to compile to C#:

```
// Comments start with //
IP-MIB.txt
OTHER-MIB.txt
```

You only need to specify the document names, not file paths.

### Step 3: Include Supporting Files as AdditionalFiles

Ensure the following files are included as AdditionalFiles in your project:

```xml
<ItemGroup>
  <AdditionalFiles Include="Mibs\.mibs" />
  <AdditionalFiles Include="Mibs\.customized" />
  <AdditionalFiles Include="Mibs\*.txt" />
</ItemGroup>
```

## Generated Code

The source generator produces two types of files:

1. `*.Generated.g.cs` - Contains the auto-generated code with OID definitions, metadata, and class structures.
2. `*.g.cs` - Contains partial class implementations that you can customize.

The auto-generated files should not be modified as they will be overwritten during each build. Instead, customize the behavior of your MIB objects by editing your own implementations of the partial classes.

## How to Handle MIB Dependencies

Many MIB files import definitions from other MIB modules. The source generator automatically attempts to handle these dependencies when compiling MIB files.

### Automatic Dependency Resolution

The source generator automatically tries to resolve dependencies by looking for imported modules in:

1. The modules already loaded from your `.mibs` files
2. Any additional modules in the same directory as the MIB files being processed

### Manual Dependency Handling

If you encounter unresolved imports, you can place all related MIB files in the same directory as your primary MIB files.

### Troubleshooting MIB Dependencies

If you see warnings about unresolved imports, check the build output for detailed messages:

```
WARN001: MibGenerator Warning: Module(s) from C:\path\to\IP-MIB.txt has 3 unresolved imports
WARN001: MibGenerator Warning: Unresolved import: SNMPv2-SMI (referenced from IP-MIB)
```

To fix these warnings, ensure all the referenced MIB modules are included in your project.

## License

This source generator is part of the SharpSnmpPro.Mib.

The source generator creates two types of files:

1. `ModuleName.Generated.g.cs` - Generated code containing class definitions (do not modify)
2. `ModuleName.g.cs` - Customizable partial classes for your implementations

Once you copy a customizable partial class file to your project (such as `IP-MIB.g.cs`), you can suppress its recreation by the source generator by adding this file name to `.customized` file,

```
// Comments start with //
IP-MIB
```

Example of implementing a partial class:

```csharp
// This file can be modified with your custom implementation
using Lextm.SharpSnmpLib;
using Samples.Pipeline;

namespace IP_MIB
{
    partial class ipForwarding
    {
        private ISnmpData _data = new Integer32(1); // Default to forwarding enabled

        void OnCreate()
        {
            // Your initialization logic here
            // For example, get the actual forwarding state from the system
        }
    }
}
```

### Step 5: Use the Generated Classes

Use the generated classes in your SNMP agent:

```csharp
using IP_MIB;
using Samples.Pipeline;

// In your SNMP agent setup
var store = new ObjectStore();
store.Add(new ipForwarding());
store.Add(new ipDefaultTTL());
// Add more MIB objects as needed
```

## Benefits

- **Seamless Integration**: MIB compilation is integrated into your build process without manual steps.
- **Keep MIB Documents in Your Project**: MIB files stay in your project folder, improving project organization.
- **Automatic Updates**: Classes are always in sync with your MIB documents.
- **Separation of Generated and Custom Code**: Clear separation between generated code and your custom implementations.
- **Diagnostics Integration**: Compilation errors and warnings appear in your IDE's error list.

## Requirements

- .NET SDK 8.0 or higher
- SharpSnmpLib (available as a NuGet package)
- SharpSnmpPro.Mib (referenced as a project reference)
- SharpSnmpPro.Mib.Extensions (referenced as a project reference)

## Example Project Setup

Here's a complete example of configuring your SNMP agent project to use the source generator:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Lextm.SharpSnmpLib" Version="12.5.6" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Samples.Engine\Samples.Engine.csproj" />
    <ProjectReference Include="..\..\..\..\MibSourceGenerator\MibSourceGenerator.csproj" 
                      OutputItemType="Analyzer" 
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="Mibs\.mibs" />
    <AdditionalFiles Include="Mibs\.customized" />
    <AdditionalFiles Include="Mibs\*.txt" />
  </ItemGroup>

</Project>
```
