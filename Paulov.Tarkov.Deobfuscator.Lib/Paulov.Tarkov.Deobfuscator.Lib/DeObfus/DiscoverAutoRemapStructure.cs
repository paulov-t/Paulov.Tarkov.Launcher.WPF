using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Paulov.Tarkov.Deobfuscator.Lib.DeObfus
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DiscoverAutoRemapStructure
    {
        public string OriginalName { get; set; }

        public string NewName { get; set; }

        public DiscoverAutoRemapStructure(string originalName, string newName)
        {
            OriginalName = originalName;
            NewName = newName;
        }

        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is not DiscoverAutoRemapStructure other)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return (OriginalName == other.OriginalName && NewName == other.NewName);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {

            if (string.IsNullOrEmpty(OriginalName) && string.IsNullOrEmpty(NewName))
            {
                return "DiscoverAutoRemapStructure: Empty";
            }

            if (!string.IsNullOrEmpty(OriginalName) && !string.IsNullOrEmpty(NewName))
            {
                return $"DiscoverAutoRemapStructure: {OriginalName} =>> {NewName}";
            }

            if (string.IsNullOrEmpty(OriginalName))
            {
                return $"DiscoverAutoRemapStructure: NewName = {NewName}";
            }

            if (string.IsNullOrEmpty(NewName))
            {
                return $"DiscoverAutoRemapStructure: OriginalName = {OriginalName}";
            }

            if (OriginalName == NewName)
            {
                return $"DiscoverAutoRemapStructure: {OriginalName} = {NewName} (no change)";
            }




            return base.ToString();
        }
    }
}
