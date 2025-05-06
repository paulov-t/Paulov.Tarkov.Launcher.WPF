using Paulov.Tarkov.Deobfuscator.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Paulov.Launcher
{
    /// <summary>
    /// Interaction logic for LoadingDialog.xaml
    /// </summary>
    public partial class LoadingDialog : UserControl, ILogger
    {
        public LoadingDialog()
        {
            InitializeComponent();
            this.Visibility = Visibility.Collapsed;
        }

        public void SetProgressBarMaximum(int maximum)
        {
            pbar.Maximum = maximum;
        }

        public void Update(int progress)
        {
            Dispatcher.Invoke(() =>
            {
                pbar.Value = progress;
            });
        }

        public async Task UpdateAsync(int progress)
        {
            await Task.Run(() => { Update(progress); });
        }

        Random randomNumber = new();

        public void Hide()
        {
            Update(null, null);
        }

        public void ShowWithGenericMessage()
        {
            Dispatcher.Invoke(() =>
            {

                pbar.Value = randomNumber.Next(0, 100);
                lblLoadingSubtitle.Text = "Busy. Please Wait!";
                lblProgress.Text = "";

                this.Visibility = Visibility.Visible;
            });
        }

        public void Update(string loadingSubTitle, string loadingCurrentMessage)
        {
            Dispatcher.Invoke(() =>
            {

                pbar.Value = randomNumber.Next(0, 100);
                lblLoadingSubtitle.Text = string.IsNullOrEmpty(loadingSubTitle) ? "" : loadingSubTitle;
                lblProgress.Text = string.IsNullOrEmpty(loadingCurrentMessage) ? "" : loadingCurrentMessage;

                this.Visibility = string.IsNullOrEmpty(loadingSubTitle) && string.IsNullOrEmpty(loadingCurrentMessage) ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        public async Task<bool> UpdateAsync(string loadingSubTitle, string loadingCurrentMessage)
        {
            return await Task.Run(() => { Update(loadingSubTitle, loadingCurrentMessage); return true; });
        }



        public void Update(string loadingSubTitle, string loadingCurrentMessage, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                lblLoadingSubtitle.Text = string.IsNullOrEmpty(loadingSubTitle) ? "" : loadingSubTitle;
                lblProgress.Text = string.IsNullOrEmpty(loadingCurrentMessage) ? "" : loadingCurrentMessage;
                pbar.Value = progress;
                this.Visibility = string.IsNullOrEmpty(loadingSubTitle) && string.IsNullOrEmpty(loadingCurrentMessage) ? Visibility.Collapsed : Visibility.Visible;

            });
        }

        public async Task<bool> UpdateAsync(string loadingSubTitle, string loadingCurrentMessage, int progress, int progressMax = 100)
        {
            Dispatcher.Invoke(() =>
            {
                pbar.Maximum = progressMax;
            });

            return await Task.Run(() => { Update(loadingSubTitle, loadingCurrentMessage, progress); return true; });
        }

        public void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                Update(lblLoadingSubtitle.Text.ToString(), message);
            });
        }

        public void LogWarning(string text)
        {
            Update(lblLoadingSubtitle.Text.ToString(), text);
        }

        public void LogError(string text)
        {
            Update(lblLoadingSubtitle.Text.ToString(), text);
        }

    }
}
