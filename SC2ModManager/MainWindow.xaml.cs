using Microsoft.Win32;
using SC2ModManager.Models;
using SC2ModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
            ShowView("Home");
        }

        // ================= NAVIGATION =================

        private void ShowView(string view)
        {
            HomeView.Visibility = Visibility.Collapsed;
            ModsView.Visibility = Visibility.Collapsed;
            BackupsView.Visibility = Visibility.Collapsed;
            InstalledModsView.Visibility = Visibility.Collapsed;
            InstalledMapsView.Visibility = Visibility.Collapsed;
            InstalledGenericModsView.Visibility = Visibility.Collapsed;
            DownloadModsView.Visibility = Visibility.Collapsed;
            DownloadMapsView.Visibility = Visibility.Collapsed;
            DownloadGenericModsView.Visibility = Visibility.Collapsed;
            ManualImportView.Visibility = Visibility.Collapsed;

            switch (view)
            {
                case "Home": HomeView.Visibility = Visibility.Visible; break;
                case "Mods": ModsView.Visibility = Visibility.Visible; break;
                case "Backups": BackupsView.Visibility = Visibility.Visible; break;
                case "InstalledMods": InstalledModsView.Visibility = Visibility.Visible; break;
                case "InstalledMaps": InstalledMapsView.Visibility = Visibility.Visible; break;
                case "InstalledGenericMods": InstalledGenericModsView.Visibility = Visibility.Visible; break;
                case "DownloadMods": DownloadModsView.Visibility = Visibility.Visible; break;
                case "DownloadMaps": DownloadMapsView.Visibility = Visibility.Visible; break;
                case "DownloadGenericMods": DownloadGenericModsView.Visibility = Visibility.Visible; break;
                case "ManualImport": ManualImportView.Visibility = Visibility.Visible; break;
            }
        }

        private void GoHome(object sender, RoutedEventArgs e) => ShowView("Home");
        private void GoToMods(object sender, RoutedEventArgs e) => ShowView("Mods");
        private void GoToBackups(object sender, RoutedEventArgs e) => ShowView("Backups");
        private void GoToInstalledMods(object sender, RoutedEventArgs e) => ShowView("InstalledMods");
        private void GoToDownloadMods(object sender, RoutedEventArgs e) => ShowView("DownloadMods");

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

        private void GoToManualImport(object sender, RoutedEventArgs e) => ShowView("ManualImport");

        // ================= HOME =================

        private void LaunchGame_Click(object sender, RoutedEventArgs e) => vm.LaunchGame();
        private async void Update_Click(object sender, RoutedEventArgs e) => await vm.RunUpdater();

        // ================= BACKUPS =================

        private async void RestoreOriginalGameData_Click(object sender, RoutedEventArgs e)
            => await vm.RestoreOriginalGamedataAsync();

        // ================= INSTALLED: MAPS =================

        private void EnableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledMapsList.SelectedItems.Cast<Map>()
                .Concat(DisabledMapsList.SelectedItems.Cast<Map>())
                .Where(m => !m.IsEnabled)
                .ToList();

            vm.EnableSelectedMaps(selected);
        }

        private void DisableSelectedMaps_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledMapsList.SelectedItems.Cast<Map>()
                .Where(m => m.IsEnabled)
                .ToList();

            vm.DisableSelectedMaps(selected);
        }

        private void EnableAllMaps_Click(object sender, RoutedEventArgs e) => vm.EnableAllMaps();
        private void DisableAllMaps_Click(object sender, RoutedEventArgs e) => vm.DisableAllMaps();

        private void SaveMaps_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveMapsToGamedata();
            MessageBox.Show("Maps saved successfully.");
        }

        // ================= INSTALLED: GENERIC MODS =================

        private void EnableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = DisabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>()
                .ToList();

            vm.EnableSelectedGenericMods(selected);
        }

        private void DisableSelectedGenericMods_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnabledGenericModsList.SelectedItems.Cast<GenericGamedataMod>()
                .ToList();

            vm.DisableSelectedGenericMods(selected);
        }

        private void EnableAllGenericMods_Click(object sender, RoutedEventArgs e) => vm.EnableAllGenericMods();
        private void DisableAllGenericMods_Click(object sender, RoutedEventArgs e) => vm.DisableAllGenericMods();

        private void SaveGenericMods_Click(object sender, RoutedEventArgs e)
        {
            vm.SaveGenericModsToGamedata();
            MessageBox.Show("Generic mods saved successfully.");
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
            MessageBox.Show($"{selected.Count} map(s) downloaded.");
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
            MessageBox.Show($"{selected.Count} mod(s) downloaded.");
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