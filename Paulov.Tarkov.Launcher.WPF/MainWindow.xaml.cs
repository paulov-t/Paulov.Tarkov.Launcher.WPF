using FolderBrowserEx;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
using Paulov.Launcher.Windows;
using Paulov.Tarkov.Deobfuscator.Lib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Tarkov.Deobfuscator;

namespace Paulov.Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, ILogger
    {
        public ImageSource SITIcon { get; set; }
        public ImageSource ArenaIcon { get; set; }
        public ImageSource BackgroundImage { get; set; }

        //public LauncherConfig Config { get; } = LauncherConfig.Instance;

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

        #region Check Installs
        public bool HasEFTInstalled { get; } = OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE) != null;
        public bool HasArenaInstalled { get; } = ArenaGameFinder.FindOfficialGame() != null;

        #endregion

        #region ReleasesBindings

        public static readonly DependencyProperty SITReleasesProperty = DependencyProperty.Register("SITReleases", typeof(ObservableCollection<Release>), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public ObservableCollection<Release> SITReleases
        {
            get => (ObservableCollection<Release>)GetValue(SITReleasesProperty);
            set => SetValue(SITReleasesProperty, value);
        }

        public static readonly DependencyProperty SelectedSITReleaseProperty = DependencyProperty.Register("SelectedSITRelease", typeof(Release), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public Release SelectedSITRelease
        {
            get => (Release)GetValue(SelectedSITReleaseProperty);
            set => SetValue(SelectedSITReleaseProperty, value);
        }

        public static readonly DependencyProperty ArenaReleasesProperty = DependencyProperty.Register("ArenaReleases", typeof(ObservableCollection<Release>), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public ObservableCollection<Release> ArenaReleases
        {
            get => (ObservableCollection<Release>)GetValue(ArenaReleasesProperty);
            set => SetValue(ArenaReleasesProperty, value);
        }

        public static readonly DependencyProperty SelectedArenaReleaseProperty = DependencyProperty.Register("SelectedArenaRelease", typeof(Release), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public Release SelectedArenaRelease
        {
            get => (Release)GetValue(SelectedArenaReleaseProperty);
            set => SetValue(SelectedArenaReleaseProperty, value);
        }

        public bool EFTInstalled => Config != null && !string.IsNullOrEmpty(Config.InstallLocationEFT) && new FileInfo(Config.InstallLocationEFT).Exists;

        public Visibility EFTInstalledVisibility => EFTInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EFTNotInstalledVisibility => !EFTInstalled ? Visibility.Visible : Visibility.Collapsed;


        public string EFTGameVersion => EFTInstalled
                ? FileVersionInfo.GetVersionInfo(Config.InstallLocationEFT).ProductVersion.Split('-')[0]
                + "." + FileVersionInfo.GetVersionInfo(Config.InstallLocationEFT).ProductVersion.Split('-')[1]
                 : "";

        public static readonly DependencyProperty AutoInstallSITProperty = DependencyProperty.Register("AutoInstallSIT", typeof(bool), typeof(MainWindow), new FrameworkPropertyMetadata(null));
        public bool AutoInstallSIT
        {
            get
            {
                GetValue(AutoInstallSITProperty);
                return Config.AutomaticallyInstallSIT;
            }
            set
            {
                SetValue(AutoInstallSITProperty, value);
                Config.AutomaticallyInstallSIT = value;
            }
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



        public string GetEFTSITPluginPath()
        {
            if (Config == null)
                return null;

            if (string.IsNullOrEmpty(Config.InstallLocationEFT))
                return null;

            return Path.Combine(GetBepInExPluginsPathInInstall(Config.InstallLocationEFT), "StayInTarkov.dll");
        }

        public string EFTSITVersion => File.Exists(GetEFTSITPluginPath())
               ? FileVersionInfo.GetVersionInfo(GetEFTSITPluginPath()).ProductVersion
                : "";

        #endregion

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
            //await GetLatestSITRelease();

            //if (Config.AutoCheckForOfficialUpdates)
            //{
            //    NewInstallFromOfficial();
            //    await UpdateInstallFromOfficial();
            //}

            this.DataContext = this;

            if (!Config.InstallInstances.Any())
            {
                NewInstallationWizard wizard = new();
                wizard.ShowDialog();
            }
        }



        private async Task GetLatestSITRelease()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("SIT-Launcher"));
                var user = await github.User.Get("paulov-t");
                SITReleases = new ObservableCollection<Release>(await github.Repository.Release.GetAll("stayintarkov", "StayInTarkov.Client", new ApiOptions() { }));
                SelectedSITRelease = SITReleases.OrderByDescending(x => x.CreatedAt).First();
            }
            catch (Exception)
            {

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

        private async Task UpdateInstallFromOfficial()
        {
            if (!IsGameUpdateAvailable())
                return;

            MessageBoxResult messageBoxResult = MessageBox.Show("New update of Official EFT detected. Would you like to update now? (BEWARE, THIS COULD BREAK SIT!)", "EFT Update Detected", MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                var exeLocation = string.Empty;
                exeLocation = await CopyInstallFromOfficial(
                    OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE)
                    , Directory.GetParent(Config.InstallLocationEFT).FullName
                    , exeLocation);
            }

        }

        private bool IsGameUpdateAvailable()
        {
            if (string.IsNullOrEmpty(Config.InstallLocationEFT))
                return false;

            if (OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE) == null)
            {
                try
                {
                    throw new Exception("Official EFT has not been found!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return false;
                }
            }

            // if the original assembly-csharp doesnt exist, false
            var officialAssemblyCSharpPath = new FileInfo(Path.Combine(Directory.GetParent(OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE).FullName).FullName, "EscapeFromTarkov_Data", "Managed", "Assembly-CSharp.dll"));
            if (!officialAssemblyCSharpPath.Exists)
                return false;

            // if the offline assembly-csharp doesn't exist, run update
            var offlineAssemblyCSharpPath = new FileInfo(Path.Combine(Directory.GetParent(Config.InstallLocationEFT).FullName, "EscapeFromTarkov_Data", "Managed", "Assembly-CSharp.dll"));
            if (!offlineAssemblyCSharpPath.Exists)
                return true;

            var officialIsUpdated = officialAssemblyCSharpPath.CreationTime > offlineAssemblyCSharpPath.LastWriteTime;
            return officialIsUpdated;
        }

        private async void NewInstallFromOfficial()
        {
            // Brand new setup of SIT
            if (string.IsNullOrEmpty(Config.InstallLocationEFT)

                // Config Install Location exists, but the install location looks suspiciuosly like a direct copy of Live
                // Check BepInEx
                || !DoesBepInExExistInInstall(Config.InstallLocationEFT)
                // Check SIT.Core
                || !IsSITCoreInstalled(Config.InstallLocationEFT)


                )
            {
                if (MessageBox.Show("No OFFLINE install found. Would you like to install now?", "Install", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var fiOfficialGame = OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE);
                    if (fiOfficialGame == null)
                        return;

                    FolderBrowserDialog folderBrowserDialogOffline = new();
                    folderBrowserDialogOffline.Title = "Select New Offline EFT Install Folder";
                    if (folderBrowserDialogOffline.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (fiOfficialGame.DirectoryName == folderBrowserDialogOffline.SelectedFolder)
                        {
                            MessageBox.Show("You cannot install OFFLINE into your Official Folder!", "Install");
                            NewInstallFromOfficial();
                            return;
                        }

                        var exeLocation = string.Empty;
                        exeLocation = await CopyInstallFromOfficial(fiOfficialGame, folderBrowserDialogOffline.SelectedFolder, exeLocation);
                    }
                }
            }
        }

        private async Task<string> CopyInstallFromOfficial(FileInfo fiOfficialGame, string offlineFolder, string exeLocation)
        {
            var officialFiles = Directory
                                        .GetFiles(fiOfficialGame.DirectoryName, "*", new EnumerationOptions() { RecurseSubdirectories = true })
                                        .Select(x => new FileInfo(x));

            var countOfOfficialFiles = officialFiles.Count();
            var currentNumber = 1;
            await loadingDialog.UpdateAsync("Installing", $"Found {countOfOfficialFiles} files to Copy", 0);
            foreach (var file in officialFiles)
            {
                try
                {
                    var percent = Math.Round((((decimal)currentNumber / (decimal)countOfOfficialFiles) * 100));
                    await loadingDialog.UpdateAsync("Installing", $"Copying file {currentNumber}/{countOfOfficialFiles} ({percent}%): {file.Name}", (int)percent);
                    var newFilePath = file.FullName.Replace(fiOfficialGame.DirectoryName, offlineFolder);
                    Directory.CreateDirectory(Directory.GetParent(newFilePath).FullName);

                    var fiNewFile = new FileInfo(newFilePath);
                    if (!fiNewFile.Exists || fiNewFile.LastWriteTime < file.LastWriteTime)
                    {
                        using (FileStream SourceStream = File.Open(file.FullName, System.IO.FileMode.Open))
                        {
                            using (FileStream DestinationStream = !fiNewFile.Exists ? File.Create(newFilePath) : File.Open(newFilePath, System.IO.FileMode.Open))
                            {
                                await SourceStream.CopyToAsync(DestinationStream);
                            }
                        }
                    }

                    if (newFilePath.Contains("EscapeFromTarkov.exe"))
                        exeLocation = newFilePath;

                }
                catch
                {

                }
                currentNumber++;
            }

            Config.InstallLocationEFT = offlineFolder + "\\EscapeFromTarkov.exe";
            this.DataContext = null;
            this.DataContext = this;

            // ----------------------------------------------------------------------------------------
            // Delete Deobfuscated and Backup Assembly CSharps
            await loadingDialog.UpdateAsync("Installing", $"Deleting old Assembly-CSharp backups");
            var offlineFolderFiles = Directory
                                        .GetFiles(offlineFolder, "Assembly-CSharp*", new EnumerationOptions() { RecurseSubdirectories = true })
                                        .Select(x => new FileInfo(x));
            foreach (var file in offlineFolderFiles)
            {
                if (file.FullName.EndsWith(".backup") || file.FullName.EndsWith("-cleaned.dll"))
                    file.Delete();
            }
            //
            // ----------------------------------------------------------------------------------------

            await loadingDialog.UpdateAsync("Installing", $"Cleaning EFT OFFLINE Directory");
            CleanupDirectory(exeLocation);

            await loadingDialog.UpdateAsync("Installing", $"Installing BepInEx");
            await DownloadAndInstallBepInEx5(exeLocation);

            //await loadingDialog.UpdateAsync("Installing", $"Deobfuscating Assembly-CSharp. This can take some time...");
            //await Deobfuscate(exeLocation);

            await loadingDialog.UpdateAsync("Installing", $"Installing StayInTarkov.Client");
            await DownloadAndInstallSIT(exeLocation);



            UpdateButtonText(null);

            await loadingDialog.UpdateAsync(null, null);
            return exeLocation;
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

        private async void btnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            Config.Save();

            var returnData = LoginToServer();

            if (string.IsNullOrEmpty(returnData))
            {
                var messageBoxResult = MessageBox.Show("Something went wrong. Maybe the server hasn't been started? Check the logs.", "Account");
                return;
            }

            // If all good, launch game with AID
            if (!string.IsNullOrEmpty(returnData) && returnData != "FAILED" && returnData != "ALREADY_IN_USE")
            {
                BrowseForOfflineGame();

                // Check that above actually did something
                if (!string.IsNullOrEmpty(Config.InstallLocationEFT) && Config.InstallLocationEFT.EndsWith(".exe"))
                {
                    await DownloadInstallAndStartGame(returnData);
                }

            }
            else if (returnData == "ALREADY_IN_USE")
            {
                var messageBoxResult = MessageBox.Show("The username/email has already been created, please use another one.", "Account");
            }
            else if (returnData.Length != 24) // NewId or something
            {
                var messageBoxResult = MessageBox.Show("Something went wrong. Maybe the server hasn't been started? Check the logs.", "Account");
            }

            if (Config.CloseLauncherAfterLaunch)
            {
                App.Current.Shutdown();
            }
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

        private async Task DownloadInstallAndStartGame(string sessionId)
        {


            //btnLaunchGame.IsEnabled = false;

            var installLocation = Config.InstallLocationEFT;
            if (!await DownloadAndInstallBepInEx5(installLocation))
            {
                MessageBox.Show("Install and Start aborted");
                return;
            }

            if (!await DownloadAndInstallSIT(installLocation))
            {
                MessageBox.Show("Install and Start aborted");
                return;
            }

            // Copy Aki Dlls for support
            if (!DownloadAndInstallAki(installLocation))
            {
                MessageBox.Show("Install and Start aborted");
                return;
            }


            // Deobfuscate Assembly-CSharp
            if (Config.AutomaticallyDeobfuscateDlls
                && NeedsDeobfuscation(installLocation))
            {
                MessageBox.Show("Your game has not been deobfuscated and no client mods have been installed to allow OFFLINE play. Please install SIT or manually deobfuscate using \"Tools->Deobfuscate And Remap Assembly\"");
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

        private async Task<bool> DownloadAndInstallSIT(string exeLocation, bool forceInstall = false)
        {
            if (!Config.AutomaticallyInstallSIT && IsSITCoreInstalled(exeLocation))
                return true;

            await loadingDialog.UpdateAsync("Installing SIT", $"Disovering files");

            var baseGamePath = Directory.GetParent(exeLocation).FullName;
            var bepinexPath = exeLocation.Replace("EscapeFromTarkov.exe", "");
            bepinexPath += "BepInEx";

            var bepinexPluginsPath = Path.Combine(bepinexPath, "plugins");
            if (!Directory.Exists(bepinexPluginsPath))
                return false;

            try
            {

                Release latestCore = SelectedSITRelease;

                var clientModsDeliveryPath = Path.Combine(App.ApplicationDirectory, "ClientMods");
                Directory.CreateDirectory(clientModsDeliveryPath);

                // Checks the current downloaded version and only downloads if needed
                if (File.Exists("CurrentSITVersion.txt"))
                {
                    var currentSITVersionText = File.ReadAllText("CurrentSITVersion.txt");
                }
                if (File.Exists("CurrentSITVersion.txt") && File.ReadAllText("CurrentSITVersion.txt") == latestCore.Name && !Config.ForceInstallLatestSIT)
                {
                    await loadingDialog.UpdateAsync(null, null);
                    return true;
                }

                var maxSize = 90000000;
                var allAssets = latestCore
                    .Assets
                    .Where(x => x.Size < maxSize)
                    .OrderByDescending(x => x.CreatedAt).DistinctBy(x => x.Name);

                await loadingDialog.UpdateAsync("Installing SIT", $"Downloading files");

                var allAssetsCount = allAssets.Count();
                var assetIndex = 0;

                var httpClient = new HttpClient();
                httpClient.Timeout = new TimeSpan(0, 5, 0);

                foreach (var asset in allAssets)
                {
                    var response = await httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseContentRead);
                    if (response != null)
                    {
                        var ms = new MemoryStream();
                        await response.Content.CopyToAsync(ms);

                        var deliveryPath = Path.Combine(clientModsDeliveryPath, asset.Name);
                        var fiDelivery = new FileInfo(deliveryPath);
                        await File.WriteAllBytesAsync(deliveryPath, ms.ToArray());
                    }
                    assetIndex++;
                    await loadingDialog.UpdateAsync("Installing SIT", $"Downloading files ({assetIndex}/{allAssetsCount})");
                }


                await loadingDialog.UpdateAsync("Installing SIT", $"Installing files");

                UpdateButtonText("Installing SIT");

                using (var z = ZipFile.OpenRead(Path.Combine(App.ApplicationDirectory, "ClientMods", "StayInTarkov-Release.zip")))
                {
                    foreach (var ent in z.Entries.Where(x => !string.IsNullOrEmpty(x.Name)))
                    {
                        ent.ExtractToFile(Path.Combine(App.ApplicationDirectory, "ClientMods", ent.Name), true);
                    }
                }
                File.Delete(Path.Combine(App.ApplicationDirectory, "ClientMods", "StayInTarkov-Release.zip"));

                foreach (var clientFile in Directory.GetFiles(Path.Combine(App.ApplicationDirectory, "ClientMods")).Where(x => !x.Contains("DONOTDELETE")))
                {
                    if (clientFile.Contains("Assembly-CSharp"))
                    {
                        var assemblyLocation = Path.Combine(Directory.GetParent(exeLocation).FullName, "EscapeFromTarkov_Data", "Managed", "Assembly-CSharp.dll");

                        // Backup the Assembly-CSharp and place the newest clean one
                        if (!File.Exists(assemblyLocation + ".backup"))
                        {
                            File.Copy(assemblyLocation, assemblyLocation + ".backup");
                            File.Copy(clientFile, assemblyLocation, true);
                        }

                        if (Config.ForceInstallLatestSIT)
                            File.Copy(clientFile, assemblyLocation, true);
                    }
                    else
                    {
                        bool shouldCopy = false;
                        var fiClientMod = new FileInfo(clientFile);
                        var fiExistingMod = new FileInfo(bepinexPluginsPath + "\\" + fiClientMod.Name);
                        if (fiExistingMod.Exists)
                        {
                            var createdDateOfDownloadedAsset = allAssets.First().CreatedAt;
                            shouldCopy = (fiExistingMod.LastWriteTime < createdDateOfDownloadedAsset);
                        }
                        else
                            shouldCopy = true;

                        if (Config.ForceInstallLatestSIT)
                            shouldCopy = true;

                        if (shouldCopy)
                            File.Copy(clientFile, bepinexPluginsPath + "\\" + fiClientMod.Name, true);
                    }
                }

                File.WriteAllText("CurrentSITVersion.txt", latestCore.Name);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to download and install SIT.{Environment.NewLine} {ex.Message}", "Error");
                return false;
            }

            await loadingDialog.UpdateAsync(null, null);

            return true;


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





        private void btnDeobfuscateBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "DLL (Assembly-CSharp)|Assembly-CSharp*.dll;";
            if (openFileDialog.ShowDialog() == true)
            {
                new PaulovDeobfuscator().DeobfuscateAssembly(openFileDialog.FileName, Directory.GetParent(openFileDialog.FileName).FullName, out var resultsRenamedClasses, doRemapping: true);
            }
        }



        private void btnServerCommand_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserEx.FolderBrowserDialog folderBrowserDialog = new();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Process p = new();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.FileName = "CMD.exe";
                p.StartInfo.Arguments = @"\C npm i";
                p.OutputDataReceived += process_OutputDataReceived;
                p.Start();
                p.WaitForExit();


                //p.StartInfo.FileName = @"c:\node\node.exe"; //Path to node installed folder****
                //string argument = @"\\ bundle\main.js";
                //p.StartInfo.Arguments = @argument;
                //p.Start();

                //Process.Start("CMD.exe", @"/C npm i");
                //Process.Start("CMD.exe", @"/C npm run run:server");

            }
        }

        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {


            });
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

        private async void btnLaunchArena_Click(object sender, RoutedEventArgs e)
        {
            Config.Save();

            var returnData = LoginToServer();

            if (string.IsNullOrEmpty(returnData))
            {
                var messageBoxResult = MessageBox.Show("Something went wrong. Maybe the server hasn't been started? Check the logs.", "Account");
                return;
            }

            // If all good, launch game with AID
            if (!string.IsNullOrEmpty(returnData) && returnData != "FAILED" && returnData != "ALREADY_IN_USE")
            {
                BrowseForOfflineGameArena();

                // Check that above actually did something
                if (!string.IsNullOrEmpty(Config.InstallLocationArena) && Config.InstallLocationArena.EndsWith(".exe"))
                {
                    await DownloadInstallAndStartArena(returnData);
                }

            }
            else if (returnData == "ALREADY_IN_USE")
            {
                var messageBoxResult = MessageBox.Show("The username/email has already been created, please use another one.", "Account");
            }
            else if (returnData.Length != 24) // NewId or something
            {
                var messageBoxResult = MessageBox.Show("Something went wrong. Maybe the server hasn't been started? Check the logs.", "Account");
            }

            if (Config.CloseLauncherAfterLaunch)
            {
                App.Current.Shutdown();
            }
        }

        private async void btnInstallGameCopy_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("No OFFLINE install found. Would you like to install now?", "Install", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var fiOfficialGame = OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE);
                if (fiOfficialGame == null)
                    return;

                FolderBrowserDialog folderBrowserDialogOffline = new();
                folderBrowserDialogOffline.Title = "Select New Offline EFT Install Folder";
                if (folderBrowserDialogOffline.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (fiOfficialGame.DirectoryName == folderBrowserDialogOffline.SelectedFolder)
                    {
                        MessageBox.Show("You cannot install OFFLINE into your Official Folder!", "Install");
                        NewInstallFromOfficial();
                        return;
                    }

                    var exeLocation = string.Empty;
                    exeLocation = await CopyInstallFromOfficial(fiOfficialGame, folderBrowserDialogOffline.SelectedFolder, exeLocation);
                }
            }
        }

        private async void btnCheckForOfficialUpdate_Click(object sender, RoutedEventArgs e)
        {
            await UpdateInstallFromOfficial();
        }

        private void btnSwitchEFTFolder_Click()
        {

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
