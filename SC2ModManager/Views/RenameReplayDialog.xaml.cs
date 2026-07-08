using System.Windows;
using System.Windows.Input;

namespace SC2ModManager.Views
{
    public partial class RenameReplayDialog : Window
    {
        public string NewName { get; private set; }
        public bool Confirmed { get; private set; }

        public RenameReplayDialog(string currentName)
        {
            InitializeComponent();
            NameBox.Text = currentName;
            NameBox.SelectAll();
            NameBox.Focus();
        }

        private void Rename_Click(object sender, RoutedEventArgs e) => TryConfirm();
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  TryConfirm();
            if (e.Key == Key.Escape) Close();
        }

        private void TryConfirm()
        {
            string name = NameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Name cannot be empty.");
                return;
            }

            // Reject characters that are invalid in file names
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalid) >= 0)
            {
                ShowError("Name contains invalid characters.");
                return;
            }

            NewName = name;
            Confirmed = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
