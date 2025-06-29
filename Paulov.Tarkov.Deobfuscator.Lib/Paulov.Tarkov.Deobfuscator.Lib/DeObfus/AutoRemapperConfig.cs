using Newtonsoft.Json;
using System.Collections.Generic;

namespace Tarkov.Deobfuscator
{
    public partial class AutoRemapperConfig
    {
        public AutoRemapType? AutomaticRemapping { get; set; }
        public AutoRemapType? EnableDefinedRemapping { get; set; }

        public DefinedTypeRemappingConfiguration[] DefinedRemapping { get; set; }

        public bool? EnableForceAllTypesPublic { get; set; }
        public string[] DefinedTypesToForcePublic { get; set; }
        public string[] TypesToForceAllPublicMethods { get; set; }
        public string[] TypesToForceAllPublicFieldsAndProperties { get; set; }

        public string[] TypesToConvertConstructorsToPublic { get; set; }

        /// <summary>
        /// Types to not Automatically remap
        /// </summary>
        public string[] AutoRemapTypeExcemptions { get; set; }

        /// <summary>
        /// Automatically remap Descriptor types (i.e. GridItemAddressDescriptor)
        /// </summary>
        public bool? AutoRemapDescriptors { get; set; }

        /// <summary>
        /// Attempt to make all methods of all types public
        /// </summary>
        public bool? AttemptToForceAllPublicMethods { get; set; }

        /// <summary>
        /// Add values to any Enum type within the Dll
        /// </summary>
        public List<RemapEnumAddition> EnumAdditions { get; set; }
        public bool? EnableForceAllMethodsPublic { get; internal set; }

        public class RemapEnumAddition
        {
            public string Type { get; set; }

            [JsonProperty("eValue")]
            public string EValue { get; set; }

            [JsonProperty("iValue")]
            public int? IValue { get; set; }
        }

        public enum AutoRemapType
        {
            /// <summary>
            /// Do nothing
            /// </summary>
            None = 0,
            /// <summary>
            /// Will auto remap type names
            /// </summary>
            Remap = 1,
            /// <summary>
            /// Will only discover but not remap type names
            /// </summary>
            Test = 2,
            /// <summary>
            /// Will use Aki method
            /// </summary>
            Aki = 3,
        }

    }
}
