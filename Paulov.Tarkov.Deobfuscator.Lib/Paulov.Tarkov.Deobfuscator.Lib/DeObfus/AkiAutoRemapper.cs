using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace Paulov.Tarkov.Deobfuscator.Lib.DeObfus
{

    /// <summary>
    /// SPT-Aki's auto remapper.
    /// https://dev.sp-tarkov.com/SPT-AKI/SPT-AssemblyTool/raw/branch/master/SPT-AssemblyTool/AutoMapper.cs
    /// </summary>
    public static class AkiAutoRemapper
    {
        /// <summary>
        /// Iterate over all types in provided assembly
        /// Find properties/parameters that have Types we want to find name of
        /// Store in dict the names found throughout code
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Dictionary<string, Dictionary<string, int>> CreateAutoMapping(AssemblyDefinition assembly)
        {
            var namesToIgnore = new List<string>
            {
                "data", "object", "entities", "value",
                "body", "result", "parent", "area",
                "place", "info", "shot", "request",
                "source", "writer", "graph", "currequest",
                "controller", "counter", "closest", "newobject",
                "setting", "dictionary", "instance", "settings",
                "variation", "operation", "template", "emitter"
            };
            int minNameSize = 6;

            var mappings = new Dictionary<string, Dictionary<string, int>>();
            // Iterate over all non-nested classes
            foreach (var type in assembly.MainModule.Types.Where(x => !x.IsNested && !x.IsNestedPrivate && !x.IsNestedPublic && !x.IsArray))
            {
                // Type has methods, we want them
                if (type.HasMethods)
                {
                    // Iterate over methods that are public and have params
                    foreach (var method in type.Methods.Where(x => x.HasParameters))
                    {
                        // Iterate over each methods params, avoid generics + primitives + arrays
                        foreach (var parameter in method.Parameters.Where(x => !x.ParameterType.IsGenericParameter && !x.ParameterType.IsPrimitive && !x.ParameterType.IsArray))
                        {
                            var paramIsInterface = parameter.ParameterType.Name.Contains("GInterface");
                            var paramIsStruct = parameter.ParameterType.Name.Contains("GStruct");
                            var paramIsClass = parameter.ParameterType.Name.Contains("GClass");
                            if (!paramIsClass && !paramIsInterface && !paramIsStruct)
                            {
                                continue;
                            }

                            if (parameter.Name.Length <= minNameSize)
                            {
                                continue;
                            }

                            if (NameStartsWithBannedText(parameter.Name))
                            {
                                continue;
                            }

                            if (namesToIgnore.Contains(parameter.Name.ToLower()))
                            {
                                continue;
                            }

                            if (parameter.ParameterType.Namespace.ToLower() == "unityengine")
                            {
                                continue;
                            }

                            var cleanedName = CleanedTypeName(parameter.Name, paramIsInterface, paramIsClass, paramIsStruct);
                            AddToDictionary(parameter.ParameterType.Name, cleanedName, mappings);
                        }
                    }
                }

                // iterate over public/private fields that are not privitives + not arrays
                foreach (var field in type.Fields.Where(x => !x.FieldType.IsPrimitive && !x.FieldType.IsArray))
                {
                    var fieldIsStruct = field.FieldType.Name.Contains("GStruct");
                    var fieldIsInterface = field.FieldType.Name.Contains("GInterface");
                    var fieldIsClass = field.FieldType.Name.Contains("GClass");

                    // We only want to remap GClass/GStruct/GInterfaces, skip everything else
                    if (!fieldIsClass && !fieldIsInterface && !fieldIsStruct)
                    {
                        continue;
                    }

                    // Skip really short fiend names like graph/data/result
                    if (field.Name.Length <= minNameSize)
                    {
                        continue;
                    }

                    // Skip when named "Gclass"/"GInterface" etc
                    if (NameStartsWithBannedText(field.Name))
                    {
                        continue;
                    }

                    // Skip various unhelpful names
                    if (namesToIgnore.Contains(field.Name.ToLower()))
                    {
                        continue;
                    }

                    // Ignore unity engine types
                    if (field.FieldType.Namespace.ToLower() == "unityengine")
                    {
                        continue;
                    }

                    // Format field name for consistency
                    var cleanedName = CleanedTypeName(field.Name, fieldIsInterface, fieldIsClass, fieldIsStruct);

                    AddToDictionary(field.FieldType.Name, cleanedName, mappings);
                }
            }

            //mappings.OrderByDescending(x => x.Value.Values);
            // Cleanup of dict before return
            mappings = CleanUpMappings(mappings);

            return mappings;
        }

        private static bool NameStartsWithBannedText(string name)
        {
            var loweredName = name;
            return loweredName.StartsWith("gclass") || loweredName.StartsWith("gstruct") || loweredName.StartsWith("ginterface");
        }

        private static void AddToDictionary(string key, string cleanedName, Dictionary<string, Dictionary<string, int>> dictionary)
        {
            if (dictionary.ContainsKey(key))
            {
                var existingMap = dictionary[key];
                if (existingMap.ContainsKey(cleanedName))
                {
                    existingMap[cleanedName]++;
                }
                else
                {
                    dictionary[key].Add(cleanedName, 1);
                }
            }
            else
            {
                dictionary.Add(key, new Dictionary<string, int>());
                dictionary[key].Add(cleanedName, 1);
            }
        }

        private static string CleanedTypeName(string name, bool isInterface, bool isClass, bool isStruct)
        {
            name = name.Replace("_", "");
            name = name[0].ToString().ToUpper() + name.Substring(1);

            if (isInterface)
            {
                name = $"I{name}";
            }

            if (isClass)
            {
                name = $"{name}Class";
            }

            if (isStruct)
            {
                name = $"{name}Struct";
            }

            return name;
        }

        private static Dictionary<string, Dictionary<string, int>> CleanUpMappings(Dictionary<string, Dictionary<string, int>> mappings)
        {
            var mappingsCopy = mappings.ToDictionary(entry => entry.Key,
                                               entry => entry.Value);
            foreach (var mapping in mappings)
            {
                // Has only 1 type map + not 3 separate matches for it
                //if (mapping.Value.Count == 1 && mapping.Value.First().Value < 3)
                //{
                //    // not good, remove it
                //    mappings.Remove(mapping.Key);
                //}

                // More than 1 finding and they all names have one match
                if (mapping.Value.Count > 1 && mapping.Value.All(x => x.Value == 1))
                {
                    mappingsCopy.Remove(mapping.Key);
                }

                if (mapping.Value.Count > 3 && mapping.Value.Sum(x => x.Value) == mapping.Value.Count && mapping.Value.Max(x => x.Value) < 3)
                {
                    // lots of various names, skip
                    mappingsCopy.Remove(mapping.Key);
                }
            }

            return mappingsCopy;
        }

        internal static void ApplyChangesToAssembly(Dictionary<string, Dictionary<string, int>> autoMapResults, AssemblyDefinition newAssembly)
        {
            // Create a 1-1 mapping from the automap results
            var remappingDict = new Dictionary<string, string>();
            foreach (var mapResult in autoMapResults)
            {
                var highestValueName = mapResult.Value.OrderByDescending(x => x.Value).First();
                remappingDict.Add(mapResult.Key, highestValueName.Key);
            }

            // TODO - remap
            //AssemblyHelper.PerformRemapping()
        }
    }
}
