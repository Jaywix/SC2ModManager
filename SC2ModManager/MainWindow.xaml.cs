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
using SC2ModManager.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SC2ModManager
{
    public partial class MainWindow : Window
    {
        private MainViewModel vm;

        private readonly ThemeService themeService = new(new ConfigService());


        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
            DataContext = vm;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

            ShowView("Home");
            ApplyCurrentTheme();

            Loaded += async (s, e) => await vm.ScanGamedataForUnknownMods(silentOnMissingPath: true);
        }

        // ================= THEMES ===============
        public void ApplyCurrentTheme()
        {
            string theme = themeService.GetCurrentTheme();
            themeService.ApplyThemeResources(theme);
            UpdateThemeButtonHighlight(theme);
        }

        private void ThemeStandard_Click_Border(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            vm.ChangeTheme(AppTheme.Standard);
            ApplyCurrentTheme();
            UpdateThemeButtonHighlight(AppTheme.Standard);
        }

        private void ThemeUEF_Click_Border(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            vm.ChangeTheme(AppTheme.UEF);
            ApplyCurrentTheme();
            UpdateThemeButtonHighlight(AppTheme.UEF);
        }

        private void ThemeCybran_Click_Border(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            vm.ChangeTheme(AppTheme.Cybran);
            ApplyCurrentTheme();
            UpdateThemeButtonHighlight(AppTheme.Cybran);
        }

        private void ThemeAeon_Click_Border(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            vm.ChangeTheme(AppTheme.Aeon);
            ApplyCurrentTheme();
            UpdateThemeButtonHighlight(AppTheme.Aeon);
        }

        private void UpdateThemeButtonHighlight(string activeTheme)
        {
            var inactive = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

            ThemeStandardBtn.BorderBrush = activeTheme == AppTheme.Standard
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF))
                : inactive;

            ThemeUEFBtn.BorderBrush = activeTheme == AppTheme.UEF
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF))
                : inactive;

            ThemeCybranBtn.BorderBrush = activeTheme == AppTheme.Cybran
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0x22, 0x00))
                : inactive;

            ThemeAeonBtn.BorderBrush = activeTheme == AppTheme.Aeon
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xBF, 0xA5))
                : inactive;
        }

        // ================= NAVIGATION =================

        private void ShowView(string view)
        {
            HomeView.Visibility = Visibility.Collapsed;
            ModsView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            BackupsView.Visibility = Visibility.Collapsed;
            PresetsView.Visibility = Visibility.Collapsed;
            ComparePresetsView.Visibility = Visibility.Collapsed;
            InstalledModsView.Visibility = Visibility.Collapsed;
            InstalledMapsView.Visibility = Visibility.Collapsed;
            InstalledGenericModsView.Visibility = Visibility.Collapsed;
            ScanGamedataView.Visibility = Visibility.Collapsed;
            DownloadModsView.Visibility = Visibility.Collapsed;
            DownloadMapsView.Visibility = Visibility.Collapsed;
            DownloadGenericModsView.Visibility = Visibility.Collapsed;
            ManualImportView.Visibility = Visibility.Collapsed;
            FileLocationsView.Visibility = Visibility.Collapsed;
            HotkeyEditorView.Visibility = Visibility.Collapsed;
            PreviousVersionsView.Visibility = Visibility.Collapsed;

            switch (view)
            {
                case "Home": HomeView.Visibility = Visibility.Visible; break;
                case "Mods": ModsView.Visibility = Visibility.Visible; break;
                case "Settings": SettingsView.Visibility = Visibility.Visible; break;
                case "Backups": BackupsView.Visibility = Visibility.Visible; break;
                case "Presets": PresetsView.Visibility = Visibility.Visible; break;
                case "ComparePresets": ComparePresetsView.Visibility = Visibility.Visible; break;
                case "InstalledMods": InstalledModsView.Visibility = Visibility.Visible; break;
                case "InstalledMaps": InstalledMapsView.Visibility = Visibility.Visible; break;
                case "InstalledGenericMods": InstalledGenericModsView.Visibility = Visibility.Visible; break;
                case "ScanGamedata": ScanGamedataView.Visibility = Visibility.Visible; break;
                case "DownloadMods": DownloadModsView.Visibility = Visibility.Visible; break;
                case "DownloadMaps": DownloadMapsView.Visibility = Visibility.Visible; break;
                case "DownloadGenericMods": DownloadGenericModsView.Visibility = Visibility.Visible; break;
                case "ManualImport": ManualImportView.Visibility = Visibility.Visible; break;
                case "FileLocations": FileLocationsView.Visibility = Visibility.Visible; break;
                case "HotkeyEditor": HotkeyEditorView.Visibility = Visibility.Visible; break;
                case "PreviousVersions": PreviousVersionsView.Visibility = Visibility.Visible; break;
            }
        }

        private void GoHome(object sender, RoutedEventArgs e) => ShowView("Home");
        private void GoToMods(object sender, RoutedEventArgs e) => ShowView("Mods");
        private void GoToSettings(object sender, RoutedEventArgs e) => ShowView("Settings");
        private void GoToBackups(object sender, RoutedEventArgs e) => ShowView("Backups");
        private void GoToInstalledMods(object sender, RoutedEventArgs e) => ShowView("InstalledMods");
        private void GoToDownloadMods(object sender, RoutedEventArgs e) => ShowView("DownloadMods");
        private void GoToManualImport(object sender, RoutedEventArgs e) => ShowView("ManualImport");

        private void GoToHotkeyEditor(object sender, RoutedEventArgs e)
        {
            vm.HotkeyEditor.LoadNormalHotkeys();
            vm.HotkeyEditor.LoadBuildModeHotkeys();
            ShowView("HotkeyEditor");
        }

        private void GoToScanGamedata(object sender, RoutedEventArgs e)
        {
            _ = vm.ScanGamedataForUnknownMods();
            ShowView("ScanGamedata");
        }

        private void GoToPresets(object sender, RoutedEventArgs e)
        {
            vm.LoadPresets();
            ShowView("Presets");
        }

        private void GoToComparePresets(object sender, RoutedEventArgs e)
        {
            vm.LoadCompareOptions();
            ShowView("ComparePresets");
        }

        private void GoToInstalledMaps(object sender, RoutedEventArgs e)
        {
            vm.LoadInstalledMaps();
            ShowView("InstalledMaps");
        }

        private void GoToInstalledGenericMods(object sender, RoutedEventArgs e)
        {
            vm.LoadInstalledGenericMods();
            ShowView("InstalledGenericMods");
        }

        private async void GoToDownloadMaps(object sender, RoutedEventArgs e)
        {
            await vm.LoadDownloadableMapsAsync();
            ShowView("DownloadMaps");
        }

        private async void GoToDownloadGenericMods(object sender, RoutedEventArgs e)
        {
            await vm.LoadDownloadableGenericModsAsync();
            ShowView("DownloadGenericMods");
        }

        // ================= HOME =================

        private void LaunchGame_Click(object sender, RoutedEventArgs e) => vm.LaunchGame();
        private async void Update_Click(object sender, RoutedEventArgs e) => await vm.RunUpdater();

        private void NewsLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }

        private void HowToToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = HowToPanel.Visibility == Visibility.Visible;
            HowToPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            HowToToggleButton.Content = isVisible ? "▼  How to Use" : "▲  How to Use";
        }

        private void NewsItemToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NewsItemViewModel item)
                item.ToggleExpanded();
        }

        // ================= SETTINGS =================

        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select SupremeCommander2.exe",
                Filter = "SupremeCommander2.exe|SupremeCommander2.exe"
            };

            if (dialog.ShowDialog() != true) return;
            GamePathInput.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
        }

        private void SetGamePath_Click(object sender, RoutedEventArgs e)
        {
            string path = GamePathInput.Text?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Please enter or browse for a path.", "No Path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                vm.SetGamePath(path);
                GamePathInput.Text = string.Empty;
                MessageBox.Show("Game path updated successfully.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting game path: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                GamePathInput.Text = string.Empty;
            }
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e) => vm.Uninstall();

        private async void SeePreviousVersions_Click(object sender, RoutedEventArgs e)
        {
            // Clear any previous state
            var staticChildren = new System.Windows.UIElement[]
            {
                PreviousVersionsLoadingText,
                PreviousVersionsErrorText
            };
            var toRemove = new System.Collections.Generic.List<System.Windows.UIElement>();
            foreach (System.Windows.UIElement child in PreviousVersionsListPanel.Children)
                if (!System.Array.Exists(staticChildren, c => c == child))
                    toRemove.Add(child);
            foreach (var child in toRemove)
                PreviousVersionsListPanel.Children.Remove(child);

            PreviousVersionsLoadingText.Visibility = Visibility.Visible;
            PreviousVersionsErrorText.Visibility = Visibility.Collapsed;

            ShowView("PreviousVersions");

            try
            {
                var releases = await vm.GetPreviousReleasesAsync();
                PreviousVersionsLoadingText.Visibility = Visibility.Collapsed;

                if (releases == null || releases.Count == 0)
                {
                    PreviousVersionsErrorText.Text = "No previous versions are available for restore.";
                    PreviousVersionsErrorText.Visibility = Visibility.Visible;
                    return;
                }

                foreach (var release in releases)
                    PreviousVersionsListPanel.Children.Add(BuildReleaseCard(release));
            }
            catch (Exception ex)
            {
                PreviousVersionsLoadingText.Visibility = Visibility.Collapsed;
                PreviousVersionsErrorText.Text = $"Failed to load versions: {ex.Message}";
                PreviousVersionsErrorText.Visibility = Visibility.Visible;
            }
        }

        private System.Windows.UIElement BuildReleaseCard(SC2ModManager.Models.ReleaseInfo release)
        {
            var card = new System.Windows.Controls.Border
            {
                Style = (System.Windows.Style)TryFindResource("ContentPanel"),
                Margin = new System.Windows.Thickness(0, 0, 0, 10),
                Padding = new System.Windows.Thickness(16, 12, 16, 14)
            };

            var outer = new System.Windows.Controls.Grid();
            outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Header row: version label + restore button
            var headerGrid = new System.Windows.Controls.Grid();
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

            var versionLabel = new System.Windows.Controls.TextBlock
            {
                Text = release.TagName,
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)TryFindResource("AccentBrush") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetColumn(versionLabel, 0);

            var restoreBtn = new System.Windows.Controls.Button
            {
                Content = "Restore this Version",
                Tag = release,
                Style = (System.Windows.Style)TryFindResource("ModernButton"),
                MinWidth = 155,
                FontWeight = System.Windows.FontWeights.SemiBold
            };
            restoreBtn.Click += ReleaseCard_RestoreClick;
            System.Windows.Controls.Grid.SetColumn(restoreBtn, 1);

            headerGrid.Children.Add(versionLabel);
            headerGrid.Children.Add(restoreBtn);
            System.Windows.Controls.Grid.SetRow(headerGrid, 0);

            var sep = new System.Windows.Controls.Separator
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x36, 0x3D)),
                Margin = new System.Windows.Thickness(0, 8, 0, 8),
                Height = 1
            };
            System.Windows.Controls.Grid.SetRow(sep, 1);

            var bodyText = new System.Windows.Controls.TextBlock
            {
                Text = string.IsNullOrWhiteSpace(release.Body)
                    ? "No release notes provided."
                    : release.Body.Replace("\r\n", "\n"),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 12,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                LineHeight = 18
            };
            System.Windows.Controls.Grid.SetRow(bodyText, 2);

            outer.Children.Add(headerGrid);
            outer.Children.Add(sep);
            outer.Children.Add(bodyText);
            card.Child = outer;
            return card;
        }

        private async void ReleaseCard_RestoreClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SC2ModManager.Models.ReleaseInfo release)
            {
                var confirm = MessageBox.Show(
                    $"You are about to restore SC2 Mod Manager to {release.TagName}.\n\n" +
                    "\u26a0 Warning: Rolling back to an older version may cause issues:\n" +
                    "  \u2022 Some mods may stop working correctly\n" +
                    "  \u2022 Features added in newer versions will be unavailable\n" +
                    "  \u2022 Configuration saved by newer versions may not be compatible\n\n" +
                    "The application will close and restart after the restore.\n\n" +
                    "Are you sure you want to restore this version?",
                    $"Restore {release.TagName}?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                btn.IsEnabled = false;
                btn.Content = "Restoring...";

                await vm.RestoreVersionAsync(release.DownloadUrl, release.TagName);
            }
        }

        // ================= FILE LOCATIONS =================

        private void GoToFileLocations(object sender, RoutedEventArgs e)
        {
            vm.InitializeFileLocations();
            ShowView("FileLocations");
        }

        private void OpenReplaysFolder_Click(object sender, RoutedEventArgs e)
            => vm.OpenFolder(vm.ReplaysPath);

        private void OpenGameDataFolder_Click(object sender, RoutedEventArgs e)
            => vm.OpenFolder(vm.GameDataPath);

        private void OpenGamePrefsFolder_Click(object sender, RoutedEventArgs e)
            => vm.OpenFolder(vm.GamePrefsFolder);

        private void BrowseReplaysFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select your Supreme Commander 2 Replays folder" };
            if (dialog.ShowDialog() == true)
                vm.ReplaysPath = dialog.FolderName;
        }

        private void BrowseGamePrefsFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select the folder containing Game.prefs"
            };

            if (dialog.ShowDialog() != true) return;

            if (!File.Exists(Path.Combine(dialog.FolderName, "Game.prefs")))
            {
                MessageBox.Show(
                    "Game.prefs was not found in that folder. Please select the folder that contains Game.prefs directly.",
                    "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            vm.GamePrefsFolder = dialog.FolderName;
        }

        // ================= BACKUPS =================

        private async void RestoreOriginalGameData_Click(object sender, RoutedEventArgs e)
            => await vm.RestoreOriginalGamedataAsync();

        // ================= PRESETS =================

        private void SavePreset_Click(object sender, RoutedEventArgs e) => vm.SaveCurrentStateAsPreset();
        private void ApplyPreset_Click(object sender, RoutedEventArgs e) => vm.ApplySelectedPreset();
        private void DeletePreset_Click(object sender, RoutedEventArgs e) => vm.DeleteSelectedPreset();

        private void ViewPresetFiles_Click(object sender, RoutedEventArgs e)
        {
            if (vm.SelectedPreset == null) { MessageBox.Show("No preset selected."); return; }

            var files = vm.SelectedPreset.Files;
            string msg = files.Count == 0 ? "No files in this preset." : string.Join("\n", files);

            MessageBox.Show(msg, $"Files in '{vm.SelectedPreset.Name}'",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================= COMPARE =================

        private void RunComparison_Click(object sender, RoutedEventArgs e) => vm.RunComparison();

        // ================= INSTALLED: MAPS =================

        // Clicking the row highlights it (detail panel updates via SelectedItem binding).
        // Clicking the checkbox checks it (used for enable/disable/delete operations).
        // e.Handled = true stops the checkbox click from also toggling ListBoxItem.IsSelected.
        private void MapCheckBox_Click(object sender, RoutedEventArgs e) => e.Handled = true;

        private void EnableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.DisabledMaps.Where(m => m.IsChecked).ToList();
            vm.EnableSelectedMaps(selected);
            foreach (var m in selected) m.IsChecked = false;
            vm.RefreshInstalledMapFilters();
        }

        private void DisableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.EnabledMaps.Where(m => m.IsChecked).ToList();
            vm.DisableSelectedMaps(selected);
            foreach (var m in selected) m.IsChecked = false;
            vm.RefreshInstalledMapFilters();
        }

        private void EnableAllMaps_Click(object sender, RoutedEventArgs e)
        {
            vm.EnableAllMaps();
            foreach (var m in vm.EnabledMaps) m.IsChecked = false;
            vm.RefreshInstalledMapFilters();
        }

        private void DisableAllMaps_Click(object sender, RoutedEventArgs e)
        {
            vm.DisableAllMaps();
            foreach (var m in vm.DisabledMaps) m.IsChecked = false;
            vm.RefreshInstalledMapFilters();
        }

        private void SaveMaps_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveMapsToGamedata();
            MessageBox.Show("Maps saved successfully.");
        }

        private void DeleteSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.EnabledMaps.Concat(vm.DisabledMaps)
                .Where(m => m.IsChecked).ToList();

            if (!selected.Any()) { MessageBox.Show("No maps selected."); return; }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} map(s) from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DisableSelectedMaps(selected.Where(m => m.IsEnabled).ToList());
            vm.SaveMapsToGamedata();
            foreach (var map in selected)
                vm.UninstallMap(map);

            vm.CleanupPresetsAfterDeletion(selected.Select(m => m.FileName));
            vm.RefreshInstalledMapFilters();
        }

        private void DeleteAllMaps_Click(object sender, RoutedEventArgs e)
        {
            var allMaps = vm.EnabledMaps.Concat(vm.DisabledMaps).ToList();
            if (!allMaps.Any()) { MessageBox.Show("No maps installed."); return; }

            var confirm = MessageBox.Show(
                "Delete ALL installed maps from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DisableAllMaps();
            vm.SaveMapsToGamedata();
            vm.UninstallAllMaps();
            vm.CleanupPresetsAfterDeletion(allMaps.Select(m => m.FileName));
            vm.RefreshInstalledMapFilters();
        }

        // ================= INSTALLED: GENERIC MODS =================

        private void GenericModCheckBox_Click(object sender, RoutedEventArgs e) => e.Handled = true;

        private void EnableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.DisabledGenericMods.Where(m => m.IsChecked).ToList();
            vm.EnableSelectedGenericMods(selected);
            foreach (var m in selected) m.IsChecked = false;
            vm.RefreshInstalledGenericModSort();
        }

        private void DisableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.EnabledGenericMods.Where(m => m.IsChecked).ToList();
            vm.DisableSelectedGenericMods(selected);
            foreach (var m in selected) m.IsChecked = false;
            vm.RefreshInstalledGenericModSort();
        }

        private void EnableAllGenericMods_Click(object sender, RoutedEventArgs e) => vm.EnableAllGenericMods();
        private void DisableAllGenericMods_Click(object sender, RoutedEventArgs e) => vm.DisableAllGenericMods();

        private void SaveGenericMods_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveGenericModsToGamedata();
            MessageBox.Show("Generic mods saved successfully.");
        }

        private void DeleteSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.EnabledGenericMods
                .Concat(vm.DisabledGenericMods)
                .Where(m => m.IsChecked)
                .ToList();

            if (!selected.Any()) { MessageBox.Show("No mods checked. Check the boxes next to the mods you want to delete."); return; }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} mod(s) from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DisableSelectedGenericMods(selected.Where(m => m.IsEnabled).ToList());
            vm.SaveGenericModsToGamedata();
            foreach (var mod in selected)
                vm.UninstallGenericMod(mod);

            vm.CleanupPresetsAfterDeletion(selected.Select(m => m.FileName));
            vm.RefreshInstalledGenericModSort();
        }

        private void DeleteAllGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var allMods = vm.EnabledGenericMods.Concat(vm.DisabledGenericMods).ToList();
            if (!allMods.Any()) { MessageBox.Show("No mods installed."); return; }

            var confirm = MessageBox.Show(
                "Delete ALL installed generic mods from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DisableAllGenericMods();
            vm.SaveGenericModsToGamedata();
            vm.UninstallAllGenericMods();
            vm.CleanupPresetsAfterDeletion(allMods.Select(m => m.FileName));
        }

        // ================= SCAN GAMEDATA =================

        private void RunScan_Click(object sender, RoutedEventArgs e)
            => _ = vm.ScanGamedataForUnknownMods();

        private void SelectAllScanResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanResults) item.IsSelected = true;
        }

        private void DeselectAllScanResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanResults) item.IsSelected = false;
        }

        private async void ImportScanResults_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.ScanResults.Where(r => r.IsSelected).ToList();

            if (!selected.Any())
            {
                MessageBox.Show("No files selected.", "Nothing to Import",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Import {selected.Count} file(s) as Generic Gamedata Mods?\n\n" +
                "These will appear in Installed → Generic Gamedata Mods as Enabled.\n\n" +
                "Warning: If you later delete them from the mod manager, they cannot be automatically restored.",
                "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            await vm.ImportSelectedScanResultsAsync(selected);
            _ = vm.ScanGamedataForUnknownMods();
        }

        private void SelectAllMatchResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanMatchResults) item.IsSelected = true;
        }

        private void DeselectAllMatchResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanMatchResults) item.IsSelected = false;
        }

        private async void ImportMatchedMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.ScanMatchResults.Where(r => r.IsSelected).ToList();

            if (!selected.Any())
            {
                MessageBox.Show("No mods selected.", "Nothing to Import",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Import {selected.Count} mod(s) with full metadata?\n\n" +
                "These will appear in Installed as Enabled and can be managed normally.",
                "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            await vm.ImportMetadataForMatchedMods(selected);
            _ = vm.ScanGamedataForUnknownMods();
        }

        private void DeleteMatchedMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.ScanMatchResults.Where(r => r.IsSelected).ToList();

            if (!selected.Any())
            {
                MessageBox.Show("No mods selected.", "Nothing to Delete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} file(s) from gamedata?\n\n" +
                "You can re-download them anytime from the Download screen.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DeleteMatchedMods(selected);
            _ = vm.ScanGamedataForUnknownMods();
        }

        private void DeleteUnknownFiles_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.ScanResults.Where(r => r.IsSelected).ToList();

            if (!selected.Any())
            {
                MessageBox.Show("No files selected.", "Nothing to Delete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Permanently delete {selected.Count} file(s) from gamedata?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DeleteUnknownFiles(selected);
            _ = vm.ScanGamedataForUnknownMods();
        }

        // ================= DOWNLOAD: MAPS =================

        private void SelectAllDownloadMaps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var m in vm.FilteredDownloadableMaps) m.IsChecked = true;
        }

        private void DeselectAllDownloadMaps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var m in vm.FilteredDownloadableMaps) m.IsChecked = false;
        }

        private async void DownloadSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.FilteredDownloadableMaps.Where(m => m.IsChecked).ToList();
            if (!selected.Any()) { MessageBox.Show("No maps selected."); return; }

            await vm.DownloadSelectedMapsAsync(selected);
            foreach (var m in selected) m.IsChecked = false;
        }

        // ================= DOWNLOAD: GENERIC MODS =================

        private void SelectAllDownloadGenericMods_Click(object sender, RoutedEventArgs e)
        {
            foreach (var m in vm.DownloadableGenericMods) m.IsChecked = true;
        }

        private void DeselectAllDownloadGenericMods_Click(object sender, RoutedEventArgs e)
        {
            foreach (var m in vm.DownloadableGenericMods) m.IsChecked = false;
        }

        private async void DownloadSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = vm.DownloadableGenericMods.Where(m => m.IsChecked).ToList();
            if (!selected.Any()) { MessageBox.Show("No mods selected."); return; }

            await vm.DownloadSelectedGenericModsAsync(selected);
            foreach (var m in selected) m.IsChecked = false;
        }

        // ================= SORT =================

        private void InstalledMapSortName_Click(object sender, RoutedEventArgs e)
        {
            vm.InstalledMapSort.SortByName();
            vm.RefreshInstalledMapFilters();
        }

        private void InstalledMapSortDate_Click(object sender, RoutedEventArgs e)
        {
            vm.InstalledMapSort.SortByDate();
            vm.RefreshInstalledMapFilters();
        }

        private void DownloadMapSortName_Click(object sender, RoutedEventArgs e)
        {
            vm.DownloadMapSort.SortByName();
            vm.RefreshDownloadMapFilters();
        }

        private void DownloadMapSortDate_Click(object sender, RoutedEventArgs e)
        {
            vm.DownloadMapSort.SortByDate();
            vm.RefreshDownloadMapFilters();
        }

        private void InstalledGenericModSortName_Click(object sender, RoutedEventArgs e)
        {
            vm.InstalledGenericModSort.SortByName();
            vm.RefreshInstalledGenericModSort();
        }

        private void InstalledGenericModSortDate_Click(object sender, RoutedEventArgs e)
        {
            vm.InstalledGenericModSort.SortByDate();
            vm.RefreshInstalledGenericModSort();
        }

        private void DownloadGenericModSortName_Click(object sender, RoutedEventArgs e)
        {
            vm.DownloadGenericModSort.SortByName();
            vm.RefreshDownloadGenericModSort();
        }

        private void DownloadGenericModSortDate_Click(object sender, RoutedEventArgs e)
        {
            vm.DownloadGenericModSort.SortByDate();
            vm.RefreshDownloadGenericModSort();
        }

        // ================= MAP FILTERS =================

        private void InstalledMapFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (vm == null) return;
            vm.RefreshInstalledMapFilters();
        }

        private void DownloadMapFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (vm == null) return;
            vm.RefreshDownloadMapFilters();
        }

        private void ClearInstalledMapFilter_Click(object sender, RoutedEventArgs e)
        {
            vm.InstalledMapsFilter.ClearAll();
            vm.RefreshInstalledMapFilters();
        }

        private void ClearDownloadMapFilter_Click(object sender, RoutedEventArgs e)
        {
            vm.DownloadMapsFilter.ClearAll();
            vm.RefreshDownloadMapFilters();
        }

        private void InstalledFiltersToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = InstalledFiltersPanel.Visibility == Visibility.Visible;
            InstalledFiltersPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            InstalledFiltersToggle.Content = isVisible ? "▼  Show Filters" : "▲  Hide Filters";
        }

        private void DownloadFiltersToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = DownloadFiltersPanel.Visibility == Visibility.Visible;
            DownloadFiltersPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            DownloadFiltersToggle.Content = isVisible ? "▼  Show Filters" : "▲  Hide Filters";
        }

        //private PlayerCountOperator GetInstalledPlayerOperator()
        //{
        //    if (OpAny.IsChecked == true) return PlayerCountOperator.Any;

        //    var panel = OpAny.Parent as StackPanel;
        //    if (panel == null) return PlayerCountOperator.Any;

        //    foreach (var child in panel.Children.OfType<RadioButton>())
        //    {
        //        if (child.IsChecked != true) continue;
        //        return child.Content?.ToString() switch
        //        {
        //            "=" => PlayerCountOperator.Equal,
        //            ">" => PlayerCountOperator.GreaterThan,
        //            "<" => PlayerCountOperator.LessThan,
        //            ">=" => PlayerCountOperator.GreaterThanOrEqual,
        //            "<=" => PlayerCountOperator.LessThanOrEqual,
        //            _ => PlayerCountOperator.Any
        //        };
        //    }
        //    return PlayerCountOperator.Any;
        //}

        //private PlayerCountOperator GetDownloadPlayerOperator()
        //{
        //    if (DownloadOpAny.IsChecked == true) return PlayerCountOperator.Any;

        //    var panel = DownloadOpAny.Parent as StackPanel;
        //    if (panel == null) return PlayerCountOperator.Any;

        //    foreach (var child in panel.Children.OfType<RadioButton>())
        //    {
        //        if (child.IsChecked != true) continue;
        //        return child.Content?.ToString() switch
        //        {
        //            "=" => PlayerCountOperator.Equal,
        //            ">" => PlayerCountOperator.GreaterThan,
        //            "<" => PlayerCountOperator.LessThan,
        //            ">=" => PlayerCountOperator.GreaterThanOrEqual,
        //            "<=" => PlayerCountOperator.LessThanOrEqual,
        //            _ => PlayerCountOperator.Any
        //        };
        //    }
        //    return PlayerCountOperator.Any;
        //}

        //// ================= PLAYER COUNT STEPPERS =================

        //private void InstalledPlayerCountDown_Click(object sender, RoutedEventArgs e)
        //{
        //    if (vm.InstalledMapsFilter.PlayerCountValue > 2)
        //    {
        //        vm.InstalledMapsFilter.PlayerCountValue--;
        //        InstalledPlayerCountText.Text = vm.InstalledMapsFilter.PlayerCountValue.ToString();
        //        vm.RefreshInstalledMapFilters();
        //    }
        //}

        //private void InstalledPlayerCountUp_Click(object sender, RoutedEventArgs e)
        //{
        //    if (vm.InstalledMapsFilter.PlayerCountValue < 8)
        //    {
        //        vm.InstalledMapsFilter.PlayerCountValue++;
        //        InstalledPlayerCountText.Text = vm.InstalledMapsFilter.PlayerCountValue.ToString();
        //        vm.RefreshInstalledMapFilters();
        //    }
        //}

        //private void DownloadPlayerCountDown_Click(object sender, RoutedEventArgs e)
        //{
        //    if (vm.DownloadMapsFilter.PlayerCountValue > 2)
        //    {
        //        vm.DownloadMapsFilter.PlayerCountValue--;
        //        DownloadPlayerCountText.Text = vm.DownloadMapsFilter.PlayerCountValue.ToString();
        //        vm.RefreshDownloadMapFilters();
        //    }
        //}

        //private void DownloadPlayerCountUp_Click(object sender, RoutedEventArgs e)
        //{
        //    if (vm.DownloadMapsFilter.PlayerCountValue < 8)
        //    {
        //        vm.DownloadMapsFilter.PlayerCountValue++;
        //        DownloadPlayerCountText.Text = vm.DownloadMapsFilter.PlayerCountValue.ToString();
        //        vm.RefreshDownloadMapFilters();
        //    }
        //}

        // ================= MANUAL IMPORT =================

        private void ManualImport_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private async void ManualImport_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            bool anySuccess = await vm.ImportModFilesAsync(files);
            if (anySuccess)
                MessageBox.Show("Import complete. Files added to Generic Mods (Disabled).");
        }

        private async void ManualImportBrowse_Click(object sender, RoutedEventArgs e)
        {
            bool anySuccess = await vm.ImportModFromFilePickerAsync();
            if (anySuccess)
                MessageBox.Show("Import complete. Files added to Generic Mods (Disabled).");
        }

        // ================= HOTKEY EDITOR =================

        private string GetGamedataPath()
        {
            var config = new SC2ModManager.Services.ConfigService().Load();
            if (string.IsNullOrEmpty(config?.GamePath))
            {
                MessageBox.Show("Game path is not set. Please configure it in Settings first.",
                    "Game Path Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return System.IO.Path.Combine(config.GamePath, "gamedata");
        }

        private async void DownloadAndInstallNormalHotkey_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            await vm.HotkeyEditor.DownloadAndInstallNormalMod(gamedataPath);
        }

        private async void DownloadAndInstallBuildModeHotkey_Click(object sender, RoutedEventArgs e)
            => await vm.HotkeyEditor.DownloadAndInstallBuildModeMod();

        private void SaveNormalHotkeys_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.SaveNormalHotkeys(gamedataPath);
            MessageBox.Show("Normal hotkeys saved and applied to game.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveBuildModeHotkeys_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.SaveBuildModeHotkeys(gamedataPath);
            MessageBox.Show("Build mode hotkeys saved and applied to game.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RestoreNormalHotkeys_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.RestoreNormalDefaults(gamedataPath);
        }

        private void RestoreBuildModeHotkeys_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.RestoreBuildModeDefaults(gamedataPath);
        }

        private void UninstallNormalHotkeyMod_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.UninstallNormalMod(gamedataPath);
        }

        private void UninstallBuildModeHotkeyMod_Click(object sender, RoutedEventArgs e)
        {
            string gamedataPath = GetGamedataPath();
            if (gamedataPath == null) return;
            vm.HotkeyEditor.UninstallBuildModeMod(gamedataPath);
        }

        /// <summary>
        /// Forwards mouse wheel events from a DataGrid up to the nearest parent ScrollViewer,
        /// so the outer scroll area handles scrolling instead of the DataGrid swallowing the event.
        /// </summary>
        private void HotkeyDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;
            var args = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            if (((FrameworkElement)sender).Parent is UIElement parent)
                parent.RaiseEvent(args);
        }

        /// <summary>
        /// Captures a keystroke from a DataGrid TextBox and formats it to game notation.
        /// E.g. Ctrl+X → "Ctrl-X",  Shift+M → "Shift-M",  R → "R".
        /// Escape clears the binding.
        /// </summary>
        private void HotkeyCapture_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;
            e.Handled = true;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.Escape)
            {
                tb.Text = string.Empty;
                return;
            }

            // Ignore lone modifier key presses
            if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
                    or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
                    or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt)
                return;

            var mods = System.Windows.Input.Keyboard.Modifiers;
            var parts = new System.Collections.Generic.List<string>();

            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Shift))   parts.Add("Shift");
            if (mods.HasFlag(System.Windows.Input.ModifierKeys.Alt))     parts.Add("Alt");

            string keyStr = KeyToGameString(key);
            if (string.IsNullOrEmpty(keyStr)) return;

            parts.Add(keyStr);
            tb.Text = string.Join("-", parts);
        }

        /// <summary>Same as HotkeyCapture but only captures single uppercase letters (build mode constraint).</summary>
        private void BuildModeHotkeyCapture_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;
            e.Handled = true;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.Escape)
            {
                tb.Text = string.Empty;
                return;
            }

            if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
                    or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
                    or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt)
                return;

            string keyStr = KeyToGameString(key);
            if (string.IsNullOrEmpty(keyStr)) return;

            // Build mode uses only single characters (the game file uses single letters)
            tb.Text = keyStr;
        }

        private static string KeyToGameString(System.Windows.Input.Key key)
        {
            // A–Z
            if (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
                return key.ToString(); // WPF names these "A","B",... matching game format

            // 0–9 (main row)
            if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
                return ((int)(key - System.Windows.Input.Key.D0)).ToString();

            // Numpad
            if (key >= System.Windows.Input.Key.NumPad0 && key <= System.Windows.Input.Key.NumPad9)
                return "Num" + (int)(key - System.Windows.Input.Key.NumPad0);

            // Function keys
            if (key >= System.Windows.Input.Key.F1 && key <= System.Windows.Input.Key.F12)
                return key.ToString();

            return key switch
            {
                System.Windows.Input.Key.Space     => "Space",
                System.Windows.Input.Key.Return    => "Enter",
                System.Windows.Input.Key.Tab       => "Tab",
                System.Windows.Input.Key.Back      => "Backspace",
                System.Windows.Input.Key.Delete    => "Delete",
                System.Windows.Input.Key.Insert    => "Insert",
                System.Windows.Input.Key.Home      => "Home",
                System.Windows.Input.Key.End       => "End",
                System.Windows.Input.Key.Prior     => "PageUp",
                System.Windows.Input.Key.Next      => "PageDown",
                System.Windows.Input.Key.Up        => "Up",
                System.Windows.Input.Key.Down      => "Down",
                System.Windows.Input.Key.Left      => "Left",
                System.Windows.Input.Key.Right     => "Right",
                System.Windows.Input.Key.OemMinus  => "-",
                System.Windows.Input.Key.OemPlus   => "=",
                System.Windows.Input.Key.OemOpenBrackets => "[",
                System.Windows.Input.Key.OemCloseBrackets => "]",
                System.Windows.Input.Key.OemSemicolon => ";",
                System.Windows.Input.Key.OemQuotes => "'",
                System.Windows.Input.Key.OemComma  => ",",
                System.Windows.Input.Key.OemPeriod => ".",
                System.Windows.Input.Key.OemQuestion => "/",
                System.Windows.Input.Key.OemBackslash => "\\",
                System.Windows.Input.Key.Oem5      => "\\",
                _ => string.Empty
            };
        }
    }
}