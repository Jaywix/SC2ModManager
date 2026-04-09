/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace SC2ModManager
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configService = new ConfigService();
            var config = configService.Load();

            if (config == null || string.IsNullOrEmpty(config.GamePath))
            {
                var setup = new SetupWindow();
                setup.Show();
            }
            else
            {
                var main = new MainWindow();
                main.Show();
            }
        }
    }

}
