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
        ManageMods,
        Backups,
        InstalledMods,
        InstalledMaps,
        InstalledGenericMods,
        DownloadMods,
        DownloadMaps,
        DownloadGenericMods,
        ManualImport
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // ================= SERVICES =================

        private readonly ModRepositoryService repositoryService;
        private readonly ModStorageService storageService;
        private readonly GamedataService gamedataService;
        private readonly ConfigService configService;
        private readonly GameService gameService;
        private readonly UpdateService updateService = new();

        // ================= NAVIGATION =================

        private MainView currentView;
        public MainView CurrentView
        {
            get => currentView;
            set { currentView = value; OnPropertyChanged(nameof(CurrentView)); }
        }

        // ================= INSTALLED MOD LISTS =================

        public ObservableCollection<Map> EnabledMaps { get; set; } = new();
        public ObservableCollection<Map> DisabledMaps { get; set; } = new();

        public ObservableCollection<GenericGamedataMod> EnabledGenericMods { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> DisabledGenericMods { get; set; } = new();

        // ================= DOWNLOADABLE MOD LISTS =================

        public ObservableCollection<Map> DownloadableMaps { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> DownloadableGenericMods { get; set; } = new();

        // ================= GAME PATH =================

        private string gamePath;
        public string GamePath
        {
            get => gamePath;
            set { gamePath = value; OnPropertyChanged(nameof(GamePath)); }
        }

        // ================= UPDATE =================

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
            set { downloadProgress = value; OnPropertyChanged(nameof(DownloadProgress)); }
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
        }

        // ================= GAME =================

        public void LaunchGame()
        {
            try { gameService.LaunchGame(); }
            catch (Exception ex) { MessageBox.Show($"Error launching game: {ex.Message}"); }
        }

        public void InitializeGamePath()
        {
            var config = configService.Load();

            if (!string.IsNullOrEmpty(config.GamePath))
            {
                GamePath = config.GamePath;
                return;
            }

            var detected = configService.DetectGamePath();

            if (!string.IsNullOrEmpty(detected))
            {
                config.GamePath = detected;
                configService.Save(config);
                GamePath = detected;
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

            if (dialog.ShowDialog() != true) return;

            var path = Path.GetDirectoryName(dialog.FileName);
            var config = configService.Load();
            config.GamePath = path;
            configService.Save(config);
            GamePath = path;
        }

        // ================= BACKUPS =================

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

        // ================= INSTALLED: MAPS =================

        /// <summary>
        /// Loads installed maps from disk into EnabledMaps and DisabledMaps.
        /// Merges with saved state JSON so richer metadata (author, version, etc.) is preserved.
        /// </summary>
        public void LoadInstalledMaps()
        {
            var onDisk = storageService.GetInstalledMaps();
            var savedState = storageService.LoadMapsState();

            // Build a lookup from the saved state to merge metadata back in
            var stateByFile = savedState.ToDictionary(m => m.FileName, m => m);

            var enriched = onDisk.Select(m =>
            {
                if (stateByFile.TryGetValue(m.FileName, out var saved))
                {
                    // Preserve richer metadata from state, keep IsEnabled from disk
                    saved.IsEnabled = m.IsEnabled;
                    saved.IsDownloaded = true;
                    return saved;
                }
                return m;
            }).ToList();

            EnabledMaps = new ObservableCollection<Map>(enriched.Where(m => m.IsEnabled));
            DisabledMaps = new ObservableCollection<Map>(enriched.Where(m => !m.IsEnabled));

            OnPropertyChanged(nameof(EnabledMaps));
            OnPropertyChanged(nameof(DisabledMaps));
        }

        /// <summary>
        /// Moves selected maps from Disabled to Enabled (storage + collection).
        /// Does not touch gamedata yet — that happens on Save.
        /// </summary>
        public void EnableSelectedMaps(IEnumerable<Map> maps)
        {
            foreach (var map in maps.ToList())
            {
                storageService.MoveMapToEnabled(map);
                map.IsEnabled = true;
                DisabledMaps.Remove(map);
                EnabledMaps.Add(map);
            }
        }

        /// <summary>
        /// Moves selected maps from Enabled to Disabled (storage + collection).
        /// Does not touch gamedata yet — that happens on Save.
        /// </summary>
        public void DisableSelectedMaps(IEnumerable<Map> maps)
        {
            foreach (var map in maps.ToList())
            {
                storageService.MoveMapToDisabled(map);
                map.IsEnabled = false;
                EnabledMaps.Remove(map);
                DisabledMaps.Add(map);
            }
        }

        public void EnableAllMaps() => EnableSelectedMaps(DisabledMaps.ToList());
        public void DisableAllMaps() => DisableSelectedMaps(EnabledMaps.ToList());

        /// <summary>
        /// Syncs the game's gamedata folder to match EnabledMaps, then persists state.
        /// </summary>
        public void SaveMapsToGamedata()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string mapsEnabledPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName, "Mods", "Maps", "Enabled"
            );
            string gameDataPath = Path.Combine(GamePath, "gamedata");

            // Remove all previously enabled maps from gamedata
            var allInstalled = EnabledMaps.Concat(DisabledMaps);
            foreach (var map in allInstalled)
                gamedataService.DisableMap(map, gameDataPath);

            // Copy currently enabled maps into gamedata
            foreach (var map in EnabledMaps)
            {
                try { gamedataService.EnableMap(map, mapsEnabledPath, gameDataPath); }
                catch (Exception ex) { MessageBox.Show($"Could not enable {map.FileName}: {ex.Message}"); }
            }

            // Persist state
            storageService.SaveMapsState(EnabledMaps.Concat(DisabledMaps));
        }

        public void UninstallMap(Map map)
        {
            storageService.DeleteMap(map);
            EnabledMaps.Remove(map);
            DisabledMaps.Remove(map);
        }

        public void UninstallAllMaps()
        {
            foreach (var map in EnabledMaps.Concat(DisabledMaps).ToList())
                storageService.DeleteMap(map);

            EnabledMaps.Clear();
            DisabledMaps.Clear();
        }

        // ================= INSTALLED: GENERIC MODS =================

        /// <summary>
        /// Loads installed generic mods from disk, merging with saved state JSON.
        /// </summary>
        public void LoadInstalledGenericMods()
        {
            var onDisk = storageService.GetInstalledGenericMods();
            var savedState = storageService.LoadGenericModsState();

            var stateByFile = savedState.ToDictionary(m => m.FileName, m => m);

            var enriched = onDisk.Select(m =>
            {
                if (stateByFile.TryGetValue(m.FileName, out var saved))
                {
                    saved.IsEnabled = m.IsEnabled;
                    saved.IsDownloaded = true;
                    return saved;
                }
                return m;
            }).ToList();

            EnabledGenericMods = new ObservableCollection<GenericGamedataMod>(enriched.Where(m => m.IsEnabled));
            DisabledGenericMods = new ObservableCollection<GenericGamedataMod>(enriched.Where(m => !m.IsEnabled));

            OnPropertyChanged(nameof(EnabledGenericMods));
            OnPropertyChanged(nameof(DisabledGenericMods));
        }

        public void EnableSelectedGenericMods(IEnumerable<GenericGamedataMod> mods)
        {
            foreach (var mod in mods.ToList())
            {
                storageService.MoveGenericModToEnabled(mod);
                mod.IsEnabled = true;
                DisabledGenericMods.Remove(mod);
                EnabledGenericMods.Add(mod);
            }
        }

        public void DisableSelectedGenericMods(IEnumerable<GenericGamedataMod> mods)
        {
            foreach (var mod in mods.ToList())
            {
                storageService.MoveGenericModToDisabled(mod);
                mod.IsEnabled = false;
                EnabledGenericMods.Remove(mod);
                DisabledGenericMods.Add(mod);
            }
        }

        public void EnableAllGenericMods() => EnableSelectedGenericMods(DisabledGenericMods.ToList());
        public void DisableAllGenericMods() => DisableSelectedGenericMods(EnabledGenericMods.ToList());

        public void SaveGenericModsToGamedata()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string modsEnabledPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName, "Mods", "GenericMods", "Enabled"
            );
            string gameDataPath = Path.Combine(GamePath, "gamedata");

            var allInstalled = EnabledGenericMods.Concat(DisabledGenericMods);
            foreach (var mod in allInstalled)
                gamedataService.DisableGenericMod(mod, gameDataPath);

            foreach (var mod in EnabledGenericMods)
            {
                try { gamedataService.EnableGenericMod(mod, modsEnabledPath, gameDataPath); }
                catch (Exception ex) { MessageBox.Show($"Could not enable {mod.FileName}: {ex.Message}"); }
            }

            storageService.SaveGenericModsState(EnabledGenericMods.Concat(DisabledGenericMods));
        }

        public void UninstallGenericMod(GenericGamedataMod mod)
        {
            storageService.DeleteGenericMod(mod);
            EnabledGenericMods.Remove(mod);
            DisabledGenericMods.Remove(mod);
        }

        public void UninstallAllGenericMods()
        {
            foreach (var mod in EnabledGenericMods.Concat(DisabledGenericMods).ToList())
                storageService.DeleteGenericMod(mod);

            EnabledGenericMods.Clear();
            DisabledGenericMods.Clear();
        }

        // ================= DOWNLOAD: MAPS =================

        public async Task LoadDownloadableMapsAsync()
        {
            var all = await storageService.GetDownloadableMapsAsync();
            var installed = storageService.GetInstalledMaps();
            var fileNames = installed.Select(m => m.FileName).ToHashSet();

            foreach (var map in all)
                map.IsDownloaded = fileNames.Contains(map.FileName);

            DownloadableMaps = new ObservableCollection<Map>(all);
            OnPropertyChanged(nameof(DownloadableMaps));
        }

        public async Task DownloadSelectedMapsAsync(IEnumerable<Map> maps)
        {
            foreach (var map in maps)
            {
                try
                {
                    await storageService.DownloadMapAsync(map);
                    map.IsDownloaded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to download {map.FileName}: {ex.Message}");
                }
            }
        }

        // ================= DOWNLOAD: GENERIC MODS =================

        public async Task LoadDownloadableGenericModsAsync()
        {
            var all = await storageService.GetDownloadableGenericModsAsync();
            var installed = storageService.GetInstalledGenericMods();
            var fileNames = installed.Select(m => m.FileName).ToHashSet();

            foreach (var mod in all)
                mod.IsDownloaded = fileNames.Contains(mod.FileName);

            DownloadableGenericMods = new ObservableCollection<GenericGamedataMod>(all);
            OnPropertyChanged(nameof(DownloadableGenericMods));
        }

        public async Task DownloadSelectedGenericModsAsync(IEnumerable<GenericGamedataMod> mods)
        {
            foreach (var mod in mods)
            {
                try
                {
                    await storageService.DownloadGenericModAsync(mod);
                    mod.IsDownloaded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to download {mod.FileName}: {ex.Message}");
                }
            }
        }

        // ================= MANUAL IMPORT =================

        public async Task ImportModFromFilePickerAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a mod file",
                Filter = "SC2 Mod Files (*.scd)|*.scd|ZIP files (*.zip)|*.zip",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true) return;

            await ImportModFilesAsync(dialog.FileNames);
        }

        public async Task ImportModFilesAsync(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract any .scd inside as generic mods
                        string tempDir = Path.Combine(Path.GetTempPath(), "SC2_import_extract");
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                        Directory.CreateDirectory(tempDir);

                        await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(file, tempDir));

                        foreach (var scd in Directory.GetFiles(tempDir, "*.scd", SearchOption.AllDirectories))
                            await storageService.ImportGenericModAsync(scd);

                        Directory.Delete(tempDir, true);
                    }
                    else if (file.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                    {
                        await storageService.ImportGenericModAsync(file);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            // Refresh the installed generic mods list after import
            LoadInstalledGenericMods();
        }

        // ================= UPDATER =================

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
                UpdateAvailable = false;
            }
        }

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
                    DownloadProgress = (double)totalRead / totalBytes * 100;
            }
        }

        // ================= EVENTS =================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}