#pragma warning disable CA1416 // Validate platform compatibility
using Microsoft.Win32;

namespace Paulov.Launcher
{
    public static class RegistryManager
    {
        public static string ArenaGamePathEXE
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkovArena_live"))
                    {
                        if (key != null)
                        {
                            string exePath = key.GetValue("DisplayIcon").ToString();
                            return exePath;
                        }
                    }
                }
                catch
                {

                }

                return string.Empty;
            }
        }

        public static string EFTGamePathEXE
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov"))
                    {
                        if (key != null)
                        {
                            string exePath = key.GetValue("DisplayIcon").ToString();
                            return exePath;
                        }
                    }
                }
                catch
                {

                }
                return string.Empty;
            }
        }

    }
}
#pragma warning restore CA1416 // Validate platform compatibility
