using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Newtonsoft.Json;
using Paulov.Tarkov.Deobfuscator.Lib;
using Paulov.Tarkov.Deobfuscator.Lib.DeObfus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

/**
 * Original code for this was written by Bepis here - https://dev.sp-tarkov.com/bepis/SPT-AssemblyTool/src/branch/master/SPT-AssemblyTool/Deobfuscator.cs
 */
namespace Tarkov.Deobfuscator
{
    public class PaulovDeobfuscator
    {
        HashSet<string> TypesToNotRemap { get; } = new HashSet<string>
            {
                "module", "data", "object", "entities", "value",
                "body", "result", "parent", "area",
                "place", "info", "shot", "request",
                "source", "writer", "graph", "currequest",
                "controller", "counter", "closest", "newobject",
                "setting", "dictionary", "instance", "settings",
                "variation", "operation", "template", "emitter"
            };


        public HashSet<string> UsedTypesByOtherDlls { get; } = new HashSet<string>();

        public string RemappedClassCurrentFile { get; set; }

        public Dictionary<string, Dictionary<string, string>> RemappedClassForCSFile { get; set; } = new();

        public Dictionary<string, List<AutoRemapperInfo>> RemappedClassForGeneratedConfigFile { get; set; } = new();


        public StringBuilder LoggedStringBuilder { get; } = new StringBuilder();

        private ILogger Logger { get; set; }

        internal void Log(string text)
        {
            LoggedStringBuilder.AppendLine(text);

            if (Logger != null)
            {
                Logger.Log(text);
                return;
            }
        }

        internal void LogRemap(string text, AutoRemapperConfig.AutoRemapType remapType)
        {
            switch (remapType)
            {
                case AutoRemapperConfig.AutoRemapType.Remap:
                    Log($"Remapper: {text}");
                    break;
                case AutoRemapperConfig.AutoRemapType.Test:
                    Log($"Remapper: [TEST] {text}");
                    break;
            }
            return;
        }


        void AddRemappedClassForCSFile(string oldName, string newName, bool overwrite = false)
        {

#if DEBUG
            if (newName == "ProfileHealth")
            {

            }
#endif 
            if (string.IsNullOrEmpty(RemappedClassCurrentFile))
                throw new ArgumentNullException($"{nameof(RemappedClassCurrentFile)} has not been set. Please set the name before using it.");

            if (!RemappedClassForCSFile.ContainsKey(RemappedClassCurrentFile))
                RemappedClassForCSFile.Add(RemappedClassCurrentFile, new Dictionary<string, string>());

            var remappingFile = RemappedClassForCSFile[RemappedClassCurrentFile];

            var old = oldName.Replace("/", ".").Replace("&", ".");
            var n = newName.Replace("/", ".").Replace("&", ".").Replace(" ", ".").Replace("`", "");

            if (!remappingFile.ContainsKey(old) && !remappingFile.Values.Contains(n))
                remappingFile.Add(old, n);

            if (overwrite && remappingFile.ContainsKey(old))
                remappingFile[old] = n;

        }

        void AddRemappedClassForGeneratedConfigFile(TypeDefinition typeDefinition, string newName, bool overwrite = false)
        {
            AutoRemapperInfo autoRemapperInfo = new();

            autoRemapperInfo.RenameClassNameTo = newName;

            if (typeDefinition.IsClass)
                autoRemapperInfo.IsClass = true;

            List<string> fields = new();
            foreach (var f in typeDefinition.Fields)
            {
                if (!f.IsDefinition)
                    continue;

                if (f.IsStatic)
                    continue;

                if (f.Name.StartsWith("G", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (f.Name.StartsWith("class", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (f.Name.IndexOf("_") != -1)
                    continue;

                fields.Add(f.Name);
            }
            autoRemapperInfo.HasFields = fields.ToArray();

            List<string> methods = new();
            foreach (var m in typeDefinition.Methods)
            {
                if (!m.IsDefinition)
                    continue;

                if (m.IsStatic)
                    continue;

                if (m.Name.StartsWith("G", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (m.Name.IndexOf("_") != -1)
                    continue;

                methods.Add(m.Name);
            }
            autoRemapperInfo.HasMethods = methods.ToArray();

            // If it has no definition, ignore
            if (autoRemapperInfo.HasFields.Length == 0 && autoRemapperInfo.HasMethods.Length == 0)
                return;

            if (!RemappedClassForGeneratedConfigFile.ContainsKey(typeDefinition.Namespace))
                RemappedClassForGeneratedConfigFile.Add(typeDefinition.Namespace, new List<AutoRemapperInfo>());

            RemappedClassForGeneratedConfigFile[typeDefinition.Namespace].Add(autoRemapperInfo);
        }

        void GenerateCSharpFileOfRemappedClasses(string name)
        {
            if (!RemappedClassForCSFile.ContainsKey(name))
                return;

            var remapFile = RemappedClassForCSFile[name];
            StringBuilder sb = new();
            foreach (var item in remapFile)
            {
                sb.AppendLine($"global using {item.Value} = {item.Key};");
            }

            File.WriteAllText($"_GlobalUsings.{name}.cs", sb.ToString());

            var jsonSerializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            foreach (var item in RemappedClassForGeneratedConfigFile)
            {
                AutoRemapperConfig config = new();
                config.EnableDefinedRemapping = AutoRemapperConfig.AutoRemapType.Test;
                config.DefinedRemapping = item.Value.ToArray();
                File.WriteAllText($"_{name}.{item.Key}.generated.RemapperConfig.json"
                    , JsonConvert.SerializeObject(config, jsonSerializerSettings)
                    );
            }

        }

        public bool DeobfuscateAssembly(
            string assemblyPath
            , string managedPath
            , out HashSet<string> renamedClasses
            , bool createBackup = true
            , bool overwriteExisting = false
            , bool doRemapping = false
            , ILogger logger = null)
        {
            if (logger != null)
                Logger = logger;

            renamedClasses = new();

            var executablePath = AppContext.BaseDirectory;

            var cleanedDllPath = Path.Combine(Directory.GetParent(assemblyPath).FullName, Path.GetFileNameWithoutExtension(assemblyPath) + "-cleaned.dll");
            var de4dotPath = Path.Combine(Directory.GetParent(executablePath).FullName, "DeObfus", "de4dot", "de4dot.exe");

            // If backup file exists, delete .dll and replace with .backup
            if (File.Exists(assemblyPath + ".backup") || File.Exists(cleanedDllPath))
            {
                if (File.Exists(assemblyPath))
                    File.Delete(assemblyPath);

                if (File.Exists(cleanedDllPath))
                    File.Delete(cleanedDllPath);

                File.Move(assemblyPath + ".backup", assemblyPath);
            }

            if (createBackup)
                BackupExistingAssembly(assemblyPath);

            De4DotDeobfuscate(assemblyPath, managedPath, cleanedDllPath, de4dotPath);

            if (doRemapping)
                RemapClasses(managedPath, assemblyPath, out renamedClasses);
            // Do final backup
            //if (createBackup)
            //    BackupExistingAssembly(assemblyPath);
            //if (overwriteExisting)
            //    OverwriteExistingAssembly(assemblyPath, cleanedDllPath);


            Log($"DeObfuscation complete!");

            return true;
        }

        public bool Deobfuscate(string assemblyPath, out HashSet<string> renamedClasses, bool createBackup = true, bool overwriteExisting = false, bool doRemapping = false, ILogger logger = null)
        {
            if (logger != null)
                Logger = logger;

            renamedClasses = new();
            ExtractDe4Dot();
            ExtractRemapperConfig();

            var assemblyInfo = new FileInfo(assemblyPath);
            if (!assemblyInfo.Exists)
                return false;

            string managedPath = Directory.GetParent(assemblyInfo.FullName).FullName;

            if (assemblyInfo.Extension.Equals(".exe"))
            {
                // Get the Assembly Path by removing the .exe file in the path
                assemblyPath = assemblyPath
                    .Replace(".exe", "", StringComparison.InvariantCulture);
                // Add "_Data" to get the Data folder
                managedPath = Path.Combine((string)(assemblyPath + "_Data"), "Managed");
                // Find the Assembly-CSharp.dll
                assemblyPath = Path.Combine(managedPath, "Assembly-CSharp.dll");
            }

            return DeobfuscateAssembly(assemblyPath, managedPath, out renamedClasses, createBackup, overwriteExisting, doRemapping);
        }

        private void ExtractRemapperConfig()
        {
            Directory.CreateDirectory(Path.Combine("DeObfus", "mappings"));

            var assembly = Assembly.GetExecutingAssembly();

            var resources = assembly.GetManifestResourceNames().Where(x => x.Contains("mappings"));
            foreach (var resource in resources)
            {
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    byte[] ba = new byte[stream.Length];
                    stream.Read(ba, 0, ba.Length);
                    var extractPath = Path.Combine("DeObfus", "mappings", resource.Replace("Paulov.Tarkov.Deobfuscator.Lib.DeObfus.mappings.", ""));
                    File.WriteAllBytes(extractPath, ba);
                    ba = null;
                }

            }
        }

        private void ExtractDe4Dot()
        {
            Directory.CreateDirectory(Path.Combine("DeObfus", "de4dot"));

            var assembly = Assembly.GetExecutingAssembly();

            var resources = assembly.GetManifestResourceNames();
            foreach (var resource in resources)
            {
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    byte[] ba = new byte[stream.Length];
                    stream.Read(ba, 0, ba.Length);
                    var extractPath = Path.Combine("DeObfus", "de4dot", resource.Replace("Paulov.Tarkov.Deobfuscator.Lib.DeObfus.de4dot.", ""));
                    File.WriteAllBytes(extractPath, ba);
                    ba = null;
                }

            }


        }

        private void De4DotDeobfuscate(string assemblyPath, string managedPath, string cleanedDllPath, string de4dotPath)
        {
            if (File.Exists(cleanedDllPath))
            {
                Log($"Initial Deobfuscation Ignored. Cleaned DLL already exists.");
                return;
            }

            Log($"Initial Deobfuscation. Firing up de4dot.");

            string token;

            using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath))
            {
                var potentialStringDelegates = new List<MethodDefinition>();

                foreach (var type in assemblyDefinition.MainModule.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.ReturnType.FullName != "System.String"
                            || method.Parameters.Count != 1
                            || method.Parameters[0].ParameterType.FullName != "System.Int32"
                            || method.Body == null
                            || !method.IsStatic)
                        {
                            continue;
                        }

                        if (!method.Body.Instructions.Any(x =>
                            x.OpCode.Code == Code.Callvirt &&
                            ((MethodReference)x.Operand).FullName == "System.Object System.AppDomain::GetData(System.String)"))
                        {
                            continue;
                        }

                        potentialStringDelegates.Add(method);
                    }
                }

                if (potentialStringDelegates.Count != 1)
                {
                    //Program.WriteError($"Expected to find 1 potential string delegate method; found {potentialStringDelegates.Count}. Candidates: {string.Join("\r\n", potentialStringDelegates.Select(x => x.FullName))}");
                }

                var deobfRid = potentialStringDelegates[0].MetadataToken;

                token = $"0x{((uint)deobfRid.TokenType | deobfRid.RID):x4}";

                Debug.WriteLine($"Deobfuscation token: {token}");
                Log($"Deobfuscation token: {token}");
            }

            var fullAssemblyInfo = new FileInfo(assemblyPath);
            ProcessStartInfo psi = new();
            psi.FileName = de4dotPath;
            psi.UseShellExecute = true;
            psi.CreateNoWindow = true;
            psi.Arguments = $"--un-name \"!^<>[a-z0-9]$&!^<>[a-z0-9]__.*$&![A-Z][A-Z]\\$<>.*$&^[a-zA-Z_<{{$][a-zA-Z_0-9<>{{}}$.`-]*$\" \"{fullAssemblyInfo.FullName}\" --strtyp delegate --strtok \"{token}\"";

            Process proc = Process.Start(psi);
            while (!proc.WaitForExit(new TimeSpan(0, 0, 30)))
            {
            }
            if (proc != null && !proc.HasExited)
                proc.Kill(true);


            // Final Cleanup
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedPath);

            using (var memoryStream = new MemoryStream(File.ReadAllBytes(cleanedDllPath)))
            {
                using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(memoryStream
                    , new ReaderParameters()
                    {
                        AssemblyResolver = resolver
                    }))
                {
                    assemblyDefinition.Write(cleanedDllPath);
                }
            }

            File.Copy(cleanedDllPath, assemblyPath, true);

        }

        private void OverwriteExistingAssembly(string assemblyPath, string cleanedDllPath, bool deleteCleaned = true)
        {
            // Do final copy to Assembly
            File.Copy(cleanedDllPath, assemblyPath, true);
            //// Delete -cleaned
            //if(deleteCleaned)
            //    File.Delete(cleanedDllPath);
        }

        private void BackupExistingAssembly(string assemblyPath)
        {
            if (!File.Exists(assemblyPath + ".backup"))
                File.Copy(assemblyPath, assemblyPath + ".backup", false);
        }

        /// <summary>
        /// A remapping of classes. An idea inspired by Bepis SPT-AssemblyTool to rename known classes from GClass to proper names
        /// </summary>
        /// <param name="managedPath"></param>
        /// <param name="assemblyPath"></param>
        private void RemapClasses(string managedPath, string assemblyPath, out HashSet<string> renamedClasses)
        {
            renamedClasses = new HashSet<string>();
            RemappedClassForCSFile = new();
            RemappedClassForGeneratedConfigFile = new();

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedPath);
            var readerParameters = new ReaderParameters { AssemblyResolver = resolver, InMemory = false };

            UsedTypesByOtherDlls.Clear();

            var managedFiles = Directory.GetFiles(managedPath);
            foreach (var managedFile in managedFiles)
            {
                if (managedFile.Contains("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("DOTween", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("FilesChecker", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("TextMeshPro", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("UnityEngine.CoreModule", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("AnimationSystem.Types", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("bsg.console.core", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (managedFile.Contains("cinem", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using (var fsManagedFile = new FileStream(managedFile, FileMode.Open))
                    {
                        using (var managedFileAssembly = AssemblyDefinition.ReadAssembly(fsManagedFile, readerParameters))
                        {
                            if (managedFileAssembly != null)
                            {
                                foreach (var t in managedFileAssembly.MainModule.Types)
                                {
                                    UsedTypesByOtherDlls.Add(t.Name);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            using (var fsAssembly = new FileStream(assemblyPath, FileMode.Open))
            {
                using (var assemblyDefin = AssemblyDefinition.ReadAssembly(fsAssembly, readerParameters))
                {
                    if (assemblyDefin != null)
                    {
                        foreach (var fI in Directory.GetFiles(AppContext.BaseDirectory + "//DeObfus//mappings//", "*.json", SearchOption.AllDirectories).Select(x => new FileInfo(x)))
                        {
                            if (!fI.Exists)
                                continue;

                            if (fI.Extension != ".json")
                                continue;

                            Log($"-Deobfuscating-Run file--------------------------------------------------------------------------------------");
                            Log($"{fI.Name}");
                            RemappedClassCurrentFile = fI.Name.Replace(".json", "");

                            AutoRemapperConfig autoRemapperConfig = JsonConvert.DeserializeObject<AutoRemapperConfig>(File.ReadAllText(fI.FullName));

                            if (autoRemapperConfig.AutomaticRemapping.HasValue)
                            {
                                switch (autoRemapperConfig.AutomaticRemapping.Value)
                                {
                                    case AutoRemapperConfig.AutoRemapType.Remap:
                                    case AutoRemapperConfig.AutoRemapType.Test:
                                        // If the Remapper Config is set to use Auto Configuration. Run these two passes
                                        Log("Remapping by Auto Configuration: PASS 1");
                                        RemapByAutoConfiguration(assemblyDefin, autoRemapperConfig, ref renamedClasses, pass: 1);
                                        // A second pass finds unmapped GClass that use Interfaces that have been renamed
                                        Log("Remapping by Auto Configuration: PASS 2");
                                        RemapByAutoConfiguration(assemblyDefin, autoRemapperConfig, ref renamedClasses, pass: 2);
                                        // Remapping Descriptors
                                        RemapDescriptors(assemblyDefin, autoRemapperConfig, ref renamedClasses);
                                        break;
                                    case AutoRemapperConfig.AutoRemapType.Aki:
                                        var akiAutoResults = AkiAutoRemapper.CreateAutoMapping(assemblyDefin);
                                        foreach (var result in akiAutoResults)
                                        {
                                            var typeToRename = assemblyDefin.MainModule.GetType(result.Key);
                                            if (typeToRename != null)
                                                typeToRename.Name = result.Value.OrderBy(x => x.Value).First().Key;
                                            else
                                            {

                                            }
                                        }
                                        break;
                                }

                            }

                            // Run the defined mapping in the configuration file
                            Log("Remapping by Defined Configuration");
                            RemapByDefinedConfiguration(assemblyDefin, autoRemapperConfig, ref renamedClasses);

                            RemapByDefinedEnumConfigurations(assemblyDefin, autoRemapperConfig);

                            // Remap the all types to public dependant on Config. This has to run after defined because Aki requires it to fix certain types.
                            if (autoRemapperConfig.EnableForceAllTypesPublic == true)
                                Publicizer.PublicizeClasses(assemblyDefin);

                            // Remap the Public Types dependant on mapping
                            //RemapPublicTypesMethodsAndFields(oldAssembly, autoRemapperConfig);

                            // Push this to the end so people can force on the remapped names
                            //RemapAllMethodsToPublic(oldAssembly, autoRemapperConfig);

                            Log(Environment.NewLine);

                            GenerateCSharpFileOfRemappedClasses(RemappedClassCurrentFile);

                        }

                        Log(Environment.NewLine);

                        assemblyDefin.Write(assemblyPath.Replace(".dll", "-remapped.dll"));
                    }
                }
            }

            File.Copy(assemblyPath.Replace(".dll", "-remapped.dll"), assemblyPath, true);
            File.Delete(assemblyPath.Replace(".dll", "-remapped.dll"));

        }

        //private void RemapAllMethodsToPublic(AssemblyDefinition assemblyDefinition, AutoRemapperConfig autoRemapperConfig)
        //{
        //    // ------------------------------------------------
        //    // Auto publicize methods
        //    if (!autoRemapperConfig.EnableForceAllMethodsPublic.HasValue || !autoRemapperConfig.EnableForceAllMethodsPublic.Value)
        //        return;

        //    foreach (var t in assemblyDefinition.MainModule.GetTypes())
        //    {
        //        foreach(var m in t.GetMethods().Where(x=>x.DeclaringType == t))
        //        {
        //            m.IsPrivate = false;
        //            m.IsInternalCall = false;
        //            m.IsPublic = true;
        //        }
        //    }
        //}

        private void RemapDescriptors(AssemblyDefinition assemblyDefinition, AutoRemapperConfig autoRemapperConfig, ref HashSet<string> renamedClasses)
        {
            // ------------------------------------------------
            // Auto rename descriptors
            if (!autoRemapperConfig.AutoRemapDescriptors.HasValue || !autoRemapperConfig.AutoRemapDescriptors.Value)
            {
                Log($"Remapper: {nameof(autoRemapperConfig.AutoRemapDescriptors)}: {autoRemapperConfig.AutoRemapDescriptors}");
                return;
            }

            foreach (var t in assemblyDefinition.MainModule.GetTypes())
            {
                foreach (var m in t.Methods.Where(x => x.Name.StartsWith("ReadEFT")))
                {
                    var rT = assemblyDefinition.MainModule.GetTypes().FirstOrDefault(x => x == m.ReturnType);
                    if (rT != null)
                    {
                        var oldFullName = rT.FullName;
                        var oldTypeName = rT.Name;
                        var newName = m.Name.Replace("ReadEFT", "");

                        if (autoRemapperConfig.AutomaticRemapping.HasValue && autoRemapperConfig.AutomaticRemapping.Value == AutoRemapperConfig.AutoRemapType.Remap)
                            rT.Name = newName;

                        if (oldTypeName != newName && newName.EndsWith("Descriptor"))
                        {
                            renamedClasses.Add(newName);

                            AddRemappedClassForCSFile(oldFullName, newName, true);
                            AddRemappedClassForGeneratedConfigFile(rT, newName, true);
                            if (autoRemapperConfig.AutomaticRemapping.HasValue)
                                LogRemap($"Remapper: Auto Remapped {oldTypeName} to {newName}", autoRemapperConfig.AutomaticRemapping.Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Using the mapping section EnumAdditions, Add definited Enums
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="config"></param>
        private void RemapByDefinedEnumConfigurations(AssemblyDefinition assembly, AutoRemapperConfig config)
        {
            if (config.EnumAdditions == null || config.EnumAdditions.Count == 0)
                return;

            foreach (var item in config.EnumAdditions)
            {
                Log($"Attempting to add definition to {item.Type} with {item.EValue}");

                var enumDefinition = assembly.MainModule.GetType(item.Type);
                if (enumDefinition == null)
                {
                    Log($"Could not find enum of type {item.Type}");
                    continue;
                }

                if (enumDefinition.Fields.Any(x => x.Name == item.EValue))
                {
                    Log($"{item.EValue} already exists in {item.Type}");
                    continue;
                }

                if (item.IValue.HasValue && enumDefinition.Fields.Any(x => x.Constant != null && x.Constant.GetType() == typeof(int) && (int)x.Constant == item.IValue.Value))
                {
                    Log($"{item.IValue} already exists in {item.Type}");
                    continue;
                }

                var newDefinition = new FieldDefinition(item.EValue,
                    Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Literal | Mono.Cecil.FieldAttributes.HasDefault,
                    enumDefinition);

                if (item.IValue.HasValue)
                {
                    newDefinition.Constant = item.IValue.Value;
                }

                enumDefinition.Fields.Add(newDefinition);
                Log($"Remapper: Added {item.EValue} to {item.Type}");

            }
        }

        private void RemapPublicTypesMethodsAndFields(AssemblyDefinition assemblyDefinition, AutoRemapperConfig autoRemapperConfig)
        {
            if (autoRemapperConfig.DefinedTypesToForcePublic == null || autoRemapperConfig.DefinedTypesToForcePublic.Length == 0)
                return;

            Log($"{nameof(RemapPublicTypesMethodsAndFields)}");

            foreach (var ctf in autoRemapperConfig.DefinedTypesToForcePublic)
            {
                var foundTypes = assemblyDefinition.MainModule.GetTypes()
                  .Where(x => x.FullName.StartsWith(ctf) || x.FullName.EndsWith(ctf));

                foreach (var t in foundTypes.Where(x => x.IsClass))
                {
                    PublicizeType(t);
                }
            }

            foreach (var ctf in autoRemapperConfig.TypesToForceAllPublicMethods)
            {
                ForcePublicMethodsForType(assemblyDefinition, ctf);
            }

            foreach (var ctf in autoRemapperConfig.TypesToForceAllPublicFieldsAndProperties)
            {
                var foundTypes = assemblyDefinition.MainModule.GetTypes()
                    .Where(x => x.FullName.StartsWith(ctf, StringComparison.OrdinalIgnoreCase) || x.FullName.EndsWith(ctf));
                foreach (var t in foundTypes)
                {
                    foreach (var field in t.Fields)
                    {
                        if (!field.IsDefinition)
                            continue;

                        if (!field.IsPublic)
                            field.IsPublic = true;
                    }

                    foreach (var property in t.Properties)
                    {
                        if (property.GetMethod != null) PublicizeMethod(property.GetMethod);
                        if (property.SetMethod != null) PublicizeMethod(property.SetMethod);
                    }

                }
            }

            if (autoRemapperConfig.TypesToConvertConstructorsToPublic != null)
            {
                foreach (var ctf in autoRemapperConfig.TypesToConvertConstructorsToPublic)
                {
                    var foundTypes = assemblyDefinition.MainModule.GetTypes()
                        .Where(x => x.FullName.StartsWith(ctf, StringComparison.OrdinalIgnoreCase) || x.FullName.EndsWith(ctf));
                    foreach (var t in foundTypes)
                    {
                        foreach (var c in t.GetConstructors())
                        {
                            c.IsPublic = true;
                        }
                    }
                }
            }
        }

        private void ForcePublicMethodsForType(AssemblyDefinition assemblyDefinition, TypeDefinition t)
        {
            if (t.HasMethods && t.Methods.Count(x => !x.IsPublic) > 0)
            {
                Log($"{nameof(ForcePublicMethodsForType)}:{t.Name}:{t.Methods.Count(x => !x.IsPublic)}");
                foreach (var m in t.Methods)
                {
                    PublicizeMethod(m);
                }

            }

            foreach (var nt in t.NestedTypes)
            {
                ForcePublicMethodsForType(assemblyDefinition, nt);
            }

            //foreach (var otherT in assemblyDefinition.MainModule.GetTypes())
            //{
            //    if (otherT.BaseType != null && otherT.BaseType.FullName != "System.Object")
            //    {
            //        ForcePublicMethodsForType(assemblyDefinition, otherT);
            //    }
            //}
        }

        /// <summary>
        /// PublicizeMethod from SPT-Aki SPT-AssemblyTool
        /// https://dev.sp-tarkov.com/SPT-AKI/SPT-AssemblyTool/src/commit/46c6157555091e330477d23a856649010d7f4c76/SPT-AssemblyTool/Publicizer.cs
        /// All credits to SPT-Aki team
        /// </summary>
        /// <param name="method"></param>
        private void PublicizeMethod(MethodDefinition method)
        {
            if (method.IsCompilerControlled)
            {
                return;
            }

            if (method.IsPublic)
                return;

            // if (!CanPublicizeMethod(method)) return;

            // Workaround to not publicize a specific method so the game doesn't crash
            if (method.Name == "TryGetScreen") return;

            method.Attributes &= ~Mono.Cecil.MethodAttributes.MemberAccessMask;
            method.Attributes |= Mono.Cecil.MethodAttributes.Public;
        }

        /// <summary>
        /// PublicizeType from SPT-Aki SPT-AssemblyTool
        /// https://dev.sp-tarkov.com/SPT-AKI/SPT-AssemblyTool/src/commit/46c6157555091e330477d23a856649010d7f4c76/SPT-AssemblyTool/Publicizer.cs
        /// All credits to SPT-Aki team
        /// </summary>
        /// <param name="type"></param>
        private void PublicizeType(TypeDefinition type)
        {
            if (!type.IsNested && !type.IsPublic || type.IsNested && !type.IsNestedPublic)
            {
                type.Attributes &= ~Mono.Cecil.TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
                type.Attributes |= type.IsNested ? Mono.Cecil.TypeAttributes.NestedPublic : Mono.Cecil.TypeAttributes.Public; // Apply a public visibility attribute
            }

            if (type.IsSealed)
            {
                type.Attributes &= ~Mono.Cecil.TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
            }

            foreach (var method in type.Methods)
            {
                PublicizeMethod(method);
            }

            foreach (var property in type.Properties)
            {
                if (property.GetMethod != null) PublicizeMethod(property.GetMethod);
                if (property.SetMethod != null) PublicizeMethod(property.SetMethod);
            }

            var nestedTypesToPublicize = type.NestedTypes.ToArray();

            // Workaround to not publicize some nested types that cannot be patched easily and cause issues
            if (type.Interfaces.Any(i => i.InterfaceType.Name == "IHealthController"))
            {
                // Specifically, any type that implements the IHealthController interface needs to not publicize any nested types that implement the IEffect interface
                nestedTypesToPublicize = type.NestedTypes.Where(t => t.IsAbstract || t.Interfaces.All(i => i.InterfaceType.Name != "IEffect")).ToArray();
            }

            foreach (var nestedType in nestedTypesToPublicize)
            {
                PublicizeType(nestedType);
            }
        }

        private void ForcePublicMethodsForType(AssemblyDefinition assemblyDefinition, string ctf)
        {

            var foundTypes = assemblyDefinition.MainModule.GetTypes()
                                .Where(x => x.FullName.StartsWith(ctf, StringComparison.OrdinalIgnoreCase) || x.FullName.EndsWith(ctf));
            foreach (var t in foundTypes)
            {
                ForcePublicMethodsForType(assemblyDefinition, t);
            }
        }

        private void RemapAutoDiscoverAndCountType(TypeDefinition t, Dictionary<string, int> gclassToNameCounts, TypeDefinition[] allTypes)
        {
#if DEBUG
            if (t.Name == "GInterface147")
            {

            }
#endif
            // --------------------------------------------------------
            // Renaming by the classes being in methods
            RemapAutoDiscoverAndCountByMethodParameters(ref gclassToNameCounts, t);

            // --------------------------------------------------------
            // Renaming by the classes being used as Members/Properties/Fields in other classes
            RemapAutoDiscoverAndCountByProperties(ref gclassToNameCounts, t);

            RemapAutoDiscoverAndCountByNameMethod(ref gclassToNameCounts, t);

            RemapAutoDiscoverAndCountByReturnParameters(ref gclassToNameCounts, t, allTypes);

            if (t.HasNestedTypes)
            {
                foreach (var nestedType in t.NestedTypes)
                {
                    RemapAutoDiscoverAndCountType(nestedType, gclassToNameCounts, allTypes);
                }
            }
        }

        /// <summary>
        /// Attempts to remap all GClass/GInterface/GStruct to a readable name
        /// </summary>
        /// <param name="assemblyDefinition"></param>
        /// <param name="autoRemapperConfig"></param>
        private void RemapByAutoConfiguration(
            AssemblyDefinition assemblyDefinition
            , AutoRemapperConfig autoRemapperConfig
            , ref HashSet<string> renamedClasses
            , int pass = 1)
        {
            if (!autoRemapperConfig.AutomaticRemapping.HasValue || autoRemapperConfig.AutomaticRemapping.Value == AutoRemapperConfig.AutoRemapType.None)
                return;

            Log("Remapping by Auto Configuration");
            Stopwatch stopwatch = Stopwatch.StartNew();

            var allTypes =
                assemblyDefinition.MainModule.GetTypes()
                .Where(x
                =>
                !x.Name.Contains("MainModule")
                && !x.Name.Contains("<M")
                && !TypesToNotRemap.Contains(x.Name.ToLower())
                )
                .ToArray();

            var gclasses = assemblyDefinition.MainModule.GetTypes()
                .Where(x =>
                x.Name.StartsWith("GClass")
                || x.Name.StartsWith("GStruct")
                || x.Name.StartsWith("Class")
                || x.Name.StartsWith("GInterface"))
                .OrderBy(x => x.Name)
                .ToArray();

            var gclassToNameCounts = new Dictionary<string, int>();

            if (pass == 1)
            {
                Stopwatch swDiscoveryPass1 = new();
                swDiscoveryPass1.Start();
                foreach (var t in gclasses)
                {
                    RemapAutoDiscoverAndCountType(t, gclassToNameCounts, allTypes);
                }
                Log($"{nameof(RemapByAutoConfiguration)}:TimeTaken:Pass1:{swDiscoveryPass1.Elapsed}");
            }

            foreach (var t in gclasses)
            //foreach (var t in allTypes)
            {
                RemapAutoDiscoverAndCountByBaseType(ref gclassToNameCounts, t);
                RemapAutoDiscoverAndCountByInterfaces(ref gclassToNameCounts, t);
            }

            var autoRemappedClassCount = 0;

            // ----------------------------------------------------------------------------------------
            // Rename classes based on discovery above
            RenameClassesByScore(assemblyDefinition, ref gclassToNameCounts, ref renamedClasses, autoRemapperConfig, allTypes);
            // end of renaming based on discovery
            // ---------------------------------------------------------------------------------------

            // ------------------------------------------------
            // Auto rename FirearmController sub classes
            foreach (var t in assemblyDefinition.MainModule.GetTypes().Where(x
                =>
                    x.FullName == "EFT.Player/FirearmController"

                ))
            {
                var indexOfControllerNest = 0;
                foreach (var nc in t.NestedTypes.Where(x => x != null && x.Name.StartsWith("Class")))
                {
                    var oldClassName = nc.Name;
                    var desiredName = t.Name + "Sub" + indexOfControllerNest++;
                    renamedClasses.Add(desiredName);

                    if (autoRemapperConfig.AutomaticRemapping.Value == AutoRemapperConfig.AutoRemapType.Remap)
                    {
                        nc.Name = desiredName;
                        LogRemap($"Auto Remap {oldClassName} to {desiredName}", autoRemapperConfig.AutomaticRemapping.Value);
                    }
                    else
                        LogRemap($"Auto Remap {oldClassName} to {desiredName}", autoRemapperConfig.AutomaticRemapping.Value);

                    //nc.Name = nc.Name.Replace("Class", newStartOfName).Substring(0, newStartOfName.Length) + indexOfControllerNest.ToString();
                }
            }

            // ------------------------------------------------
            // Auto rename GrenadeController sub classes
            //foreach (var t in assemblyDefinition.MainModule.GetTypes().Where(x
            //    =>
            //        x.FullName == "EFT.Player/GrenadeController"
            //    ))
            //{
            //    var indexOfGrenadeControllerNest = 0;
            //    foreach (var nc in t.NestedTypes.Where(x => x != null))
            //    {
            //        indexOfGrenadeControllerNest++;
            //        nc.Name = nc.Name.Replace("Class", "GrenadeControllerSub").Substring(0, "GrenadeControllerSub".Length) + indexOfGrenadeControllerNest.ToString();
            //    }
            //}



            autoRemappedClassCount = renamedClasses.Count;
            LogRemap($"Auto Remap {autoRemappedClassCount} classes in {stopwatch.Elapsed}", autoRemapperConfig.AutomaticRemapping.Value);


            RemapBrokenArrayNames(assemblyDefinition);
        }

        private void RemapBrokenArrayNames(AssemblyDefinition assemblyDefinition)
        {
            var brokenArrayTypes = assemblyDefinition.MainModule.GetTypes().Where(x => x.Name.EndsWith("[]"));

            if (!brokenArrayTypes.Any())
                return;

            foreach (var t in brokenArrayTypes)
            {
                t.Name = t.Name.Replace("[]", string.Empty);
            }
        }

        private void RenameClassesByScore
            (
            AssemblyDefinition assemblyDefinition
            , ref Dictionary<string, int> gclassToNameCounts
            , ref HashSet<string> renamedClasses
            , AutoRemapperConfig autoRemapperConfig,
TypeDefinition[] allTypes)
        {
            var orderedGClassCounts = gclassToNameCounts
            .Where(x => x.Value > 0)
            .Where(x => !x.Key.Contains("_"))
            .Where(x => !x.Key.Contains("("))
            .Where(x => !x.Key.Contains(")"))
            .Where(x => !x.Key.Contains("<"))
            .Where(x => !x.Key.Contains(".Value", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Key.Contains(".Attribute", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Key.Contains(".Instance", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Key.Contains(".Default", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Key.Contains(".Current", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Value)
            //.ThenByDescending(x => x.Key.Length)
            .ToArray();

            var usedNamesCount = new Dictionary<string, int>();
            foreach (var g in orderedGClassCounts)
            {
                var keySplit = g.Key.Split('.');
                var className = keySplit[0];
                var classNameNew = keySplit[1];
                var value = g.Value;

                if (autoRemapperConfig.AutoRemapTypeExcemptions != null
                    && autoRemapperConfig.AutoRemapTypeExcemptions.Any(x => x.Equals(g.Key)))
                    continue;

                if (classNameNew.Length <= 3)
                    continue;

                if (classNameNew.StartsWith("Value", StringComparison.OrdinalIgnoreCase)
                    || classNameNew.StartsWith("Attribute", StringComparison.OrdinalIgnoreCase)
                    || classNameNew.StartsWith("Instance", StringComparison.OrdinalIgnoreCase)
                    || classNameNew.StartsWith("_", StringComparison.OrdinalIgnoreCase)
                    || classNameNew.StartsWith("<", StringComparison.OrdinalIgnoreCase)
                    //|| Assembly.GetAssembly(typeof(Attribute)).GetTypes().Any(x => x.Name.StartsWith(classNameNew, StringComparison.OrdinalIgnoreCase))
                    )
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {className} to {classNameNew}. {classNameNew} has an exception value!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                var t = allTypes.FirstOrDefault(x => x.Name == className);
                if (t == null)
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {className} to {classNameNew}. {className} could not be found!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                if (t.IsEnum)
                    continue;

                // Follow standard naming convention, PascalCase all class names
                var desiredName = char.ToUpper(classNameNew[0]) + classNameNew.Substring(1);
                // Following BSG naming convention, begin Abstract classes names with "Abstract"
                if (t.IsAbstract && !t.IsInterface && !desiredName.Contains("Abstract"))
                    desiredName = "Abstract" + desiredName;
                // Follow standard naming convention, Interface names begin with "I"
                else if (t.IsInterface)
                    desiredName = "I" + desiredName;

                if (string.IsNullOrEmpty(t.Namespace)
                    &&
                     (
                        assemblyDefinition.MainModule.GetTypes().Count(x => x.Name.Equals(desiredName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(x.Namespace)) > 0
                        ||
                        renamedClasses.Count(x => x.Equals(desiredName)) > 0
                     //|| 
                     //UsedTypesByOtherDlls.Count(x => x.Equals(ultimateGoalName)) > 0
                     )
                    )
                    desiredName = "G" + desiredName;

                // If the class is nested in another class
                // Use the Class Name (this will include namespace "." too)
                if (t.FullName.Contains("/"))
                {
                    t.Resolve();
                    if (t.DeclaringType.Properties.Any(x => x.Name.Equals(desiredName)) || t.DeclaringType.Fields.Any(x => x.Name.Equals(desiredName)))
                    {
                        desiredName = t.FullName.Split('/')[0] + desiredName;
                    }
                }

                // If the new name contains a "." (namespace), then remove the namespace from the new name
                if (desiredName.Contains("."))
                {
                    var indexOfLastDot = desiredName.LastIndexOf(".");
                    if (indexOfLastDot != -1)
                    {
                        desiredName = desiredName.Substring(indexOfLastDot + 1, desiredName.Length - indexOfLastDot - 1);
                    }
                }

                desiredName = desiredName.Replace(".", "");

                // Do a check. You cannot have two classes with the same name.
                var countOfExisting = 0;
                var loopName = desiredName;
                if (assemblyDefinition.MainModule.GetTypes().Any(x => x.Name.Equals(loopName)))
                {
                    while (assemblyDefinition.MainModule.GetTypes().Any(x => x.Name.Equals(loopName)))
                    {
                        countOfExisting++;
                        loopName = desiredName + countOfExisting.ToString();
                    }
                }
                if (renamedClasses.Any(x => x.Equals(loopName)))
                {
                    while (renamedClasses.Any(x => x.Equals(loopName)))
                    {
                        countOfExisting++;
                        loopName = desiredName + countOfExisting.ToString();
                    }
                }
                if (UsedTypesByOtherDlls.Any(x => x.Equals(loopName)))
                {
                    while (UsedTypesByOtherDlls.Any(x => x.Equals(loopName)))
                    {
                        countOfExisting++;
                        loopName = desiredName + countOfExisting.ToString();
                    }
                }
                desiredName = loopName;

                // Store the old name
                var oldFullName = t.FullName;
                var oldClassName = t.Name;

                desiredName = desiredName.Replace("`1", "");
                desiredName = desiredName.Replace("`2", "");
                desiredName = desiredName.Replace("`3", "");
                desiredName = desiredName.Replace("`4", "");

                if (string.IsNullOrEmpty(t.Namespace)
                    &&
                    Assembly.GetAssembly(typeof(Attribute))
                    .GetTypes()
                    .Any(x => x.Name.Contains(desiredName, StringComparison.OrdinalIgnoreCase))
                    )
                {
                    //    //t.Namespace = "EFT";
                    continue;
                }

                if (desiredName.Contains("MonoBehavior"))
                    continue;

                if (desiredName.Contains("Enum"))
                    continue;

                // The new class name has already been used, ignore
                if (renamedClasses.Any(x => x.Equals(desiredName)))
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {oldClassName} to {desiredName}. {desiredName} has already been used!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                if (assemblyDefinition.MainModule.GetTypes().Any(x => x.Name.Equals(desiredName, StringComparison.OrdinalIgnoreCase)))
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {oldClassName} to {desiredName}. {desiredName} has already been used by BSG or previous process!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                if (UsedTypesByOtherDlls.Contains(desiredName))
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {oldClassName} to {desiredName}. {desiredName} has already been used by another Dll!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                if (desiredName.Equals("Time"))
                {
                    LogRemap($"Remapper (ERROR): You cannot remap to \"Time\"", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }

                if (assemblyDefinition.MainModule.GetTypes().Any(x => x.Namespace.Equals(desiredName, StringComparison.OrdinalIgnoreCase)))
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {oldClassName} to {desiredName}. {desiredName} is a namespace!", autoRemapperConfig.AutomaticRemapping.Value);
                    continue;
                }


                if (renamedClasses.Add(desiredName))
                {
                    RemapType(autoRemapperConfig, 0, desiredName, t, false, true);

                    LogRemap($"Auto Remap {oldClassName} to {desiredName}", autoRemapperConfig.AutomaticRemapping.Value);
                }
                else
                {
                    LogRemap($"Remapper (ERROR): Unable to Auto Remap {oldClassName} to {desiredName}. {desiredName} has already been used!", autoRemapperConfig.AutomaticRemapping.Value);
                }

            }

        }

        private void RemapAutoDiscoverAndCountByProperties(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t)
        {

            PropertyDefinition[] propertyDefinitions = t.Properties.Where(p =>
                                p.PropertyType.Name == t.Name
                                && p.PropertyType.Name.Length > 4
                                ).ToArray();

            foreach (var prop in propertyDefinitions)
            {
                // if the property name includes "gclass" or whatever, then ignore it as its useless to us
                if (prop.Name.StartsWith("GClass", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.StartsWith("GStruct", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.StartsWith("GInterface", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.StartsWith("Class", StringComparison.OrdinalIgnoreCase)
                    )
                    continue;

                var n = prop.PropertyType.Name
                    .Replace("[]", "")
                    .Replace("`1", "")
                    .Replace("`2", "")
                    .Replace("`3", "")
                    .Replace("&", "")
                    .Replace(" ", "")
                    + "." + char.ToUpper(prop.Name[0]) + prop.Name.Substring(1)
                    ;
                if (!gclassToNameCounts.ContainsKey(n))
                    gclassToNameCounts.Add(n, 0);

                gclassToNameCounts[n] += 1;
            }

            RemapAutoDiscoverAndCountByFields(ref gclassToNameCounts, t);
        }

        private void RemapAutoDiscoverAndCountByFields(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t)
        {
            foreach (var prop in t.Fields.Where(p =>
                                p.FieldType.Name == t.Name
                                && p.FieldType.Name.Length > 4
                                // if the property name includes "gclass" or whatever, then ignore it as its useless to us
                                && !(p.Name.StartsWith("GClass", StringComparison.OrdinalIgnoreCase)
                                    || p.Name.StartsWith("GStruct", StringComparison.OrdinalIgnoreCase)
                                    || p.Name.StartsWith("GInterface", StringComparison.OrdinalIgnoreCase)
                                    || p.Name.StartsWith("Class", StringComparison.OrdinalIgnoreCase)
                                    )
                                ))
            {
                var n = prop.FieldType.Name
                    .Replace("[]", "")
                    .Replace("`1", "")
                    .Replace("`2", "")
                    .Replace("`3", "")
                    .Replace("&", "")
                    .Replace(" ", "")
                    + "." + char.ToUpper(prop.Name[0]) + prop.Name.Substring(1)
                    ;
                if (!gclassToNameCounts.ContainsKey(n))
                    gclassToNameCounts.Add(n, 0);

                gclassToNameCounts[n] += 1;
            }
        }

        private void RemapAutoDiscoverAndCountByNameMethod(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t)
        {
            try
            {
                var nameMethod = t.Methods.FirstOrDefault(x => x.Name == "Name");
                if (nameMethod == null)
                    return;

                if (nameMethod.HasBody)
                {
                    if (!nameMethod.Body.Instructions.Any())
                        return;

                    var instructionString = nameMethod.Body.Instructions[0];
                    if (instructionString == null)
                        return;

                    if (instructionString.OpCode.Name != "ldstr")
                        return;

                    if (string.IsNullOrEmpty(instructionString.Operand.ToString()))
                        return;

                    var matchingGClassName = t.Name + "." + instructionString.Operand.ToString().Replace(" ", "").Replace("&", "");
                    if (!gclassToNameCounts.ContainsKey(matchingGClassName))
                        gclassToNameCounts.Add(matchingGClassName, 0);

                    gclassToNameCounts[matchingGClassName]++;
                }
            }
            catch { }
        }

        private void RemapAutoDiscoverAndCountByMethodParameters(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition type)
        {
            if (!type.HasMethods)
                return;

            var methodsWithParams = type.Methods.Where(x => x.HasParameters);
            if (!methodsWithParams.Any())
                return;

            foreach (var method in methodsWithParams)
            {
                // Iterate over each methods params, avoid generics + primitives + arrays
                foreach (var parameter in method.Parameters.Where(x => !x.ParameterType.IsGenericParameter))
                {
                    var paramIsInterface = parameter.ParameterType.Name.Contains("GInterface");
                    var paramIsStruct = parameter.ParameterType.Name.Contains("GStruct");
                    var paramIsClass = parameter.ParameterType.Name.Contains("GClass");
                    if (!paramIsClass && !paramIsInterface && !paramIsStruct)
                        continue;

                    if (parameter.Name.Length <= 5)
                        continue;

                    if (TypesToNotRemap.Contains(parameter.Name.ToLower()))
                        continue;

                    if (parameter.ParameterType.Namespace.ToLower() == "unityengine")
                        continue;

                    var n =
                        // Key Value is Built like so. KEY.VALUE
                        parameter.ParameterType.Name
                        .Replace("[]", "")
                        .Replace("`1", "")
                        .Replace("`2", "")
                        .Replace("`3", "")
                        .Replace("&", "")
                        .Replace(" ", "")
                        + "."
                        + char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1)
                        ;
                    if (!gclassToNameCounts.ContainsKey(n))
                        gclassToNameCounts.Add(n, 0);

                    gclassToNameCounts[n]++;
                }

                // Iterate over each methods params generics only
                foreach (var parameter in method.Parameters.Where(x => x.ParameterType.ContainsGenericParameter))
                {
                    var paramIsInterface = parameter.ParameterType.Name.Contains("GInterface");
                    var paramIsStruct = parameter.ParameterType.Name.Contains("GStruct");
                    var paramIsClass = parameter.ParameterType.Name.Contains("GClass");
                    if (!paramIsClass && !paramIsInterface && !paramIsStruct)
                        continue;

                    if (parameter.ParameterType.GenericParameters.Count == 0)
                        continue;

                    if (parameter.ParameterType.GenericParameters.Count > 1)
                        continue;

                    var genParameter = parameter.ParameterType.GenericParameters[0];
                    var genericParameterName = genParameter.DeclaringType.Name;

                    if (parameter.Name.Length <= 5)
                        continue;

                    if (TypesToNotRemap.Contains(genericParameterName.ToLower()))
                        continue;

                    if (genericParameterName.ToLower() == "unityengine")
                        continue;

                    var n =
                        // Key Value is Built like so. KEY.VALUE
                        genericParameterName
                        .Replace("[]", "")
                        .Replace("`1", "")
                        .Replace("`2", "")
                        .Replace("`3", "")
                        .Replace("&", "")
                        .Replace(" ", "")
                        + "."
                        + char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1)
                        ;
                    if (!gclassToNameCounts.ContainsKey(n))
                        gclassToNameCounts.Add(n, 0);

                    gclassToNameCounts[n]++;
                }


            }
        }

        private void RemapAutoDiscoverAndCountByReturnParameters(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t, IEnumerable<TypeDefinition> otherTypes)
        {

            var otherTypesWithMethods = otherTypes
                .Where(x => x.IsClass && x.HasMethods && x.Methods.Any(m => m.HasParameters));
            foreach (var other in otherTypesWithMethods)
            {
                var validMethods = other.Methods
                    .Where(method => method.ReturnType.Equals(t));
                foreach (var method in validMethods)
                {
                    var n =
                    // Key Value is Built like so. KEY.VALUE
                    t.Name
                    .Replace("[]", "")
                    .Replace("`1", "")
                    .Replace("`2", "")
                    .Replace("`3", "")
                    .Replace("&", "")
                    .Replace(" ", "")
                    + "."
                    + char.ToUpper(method.Name[0]) + method.Name.Substring(1)
                    ;
                    if (!gclassToNameCounts.ContainsKey(n))
                        gclassToNameCounts.Add(n, 0);

                    gclassToNameCounts[n]++;
                }

            }


        }

        //        private void RemapAutoDiscoverAndCountByMethodBody(ref Dictionary<(string, TypeDefinition), int> gclassToNameCounts, TypeDefinition t, IEnumerable<TypeDefinition> otherTypes)
        //        {
        //            foreach (var other in otherTypes)
        //            {
        //                if (!other.HasMethods || other.Methods == null)
        //                    continue;

        //                foreach (var method in other.Methods)
        //                {
        //                    if (!method.HasBody)
        //                        continue;

        //                    if (!method.Parameters.Any())
        //                        continue;

        //                    var methodBody = method.Body;
        //                    if (methodBody == null)
        //                        continue;

        //#if DEBUG
        //                    //if (other.Name.Contains("BaseLocalGame", StringComparison.OrdinalIgnoreCase))
        //                    //{
        //                    //    if(method.Name.Contains("method_4", StringComparison.OrdinalIgnoreCase))
        //                    //    {
        //                    //        foreach (var instruction in methodBody.Instructions)
        //                    //        {
        //                    //            Debug.WriteLine($"{instruction.OpCode} \"{instruction.Operand}\"");
        //                    //        }
        //                    //    }
        //                    //}



        //#endif


        //                    foreach (var parameter in method.Parameters
        //                        .Where(x => x.ParameterType.Name.Replace("[]", "").Replace("`1", "") == t.Name)
        //                        .Where(x => x.ParameterType.Name.Length > 3)
        //                        )
        //                    {


        //                        var n =
        //                        // Key Value is Built like so. KEY.VALUE
        //                        parameter.ParameterType.Name
        //                        .Replace("[]", "")
        //                        .Replace("`1", "")
        //                        .Replace("`2", "")
        //                        .Replace("`3", "")
        //                        .Replace("&", "")
        //                        .Replace(" ", "")
        //                        + "."
        //                        + char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1)
        //                        ;
        //                        if (!gclassToNameCounts.ContainsKey((n, t)))
        //                            gclassToNameCounts.Add((n, t), 0);

        //                        gclassToNameCounts[(n, t)]++;
        //                    }
        //                }

        //            }
        //        }

        /// <summary>
        /// This will likely only work on a 2nd pass
        /// </summary>
        /// <param name="gclassToNameCounts"></param>
        /// <param name="t"></param>
        /// <param name="allTypes"></param>
        private void RemapAutoDiscoverAndCountByBaseType(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t)
        {

            if (t.BaseType == null)
                return;

            if (t.BaseType.Name.Contains("GClass")
                || t.BaseType.Name.Contains("GStruct")
                || t.BaseType.Name.Contains("GInterface")
                || t.BaseType.Name.Contains("Class")
                || t.BaseType.Name == "Object"
                )
                return;

            var n =
            // Key Value is Built like so. KEY.VALUE
            t.Name
            .Replace("[]", "")
            .Replace("`1", "")
            .Replace("`2", "")
            .Replace("`3", "")
            .Replace("&", "")
            .Replace(" ", "")
            + "."

            + char.ToUpper(t.BaseType.Name[0]) + t.BaseType.Name.Substring(1)

            // + (t.BaseType.Name.Contains("`1") ? "`1" : "") // cater for `1
            // + (t.BaseType.Name.Contains("`2") ? "`2" : "") // cater for `2
            ;
            if (!gclassToNameCounts.ContainsKey(n))
                gclassToNameCounts.Add(n, 0);

            gclassToNameCounts[n]++;
        }

        private void RemapAutoDiscoverAndCountByInterfaces(ref Dictionary<string, int> gclassToNameCounts, TypeDefinition t)
        {

            if (t.Interfaces == null)
                return;

            foreach (var interf in t.Interfaces.OrderByDescending(x => x.InterfaceType.Name.Length))
            {

                if (interf.InterfaceType.Name.Contains("GClass")
                    || interf.InterfaceType.Name.Contains("GStruct")
                    || interf.InterfaceType.Name.Contains("GInterface")
                    || interf.InterfaceType.Name.Contains("Class")
                    || interf.InterfaceType.Name.Contains("Disposable")
                    || interf.InterfaceType.Name.Contains("Enumerator")
                    || interf.InterfaceType.Name.Contains("Comparer")
                    || interf.InterfaceType.Name.Contains("Enumerable")
                    || interf.InterfaceType.Name.Contains("Interface")
                    || interf.InterfaceType.Name.Contains("Equatable")
                    || interf.InterfaceType.Name.Contains("Exchangeable")
                    )
                    continue;

                var n =
                // Key Value is Built like so. KEY.VALUE
                t.Name
                .Replace("[]", "")
                .Replace("`1", "")
                .Replace("`2", "")
                .Replace("`3", "")
                .Replace("&", "")
                .Replace(" ", "")
                + "."

                + char.ToUpper(interf.InterfaceType.Name[1]) + interf.InterfaceType.Name.Substring(2)
                ;

                if (!gclassToNameCounts.ContainsKey(n))
                    gclassToNameCounts.Add(n, 0);

                gclassToNameCounts[n]++;

            }
        }

        private void RemapByDefinedConfiguration(AssemblyDefinition assembly, AutoRemapperConfig autoRemapperConfig, ref HashSet<string> renamedClasses)
        {
            if (!autoRemapperConfig.EnableDefinedRemapping.HasValue || autoRemapperConfig.EnableDefinedRemapping.Value == AutoRemapperConfig.AutoRemapType.None)
                return;

            int countOfDefinedMappingSucceeded = 0;
            int countOfDefinedMappingFailed = 0;

            var remappableTypes
               = assembly
               .MainModule
               .GetTypes()
               .Where(x => !TypesToNotRemap.Select(y => y.ToLower()).Any(y => x.Name.ToLower().IndexOf(y) != -1))
               .Where(x => !x.Namespace.StartsWith("System"))
               .Where(x => !x.Name.Contains("d__"))
               .OrderBy(x => x.Name)
               .ToList();

            foreach (var config in autoRemapperConfig.DefinedRemapping.Where(x => !string.IsNullOrEmpty(x.RenameClassNameTo)))
            {

                try
                {
                    var foundTypes = DiscoverTypeByMapping(config, remappableTypes);

                    if (foundTypes.Any())
                    {
                        var onlyRemapFirstFoundType = config.OnlyRemapFirstFoundType.HasValue && config.OnlyRemapFirstFoundType.Value;
                        // Only remap first found type.
                        countOfDefinedMappingSucceeded = RemapType(autoRemapperConfig, countOfDefinedMappingSucceeded, config.RenameClassNameTo, foundTypes.FirstOrDefault(), true);

                        if (config.RemoveAbstract.HasValue && config.RemoveAbstract.Value)
                        {
                            foreach (var type in foundTypes)
                            {
                                if (type.IsAbstract)
                                {
                                    type.IsAbstract = false;
                                }
                            }
                        }


                    }
                    else
                    {
                        Log($"Remapper: Failed to remap {config.RenameClassNameTo}");
                        countOfDefinedMappingFailed++;

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            Log($"Defined Remapper: SUCCESS: {countOfDefinedMappingSucceeded}");
            Log($"Defined Remapper: FAILED: {countOfDefinedMappingFailed}");
        }

        private int RemapType(AutoRemapperConfig autoRemapperConfig, int countSuccess, string remapName, TypeDefinition t, bool forceRemap = false, bool renameAbstract = false)
        {
            if (
                !forceRemap
                &&
                (
                    !t.Name.StartsWith("GClass")
                    && !t.Name.StartsWith("Class")
                    && !t.Name.StartsWith("GInterface")
                    && !t.Name.StartsWith("GStruct")
                )
                )
                return countSuccess;

            if (remapName == "MonoBehavior")
                return countSuccess;

            var newClassName = remapName;

            var oldFullName = t.FullName;
            var oldClassName = t.Name;
            if (t.IsInterface && !newClassName.StartsWith("I"))
                newClassName = newClassName.Insert(0, "I");

            if (renameAbstract && t.IsClass
                && t.IsAbstract
                && !newClassName.Contains("Abstract")
                && !newClassName.StartsWith("A"))
                newClassName = newClassName.Insert(0, "Abstract");

            if (oldClassName.IndexOf("`1") != -1)
            {
                StringBuilder sbConstraints = new();
                sbConstraints.Append("<");
                foreach (var gp in t.GenericParameters)
                {
                    foreach (var c in gp.Constraints)
                    {
                        sbConstraints.Append(c.ConstraintType.FullName);
                    }
                }
                sbConstraints.Append(">");

                oldFullName = oldFullName.Replace("`1", sbConstraints.ToString() == "<>" ? "" : sbConstraints.ToString());
            }

            if (autoRemapperConfig.EnableDefinedRemapping.Value == AutoRemapperConfig.AutoRemapType.Remap)
            {

                t.Name = newClassName;
                //t.Namespace = "EFT";

                Log($"Remapper: Remapped {oldFullName} to {newClassName}");
                countSuccess++;
            }
            else
            {
                Log($"Remapper: [TEST] Remap {oldFullName} to {newClassName}");
            }

            AddRemappedClassForCSFile(oldFullName, newClassName, forceRemap);
            return countSuccess;
        }



        private List<TypeDefinition> DiscoverTypeByMapping(AutoRemapperInfo config, in List<TypeDefinition> findTypes)
        {

#if DEBUG
            if (config.RenameClassNameTo == "EnergyControllerClass")
            {

            }
#endif

            List<TypeDefinition> foundDefinition = findTypes.Where(
               x =>
                   (
                       !config.MustBeGClass.HasValue
                       || (config.MustBeGClass.Value && x.Name.StartsWith("GClass"))
                   )
               ).ToList();

            foundDefinition = foundDefinition.Where(
               x =>
                   (
                       string.IsNullOrEmpty(config.IsNestedInClass)
                       || (!string.IsNullOrEmpty(config.IsNestedInClass) && x.FullName.Contains(config.IsNestedInClass + "+", StringComparison.OrdinalIgnoreCase))
                       || (!string.IsNullOrEmpty(config.IsNestedInClass) && x.FullName.Contains(config.IsNestedInClass + ".", StringComparison.OrdinalIgnoreCase))
                       || (!string.IsNullOrEmpty(config.IsNestedInClass) && x.FullName.Contains(config.IsNestedInClass + "/", StringComparison.OrdinalIgnoreCase))
                   )
               ).ToList();


            // Filter Types by Inherits Class
            foundDefinition = foundDefinition.Where(
                x =>
                    (
                        config.InheritsClass == null || config.InheritsClass.Length == 0
                        || (x.BaseType != null && x.BaseType.Name == config.InheritsClass)
                    )
                ).ToList();

            // Filter Types by Class Name Matching
            foundDefinition = foundDefinition.Where(
                x =>
                    (
                        config.ClassName == null || config.ClassName.Length == 0 || (x.Name.Equals(config.ClassName))
                    )
                ).ToList();

            // Filter Types by Methods
            foundDefinition = foundDefinition.Where(x
                    =>
                        (config.HasMethods == null || config.HasMethods.Length == 0
                            || (x.Methods.Where(x => !x.IsStatic).Select(y => y.Name.Split('.')[y.Name.Split('.').Length - 1]).Count(y => config.HasMethods.Contains(y)) >= config.HasMethods.Length))

                    ).ToList();
            // Filter Types by Virtual Methods
            if (config.HasMethodsVirtual != null && config.HasMethodsVirtual.Length > 0)
            {
                foundDefinition = foundDefinition.Where(x
                       =>
                         (x.Methods.Count(y => y.IsVirtual) > 0
                            && x.Methods.Where(y => y.IsVirtual).Count(y => config.HasMethodsVirtual.Contains(y.Name)) >= config.HasMethodsVirtual.Length
                            )
                       ).ToList();
            }
            // Filter Types by Static Methods
            foundDefinition = foundDefinition.Where(x
                    =>
                        (config.HasMethodsStatic == null || config.HasMethodsStatic.Length == 0
                            || (x.Methods.Where(x => x.IsStatic).Select(y => y.Name.Split('.')[y.Name.Split('.').Length - 1]).Count(y => config.HasMethodsStatic.Contains(y)) >= config.HasMethodsStatic.Length))

                    ).ToList();

            // Filter Types by Events
            foundDefinition = foundDefinition.Where(x
                   =>
                       (config.HasEvents == null || config.HasEvents.Length == 0
                           || (x.Events.Select(y => y.Name.Split('.')[y.Name.Split('.').Length - 1]).Count(y => config.HasEvents.Contains(y)) >= config.HasEvents.Length))

                   ).ToList();

            // Filter Types by Field/Properties
            foundDefinition = foundDefinition.Where(
                x =>
                        (
                            // fields
                            (
                            config.HasFields == null || config.HasFields.Length == 0
                            || (!config.HasExactFields && x.Fields.Count(y => config.HasFields.Contains(y.Name)) >= config.HasFields.Length)
                            || (config.HasExactFields && x.Fields.Count(y => y.IsDefinition && config.HasFields.Contains(y.Name)) == config.HasFields.Length)
                            )
                            ||
                            // properties
                            (
                            config.HasFields == null || config.HasFields.Length == 0
                            || (!config.HasExactFields && x.Properties.Count(y => config.HasFields.Contains(y.Name)) >= config.HasFields.Length)
                            || (config.HasExactFields && x.Properties.Count(y => y.IsDefinition && config.HasFields.Contains(y.Name)) == config.HasFields.Length)

                            )
                        )).ToList();

            foundDefinition = foundDefinition.Where(
                x =>
                        (
                            // properties
                            (
                            config.HasProperties == null || config.HasProperties.Length == 0
                            || (x.Properties.Count(y => config.HasProperties.Contains(y.Name)) >= config.HasProperties.Length)
                            )
                        )).ToList();

            // Filter Types by Class
            foundDefinition = foundDefinition.Where(
                x =>
                    (
                        (!config.IsClass.HasValue || (config.IsClass.HasValue && config.IsClass.Value && ((x.IsClass || x.IsAbstract) && !x.IsEnum && !x.IsInterface)))
                    )
                ).ToList();

            // Filter Types by Interface
            foundDefinition = foundDefinition.Where(
               x =>
                   (
                        (!config.IsInterface.HasValue || (config.IsInterface.HasValue && config.IsInterface.Value && (x.IsInterface && !x.IsEnum && !x.IsClass)))
                   )
               ).ToList();

            foundDefinition = foundDefinition.Where(
           x =>
               (
                    (!config.IsStruct.HasValue || (config.IsStruct.HasValue && config.IsStruct.Value && (x.IsValueType)))
               )
           ).ToList();

            // Filter types by Exact DeclaredMethodCount
            foundDefinition = foundDefinition.Where(x => !config.ExactDeclaredMethodCount.HasValue || (x.GetMethods().Count(y => y.DeclaringType == x) == config.ExactDeclaredMethodCount.Value)).ToList();

            // Filter types by Exact DeclaredFieldCount
            //foundDefinition = foundDefinition.Where(x => !config.ExactDeclaredFieldCount.HasValue || (x.Fields.Count(y => y.DeclaringType == x) + x.Properties.Count(y => y.DeclaringType == x) == config.ExactDeclaredFieldCount.Value)).ToList();

            foundDefinition = foundDefinition.Where(x => !config.ExactDeclaredFieldCount.HasValue || (x.Fields.Count(y => y.DeclaringType == x) == config.ExactDeclaredFieldCount.Value)).ToList();

            // Filter types by Exact DeclaredPropertyCount
            foundDefinition = foundDefinition.Where(x => !config.ExactDeclaredPropertyCount.HasValue || (x.Properties.Count(y => y.DeclaringType == x) == config.ExactDeclaredPropertyCount.Value)).ToList();

            // Filter types by IsSealed
            foundDefinition = foundDefinition.Where(x => !config.IsSealed.HasValue || (x.IsSealed == config.IsSealed)).ToList();

            // Filter Types by Constructor
            if (config.HasConstructorArgs != null)
                foundDefinition = foundDefinition.Where(t => t.Methods.Any(x => x.IsConstructor
                    && x.Parameters.Count == config.HasConstructorArgs.Length
                    && config.HasConstructorArgs.Length == config.HasConstructorArgs.Sum(arg => x.Parameters.Select(x => x.Name).Contains(arg) ? 1 : 0)
                    )).ToList();

            return foundDefinition;
        }

        public TypeDefinition CreateStubOfOldType(TypeDefinition oldType)
        {
            var stubTypeDefinition = new TypeDefinition(oldType.Namespace, oldType.Name, oldType.Attributes);
            foreach (var cons in oldType.GetConstructors())
            {
                MethodDefinition methodDefinition = new(cons.Name, cons.Attributes, cons.ReturnType);
                foreach (var parameters in cons.Parameters)
                {
                    methodDefinition.Parameters.Add(parameters);
                }
                methodDefinition.Body = cons.Body;
                methodDefinition.CallingConvention = cons.CallingConvention;
                foreach (var securityDeclaration in cons.SecurityDeclarations)
                {
                    methodDefinition.SecurityDeclarations.Add(securityDeclaration);
                }
                stubTypeDefinition.Methods.Add(methodDefinition);
            }
            //stubTypeDefinition.Methods.Add(new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public, null));
            return stubTypeDefinition;
        }

        public string[] SplitCamelCase(string input)
        {
            return System.Text.RegularExpressions.Regex
                .Replace(input, "(?<=[a-z])([A-Z])", ",", System.Text.RegularExpressions.RegexOptions.Compiled)
                .Trim().Split(',');
        }

        public async Task<bool> DeobfuscateAsync(string exeLocation, HashSet<string> renamedClasses, bool createBackup = true, bool overwriteExisting = false, bool doRemapping = false, ILogger logger = null)
        {
            if (renamedClasses == null)
                renamedClasses = new HashSet<string>();


            var tClasses = new HashSet<string>();
            Logger = logger;
            await Task.Run(() => { return Deobfuscate(exeLocation, out tClasses, createBackup, overwriteExisting, doRemapping, logger); });
            renamedClasses = tClasses;
            return true;
        }
    }
}
