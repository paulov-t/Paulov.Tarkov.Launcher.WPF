using Newtonsoft.Json;
using Paulov.Launcher.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Paulov.Launcher
{
    public class LauncherConfig
    {

        private static LauncherConfig instance;// = new LauncherConfig();

        public static LauncherConfig Instance
        {
            get
            {
                if (instance == null)
                    Instance = Load();

                return instance;
            }
            set { instance = value; }
        }


        private LauncherConfig()
        {
            if (instance == null)
            {
                instance = this;
                return;
            }
        }

        public ServerInstance ServerInstance { get; set; } = new ServerInstance() { ServerAddress = "https://127.0.0.1:443" };

        public string Username { get; set; }



        public ObservableCollection<InstallInstanceModel> InstallInstances { get; set; } = new ObservableCollection<InstallInstanceModel>();

        public string InstallLocationEFT { get; set; }
        public string InstallLocationArena { get; set; }

        public bool AutomaticallyInstallAssemblyDlls { get; set; } = false;

        public bool AutomaticallyDeobfuscateDlls { get; set; } = false;

        public bool AutomaticallyInstallSIT { get; set; } = false;
        public bool AutomaticallyInstallSITPreRelease { get; set; } = false;
        public bool ForceInstallLatestSIT { get; set; } = false;
        public bool AutomaticallyInstallAkiSupport { get; set; } = false;

        public bool EnableCoopServer { get; set; } = false;

        public bool SendInfoToDiscord { get; set; } = false;
        public bool CloseLauncherAfterLaunch { get; set; } = false;

        public bool AutoCheckForOfficialUpdates { get; set; } = false;

        public static string ConfigDirectoryPath => Path.Combine(App.ApplicationDirectory, "Config");
        public static string ConfigFilePath => Path.Combine(ConfigDirectoryPath, "LauncherConfig.json");

        private static LauncherConfig Load()
        {
            LauncherConfig launcherConfig = new()
            {
                AutomaticallyDeobfuscateDlls = true,
                AutomaticallyInstallAssemblyDlls = true,
                AutomaticallyInstallSIT = true,
                AutomaticallyInstallAkiSupport = true,
                CloseLauncherAfterLaunch = false,
                ServerInstance = new ServerInstance() { ServerAddress = "http://127.0.0.1:6969" }
            };

            var configPath = ConfigDirectoryPath;
            var launcherConfigPath = ConfigFilePath;
            Directory.CreateDirectory(configPath);

            if (File.Exists(launcherConfigPath))
                launcherConfig = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(launcherConfigPath));

            if (launcherConfig.ServerInstance.ServerAddress.EndsWith("/"))
            {
                launcherConfig.ServerInstance.ServerAddress = launcherConfig.ServerInstance.ServerAddress.Substring(0, launcherConfig.ServerInstance.ServerAddress.Length - 1);
            }

            if (!File.Exists(launcherConfig.InstallLocationEFT))
                launcherConfig.InstallLocationEFT = null;

            if (!File.Exists(launcherConfig.InstallLocationArena))
                launcherConfig.InstallLocationArena = null;


            if (!File.Exists(launcherConfigPath))
                launcherConfig.Save();

            return launcherConfig;
        }

        public void Save()
        {
            File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
