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

            if (!Models.Globals.IsSetupComplete())
            {
                var setup = new SetupWindow();
                setup.Show();
            }
            else
            {
                var configService = new ConfigService();
                var config = configService.Load();

                // Apply saved theme before any window opens
                if (!string.IsNullOrEmpty(config.Theme))
                {
                    var themeService = new Services.ThemeService(configService);
                    themeService.ApplyThemeResources(config.Theme);
                }

                if (!Models.Globals.IsSetupComplete())
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

}
