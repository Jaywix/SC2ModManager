/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: 2024-01-01
 * Last updated: 2024-06-01
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
        private void GoToScanGamedata(object sender, RoutedEventArgs e)
        {
            vm.ScanGamedataForUnknownMods();
            ShowView("ScanGamedata");
        }
        private void GoToDownloadMods(object sender, RoutedEventArgs e) => ShowView("DownloadMods");
        private void GoToManualImport(object sender, RoutedEventArgs e) => ShowView("ManualImport");

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
                var parentDir = Directory.GetParent(path)?.FullName;
                if (parentDir == null)
                    return;

                vm.SetGamePath(parentDir);
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

        // ================= BACKUPS =================

        private async void RestoreOriginalGameData_Click(object sender, RoutedEventArgs e)
            => await vm.RestoreOriginalGamedataAsync();

        // ================= PRESETS =================

        private void SavePreset_Click(object sender, RoutedEventArgs e)
            => vm.SaveCurrentStateAsPreset();

        private void ApplyPreset_Click(object sender, RoutedEventArgs e)
            => vm.ApplySelectedPreset();

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
            => vm.DeleteSelectedPreset();

        private void ViewPresetFiles_Click(object sender, RoutedEventArgs e)
        {
            if (vm.SelectedPreset == null)
            {
                MessageBox.Show("No preset selected.");
                return;
            }

            var files = vm.SelectedPreset.Files;
            string msg = files.Count == 0
                ? "No files in this preset."
                : string.Join("\n", files);

            MessageBox.Show(msg, $"Files in '{vm.SelectedPreset.Name}'",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================= COMPARE =================

        private void RunComparison_Click(object sender, RoutedEventArgs e)
            => vm.RunComparison();

        // ================= INSTALLED: MAPS =================

        private void EnableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = DisabledMapsList.SelectedItems.OfType<Map>().ToList();
            vm.EnableSelectedMaps(selected);
        }

        private void DisableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledMapsList.SelectedItems.OfType<Map>().ToList();
            vm.DisableSelectedMaps(selected);
        }

        private void EnableAllMaps_Click(object sender, RoutedEventArgs e) => vm.EnableAllMaps();
        private void DisableAllMaps_Click(object sender, RoutedEventArgs e) => vm.DisableAllMaps();

        private void SaveMaps_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveMapsToGamedata();
            MessageBox.Show("Maps saved successfully.");
        }

        private void DeleteSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledMapsList.SelectedItems.Cast<Map>()
                .Concat(DisabledMapsList.SelectedItems.Cast<Map>())
                .ToList();

            if (!selected.Any()) { MessageBox.Show("No maps selected."); return; }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} map(s) from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            foreach (var map in selected)
                vm.UninstallMap(map);

            vm.CleanupPresetsAfterDeletion(selected.Select(m => m.FileName));
        }

        private void DeleteAllMaps_Click(object sender, RoutedEventArgs e)
        {
            var allMaps = vm.EnabledMaps.Concat(vm.DisabledMaps).ToList();

            if (!allMaps.Any()) { MessageBox.Show("No maps installed."); return; }

            var confirm = MessageBox.Show(
                "Delete ALL installed maps from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.UninstallAllMaps();
            vm.CleanupPresetsAfterDeletion(allMaps.Select(m => m.FileName));
        }

        // ================= INSTALLED: GENERIC MODS =================

        private void EnableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = DisabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>().ToList();
            vm.EnableSelectedGenericMods(selected);
        }

        private void DisableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>().ToList();
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
            var selected = EnabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>()
                .Concat(DisabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>())
                .ToList();

            if (!selected.Any()) { MessageBox.Show("No mods selected."); return; }

            var confirm = MessageBox.Show(
                $"Delete {selected.Count} mod(s) from your PC?\n\nWarning: This may affect mod presets. Any presets that become empty as a result will also be deleted.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

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
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            vm.UninstallAllGenericMods();
            vm.CleanupPresetsAfterDeletion(allMods.Select(m => m.FileName));
        }


        // ================= SCAN GAMEDATA =================

        private void RunScan_Click(object sender, RoutedEventArgs e)
            => vm.ScanGamedataForUnknownMods();

        private void SelectAllScanResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanResults)
                item.IsSelected = true;
        }

        private void DeselectAllScanResults_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vm.ScanResults)
                item.IsSelected = false;
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
                "Confirm Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            await vm.ImportSelectedScanResultsAsync(selected);

            vm.ScanGamedataForUnknownMods();
        }

        // ================= DOWNLOAD: MAPS =================

        private void SelectAllDownloadMaps_Click(object sender, RoutedEventArgs e)
            => DownloadMapsList.SelectAll();

        private void DeselectAllDownloadMaps_Click(object sender, RoutedEventArgs e)
            => DownloadMapsList.UnselectAll();

        private async void DownloadSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = DownloadMapsList.SelectedItems.Cast<Map>().ToList();
            if (!selected.Any()) { MessageBox.Show("No maps selected."); return; }

            await vm.DownloadSelectedMapsAsync(selected);
        }

        // ================= DOWNLOAD: GENERIC MODS =================

        private void SelectAllDownloadGenericMods_Click(object sender, RoutedEventArgs e)
            => DownloadGenericModsList.SelectAll();

        private void DeselectAllDownloadGenericMods_Click(object sender, RoutedEventArgs e)
            => DownloadGenericModsList.UnselectAll();

        private async void DownloadSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = DownloadGenericModsList.SelectedItems.Cast<GenericGamedataMod>().ToList();
            if (!selected.Any()) { MessageBox.Show("No mods selected."); return; }

            await vm.DownloadSelectedGenericModsAsync(selected);
        }

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
            await vm.ImportModFilesAsync(files);
            MessageBox.Show("Import complete. Files added to Generic Mods (Disabled).");
        }

        private async void ManualImportBrowse_Click(object sender, RoutedEventArgs e)
        {
            await vm.ImportModFromFilePickerAsync();
            MessageBox.Show("Import complete. Files added to Generic Mods (Disabled).");
        }
    }
}