## Troubleshooting Guide

If you encounter issues with the MIB source generator, here are solutions to common problems:

### Build Issues

1. **Missing MIB Files**: Ensure your `.mibs` files point to valid MIB files in your project
   ```
   Error: MIB file not found: C:\path\to\NON-EXISTENT-MIB.txt
   ```
   Solution: Check file paths and ensure they exist in the referenced locations

2. **Unresolved Imports**: Common when MIB files reference symbols from other MIB modules
   ```
   Warning: Unresolved import: SNMPv2-SMI (referenced from IP-MIB)
   ```
   Solution: Add the required MIB files to your `.mibs` file or use the `MibFileAttribute`

3. **MIB Compilation Errors**: Syntax errors in the MIB files
   ```
   Error: Syntax error in MIB file: Unexpected token at line 42
   ```
   Solution: Fix the syntax in the original MIB file or use a corrected version

4. **Source Generator Not Running**: No generated code is created
   - Check that the MibSourceGenerator project is referenced correctly with `OutputItemType="Analyzer"` 
   - Ensure your `.mibs` files are included as `AdditionalFiles`
   - Check for build errors in the MibSourceGenerator project itself

### Performance Issues

For large MIB documents or projects with many MIB files:

1. **Long Build Times**: The source generator may take significant time for large MIB files
   - Consider generating code for only the MIB objects you need
   - Set up separate projects for different sets of MIB files
   - Enable MSBuild caching with the `EmitCompilerGeneratedFiles` feature

2. **Memory Usage**: Compiling complex MIB modules can require substantial memory
   - Increase the memory available to MSBuild with `/m:1 /nr:false` options
   - Split large MIB sets into separate projects or compilation steps

### Debugging Tips

If you need to debug issues with the source generator:

1. **Enable Compiler Generated Files Output**:
   ```xml
   <PropertyGroup>
     <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
     <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
   </PropertyGroup>
   ```

2. **Check Generated Code**: Examine the output files in the `Generated` folder to verify what was created

3. **Enable Detailed Build Logging**:
   ```
   dotnet build /v:diag > build.log
   ```
   Then search for "MibGenerator" in the log file

4. **Version Mismatches**: Ensure the SharpSnmpLib version matches the one expected by SharpSnmpPro.Mib
