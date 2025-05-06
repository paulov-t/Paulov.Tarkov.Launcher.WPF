using FolderBrowserEx;
using MahApps.Metro.Controls;
using Paulov.Launcher.Models;
using Paulov.Launcher.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Paulov.Launcher.Windows
{
    /// <summary>
    /// Interaction logic for NewInstallationWizard.xaml
    /// </summary>
    public partial class NewInstallationWizard : MetroWindow
    {
        InstallInstanceModel InstallInstance { get; } = new InstallInstanceModel();

        public NewInstallationWizard()
        {
            InitializeComponent();
        }

        private async void btnBrowseOfficial_Click(object sender, RoutedEventArgs e)
        {
            var officialFile = OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE);
            this.Topmost = false;

            FolderBrowserDialog folderBrowserDialogOFFICIAL = new();
            folderBrowserDialogOFFICIAL.Title = "Select OFFICIAL install folder";
            folderBrowserDialogOFFICIAL.AllowMultiSelect = false;
            folderBrowserDialogOFFICIAL.InitialFolder = officialFile?.Directory.FullName;
            if (folderBrowserDialogOFFICIAL.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallInstance.InstallLocationOfficial = folderBrowserDialogOFFICIAL.SelectedFolder;
                await BrowseToOffline();
            }

        }

        private async void btnBrowseExisting_Click(object sender, RoutedEventArgs e)
        {
            await BrowseToOffline();
        }

        private async Task BrowseToOffline()
        {
            this.Topmost = false;

            var officialFiles =
                new[] {
                OfficialGameInstallChecker.FindOfficialGame(RegistryManager.EFTGamePathEXE)?.Directory.FullName
                , OfficialGameInstallChecker.FindOfficialGame(RegistryManager.ArenaGamePathEXE)?.Directory.FullName
                };

            FolderBrowserDialog folderBrowserDialogOFFLINE = new();
            folderBrowserDialogOFFLINE.Title = "Select OFFLINE install folder";
            folderBrowserDialogOFFLINE.AllowMultiSelect = false;
            if (folderBrowserDialogOFFLINE.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.Topmost = true;
                if (officialFiles.Contains(folderBrowserDialogOFFLINE.SelectedFolder))
                {
                    ExceptionWindow exceptionWindow = new();
                    exceptionWindow.Owner = this;
                    exceptionWindow.Exception = new System.Exception("You cannot install to the same location as the OFFICIAL game");
                    exceptionWindow.ShowDialog();
                    //exceptionDialog.Exception = new System.Exception("You cannot install to the same location as the OFFICIAL game");
                    //exceptionDialog.Visibility = Visibility.Visible;
                    return;
                }
                InstallInstance.InstallLocationOffline = folderBrowserDialogOFFLINE.SelectedFolder;

                if (!string.IsNullOrEmpty(InstallInstance.InstallLocationOfficial))
                {
                    var diOfficial = new DirectoryInfo(InstallInstance.InstallLocationOfficial);
                    if (diOfficial.Exists)
                        await new InstallingService().CopyInstallFromOfficial(diOfficial, InstallInstance.InstallLocationOffline, loadingDialog);
                }
            }

            // TODO: Check this Instance does not already exist
            LauncherConfig.Instance.InstallInstances.Add(InstallInstance);
            LauncherConfig.Instance.Save();
            this.Close();
        }
    }
}
