using System.Windows;

namespace SC2ModManager.Views
{
    public partial class LobbyPasswordWindow : Window
    {
        public string? EnteredPassword { get; private set; }

        public LobbyPasswordWindow(string? initialPassword = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialPassword))
                PasswordBox.Password = initialPassword;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
