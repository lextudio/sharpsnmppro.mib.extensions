# MIB Custom File Generator

`MibCustomFileGenerator` is a small command-line utility for creating the customizable `Module.g.cs` file that the source generator normally suppresses after a module is listed in `.customized`.

This is useful when you want to start customizing a module for the first time and need the initial scaffold copied into `Customized/`.

## Why this exists

The source generator only emits `Module.g.cs` while the module is not listed in `.customized`.

That creates a bootstrap problem:

1. you need the generated `Module.g.cs` as a starting point
2. but once the module is added to `.customized`, the generator intentionally stops emitting it

This utility handles the bootstrap explicitly:

1. generate `Module.g.cs`
2. review or edit it
3. add the module name to `.customized`

## Location

Project file:

- [MibCustomFileGenerator.csproj](./MibCustomFileGenerator.csproj)

Entry point:

- [Program.cs](./Program.cs)

## Usage

From the repo root:

```bash
dotnet run --project sharpsnmplib-samples/extensions/MibCustomFileGenerator/MibCustomFileGenerator.csproj -- \
  --module IF-MIB \
  --mibs sharpsnmplib-samples/Samples/snmpd/Mibs
```

That default invocation writes:

```text
sharpsnmplib-samples/Samples/snmpd/Customized/IF-MIB.g.cs
```

because the tool assumes the target project layout is:

```text
<project>/
  Mibs/
  Customized/
```

## Options

- `--module <MODULE-NAME>`: required module name, such as `IF-MIB`
- `--mibs <FOLDER>`: required folder containing all `.txt` MIB files needed to assemble the module tree
- `--output <FILE>`: optional explicit destination path
- `--version-text <TEXT>`: optional text for the generated file header
- `--include-file-name true|false`: include the original MIB file name in the generated header, default `true`
- `--allow-pending true|false`: allow unresolved imports while assembling, default `true`

## Recommended workflow

1. Make sure the target module is **not** yet listed in `.customized`.
2. Run the generator.
3. Confirm the new `Customized/ModuleName.g.cs` file looks correct.
4. Add the module name to `.customized`.
5. Build the target sample or project.

For the `snmpd` sample, that means:

1. generate `Customized/IF-MIB.g.cs`
2. add `IF-MIB` to [`Samples/snmpd/Customized/.customized`](../../Samples/snmpd/Customized/.customized)
3. build [`Samples/snmpd/snmpd.csproj`](../../Samples/snmpd/snmpd.csproj)

## Notes

- The tool compiles all `*.txt` MIB files in the provided `Mibs` folder so imports can resolve against sibling documents.
- It reuses the shared code emission helper in [`MibCodeGenerationUtility.cs`](../SharpSnmpPro.Mib.Extensions/MibCodeGenerationUtility.cs), so the emitted file matches the generator’s customizable-file format.
