namespace Tarkov.Deobfuscator.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        [DataRow("ExampleAssemblyFolder/Assembly-CSharp.dll")]
        public void TestMethod1(string assemblyLocation)
        {
            if (!File.Exists(assemblyLocation))
                return;

            new PaulovDeobfuscator().Deobfuscate(assemblyLocation, out _, createBackup: false, overwriteExisting: false, doRemapping: true);
        }
    }
}