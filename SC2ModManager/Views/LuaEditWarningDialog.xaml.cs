using System.Windows;

namespace SC2ModManager.Views
{
    /// <summary>
    ///     Warning shown when switching the hotkey editor to Advanced (raw lua) mode.
    /// </summary>
    public partial class LuaEditWarningDialog : Window
    {
        public bool SuppressInFuture => DontShowAgainCheckBox.IsChecked == true;
        public bool Confirmed { get; private set; }

        public LuaEditWarningDialog()
        {
            InitializeComponent();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
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
