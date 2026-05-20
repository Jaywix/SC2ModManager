using System.Windows;

namespace SC2ModManager.Views
{
    public partial class ReplayWarningDialog : Window
    {
        public bool SuppressInFuture => DontShowAgainCheckBox.IsChecked == true;
        public bool Confirmed { get; private set; }

        public ReplayWarningDialog()
        {
            InitializeComponent();
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
