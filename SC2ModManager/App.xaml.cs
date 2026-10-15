/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 18, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Services;
using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;
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

            // Catch-all for errors nothing else caught. Without this, one exception in any button
            // handler just closes the whole app with no message, which looks like a crash to people
            DispatcherUnhandledException += (s, args) =>
            {
                LogError("UI error", args.Exception);
                MessageBox.Show(
                    $"Something went wrong, but the mod manager is still running:\n\n{args.Exception.Message}\n\n" +
                    $"The details were saved to:\n{GetErrorLogPath()}",
                    "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Handled = the app keeps running instead of closing
                args.Handled = true;
            };

            // Background tasks that threw and nobody awaited. These don't close the app, just log them
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogError("Background task error", args.Exception);
                args.SetObserved();
            };

            // Errors on other threads can't be recovered from, but at least leave a log behind
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogError("Fatal error", ex);
            };

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

        private static string GetErrorLogPath()
        {
            try
            {
                return Path.Combine(Models.Globals.GetDataPath(), "Logs", "errors.log");
            }
            catch
            {
                // If even the data path can't be worked out, fall back to temp so we still log somewhere
                return Path.Combine(Path.GetTempPath(), "SC2ModManager_errors.log");
            }
        }

        private static void LogError(string kind, Exception ex)
        {
            try
            {
                string path = GetErrorLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // never throw from the error logger
            }
        }
    }

}
