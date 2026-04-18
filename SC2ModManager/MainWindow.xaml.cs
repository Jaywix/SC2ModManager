/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 17, 2026
 * Author: Jacob Wixom
 * 
*/
using Microsoft.Win32;
using SC2ModManager.Models;
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

        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
            DataContext = vm;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

            ShowView("Home");
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
            }
        }

        private void GoHome(object sender, RoutedEventArgs e) => ShowView("Home");
        private void GoToMods(object sender, RoutedEventArgs e) => ShowView("Mods");
        private void GoToSettings(object sender, RoutedEventArgs e) => ShowView("Settings");
        private void GoToBackups(object sender, RoutedEventArgs e) => ShowView("Backups");
        private void GoToInstalledMods(object sender, RoutedEventArgs e) => ShowView("InstalledMods");
        private void GoToDownloadMods(object sender, RoutedEventArgs e) => ShowView("DownloadMods");
        private void GoToManualImport(object sender, RoutedEventArgs e) => ShowView("ManualImport");

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

        private void EnableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = DisabledGenericModsList.SelectedItems.OfType<GenericGamedataMod>().ToList();
            vm.EnableSelectedGenericMods(selected);
        }

        private void DisableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledGenericModsList.SelectedItems.OfType<GenericGamedataMod>().ToList();
            vm.DisableSelectedGenericMods(selected);
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
            var selected = EnabledGenericModsList.SelectedItems.OfType<GenericGamedataMod>()
                .Concat(DisabledGenericModsList.SelectedItems.OfType<GenericGamedataMod>())
                .ToList();

            if (!selected.Any()) { MessageBox.Show("No mods selected."); return; }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} mod(s) from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.DisableSelectedGenericMods(selected.Where(m => m.IsEnabled).ToList());
            vm.SaveGenericModsToGamedata();
            foreach (var mod in selected)
                vm.UninstallGenericMod(mod);

            vm.CleanupPresetsAfterDeletion(selected.Select(m => m.FileName));
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
                "These will appear in Installed → Generic Gamedata Mods as Disabled.\n\n" +
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
                "These will appear in Installed as Disabled and can be managed normally.",
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
    }
}