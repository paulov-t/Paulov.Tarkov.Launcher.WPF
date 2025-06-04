using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Paulov.Launcher.Models;
using Paulov.Launcher.Services;
using Paulov.Tarkov.Deobfuscator.Lib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tarkov.Deobfuscator;

namespace Paulov.Launcher.UserControls
{
    /// <summary>
    /// Interaction logic for InstallAndPlayControl.xaml
    /// </summary>
    public partial class InstallAndPlayControl : UserControl, ILogger
    {
        public ImageSource SITIcon { get; set; }
        public ImageSource ArenaIcon { get; set; }
        public ImageSource BackgroundImage { get; set; }

        public LauncherConfig Config => LauncherConfig.Instance;

        public InstallInstanceModel InstallInstanceConfig
        {
            get
            {
                return DataContext as InstallInstanceModel;
            }
        }

        public IEnumerable<ServerInstance> ServerInstances
        {
            get
            {
                return InstallInstanceConfig.ServerInstances.AsEnumerable();
            }
        }

        public ServerInstance ServerInstance
        {
            get
            {
                return InstallInstanceConfig.ServerInstances[0];
            }
        }


        #region ReleasesBindings

        public static readonly DependencyProperty ClientModReleasesProperty = DependencyProperty.Register(nameof(ClientModReleases), typeof(ObservableCollection<Release>), typeof(InstallAndPlayControl), new FrameworkPropertyMetadata(null));
        public ObservableCollection<Release> ClientModReleases
        {
            get => (ObservableCollection<Release>)GetValue(ClientModReleasesProperty);
            set => SetValue(ClientModReleasesProperty, value);
        }

        public static readonly DependencyProperty SelectedClientModReleaseProperty = DependencyProperty.Register(nameof(SelectedClientModRelease), typeof(Release), typeof(InstallAndPlayControl), new FrameworkPropertyMetadata(null));
        public Release SelectedClientModRelease
        {
            get => (Release)GetValue(SelectedClientModReleaseProperty);
            set => SetValue(SelectedClientModReleaseProperty, value);
        }

        #endregion


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



        #region Constructors

        public InstallAndPlayControl()
        {
            InitializeComponent();
            _ = GetLatestSITRelease();
        }

        #endregion

        private async Task GetLatestSITRelease()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("Paulov-Launcher"));
                ClientModReleases = new ObservableCollection<Release>(await github.Repository.Release.GetAll("paulov-t", "SPT.Multiplayer.Releases", new ApiOptions() { }));
                SelectedClientModRelease = ClientModReleases.OrderByDescending(x => x.CreatedAt).First();
            }
            catch (Exception)
            {

            }
        }

        private async void btnInstallAkiPack_Click(object sender, RoutedEventArgs e)
        {
            await new InstallingService().InstallAkiPack(InstallInstanceConfig.InstallLocationOffline);
        }

        private async void btnDeobfuscateAssembly_Click(object sender, RoutedEventArgs e)
        {
            new InstallingService().CleanupDirectory(InstallInstanceConfig.InstallLocationOffline);
            await loadingDialog.UpdateAsync("Deobfuscation", "");
            await Deobfuscate(InstallInstanceConfig.GetEXEPathOffline().FullName, doRemapping: true);
        }

        private async Task<bool> Deobfuscate(string exeLocation, bool createBackup = true, bool overwriteExisting = true, bool doRemapping = true)
        {
            var debobfuscator = new PaulovDeobfuscator();
            debobfuscator.LoggedStringBuilder.Clear();
            await Dispatcher.InvokeAsync(() =>
            {
            });
            await Dispatcher.InvokeAsync(() =>
            {
                btnDeobfuscateAssembly.IsEnabled = false;
            });
            loadingDialog.Update("Deobfuscating", "Deobfuscating");

            var resultsRenamedClasses = new HashSet<string>();
            var result = await debobfuscator.DeobfuscateAsync(exeLocation, resultsRenamedClasses, createBackup, overwriteExisting, doRemapping, loadingDialog);
            await Dispatcher.InvokeAsync(() =>
            {
                btnDeobfuscateAssembly.IsEnabled = true;
            });

            // Save Debofuscation Log to a time saved txt file in the DeObfus folder
            var debofusLogsPath = Path.Combine("DeObfus", "Logs");
            var deobfuscateLogPath = Path.Combine(debofusLogsPath, $"DeobfuscateLog_{DateTime.Now.ToString("yyyy-MM-dd-HH-mm")}.txt");
            // Quickly check DeObfus/Logs exists 
            Directory.CreateDirectory(debofusLogsPath);
            if (File.Exists(deobfuscateLogPath))
                File.Delete(deobfuscateLogPath);



            await File.WriteAllTextAsync(deobfuscateLogPath, debobfuscator.LoggedStringBuilder.ToString());
            //Deobfuscator.OnLog -= OnDeobfuscateLog;
            //txtDeobfuscateLog.ScrollToEnd();

            loadingDialog.Update("", "");

            return result;
        }


        public void Log(string message)
        {
        }

        private void btnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            Config.Save();

            var returnData = LoginToServer();
            // If it failed to Login. Just stop.
            if (string.IsNullOrEmpty(returnData))
                return;

            if (returnData.Length > 10 && returnData.StartsWith("{"))
            {
                var parsedbsg = JObject.Parse(returnData);
                returnData = parsedbsg["data"].ToString();
            }

            // If all good, launch game with AID
            if (!string.IsNullOrEmpty(returnData) && returnData != "FAILED" && returnData != "ALREADY_IN_USE")
            {
                // Launch game
                StartGame(returnData, InstallInstanceConfig.GetEXEPathOffline().FullName);
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

        private async void StartGame(string sessionId, string installLocationExe)
        {
            new InstallingService().CleanupDirectory(InstallInstanceConfig.InstallLocationOffline);

            var backendUrl = InstallInstanceConfig.ServerAddress;
            //backendUrl = backendUrl.Replace("http://", "");
            backendUrl = backendUrl.Replace("https://", "");
            var commandArgs = "-token={0} -config={\\\"BackendUrl\\\":\\\"{1}\\\",\\\"Version\\\":\\\"live\\\"}";
            commandArgs = commandArgs.Replace("{0}", sessionId);
            commandArgs = commandArgs.Replace("{1}", backendUrl);
            Process.Start(installLocationExe, commandArgs);
            Config.Save();
            App.Current.MainWindow.WindowState = WindowState.Minimized;

            await Task.Delay(10000);
        }

        private string LoginToServer()
        {
            if (string.IsNullOrEmpty(InstallInstanceConfig.ServerAddress))
            {
                MessageBox.Show("No Server Address Provided");
                return null;
            }

            if (InstallInstanceConfig.ServerAddress.EndsWith("/"))
            {
                MessageBox.Show("Server Address is incorrect, you should NOT have a / at the end!");
                return null;
            }
            TarkovRequesting requesting = new(null, InstallInstanceConfig.ServerAddress, false);
            try
            {
                // connect and get editions
                var returnDataConnect = requesting.PostJson("/launcher/server/connect", "{}");
                Stopwatch swPing = Stopwatch.StartNew();
                var pongResponse = requesting.PostJson("/launcher/ping", "{}");
                var ping = swPing.ElapsedMilliseconds;
                Debug.WriteLine($"Ping:{ping}ms");
                InstallInstanceConfig.Ping = ping;

                Dictionary<string, string> loginData = new();
                loginData.Add("username", InstallInstanceConfig.Username);
                loginData.Add("email", InstallInstanceConfig.Username);
                loginData.Add("edition", "Edge Of Darkness"); // default to EoD
                loginData.Add("password", InstallInstanceConfig.Password);
                // Add backendUrl to support people who connect locally
                loginData.Add("backendUrl", InstallInstanceConfig.ServerAddress);



                // attempt to login
                var returnData = requesting.PostJson("/launcher/profile/login", JsonConvert.SerializeObject(loginData));
                if (returnData.Length > 10 && returnData.StartsWith("{"))
                {
                    var parsedbsg = JObject.Parse(returnData);
                    returnData = parsedbsg["data"].ToString();
                }

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
                        if (InstallInstanceConfig.Username.Contains("Dev"))
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

        private async void btnUpdateFromOfficial_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InstallInstanceConfig.InstallLocationOfficial))
            {
                MessageBox.Show("Official Install Location has not been set!");
                return;
            }

            await new InstallingService().CopyInstallFromOfficial(new DirectoryInfo(InstallInstanceConfig.InstallLocationOfficial), InstallInstanceConfig.InstallLocationOffline, loadingDialog);
        }

        private async void btnDeleteUninstall_Click(object sender, RoutedEventArgs e)
        {
            await new InstallingService().DeleteInstall(new DirectoryInfo(InstallInstanceConfig.InstallLocationOffline).FullName);
        }

        private void btnInstallMod_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btnInstallBepInEx_Click(object sender, RoutedEventArgs e)
        {
            await new InstallingService().InstallBepInEx(new DirectoryInfo(InstallInstanceConfig.InstallLocationOffline).FullName);
            Directory.CreateDirectory(Path.Combine(InstallInstanceConfig.InstallLocationOffline, "BepInEx", "patchers"));
            Directory.CreateDirectory(Path.Combine(InstallInstanceConfig.InstallLocationOffline, "BepInEx", "plugins"));
        }

    }
}
