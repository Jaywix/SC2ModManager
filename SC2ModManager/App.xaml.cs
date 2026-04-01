using SC2ModManager.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace SC2ModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigService configService = new ConfigService();

            if (!configService.ConfigExists())
            {
                SetupWindow setupWindow = new SetupWindow();
                setupWindow.Show();
            }
            else
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }
    }

}
