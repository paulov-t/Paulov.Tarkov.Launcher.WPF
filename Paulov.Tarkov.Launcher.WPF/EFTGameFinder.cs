using System.IO;

namespace Paulov.Launcher
{
    public static class OfficialGameInstallChecker
    {
        public static FileInfo FindOfficialGame(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            if (!File.Exists(filePath))
                return null;

            return new FileInfo(filePath);
        }

    }
}
