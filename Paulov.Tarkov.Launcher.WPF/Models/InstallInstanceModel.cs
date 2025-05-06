using Octokit;
using Paulov.Launcher.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Paulov.Launcher.Models
{
    public class InstallInstanceModel
    {
        #region Constructors
        public InstallInstanceModel()
        {
            //new GitHubService(GetGameType()).GetSITReleases().ContinueWith(x =>
            //{
            //    SITReleases = x.Result;
            //    SelectedSITRelease = SITReleases.OrderByDescending(x => x.CreatedAt).First();
            //});

            new GitHubService(GetGameType()).GetPaulovMinimalReleaseLatest().ContinueWith(x =>
            {
                PaulovReleases = x.Result;
                SelectedPaulovRelease = PaulovReleases.OrderByDescending(x => x.CreatedAt).First();
            });

        }
        #endregion



        #region DeterminationFunctions

        public enum EGameType
        {
            Tarkov,
            TarkovArena
        }

        public FileInfo GetEXEPathOffline()
        {
            return GetEXEPath(InstallLocationOffline);
        }

        public FileInfo GetEXEPathOfficial()
        {
            return GetEXEPath(InstallLocationOfficial);
        }

        public FileInfo GetEXEPath(string directory)
        {
            if (directory == null)
                return null;

            var discoveredFiles = Directory.GetFiles(directory, "EscapeFromTarkov*");
            if (discoveredFiles == null || discoveredFiles.Length == 0)
                return null;

            var files = discoveredFiles.ToList();
            files = files.Where(x => !x.Contains("_BE")).ToList();
            return new FileInfo(files[0]);
        }

        public EGameType GetGameType()
        {
            if (GetEXEPathOffline() == null)
                return EGameType.Tarkov;

            switch (GetEXEPathOffline().Name)
            {
                case "EscapeFromTarkov":
                    return EGameType.Tarkov;
                case "EscapeFromTarkov_Arena":
                    return EGameType.TarkovArena;
            }

            return EGameType.Tarkov;
        }

        #endregion


        public ObservableCollection<Release> PaulovReleases { get; set; }

        //public ObservableCollection<Release> SITReleases { get; set; }
        //public Release SelectedSITRelease { get; set; }

        public Release SelectedPaulovRelease { get; set; }


        public string InstallLocationOfficial { get; set; }
        public string InstallLocationOffline { get; set; }

        public ObservableCollection<ServerInstance> ServerInstances { get; set; } = new ObservableCollection<ServerInstance>()
            {
                new() { ServerAddress = "http://127.0.0.1:6969", ServerName = "Local" }
            };

        public class InstanceAccountModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public ObservableCollection<InstanceAccountModel> InstanceAccounts { get; set; } = new ObservableCollection<InstanceAccountModel>() { new() };

        public InstanceAccountModel InstanceAccount => InstanceAccounts.FirstOrDefault();


        public bool EFTInstalled => !string.IsNullOrEmpty(InstallLocationOffline)
            && new DirectoryInfo(InstallLocationOffline).Exists
            && GetEXEPathOffline().Exists;

        public bool AkiInstalled => !string.IsNullOrEmpty(InstallLocationOffline)
            && new DirectoryInfo(InstallLocationOffline).Exists
            && GetEXEPathOffline().Exists;

        public string ClientModFileName { get; } = "Paulov.Tarkov.Minimal.dll";

        public bool ClientModInstalled => !string.IsNullOrEmpty(InstallLocationOffline)
           && new DirectoryInfo(InstallLocationOffline).Exists
           && Directory.GetFiles(InstallLocationOffline, ClientModFileName, new EnumerationOptions() { RecurseSubdirectories = true }).Length > 0;

        public bool BepInExInstalled => !string.IsNullOrEmpty(InstallLocationOffline)
          && new DirectoryInfo(InstallLocationOffline).Exists
          && Directory.GetFiles(InstallLocationOffline, "doorstop_config.ini", new EnumerationOptions() { RecurseSubdirectories = true }).Length > 0;

        public Visibility CanLaunchVisibility => EFTInstalled && ClientModInstalled ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EFTInstalledVisibility => EFTInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EFTNotInstalledVisibility => !EFTInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BepInExNotInstalledVisibility => !BepInExInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ModNotInstalledVisibility => BepInExInstalled && !ClientModInstalled ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CanUpdateVisibility => EFTInstalled && !string.IsNullOrEmpty(InstallLocationOffline) && new DirectoryInfo(InstallLocationOffline).Exists ? Visibility.Visible : Visibility.Collapsed;

        public string EFTGameVersion => EFTInstalled
                ? FileVersionInfo.GetVersionInfo(GetEXEPathOffline().FullName).ProductVersion.Split('-')[0]
                + "." + FileVersionInfo.GetVersionInfo(GetEXEPathOffline().FullName).ProductVersion.Split('-')[1]
                 : "";

        public string GetClientModPluginPath()
        {
            return Path.Combine(new InstallingService().GetBepInExPluginsPathInInstall(InstallLocationOffline), "PaulovMP", ClientModFileName);
        }

        public string ClientModVersion => File.Exists(GetClientModPluginPath())
            ? FileVersionInfo.GetVersionInfo(GetClientModPluginPath()).ProductVersion
             : "";

        public string Username
        {
            get
            {
                return InstanceAccount.Username;
            }
            set
            {
                InstanceAccount.Username = value;
            }
        }

        public string Password
        {
            get
            {
                return InstanceAccount.Password;
            }
            set
            {
                InstanceAccount.Password = value;
            }
        }

        public string ServerAddress
        {
            get
            {
                return ServerInstances[0].ServerAddress;
            }
            set
            {
                ServerInstances[0].ServerAddress = value;
            }
        }

        public long Ping { get; internal set; }

        public void Load()
        {
            if (!File.Exists(InstallLocationOfficial))
                InstallLocationOfficial = null;

            if (!File.Exists(InstallLocationOffline))
                InstallLocationOffline = null;
        }
    }
}
