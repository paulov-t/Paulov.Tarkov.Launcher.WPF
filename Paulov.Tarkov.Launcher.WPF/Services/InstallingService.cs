using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Paulov.Launcher.Services
{
    public class InstallingService
    {
        public async Task CopyInstallFromOfficial(DirectoryInfo diOfficialGame, string offlineFolder, LoadingDialog loadingDialog = null)
        {
            var officialFiles = Directory
                                        .GetFiles(diOfficialGame.FullName, "*", new EnumerationOptions() { RecurseSubdirectories = true })
                                        .Select(x => new FileInfo(x));

            var countOfOfficialFiles = officialFiles.Count();
            var currentNumber = 1;
            await loadingDialog?.UpdateAsync("Installing", $"Found {countOfOfficialFiles} files to Copy", 0);
            foreach (var file in officialFiles)
            {
                try
                {
                    var percent = Math.Round((((decimal)currentNumber / (decimal)countOfOfficialFiles) * 100));
                    await loadingDialog?.UpdateAsync("Installing", $"Copying file {currentNumber}/{countOfOfficialFiles} ({percent}%): {file.Name}", (int)percent);
                    var newFilePath = file.FullName.Replace(diOfficialGame.FullName, offlineFolder);
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
                }
                catch
                {

                }
                currentNumber++;
            }

            // ----------------------------------------------------------------------------------------
            // Delete Deobfuscated and Backup Assembly CSharps
            await loadingDialog?.UpdateAsync("Installing", $"Deleting old Assembly-CSharp backups");
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

            await loadingDialog?.UpdateAsync("Installing", $"Cleaning EFT OFFLINE Directory");
            CleanupDirectory(offlineFolder);

            //await loadingDialog.UpdateAsync("Installing", $"Installing BepInEx");
            //await DownloadAndInstallBepInEx5(exeLocation);

            //await loadingDialog.UpdateAsync("Installing", $"Installing StayInTarkov.Client");
            //await DownloadAndInstallSIT(exeLocation);

            await loadingDialog?.UpdateAsync(null, null);
        }

        public void CleanupDirectory(string directoryPath)
        {
            var battlEyeDirPath = Path.Combine(directoryPath, "BattlEye");
            if (Directory.Exists(battlEyeDirPath))
            {
                Directory.Delete(battlEyeDirPath, true);
            }

            var BEExePaths = Directory.GetFiles(directoryPath, "*_BE.exe");
            foreach (var beexepath in BEExePaths)
            {
                File.Delete(beexepath);
            }

            // Clean out the -cleaned.dll from Deobfuscation. This causes problems with PrePatchers
            var cleanedDllPaths = Directory.GetFiles(directoryPath, "*-cleaned.dll", new EnumerationOptions() { RecurseSubdirectories = true });
            foreach (var cleanedPath in cleanedDllPaths)
            {
                File.Delete(cleanedPath);
            }

            var uninstallPath = Path.Combine(directoryPath, "Uninstall.exe");
            if (File.Exists(uninstallPath))
            {
                File.Delete(uninstallPath);
            }
        }

        public string GetEFTSITPluginPath(string installDirectory)
        {
            if (string.IsNullOrEmpty(installDirectory))
                return null;

            return Path.Combine(GetBepInExPluginsPathInInstall(installDirectory), "StayInTarkov.dll");
        }

        public string GetBepInExPathInInstall(string installDirectory)
        {
            var bepinexPath = System.IO.Path.Combine(installDirectory, "BepInEx");
            return bepinexPath;
        }

        public string GetBepInExPluginsPathInInstall(string installDirectory)
        {
            var bepinexPluginsPath = System.IO.Path.Combine(GetBepInExPathInInstall(installDirectory), "plugins");
            return bepinexPluginsPath;
        }

        public bool DoesBepInExExistInInstall(string installDirectory)
        {
            // winhttp.dll is in the base directory
            var bepinexWinHttpDLL = Path.Combine(installDirectory, "winhttp.dll");

            var bepinexPath = GetBepInExPathInInstall(installDirectory);
            var bepinexCorePath = System.IO.Path.Combine(bepinexPath, "core");
            var bepinexPluginsPath = System.IO.Path.Combine(bepinexPath, "plugins");

            return (Directory.Exists(bepinexCorePath) && Directory.Exists(bepinexPluginsPath) && File.Exists(bepinexWinHttpDLL));
        }

        //public string CompiledAkiModulesUrl => "aHR0cHM6Ly9kcC1ldS5zaXRjb29wLm9yZy9ha2ktY3VzdG9tLnppcA==";

        public async Task InstallAkiPack(string installDirectory)
        {
            //var httpUrlB64 = CompiledAkiModulesUrl;
            //var httpUrlPlain = Encoding.UTF8.GetString(Convert.FromBase64String(httpUrlB64));

            //using var httpClient = new HttpClient();
            //var bytes = await httpClient.GetByteArrayAsync(httpUrlPlain);
            //if (File.Exists("AkiModules.zip"))
            //    File.Delete("AkiModules.zip");

            //await File.WriteAllBytesAsync("AkiModules.zip", bytes);

            //InstallZipPayloadToGameDirectory(installDirectory, "AkiModules.zip");
        }

        public void InstallZipPayloadToGameDirectory(string installDirectory, string zipPath)
        {
            using var fsZip = new FileStream(zipPath, FileMode.Open);
            ZipFile.ExtractToDirectory(fsZip, installDirectory);
        }

        public async Task DeleteInstall(string installDirectory)
        {
            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);

            LauncherConfig.Instance.InstallInstances.Remove(LauncherConfig.Instance.InstallInstances.FirstOrDefault(x => x.InstallLocationOffline == installDirectory));
            LauncherConfig.Instance.Save();
        }

        public async Task InstallBepInEx(string pathToInstallDirectory)
        {
            //var httpUrlB64 = CompiledAkiModulesUrl;
            var httpUrlPlain = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip";

            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(httpUrlPlain);
            if (File.Exists("BepInEx.zip"))
                File.Delete("BepInEx.zip");

            await File.WriteAllBytesAsync("BepInEx.zip", bytes);

            InstallZipPayloadToGameDirectory(pathToInstallDirectory, "BepInEx.zip");
        }
    }
}
