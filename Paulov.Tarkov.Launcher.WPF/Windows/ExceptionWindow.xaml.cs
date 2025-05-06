using MahApps.Metro.Controls;
using System;

namespace Paulov.Launcher.Windows
{
    /// <summary>
    /// Interaction logic for ExceptionWindow.xaml
    /// </summary>
    public partial class ExceptionWindow : MetroWindow
    {
        public ExceptionWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public Exception Exception { get; internal set; }
    }
}
