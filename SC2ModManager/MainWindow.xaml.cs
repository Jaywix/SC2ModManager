using SC2ModManager.Models;
using SC2ModManager.Services;
using SC2ModManager.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace SC2ModManager
{
    public partial class MainWindow : Window
    {
        private MainViewModel vm;
        private ModStorageService storageService;

        public MainWindow()
        {
            InitializeComponent();

            vm = new MainViewModel();
            storageService = new ModStorageService();

            DataContext = vm;

            ShowView("Home");
        }

        private void ShowView(string view)
        {
            HomeView.Visibility = Visibility.Collapsed;
            ModsView.Visibility = Visibility.Collapsed;
            MapsView.Visibility = Visibility.Collapsed;
            InstalledModsView.Visibility = Visibility.Collapsed;
            DownloadModsView.Visibility = Visibility.Collapsed;

            switch (view)
            {
                case "Home": HomeView.Visibility = Visibility.Visible; break;
                case "Mods": ModsView.Visibility = Visibility.Visible; break;
                case "Maps": MapsView.Visibility = Visibility.Visible; break;
                case "InstalledMods": InstalledModsView.Visibility = Visibility.Visible; break;
                case "DownloadMods": DownloadModsView.Visibility = Visibility.Visible; break;
            }
        }

        private void GoHome(object sender, RoutedEventArgs e) => ShowView("Home");
        private void GoToMods(object sender, RoutedEventArgs e) => ShowView("Mods");
        private void GoToMaps(object sender, RoutedEventArgs e) => ShowView("Maps");
        private void GoToInstalledMods(object sender, RoutedEventArgs e) => ShowView("InstalledMods");
        private void GoToDownloadMods(object sender, RoutedEventArgs e) => ShowView("DownloadMods");

        private void LaunchGame_Click(object sender, RoutedEventArgs e) => vm.LaunchGame();

        private async void Update_Click(object sender, RoutedEventArgs e) => await vm.RunUpdater();



        // ----------------- Manage Mods ----------------
        // ----------------- Restore GameData ----------------
        private async void RestoreOriginalGameData_Click(object sender, RoutedEventArgs e)
        {
            await vm.RestoreOriginalGamedataAsync();
        }


        // ----------------- Installed Mods ----------------
        private void Maps_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (var file in files)
                {
                    if (Path.GetExtension(file).Equals(".scd", StringComparison.OrdinalIgnoreCase))
                    {
                        //vm.AddMapsFromFiles(file);
                    }
                }
            }
        }

        private void SelectAll(object sender, RoutedEventArgs e)
        {
            foreach (var map in vm.Maps)
                map.IsEnabled = true;
        }

        private void RemoveAll(object sender, RoutedEventArgs e)
        {
            foreach (var map in vm.Maps)
                map.IsEnabled = false;
        }

        private void SaveMaps(object sender, RoutedEventArgs e) => vm.SaveMaps();
        private void ImportMaps(object sender, RoutedEventArgs e) => vm.ImportMaps();






        // ------------------ Download Mods ----------------








    }

}
