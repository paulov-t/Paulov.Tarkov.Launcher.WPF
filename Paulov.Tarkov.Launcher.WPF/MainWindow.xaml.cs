using MahApps.Metro.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Paulov.Launcher.Windows;
using Paulov.Tarkov.Deobfuscator.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Tarkov.Deobfuscator;

namespace Paulov.Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, ILogger
    {
        public static readonly DependencyProperty ConfigProperty = DependencyProperty.Register("Config", typeof(LauncherConfig), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public LauncherConfig Config
        {
            get
            {
                var con = GetValue(ConfigProperty);
                if (con == null)
                    SetValue(ConfigProperty, LauncherConfig.Instance);

                return (LauncherConfig)GetValue(ConfigProperty);
            }
            set => SetValue(ConfigProperty, value);
        }

        public enum ELaunchButtonState : short
        {
            Launch,
            Deob,
            BepInEx,
            Custom = short.MaxValue
        }
        public ELaunchButtonState LaunchButtonState { get; set; } = ELaunchButtonState.Launch;

        private string _launchButtonText = "Launch";

        public string LaunchButtonText
        {
            get
            {
                switch (LaunchButtonState)
                {
                    case ELaunchButtonState.Launch:
                        _launchButtonText = "Launch";
                        break;
                    case ELaunchButtonState.Deob:
                        _launchButtonText = "Deobfuscating";
                        break;
                    case ELaunchButtonState.BepInEx:
                        _launchButtonText = "Installing BepInEx";
                        break;
                    case ELaunchButtonState.Custom:
                        break;
                    default:
                        _launchButtonText = LaunchButtonState.ToString();
                        break;
                }
                return _launchButtonText;
            }
            set
            {
                LaunchButtonState = ELaunchButtonState.Custom;
                _launchButtonText = value;
            }
        }

        public string Username
        {
            get
            {

                return Config.Username;
            }
        }

        public string ServerAddress
        {
            get
            {

                return Config.ServerInstance.ServerAddress;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists("LauncherConfig.json"))
            {
                Config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText("LauncherConfig.json"));
            }

            this.DataContext = this;

            this.Title = "Paulov's Launcher - " + App.ProductVersion.ToString();

            this.ContentRendered += MainWindow_ContentRendered;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            this.DataContext = this;

            if (!Config.InstallInstances.Any())
            {
                NewInstallationWizard wizard = new();
                wizard.ShowDialog();
            }
        }


        public static FlowDocument HtmlToFlowDocument(string text)
        {
            var document = new FlowDocument();
            using (var stream = new MemoryStream((new UTF8Encoding()).GetBytes(text)))
            {
                var txt = new TextRange(document.ContentStart, document.ContentEnd);
                txt.Load(stream, DataFormats.Text);
            }
            return document;
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Config.Save();
        }

        private void btnAddNewServer_Click(object sender, RoutedEventArgs e)
        {
            AddNewServerDialog addNewServerDialog = new();
            addNewServerDialog.ShowDialog();
        }

        private void btnRemoveServer_Click(object sender, RoutedEventArgs e)
        {

        }

        private string LoginToServer()
        {
            if (string.IsNullOrEmpty(ServerAddress))
            {
                MessageBox.Show("No Server Address Provided");
                return null;
            }

            if (ServerAddress.EndsWith("/"))
            {
                MessageBox.Show("Server Address is incorrect, you should NOT have a / at the end!");
                return null;
            }
            TarkovRequesting requesting = new(null, ServerAddress, false);

            Dictionary<string, string> loginData = new();
            loginData.Add("username", Username);
            loginData.Add("email", Username);
            loginData.Add("edition", "Edge Of Darkness"); // default to EoD
            //data.Add("edition", "Standard");
            //if (string.IsNullOrEmpty(txtPassword.Password))
            //{
            //    MessageBox.Show("You cannot use an empty Password for your account!");
            //    return null;
            //}
            //loginData.Add("password", txtPassword.Password);
            // Add backendUrl to support people who connect locally
            loginData.Add("backendUrl", Config.ServerInstance.ServerAddress);

            // connect and get editions
            //var returnDataConnect = requesting.PostJson("/launcher/server/connect", JsonConvert.SerializeObject(data));

            try
            {
                // attempt to login
                var returnData = requesting.PostJson("/launcher/profile/login", JsonConvert.SerializeObject(loginData));

                // If failed, attempt to register
                if (returnData == "INVALID_PASSWORD")
                {
                    var messageBoxResult = MessageBox.Show("Invalid Password", "Account");
                    return null;
                }

                // If failed, attempt to register
                if (returnData == "FAILED")
                {
                    var messageBoxResult = MessageBox.Show("Your account has not been found, would you like to register a new account with these credentials?", "Account", MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        // Add a Developer option
                        if (Username.Contains("Dev"))
                        {
                            messageBoxResult = MessageBox.Show("Your account contains \"Dev\", would you like to create a Developer (\"cheat\") account?", "Account", MessageBoxButton.YesNo);
                            if (messageBoxResult == MessageBoxResult.Yes)
                                loginData["edition"] = "SPT Developer";

                        }

                        // Register
                        returnData = requesting.PostJson("/launcher/profile/register", JsonConvert.SerializeObject(loginData));
                        // Login attempt after register
                        returnData = requesting.PostJson("/launcher/profile/login", JsonConvert.SerializeObject(loginData));

                    }
                    else
                    {
                        return null;
                    }
                }

                return returnData;
            }
            catch (System.Net.WebException webEx)
            {
                MessageBox.Show(webEx.Message, "Unable to communicate with the Server");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to communicate with the Server");
            }
            return null;
        }

        private void BrowseForOfflineGame()
        {
            if (string.IsNullOrEmpty(Config.InstallLocationEFT) || !File.Exists(Config.InstallLocationEFT))
            {
                OpenFileDialog openFileDialog = new();
                openFileDialog.Title = "Open OFFLINE EFT";
                openFileDialog.Filter = "EFT executatable|EscapeFromTarkov*";
                if (openFileDialog.ShowDialog() == true)
                {
                    var fvi = FileVersionInfo.GetVersionInfo(openFileDialog.FileName);
                    App.GameVersion = fvi.ProductVersion;
                    Config.InstallLocationEFT = openFileDialog.FileName;

                    UpdateButtonText(null);
                }
            }
        }

        private void BrowseForOfflineGameArena()
        {
            if (string.IsNullOrEmpty(Config.InstallLocationArena) || !File.Exists(Config.InstallLocationArena))
            {
                OpenFileDialog openFileDialog = new();
                openFileDialog.Title = "Open OFFLINE Arena";
                openFileDialog.Filter = "EFT executatable|EscapeFromTarkovArena*";
                if (openFileDialog.ShowDialog() == true)
                {
                    var fvi = FileVersionInfo.GetVersionInfo(openFileDialog.FileName);
                    App.GameVersion = fvi.ProductVersion;
                    Config.InstallLocationArena = openFileDialog.FileName;

                    UpdateButtonText(null);
                }
            }
        }
        private async Task DownloadInstallAndStartArena(string sessionId)
        {
            //btnLaunchGame.IsEnabled = false;

            var installLocation = Config.InstallLocationArena;
            if (!await DownloadAndInstallBepInEx5(installLocation))
            {
                MessageBox.Show("Install and Start aborted");
                return;
            }

            // Deobfuscate Assembly-CSharp
            if (Config.AutomaticallyDeobfuscateDlls
                && NeedsDeobfuscation(installLocation))
            {
                MessageBox.Show("Your game has not been deobfuscated and no client mods have been installed to allow OFFLINE play. Please install SIT or manually deobfuscate.");
                //if (await Deobfuscate(installLocation))
                //{
                //    StartGame(sessionId, installLocation);
                //}
                UpdateButtonText(null);
                //btnLaunchGame.IsEnabled = true;
            }
            else
            {
                // Launch game
                StartGame(sessionId, installLocation);
            }
        }


        private async void StartGame(string sessionId, string installLocation)
        {
            CleanupDirectory(installLocation);

            UpdateButtonText(null);
            //btnLaunchGame.IsEnabled = true;
            var commandArgs = $"-token={sessionId} -config={{\"BackendUrl\":\"{ServerAddress}\",\"Version\":\"live\"}}";
            Process.Start(installLocation, commandArgs);
            Config.Save();
            WindowState = WindowState.Minimized;

            await Task.Delay(10000);
        }

        private void CleanupDirectory(string installLocation)
        {
            UpdateButtonText("Cleaning client directory");

            var battlEyeDirPath = Directory.GetParent(installLocation).FullName + "\\BattlEye";
            if (Directory.Exists(battlEyeDirPath))
            {
                Directory.Delete(battlEyeDirPath, true);
            }

            var BEExePaths = Directory.GetFiles(Directory.GetParent(installLocation).FullName, "*_BE.exe");
            foreach (var beexepath in BEExePaths)
            {
                File.Delete(beexepath);
            }

            //var battlEyeExePath = installLocation.Replace("EscapeFromTarkov", "EscapeFromTarkov_BE");
            //if (File.Exists(battlEyeExePath))
            //{
            //    File.Delete(battlEyeExePath);
            //}
            //var arenaBattlEyeExePath = installLocation.Replace("EscapeFromTarkovArena", "EscapeFromTarkovArena_BE");
            //if (File.Exists(arenaBattlEyeExePath))
            //{
            //    File.Delete(arenaBattlEyeExePath);
            //}
            //var cacheDirPath = Directory.GetParent(installLocation).FullName + "\\cache";
            //if (Directory.Exists(cacheDirPath))
            //{
            //    Directory.Delete(cacheDirPath, true);
            //}
            //var consistancyInfoPath = installLocation.Replace("EscapeFromTarkov.exe", "ConsistencyInfo");
            //if (File.Exists(consistancyInfoPath))
            //{
            //    File.Delete(consistancyInfoPath);
            //}
            var uninstallPath = Path.Combine(Directory.GetParent(installLocation).FullName, "Uninstall.exe");
            if (File.Exists(uninstallPath))
            {
                File.Delete(uninstallPath);
            }
            //var logsDirPath = System.IO.Path.Combine(Directory.GetParent(installLocation).FullName, "Logs");
            //if (Directory.Exists(logsDirPath))
            //{
            //    Directory.Delete(logsDirPath, true);
            //}
        }

        private string GetBepInExPathInInstall(string exePath)
        {
            var baseGamePath = Directory.GetParent(exePath).FullName;
            var bepinexPath = System.IO.Path.Combine(exePath.Replace("EscapeFromTarkov.exe", "BepInEx"));
            return bepinexPath;
        }

        private string GetBepInExPluginsPathInInstall(string exePath)
        {
            var bepinexPluginsPath = System.IO.Path.Combine(GetBepInExPathInInstall(exePath), "plugins");
            return bepinexPluginsPath;
        }

        private bool DoesBepInExExistInInstall(string exePath)
        {
            var baseGamePath = Directory.GetParent(exePath).FullName;
            var bepinexPath = System.IO.Path.Combine(exePath.Replace("EscapeFromTarkov.exe", "BepInEx"));
            var bepinexWinHttpDLL = exePath.Replace("EscapeFromTarkov.exe", "winhttp.dll");

            var bepinexCorePath = System.IO.Path.Combine(bepinexPath, "core");
            var bepinexPluginsPath = System.IO.Path.Combine(bepinexPath, "plugins");

            return (Directory.Exists(bepinexCorePath) && Directory.Exists(bepinexPluginsPath) && File.Exists(bepinexWinHttpDLL));
        }

        private async Task<bool> DownloadAndInstallBepInEx5(string exeLocation)
        {
            try
            {
                var baseGamePath = Directory.GetParent(exeLocation).FullName;
                var bepinexPath = System.IO.Path.Combine(baseGamePath, "BepInEx");
                var bepinexWinHttpDLL = System.IO.Path.Combine(baseGamePath, "winhttp.dll");

                var bepinexCorePath = System.IO.Path.Combine(bepinexPath, "core");
                var bepinexPluginsPath = System.IO.Path.Combine(bepinexPath, "plugins");

                var savedBepinexZipPath = App.ApplicationDirectory + "\\BepInEx5.4.22.zip";

                if (!File.Exists(savedBepinexZipPath))
                {
                    UpdateButtonText("Downloading BepInEx");
                    await Task.Delay(500);

                    using (var ms = new MemoryStream())
                    {
                        var httpClient = new HttpClient();
                        httpClient.Timeout = new TimeSpan(0, 1, 0);
                        using (var rStream = await httpClient.GetStreamAsync("https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip"))
                        {
                            rStream.CopyTo(ms);
                            await File.WriteAllBytesAsync(savedBepinexZipPath, ms.ToArray());
                        }
                    }
                }

                if (DoesBepInExExistInInstall(exeLocation) && File.Exists("CurrentBepinexVersion.txt") && File.ReadAllText("CurrentBepinexVersion.txt") == savedBepinexZipPath)
                    return true;

                UpdateButtonText("Installing BepInEx");

                System.IO.Compression.ZipFile.ExtractToDirectory(savedBepinexZipPath, baseGamePath, true);
                if (!Directory.Exists(bepinexPluginsPath))
                {
                    Directory.CreateDirectory(bepinexPluginsPath);
                }

                File.WriteAllText("CurrentBepinexVersion.txt", savedBepinexZipPath);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to Install BepInEx: {ex.Message}", "Error");
                return false;
            }

            UpdateButtonText(null);
            //btnLaunchGame.IsEnabled = true;
            return true;

        }

        private void UpdateButtonText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                LaunchButtonText = text;
                LaunchButtonState = ELaunchButtonState.Custom;
            }
            else
            {
                LaunchButtonState = ELaunchButtonState.Launch;
            }

            //btnLaunchGame.Content = LaunchButtonText;
        }

        private bool IsSITCoreInstalled(string exeLocation)
        {
            var baseGamePath = Directory.GetParent(exeLocation).FullName;
            var bepinexPath = exeLocation.Replace("EscapeFromTarkov.exe", "");
            bepinexPath += "BepInEx";

            var bepinexPluginsPath = bepinexPath + "\\plugins\\";
            if (!Directory.Exists(bepinexPluginsPath))
                return false;

            return File.Exists(bepinexPluginsPath + "StayInTarkov.dll");
        }

        private string GetBepInExPath(string exeLocation)
        {
            var baseGamePath = Directory.GetParent(exeLocation).FullName;
            var bepinexPath = System.IO.Path.Combine(exeLocation.Replace("EscapeFromTarkov.exe", "BepInEx"));
            return bepinexPath;
        }

        private string GetBepInExPluginsPath(string exeLocation)
        {
            var bepinexPluginsPath = System.IO.Path.Combine(GetBepInExPath(exeLocation), "plugins");
            return bepinexPluginsPath;
        }

        private string GetBepInExPatchersPath(string exeLocation)
        {
            var bepinexPluginsPath = System.IO.Path.Combine(GetBepInExPath(exeLocation), "patchers");
            return bepinexPluginsPath;
        }


        private bool DownloadAndInstallAki(string exeLocation)
        {
            if (!Config.AutomaticallyInstallAkiSupport)
                return true;

            Directory.CreateDirectory(App.ApplicationDirectory + "/AkiSupport/Bepinex/Patchers");
            Directory.CreateDirectory(App.ApplicationDirectory + "/AkiSupport/Bepinex/Plugins");
            Directory.CreateDirectory(App.ApplicationDirectory + "/AkiSupport/Managed");

            try
            {

                var bepinexPluginsPath = GetBepInExPluginsPath(exeLocation);
                var bepinexPatchersPath = GetBepInExPatchersPath(exeLocation);

                // Discover where Assembly-CSharp is within the Game Folders
                var managedPath = exeLocation.Replace("EscapeFromTarkov.exe", "");
                managedPath += "EscapeFromTarkov_Data\\Managed\\";

                var sitLauncherAkiSupportManagedPath = App.ApplicationDirectory + "/AkiSupport/Managed";
                var sitLauncherAkiSupportBepinexPluginsPath = App.ApplicationDirectory + "/AkiSupport/Bepinex/Plugins";
                var sitLauncherAkiSupportBepinexPatchersPath = App.ApplicationDirectory + "/AkiSupport/Bepinex/Patchers";
                DirectoryInfo diAkiSupportManaged = new(sitLauncherAkiSupportManagedPath);
                DirectoryInfo diManaged = new(managedPath);

                if (diManaged.Exists && diAkiSupportManaged.Exists)
                {
                    List<FileInfo> fiAkiManagedFiles = Directory.GetFiles(sitLauncherAkiSupportManagedPath).Select(x => new FileInfo(x)).ToList();
                    foreach (var fileInfo in fiAkiManagedFiles)
                    {
                        var path = System.IO.Path.Combine(managedPath, fileInfo.Name);

                        // DO NOT OVERWRITE IF NEWER VERSION OF AKI EXISTS IN DIRECTORY
                        var existingFI = new FileInfo(path);
                        if (existingFI.Exists && existingFI.LastWriteTime > fileInfo.LastWriteTime)
                            continue;

                        fileInfo.CopyTo(path, true);
                    }
                }

                DirectoryInfo diAkiSupportBepinexPlugins = new(sitLauncherAkiSupportBepinexPluginsPath);
                DirectoryInfo diBepinex = new(bepinexPluginsPath);
                if (diBepinex.Exists && diAkiSupportBepinexPlugins.Exists)
                {
                    List<FileInfo> fiAkiBepinexPluginsFiles = Directory.GetFiles(sitLauncherAkiSupportBepinexPluginsPath).Select(x => new FileInfo(x)).ToList();

                    // Delete any existing plugins in BepInEx folder. They won't work with SIT.
                    List<FileInfo> fiAkiExistingPlugins = Directory.GetFiles(bepinexPluginsPath).Where(x => x.StartsWith("aki-", StringComparison.OrdinalIgnoreCase)).Select(x => new FileInfo(x)).ToList();
                    foreach (var fileInfo in fiAkiExistingPlugins)
                    {
                        fileInfo.Delete();
                    }

                    // Install any compatible Plugins from SIT Launcher
                    foreach (var fileInfo in fiAkiBepinexPluginsFiles)
                    {
                        var existingPath = System.IO.Path.Combine(bepinexPluginsPath, fileInfo.Name);

                        // DO NOT OVERWRITE IF NEWER VERSION OF AKI EXISTS IN DIRECTORY
                        var existingFI = new FileInfo(existingPath);
                        if (existingFI.Exists && existingFI.LastWriteTime > fileInfo.LastWriteTime)
                            continue;

                        fileInfo.CopyTo(existingPath, true);
                    }
                }

                List<FileInfo> fiAkiBepinexPatchersFiles = Directory.GetFiles(sitLauncherAkiSupportBepinexPatchersPath).Select(x => new FileInfo(x)).ToList();
                DirectoryInfo diBepinexPatchers = new(bepinexPatchersPath);
                DirectoryInfo diAkiSupportBepinexPatchersPlugins = new(sitLauncherAkiSupportBepinexPatchersPath);
                if (diBepinexPatchers.Exists && diAkiSupportBepinexPatchersPlugins.Exists)
                {
                    foreach (var fileInfo in fiAkiBepinexPatchersFiles)
                    {
                        //var existingPath = System.IO.Path.Combine(bepinexPatchersPath, fileInfo.Name); // Patcher is causing problems
                        var existingPath = System.IO.Path.Combine(bepinexPluginsPath, fileInfo.Name);

                        // DO NOT OVERWRITE IF NEWER VERSION OF AKI EXISTS IN DIRECTORY
                        var existingFI = new FileInfo(existingPath);
                        if (existingFI.Exists && existingFI.LastWriteTime > fileInfo.LastWriteTime)
                            continue;

                        fileInfo.CopyTo(existingPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to download and install Aki.{Environment.NewLine} {ex.Message}", "Error");
                return false;
            }

            return true;

        }



        private bool NeedsDeobfuscation(string exeLocation)
        {
            var parentPath = System.IO.Directory.GetParent(exeLocation);
            return !System.IO.Directory.EnumerateFiles(parentPath.FullName, "*Assembly-CSharp.dll.backup", SearchOption.AllDirectories).Any();
        }

        private void btnStartServer_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "Executable (Server.exe)|Server.exe;";
            if (openFileDialog.ShowDialog() == true)
            {
                //if(!Process.GetProcessesByName("Server").Any())
                Process.Start(openFileDialog.FileName, "");
            }
        }

        private void btnChangeOfflineInstallPath_Click(object sender, RoutedEventArgs e)
        {
            Config.InstallLocationEFT = null;
            BrowseForOfflineGame();
            Config.Save();
            this.DataContext = null;
            this.DataContext = this;
        }

        private void btnNewSITManagerAvailable_Click(object sender, RoutedEventArgs e)
        {
            var destinationurl = "https://github.com/stayintarkov/SIT.Manager/releases";
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void btnNewInstall_Click(object sender, RoutedEventArgs e)
        {
            NewInstallationWizard newInstallationWizard = new();
            newInstallationWizard.Show();
            newInstallationWizard.Topmost = true;
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        private async void btnDeobfuscateAssemblyBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "DLL (Assembly-CSharp)|Assembly-CSharp*.dll;";
            if (openFileDialog.ShowDialog() == true)
            {
                await loadingDialog.UpdateAsync("Deobfuscation", "");
                new PaulovDeobfuscator().DeobfuscateAssembly(openFileDialog.FileName, Directory.GetParent(openFileDialog.FileName).FullName, out var resultsRenamedClasses, doRemapping: true, logger: loadingDialog);
                await loadingDialog.UpdateAsync("", "");

            }
        }
    }
}
