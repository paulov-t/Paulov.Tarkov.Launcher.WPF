# Paulov's Escape From Tarkov Deobfuscator

# Credits
[De4Dot](https://github.com/de4dot/de4dot)
[Mono-Cecil](https://www.mono-project.com/docs/tools+libraries/libraries/Mono.Cecil/)
[SPT-Aki](https://dev.sp-tarkov.com/SPT-AKI/SPT-AssemblyTool)

# What does this do?
This library allows you to deobfuscate any Escape from Tarkov Assembly-CSharp library using multiple options available to you.

# How to use?
- Add this Nuget package to your project     
- Add the following json file to your project in DeObfus/mappings/... and ensure it copies on compile
- Call PaulovDeobfuscator.Deobfuscate with the Assembly-CSharp.dll or EXE location

```
{
  // None/Remap/Test the Automatic System of Remapping via Code
  "AutomaticRemapping": "None",
  // None/Remap/Test the Defined Mapping list of Remapping - The defined list runs after the Automatic Remap
  "EnableDefinedRemapping": "None",
  "DefinedRemapping": [
  ],
  "DefinedTypesToForcePublic": [
  ],
  "TypesToForceAllPublicMethods": [
  ],
  "TypesToForceAllPublicFieldsAndProperties": [
  ],
  "AutoRemapTypeExcemptions": [

  ],
  "AutoRemapDescriptors": false
}
```

- Configure the json mappings file (see Configuring the Mappings Json File)

- Add this line of code to deobfuscate a library ``new PaulovDeobfuscator().Deobfuscate(assemblyLocation, createBackup: false, overwriteExisting: false, doRemapping: true);``

# Configuring the Mappings Json File

## AutomaticRemapping Options

### None
- Do nothing

### Test
- Run the discovery of mappings but only generate a .cs file (this can be used as a GlobalUsings.cs in your project)

### Remap
- Remap types based on discovery on names



