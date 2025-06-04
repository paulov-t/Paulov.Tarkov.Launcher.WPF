using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Paulov.Tarkov.Deobfuscator.Lib.DeObfus
{

    /// <summary>
    /// SPT-Aki's Publicizer https://dev.sp-tarkov.com/SPT-AKI/SPT-AssemblyTool/raw/branch/master/SPT-AssemblyTool/Publicizer.cs
    /// with some changes by Paulov-t
    /// </summary>
    public static class Publicizer
    {
        private static ModuleDefinition MainModule;

        public static void PublicizeClasses(AssemblyDefinition assembly)
        {
            MainModule = assembly.MainModule;
            var types = assembly.MainModule.GetAllTypes();

            foreach (var type in types)
            {
                if (type.IsNested)
                    continue; // Nested types are handled when publicizing the parent type

                PublicizeType(type);
            }

        }

        public static void PublicizeType(TypeDefinition type)
        {
            if (type.CustomAttributes.Any(a => a.AttributeType.Name == nameof(CompilerGeneratedAttribute)))
            {
                return;
            }

            // Paulov: This handles bad delegates in the Assembly
            if (type.Name.Contains("delegate", System.StringComparison.OrdinalIgnoreCase)
                || type.BaseType?.Name == "MulticastDelegate" || type.BaseType?.Name == "Delegate")
            {
                return;
            }

#if DEBUG
            if (type.Name == "ActiveHealthController")
            {

            }
#endif

            if (type is { IsNested: false, IsPublic: false } or { IsNested: true, IsNestedPublic: false }
            && type.Interfaces.All(i => i.InterfaceType.Name != "IEffect"))
            {
                type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
                type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
            }

            if (type.IsSealed)
            {
                type.Attributes &= ~TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
            }

            foreach (var method in type.Methods)
            {
                //PublicizeMethod(method);
            }

            foreach (var property in type.Properties)
            {
                //if (property.GetMethod != null)
                //    PublicizeMethod(property.GetMethod);
                //if (property.SetMethod != null)
                //    PublicizeMethod(property.SetMethod);
            }

            var nestedTypesToPublicize = type.NestedTypes.ToArray();

            // Workaround to not publicize some nested types that cannot be patched easily and cause issues
            // Specifically, we want to find any type that implements the "IHealthController" interface and make sure none of it's nested types that implement "IEffect" are changed
            if (GetFlattenedInterfacesRecursive(type).Any(i => i.InterfaceType.Name == "IHealthController"))
            {
                // Specifically, any type that implements the IHealthController interface needs to not publicize any nested types that implement the IEffect interface
                nestedTypesToPublicize = type.NestedTypes.Where(t => t.IsAbstract || t.Interfaces.All(i => i.InterfaceType.Name != "IEffect")).ToArray();
            }

            foreach (var nestedType in nestedTypesToPublicize)
            {
                PublicizeType(nestedType);
            }
        }

        // Don't publicize methods that implement interfaces not belonging to the current assembly
        // Unused - sometimes some ambiguous reference errors appear due to this, but the pros outweigh the cons at the moment
        private static bool CanPublicizeMethod(MethodDefinition method)
        {
            return !method.HasOverrides && method.GetBaseMethod().Equals(method) && !method.IsVirtual;
        }

        private static void PublicizeMethod(MethodDefinition method)
        {
            if (method.IsCompilerControlled /*|| method.CustomAttributes.Any(a => a.AttributeType.Name == nameof(CompilerGeneratedAttribute))*/)
            {
                return;
            }

            if (method.IsPublic) return;

            // if (!CanPublicizeMethod(method)) return;

            // Workaround to not publicize a specific method so the game doesn't crash
            if (method.Name == "TryGetScreen") return;

            method.Attributes &= ~MethodAttributes.MemberAccessMask;
            method.Attributes |= MethodAttributes.Public;
        }

        // Unused for now - publicizing fields is tricky, as it often creates MonoBehaviour loading errors and prevents scenes from loading,
        // most notably breaking the initial game loader scene and causing the game to CTD right after starting
        private static void PublicizeField(FieldDefinition field)
        {
            if (field.CustomAttributes.Any(a => a.AttributeType.Name == nameof(CompilerGeneratedAttribute))
                // || field.HasCustomAttributes
                || field.Name.StartsWith("delegate")
                || field.Name.Contains("__BackingField"))
            {
                return;
            }

            if (field.IsPublic || field.IsCompilerControlled || field.IsLiteral || field.IsStatic || field.IsInitOnly) return;

            field.Attributes &= ~FieldAttributes.FieldAccessMask;
            field.Attributes |= FieldAttributes.Public;
        }

        private static List<InterfaceImplementation> GetFlattenedInterfacesRecursive(TypeDefinition type)
        {
            var interfaces = new List<InterfaceImplementation>();

            if (type == null)
                return interfaces;

            if (type.Interfaces.Any())
            {
                interfaces.AddRange(type.Interfaces);
            }

            if (type.BaseType != null)
            {
                var baseTypeDefinition = MainModule.ImportReference(type.BaseType).Resolve();
                var baseTypeInterfaces = GetFlattenedInterfacesRecursive(baseTypeDefinition);

                if (baseTypeInterfaces.Any())
                {
                    interfaces.AddRange(baseTypeInterfaces);
                }
            }

            return interfaces;
        }
    }
}