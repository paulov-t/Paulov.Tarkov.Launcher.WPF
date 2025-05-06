using System.IO;

namespace Paulov.Launcher
{
    public static class ArenaGameFinder
    {
        public static FileInfo FindOfficialGame()
        {
            return OfficialGameInstallChecker.FindOfficialGame(RegistryManager.ArenaGamePathEXE);
        }
    }
}
