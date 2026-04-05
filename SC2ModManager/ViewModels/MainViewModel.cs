using Microsoft.Win32;
using SC2ModManager.Models;
using SC2ModManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SC2ModManager.ViewModels
{
    public enum MainView
    {
        Home,
        Mods,
        CustomMaps,
        Hotkeys
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // ================= SERVICES =================
        private readonly ModRepositoryService repositoryService;
        private readonly ModStorageService storageService;
        private readonly GamedataService gamedataService;
        private readonly ConfigService configService;
        private readonly GameService gameService;

        // ================= DATA =================
        public ObservableCollection<Map> Maps { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> GenericMods { get; set; } = new();

        private MainView currentView;
        public MainView CurrentView
        {
            get => currentView;
            set { currentView = value; OnPropertyChanged(nameof(CurrentView)); }
        }

        private string gamePath;
        public string GamePath
        {
            get => gamePath;
            set
            {
                gamePath = value;
                OnPropertyChanged(nameof(GamePath));
            }
        }

        private readonly UpdateService updateService = new();
        private string? updateDownloadUrl;

        private bool updateAvailable;
        public bool UpdateAvailable
        {
            get => updateAvailable;
            set { updateAvailable = value; OnPropertyChanged(nameof(UpdateAvailable)); }
        }

        private double downloadProgress;
        public double DownloadProgress
        {
            get => downloadProgress;
            set
            {
                downloadProgress = value;
                OnPropertyChanged(nameof(DownloadProgress));
            }
        }

        // ================= INIT =================

        public MainViewModel()
        {
            configService = new ConfigService();
            repositoryService = new ModRepositoryService();
            storageService = new ModStorageService();
            gamedataService = new GamedataService();
            gameService = new GameService(configService);

            InitializeGamePath();

            _ = CheckForUpdatesAsync();
            _ = LoadAllDataAsync();
        }

        // ================= PUBLIC LOAD =================

        public async Task LoadAllDataAsync()
        {
            await LoadMapsAsync();
            await LoadGenericModsAsync();
        }

        public async Task LoadMapsAsync()
        {
            var available = await repositoryService.GetAvailableMapsAsync();

            var downloaded = storageService.GetDownloadedMaps();
            var config = configService.Load();
            var enabled = config.EnabledMaps ?? new List<string>();

            foreach (var map in available)
            {
                map.IsDownloaded = downloaded.Contains(map.FileName);
                map.IsEnabled = enabled.Contains(map.FileName);
            }

            Maps = new ObservableCollection<Map>(available);
            OnPropertyChanged(nameof(Maps));
        }

        private async Task LoadGenericModsAsync()
        {
            var available = await repositoryService.GetAvailableGenericModsAsync();

            var downloaded = storageService.GetDownloadedGenericMods();
            var config = configService.Load();
            var enabled = config.EnabledGenericMods ?? new List<string>();

            foreach (var mod in available)
            {
                mod.IsDownloaded = downloaded.Contains(mod.FileName);
                mod.IsEnabled = enabled.Contains(mod.FileName);
            }

            GenericMods = new ObservableCollection<GenericGamedataMod>(available);
            OnPropertyChanged(nameof(GenericMods));
        }

        // ================= MAP IMPORT =================

        public async Task AddMapsFromFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (file.EndsWith(".zip"))
                {
                    await storageService.ExtractAndAddMapsAsync(file);
                }
                else if (file.EndsWith(".scd"))
                {
                    await storageService.AddMapAsync(file);
                }
            }

            await LoadMapsAsync();
        }

        public async Task ImportMaps()
        {
            // You can reuse your existing drag/drop logic here
            // Or open a file picker

            var dialog = new OpenFileDialog
            {
                Filter = "ZIP files (*.zip)|*.zip|SC2 Maps (*.scd)|*.scd",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            await AddMapsFromFiles(dialog.FileNames);
        }

        // ================= MAP ACTIONS =================

        public async Task DownloadMap(Map map)
        {
            await storageService.DownloadMapAsync(map);
            map.IsDownloaded = true;
        }

        public void EnableMap(Map map)
        {
            if (string.IsNullOrEmpty(GamePath))
                return;

            var mapsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName,
                "Maps"
            );

            var gameDataPath = Path.Combine(GamePath, "gamedata");

            gamedataService.EnableMap(map, mapsPath, gameDataPath);
            map.IsEnabled = true;

            SaveMapsState();
        }

        public void DisableMap(Map map)
        {
            var gameDataPath = Path.Combine(GamePath, "gamedata");

            gamedataService.DisableMap(map, gameDataPath);
            map.IsEnabled = false;

            SaveMapsState();
        }

        private void SaveMapsState()
        {
            var config = configService.Load();

            config.EnabledMaps = Maps
                .Where(m => m.IsEnabled)
                .Select(m => m.FileName)
                .ToList();

            configService.Save(config);
        }

        public void SaveMaps()
        {
            SaveMapsState();
        }

        public void RemoveAllMaps()
        {
            foreach (var map in Maps)
            {
                map.IsEnabled = false;
            }

            SaveMapsState();
            OnPropertyChanged(nameof(Maps));
        }

        public void SelectAllMaps()
        {
            foreach (var map in Maps)
            {
                map.IsEnabled = true;
            }

            SaveMapsState();
            OnPropertyChanged(nameof(Maps));
        }

        // ================= GAME =================

        public void LaunchGame()
        {
            try
            {
                gameService.LaunchGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}");
            }
        }

        // ================= GAME PATH =================

        public void InitializeGamePath()
        {
            var config = configService.Load();

            if (!string.IsNullOrEmpty(config.GamePath))
            {
                GamePath = config.GamePath;
                return;
            }

            var detectedPath = configService.DetectGamePath();

            if (!string.IsNullOrEmpty(detectedPath))
            {
                config.GamePath = detectedPath;
                configService.Save(config);

                GamePath = detectedPath;
            }
            else
            {
                MessageBox.Show("Game path not found. Please select it manually.");
            }
        }

        public void SelectGamePath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select SupremeCommander2.exe",
                Filter = "SupremeCommander2.exe|SupremeCommander2.exe"
            };

            if (dialog.ShowDialog() != true)
                return;

            var path = Path.GetDirectoryName(dialog.FileName);

            var config = configService.Load();
            config.GamePath = path;
            configService.Save(config);

            GamePath = path;
        }

        // ================= UPDATER =================

        public async Task RunUpdater()
        {
            try
            {
                if (string.IsNullOrEmpty(updateDownloadUrl))
                {
                    MessageBox.Show("No update available.");
                    return;
                }

                string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SC2MMUpdater.exe");
                string zipPath = Path.Combine(Path.GetTempPath(), "SC2ModManagerUpdate.zip");
                string installPath = AppDomain.CurrentDomain.BaseDirectory;
                string exeName = "SC2ModManager.exe";

                if (!File.Exists(updaterPath))
                {
                    MessageBox.Show("Updater not found.");
                    return;
                }

                await DownloadFileWithProgress(updateDownloadUrl, zipPath);

                MessageBox.Show("Download complete. Installing update...");

                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{zipPath}\" \"{installPath}\" \"{exeName}\"",
                    UseShellExecute = true,
                    WorkingDirectory = installPath
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Updater failed: {ex.Message}");
            }
        }

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                var (latestVersion, downloadUrl) = await updateService.GetLatestRelease();

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (latestVersion.CompareTo(currentVersion) > 0)
                {
                    UpdateAvailable = true;
                    updateDownloadUrl = downloadUrl;
                }
                else
                {
                    UpdateAvailable = false;
                }
            }
            catch
            {
                // silently fail (no update UI shown)
                UpdateAvailable = false;
            }
        }

        public async Task DownloadFileWithProgress(string url, string outputPath)
        {
            using HttpClient client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReport = totalBytes != -1;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);

                totalRead += read;

                if (canReport)
                {
                    DownloadProgress = (double)totalRead / totalBytes * 100;
                }
            }
        }

        // ================= MOD MANAGER =================
        // ===============================================
        // ===============================================

        // ================= RESTORE ORIGINAL GAMEDATA =================
        public async Task RestoreOriginalGamedataAsync()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }
            try
            {
                await gamedataService.RestoreOriginalGamedataAsync(GamePath + "\\gamedata");
                MessageBox.Show("Original gamedata restored successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore gamedata: {ex.Message}");
            }
        }



        // ================= INSTALLED MODS =================




        // ================= DOWNLOADED MODS =================




        // ================= EVENTS =================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}