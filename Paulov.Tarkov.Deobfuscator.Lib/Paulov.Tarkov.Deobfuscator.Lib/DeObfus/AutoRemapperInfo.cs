namespace Tarkov.Deobfuscator
{
    public partial class AutoRemapperInfo
    {
        public string RenameClassNameTo { get; set; }
        public string ClassName { get; set; }
        public string ClassFullNameContains { get; set; }
        public bool? OnlyTargetInterface { get; set; }
        public bool? IsClass { get; set; }
        public bool? IsInterface { get; set; }
        public bool? IsNotInterface { get; set; }
        public bool? IsStruct { get; set; }
        public bool HasExactFields { get; set; }
        public string[] HasFields { get; set; }
        public string[] HasFieldsStatic { get; set; }
        public string[] HasProperties { get; set; }
        public string[] HasMethods { get; set; }

        /// <summary>
        /// Number of methods within the type that are declared must match this number
        /// </summary>
        public int? ExactDeclaredMethodCount { get; set; }

        /// <summary>
        /// Number of fields + properties within the type that are declared must match this number
        /// </summary>
        public int? ExactDeclaredFieldCount { get; set; }

        /// <summary>
        /// Number of properties within the type that are declared must match this number
        /// </summary>
        public int? ExactDeclaredPropertyCount { get; set; }

        public string[] HasMethodsVirtual { get; set; }
        public string[] HasMethodsStatic { get; set; }
        public string[] HasEvents { get; set; }
        public string[] HasConstructorArgs { get; set; }
        public string InheritsClass { get; set; }
        public string IsNestedInClass { get; set; }
        public bool? OnlyRemapFirstFoundType { get; set; }
        public bool? MustBeGClass { get; set; }
        public bool? RemoveAbstract { get; set; }
        public bool? ConvertInternalMethodsToPublic { get; set; }
        public bool? IsAbstract { get; set; }
        public bool? IsSealed { get; set; }


        public override string ToString()
        {
            return !string.IsNullOrEmpty(RenameClassNameTo) ? RenameClassNameTo : base.ToString();
        }
    }
}
