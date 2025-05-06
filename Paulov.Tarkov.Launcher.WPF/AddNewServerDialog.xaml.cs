using MahApps.Metro.Controls;
using System.Windows;

namespace Paulov.Launcher
{
    /// <summary>
    /// Interaction logic for AddNewServerDialog.xaml
    /// </summary>
    public partial class AddNewServerDialog : MetroWindow
    {
        public ServerInstance Server { get; set; }
        public AddNewServerDialog()
        {
            InitializeComponent();
            Server = new ServerInstance();
            this.DataContext = this;
        }

        private void btnAddServer_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
