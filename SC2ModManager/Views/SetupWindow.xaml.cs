/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using Microsoft.Win32;
using SC2ModManager.Models;
using SC2ModManager.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Threading;

namespace SC2ModManager
{

    public partial class SetupWindow : Window
    {
        private int currentPage = 1;
        private const int totalPages = 4;

        private string validatedGamePath = null;
        private string validatedInstallPath = null;

        private readonly ConfigService configService = new();
        private readonly InstallService installService = new();

        public SetupWindow()
        {
            InitializeComponent();

            // Default install path
            InstallPathBox.Text = Globals.DefaultInstallPath;

            // Try to auto-detect game path
            string detected = configService.DetectGamePath();
            if (!string.IsNullOrEmpty(detected))
            {
                GamePathBox.Text = detected;
                ValidateGamePath(detected);
            }

            UpdateWizardState();
        }

        // ================= NAVIGATION =================

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAdvance()) return;

            if (currentPage == 3)
            {
                // Start install on page 4
                currentPage = 4;
                UpdateWizardState();
                _ = RunInstallAsync();
                return;
            }

            if (currentPage < totalPages)
            {
                currentPage++;
                UpdateWizardState();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                UpdateWizardState();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private bool CanAdvance()
        {
            return currentPage switch
            {
                1 => AcceptTermsCheckBox.IsChecked == true,
                2 => validatedGamePath != null,
                3 => !string.IsNullOrWhiteSpace(InstallPathBox.Text),
                4 => true,
                _ => false
            };
        }

        private void UpdateWizardState()
        {
            // Show/hide pages
            Page1.Visibility = currentPage == 1 ? Visibility.Visible : Visibility.Collapsed;
            Page2.Visibility = currentPage == 2 ? Visibility.Visible : Visibility.Collapsed;
            Page3.Visibility = currentPage == 3 ? Visibility.Visible : Visibility.Collapsed;
            Page4.Visibility = currentPage == 4 ? Visibility.Visible : Visibility.Collapsed;

            // Header
            WizardTitle.Text = currentPage switch
            {
                1 => "Welcome",
                2 => "Locate Game",
                3 => "Install Location",
                4 => "Installing",
                _ => ""
            };
            WizardSubtitle.Text = $"Step {currentPage} of {totalPages}";

            // Back button
            BackButton.Visibility = currentPage > 1 && currentPage < 4
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Next button text
            NextButton.Content = currentPage switch
            {
                3 => "Install",
                4 => "Finish",
                _ => "Next →"
            };

            // Disable Next if page 1 terms not accepted yet
            NextButton.IsEnabled = currentPage != 4 || InstallCompleteBorder.Visibility == Visibility.Visible;
        }

        // ================= PAGE 2: GAME PATH =================

        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select Supreme Commander 2 Folder" };
            if (dialog.ShowDialog() == true)
            {
                GamePathBox.Text = dialog.FolderName;
                ValidateGamePath(dialog.FolderName);
            }
        }

        private void ValidateGamePath(string path)
        {
            bool valid = Directory.Exists(Path.Combine(path, "bin")) &&
                         File.Exists(Path.Combine(path, "bin", "SupremeCommander2.exe")) &&
                         Directory.Exists(Path.Combine(path, "gamedata"));

            if (valid)
            {
                validatedGamePath = path;
                GamePathValidBorder.Visibility = Visibility.Visible;
                GamePathInvalidBorder.Visibility = Visibility.Collapsed;
                GamePathValidText.Text = $"✔  Valid game installation found at: {path}";
            }
            else
            {
                validatedGamePath = null;
                GamePathValidBorder.Visibility = Visibility.Collapsed;
                GamePathInvalidBorder.Visibility = Visibility.Visible;
                GamePathInvalidText.Text = "✘  This does not appear to be a valid Supreme Commander 2 installation. Make sure the folder contains bin\\SupremeCommander2.exe and a gamedata subfolder.";
            }
        }

        // ================= PAGE 3: INSTALL PATH =================

        private void BrowseInstallPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Choose Install Folder" };
            if (dialog.ShowDialog() == true)
                InstallPathBox.Text = dialog.FolderName;
        }

        // ================= PAGE 4: INSTALL =================

        private async Task RunInstallAsync()
        {
            string installFolder = InstallPathBox.Text.Trim();
            validatedInstallPath = installFolder;

            NextButton.IsEnabled = false;

            var progress = new Progress<(int percent, string status)>(report =>
            {
                InstallProgressBar.Value = report.percent;
                InstallFileText.Text = report.status;
                InstallStatusText.Text = $"Installing... {report.percent}%";
            });

            try
            {
                await installService.InstallToFolderAsync(installFolder, progress);

                // Save game path to config in the new location
                var config = new AppConfig { GamePath = validatedGamePath };
                configService.Save(config);

                // Snapshot the original gamedata files so the scan knows what belongs there
                try
                {
                    var presetService = new PresetService();
                    presetService.SaveOriginalFilesList(Path.Combine(validatedGamePath, "gamedata"));
                }
                catch(Exception e)
                {
                    // Don't do anything for now, maybe I'll implement something later
                }

                // Desktop shortcut
                if (DesktopShortcutCheckBox.IsChecked == true)
                {
                    string iconPath = Path.Combine(installFolder, "Assets", "Supreme_Commander_2_2.ico");
                    installService.CreateDesktopShortcut(installFolder, iconPath);
                }

                InstallStatusText.Text = "Installation complete!";
                InstallCompleteBorder.Visibility = Visibility.Visible;
                NextButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                InstallStatusText.Text = $"Installation failed: {ex.Message}";
                InstallFileText.Text = ex.ToString();
            }

            UpdateWizardState();
        }

        // ================= FINISH =================

        private void Finish()
        {
            // Relaunch from install location
            string newExe = Path.Combine(validatedInstallPath, Globals.ModManagerExecutableName);

            if (File.Exists(newExe) && newExe != System.Reflection.Assembly.GetExecutingAssembly().Location)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = newExe,
                    UseShellExecute = true
                });
                Application.Current.Shutdown();
            }
            else
            {
                // Already running from install location
                var main = new MainWindow();
                main.Show();
                Close();
            }
        }

        // Override Next click for the Finish case
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            NextButton.Click -= Next_Click;
            NextButton.Click += (s, ev) =>
            {
                if (currentPage == 4 && InstallCompleteBorder.Visibility == Visibility.Visible)
                    Finish();
                else
                    Next_Click(s, ev);
            };
        }
    }

}
