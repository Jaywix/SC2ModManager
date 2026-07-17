/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 23, 2026
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
        private const int totalPages = 5;
        private string selectedTheme = AppTheme.Standard;

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

            if (currentPage == 4)
            {
                currentPage = 5;
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
                2 => true,
                3 => validatedGamePath != null,
                4 => !string.IsNullOrWhiteSpace(InstallPathBox.Text),
                5 => true,
                _ => false
            };
        }

        private void UpdateWizardState()
        {
            Page1.Visibility = currentPage == 1 ? Visibility.Visible : Visibility.Collapsed;
            Page2.Visibility = currentPage == 2 ? Visibility.Visible : Visibility.Collapsed;
            Page3.Visibility = currentPage == 3 ? Visibility.Visible : Visibility.Collapsed;
            Page4.Visibility = currentPage == 4 ? Visibility.Visible : Visibility.Collapsed;
            Page5.Visibility = currentPage == 5 ? Visibility.Visible : Visibility.Collapsed;

            WizardTitle.Text = currentPage switch
            {
                1 => "Welcome",
                2 => "Choose Theme",
                3 => "Locate Game",
                4 => "Install Location",
                5 => "Installing",
                _ => ""
            };
            WizardSubtitle.Text = $"Step {currentPage} of {totalPages}";

            BackButton.Visibility = currentPage > 1 && currentPage < 5
                ? Visibility.Visible
                : Visibility.Collapsed;

            NextButton.Content = currentPage switch
            {
                4 => "Install",
                5 => "Finish",
                _ => "Next →"
            };

            NextButton.IsEnabled = currentPage != 5 || InstallCompleteBorder.Visibility == Visibility.Visible;
        }

        // ================= PAGE 2: THEME SELECTION =================

        private void SelectThemeCard(string theme)
        {
            selectedTheme = theme;

            ThemeStandardCard.BorderBrush = theme == AppTheme.Standard
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

            ThemeUEFCard.BorderBrush = theme == AppTheme.UEF
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

            ThemeCybranCard.BorderBrush = theme == AppTheme.Cybran
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0x22, 0x00))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

            ThemeAeonCard.BorderBrush = theme == AppTheme.Aeon
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xBF, 0xA5))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));
        }

        private void ThemeCard_Standard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SelectThemeCard(AppTheme.Standard);

        private void ThemeCard_UEF_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SelectThemeCard(AppTheme.UEF);

        private void ThemeCard_Cybran_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SelectThemeCard(AppTheme.Cybran);

        private void ThemeCard_Aeon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SelectThemeCard(AppTheme.Aeon);

        // ================= PAGE 3: GAME PATH =================

        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select Supreme Commander 2 Folder" };

            // Start at whatever path is already in the box (like the auto-detected one), or fall
            // back to the usual Steam library location
            string steamCommon = @"C:\Program Files (x86)\Steam\steamapps\common";
            if (!string.IsNullOrWhiteSpace(GamePathBox.Text) && Directory.Exists(GamePathBox.Text))
                dialog.InitialDirectory = GamePathBox.Text;
            else if (Directory.Exists(steamCommon))
                dialog.InitialDirectory = steamCommon;

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

        // ================= PAGE 4: INSTALL PATH =================

        private void BrowseInstallPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Choose Install Folder" };

            // Start at the folder containing the current install path in the box. The path itself
            // usually doesn't exist yet (it gets created on install), so its parent is the spot.
            if (!string.IsNullOrWhiteSpace(InstallPathBox.Text))
            {
                string current = InstallPathBox.Text;
                string startAt = Directory.Exists(current) ? current : Path.GetDirectoryName(current);
                if (!string.IsNullOrEmpty(startAt) && Directory.Exists(startAt))
                    dialog.InitialDirectory = startAt;
            }

            if (dialog.ShowDialog() == true)
                InstallPathBox.Text = Path.Combine(dialog.FolderName, Globals.LauncherName);
        }

        // ================= PAGE 5: INSTALL =================

        private async Task RunInstallAsync()
        {
            string installFolder = InstallPathBox.Text.Trim();

            // Always ensure SC2ModManager is appended
            if (!installFolder.EndsWith(Globals.LauncherName, StringComparison.OrdinalIgnoreCase))
                installFolder = Path.Combine(installFolder, Globals.LauncherName);

            InstallPathBox.Text = installFolder;
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

                var config = new AppConfig
                {
                    GamePath = validatedGamePath,
                    Theme = selectedTheme
                };
                configService.Save(config);

                // Create startup presets based on what's currently in gamedata
                try
                {
                    var presetService = new PresetService();
                    string gameDataPath = Path.Combine(validatedGamePath, "gamedata");

                    var allFiles = Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(gameDataPath, f))
                        .ToList();

                    var originalFiles = allFiles
                        .Where(f => ModStorageService.IsOriginalGameFile(f))
                        .ToList();

                    presetService.SavePresetFromFileList("Original Game Files", originalFiles);

                    var unrecognizedFiles = allFiles
                        .Where(f => !ModStorageService.IsOriginalGameFile(f))
                        .ToList();

                    if (unrecognizedFiles.Any())
                        presetService.SavePresetFromFileList("Initial Gamedata State", allFiles);
                }
                catch { }

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
                if (currentPage == 5 && InstallCompleteBorder.Visibility == Visibility.Visible)
                    Finish();
                else
                    Next_Click(s, ev);
            };
        }
    }

}
