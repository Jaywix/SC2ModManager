using System.Windows;

namespace SC2ModManager.Views
{
    public enum RestoreGamedataChoice
    {
        Cancel,
        Restore,
        BackupFirstThenRestore
    }

    public partial class RestoreGamedataDialog : Window
    {
        public RestoreGamedataChoice Choice { get; private set; } = RestoreGamedataChoice.Cancel;

        public RestoreGamedataDialog(bool backupAlreadyExists)
        {
            InitializeComponent();

            if (backupAlreadyExists)
                ReplaceWarningText.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = RestoreGamedataChoice.Cancel;
            Close();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            Choice = RestoreGamedataChoice.Restore;
            Close();
        }

        private void BackupFirst_Click(object sender, RoutedEventArgs e)
        {
            Choice = RestoreGamedataChoice.BackupFirstThenRestore;
            Close();
        }
    }
}
