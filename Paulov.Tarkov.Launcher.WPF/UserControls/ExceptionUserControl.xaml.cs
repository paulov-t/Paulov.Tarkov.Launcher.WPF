using System;
using System.Windows.Controls;

namespace Paulov.Launcher.UserControls
{
    /// <summary>
    /// Interaction logic for ExceptionUserControl.xaml
    /// </summary>
    public partial class ExceptionUserControl : UserControl
    {
        public ExceptionUserControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public Exception Exception { get; set; }

    }
}
