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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;

namespace SC2ModManager.ViewModels
{
    public enum MainView
    {
        Home,
        ManageMods,
        Backups,
        Presets,
        ComparePresets,
        InstalledMods,
        InstalledMaps,
        InstalledGenericMods,
        DownloadMods,
        DownloadMaps,
        DownloadGenericMods,
        ManualImport
    }

    /// <summary>
    ///     This is the main model for the main view. There is a ton of logic in here. It might be a good idea to somehow split it into different view models at some point.
    ///     For now, keep it very, very organized. I have included headings below, so stick with that style
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // ================= SERVICES =================

        private readonly ModRepositoryService repositoryService = new();
        private readonly ModStorageService storageService = new();
        private readonly GamedataService gamedataService = new();
        private readonly ConfigService configService = new();
        private readonly PresetService presetService = new();
        private readonly UpdateService updateService = new();

        // This can't be read only because the way I set it up makes it so that if the config is updated with a new game path, I need to create a new instance of the GameService with the updated config
        // I could maybe change this in the future
        private GameService gameService;


        // ================= THEME =================

        private readonly ThemeService themeService = new(new ConfigService());

        public void ChangeTheme(string theme)
        {
            themeService.ApplyTheme(theme);
        }

        public string GetCurrentTheme() => themeService.GetCurrentTheme();

        // ================= NAVIGATION =================

        private MainView currentView;
        public MainView CurrentView
        {
            get => currentView;
            set { currentView = value; OnPropertyChanged(nameof(CurrentView)); }
        }

        // ================= NEWS =================
        private readonly NewsService newsService = new();
        public ObservableCollection<NewsItemViewModel> NewsItems { get; set; } = new();

        // ================= SCAN RESULTS =================
        public ObservableCollection<ScanResultItem> ScanResults { get; set; } = new();
        public ObservableCollection<ScanResultItem> ScanMatchResults { get; set; } = new();

        // ================= INSTALLED MOD LISTS =================

        public ObservableCollection<Map> EnabledMaps { get; set; } = new();
        public ObservableCollection<Map> DisabledMaps { get; set; } = new();

        public ObservableCollection<GenericGamedataMod> EnabledGenericMods { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> DisabledGenericMods { get; set; } = new();

        public ObservableCollection<GenericGamedataMod> FilteredEnabledGenericMods { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> FilteredDisabledGenericMods { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> FilteredDownloadableGenericMods { get; set; } = new();

        // ================= INSTALLED FILTERED MAP VIEWS =================
        public ObservableCollection<Map> FilteredEnabledMaps { get; set; } = new();
        public ObservableCollection<Map> FilteredDisabledMaps { get; set; } = new();
        public ObservableCollection<Map> FilteredDownloadableMaps { get; set; } = new();

        // ================= DOWNLOADABLE MOD LISTS =================

        public ObservableCollection<Map> DownloadableMaps { get; set; } = new();
        public ObservableCollection<GenericGamedataMod> DownloadableGenericMods { get; set; } = new();


        // ================= MAP FILTERS =================

        public MapFilterViewModel InstalledMapsFilter { get; set; } = new();
        public MapFilterViewModel DownloadMapsFilter { get; set; } = new();

        // ================= SORTING =================
        public ModSortViewModel InstalledMapSort { get; set; } = new();
        public ModSortViewModel DownloadMapSort { get; set; } = new();
        public ModSortViewModel InstalledGenericModSort { get; set; } = new();
        public ModSortViewModel DownloadGenericModSort { get; set; } = new();

        // ================= SELECTED MAP =================

        private Map selectedInstalledMap;
        public Map SelectedInstalledMap
        {
            get => selectedInstalledMap;
            set { selectedInstalledMap = value; OnPropertyChanged(nameof(SelectedInstalledMap)); }
        }

        private Map selectedDownloadMap;
        public Map SelectedDownloadMap
        {
            get => selectedDownloadMap;
            set { selectedDownloadMap = value; OnPropertyChanged(nameof(SelectedDownloadMap)); }
        }

        // ================= PRESETS =================

        public ObservableCollection<ModPreset> Presets { get; set; } = new();

        private ModPreset selectedPreset;
        public ModPreset SelectedPreset
        {
            get => selectedPreset;
            set { selectedPreset = value; OnPropertyChanged(nameof(SelectedPreset)); }
        }

        private string newPresetName = string.Empty;
        public string NewPresetName
        {
            get => newPresetName;
            set { newPresetName = value; OnPropertyChanged(nameof(NewPresetName)); }
        }

        // ================= COMPARE =================

        public ObservableCollection<string> ComparePresetNames { get; set; } = new();

        private string compareLeftSelection;
        public string CompareLeftSelection
        {
            get => compareLeftSelection;
            set { compareLeftSelection = value; OnPropertyChanged(nameof(CompareLeftSelection)); }
        }

        private string compareRightSelection;
        public string CompareRightSelection
        {
            get => compareRightSelection;
            set { compareRightSelection = value; OnPropertyChanged(nameof(CompareRightSelection)); }
        }

        public ObservableCollection<CompareResultItem> CompareResults { get; set; } = new();

        // ================= GAME PATH =================

        private string gamePath;
        public string GamePath
        {
            get => gamePath;
            set { gamePath = value; OnPropertyChanged(nameof(GamePath)); }
        }

        public void SetGamePath(string path)
        {
            configService.UpdateGamePath(configService.Load(), path);
            gameService = new GameService(configService);

            InitializeGamePath();

            this.DisableAllGenericMods();
            this.DisableAllMaps();
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
            presetService = new PresetService();

            InitializeGamePath();
            _ = CheckForUpdatesAsync();
            _ = LoadNewsAsync();
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

        // ================= NEWS =================

        public async Task LoadNewsAsync()
        {
            try
            {
                var items = await newsService.GetNewsAsync();
                NewsItems = new ObservableCollection<NewsItemViewModel>(
                    items.Select(i => new NewsItemViewModel(i)));
                OnPropertyChanged(nameof(NewsItems));
            }
            catch { }
        }

        // ================= BACKUPS =================

        public async Task RestoreOriginalGamedataAsync()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            var confirm = MessageBox.Show(
                    "This will delete everything currently in your gamedata folder and replace it with the original game files downloaded from GitHub.\n\n" +
                    "Any mods you have enabled will be removed from gamedata (your installed mod files in the mod manager will not be deleted).\n\n" +
                    "Are you sure you want to continue?",
                    "Restore Original Game Files",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await gamedataService.RestoreOriginalGamedataAsync(GamePath + "\\gamedata");

                // Snapshot the original files list after a clean restore
                presetService.SaveOriginalFilesList(GamePath + "\\gamedata");

                // Mark all maps and generic mods as disabled since gamedata was wiped
                DisableAllMaps();
                storageService.SaveMapsState(EnabledMaps.Concat(DisabledMaps));

                DisableAllGenericMods();
                storageService.SaveGenericModsState(EnabledGenericMods.Concat(DisabledGenericMods));

                MessageBox.Show("Original gamedata restored successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore gamedata: {ex.Message}");
            }
        }

        // ================= PRESETS =================

        public void LoadPresets()
        {
            var all = presetService.LoadAllPresets();
            Presets = new ObservableCollection<ModPreset>(all);
            OnPropertyChanged(nameof(Presets));
        }

        public void SaveCurrentStateAsPreset()
        {
            if (string.IsNullOrWhiteSpace(NewPresetName))
            {
                MessageBox.Show("Please enter a preset name.");
                return;
            }

            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            try
            {
                presetService.SavePreset(NewPresetName, Path.Combine(GamePath, "gamedata"));
                NewPresetName = string.Empty;
                LoadPresets();
                MessageBox.Show("Preset saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save preset: {ex.Message}");
            }
        }

        public void DeleteSelectedPreset()
        {
            if (SelectedPreset == null)
            {
                MessageBox.Show("No preset selected.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete preset '{SelectedPreset.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes) return;

            presetService.DeletePreset(SelectedPreset.Name);
            LoadPresets();
        }

        /// <summary>
        ///     Removes deleted filenames from all presets.
        ///     Deletes any preset that becomes empty as a result.
        /// </summary>
        public void CleanupPresetsAfterDeletion(IEnumerable<string> deletedFileNames)
        {
            var deleted = deletedFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var presets = presetService.LoadAllPresets();
            bool anyChanged = false;

            foreach (var preset in presets)
            {
                int before = preset.Files.Count;
                preset.Files.RemoveAll(f => deleted.Contains(Path.GetFileName(f)));

                if (preset.Files.Count == 0)
                {
                    // Preset is now empty so it should be deleted
                    // TODO: Add logic here because all presets contain the original files, so this will never be called unless I clear gamedata
                    presetService.DeletePreset(preset.Name);
                    anyChanged = true;
                }
                else if (preset.Files.Count != before)
                {
                    // Preset was affected but still has files, so just resave it. Maybe I should delete it instead of resaving? I don't know if it matters that much since the preset files are pretty small, but it would be cleaner to delete and recreate it instead of modifying it in place. I'll think about it.
                    presetService.ResavePreset(preset);
                    anyChanged = true;
                }
            }

            if (anyChanged)
                LoadPresets();
        }

        public void ApplySelectedPreset()
        {
            if (SelectedPreset == null)
            {
                MessageBox.Show("No preset selected.");
                return;
            }

            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Apply preset '{SelectedPreset.Name}'? This will remove files from gamedata that are not in the preset.",
                "Confirm Apply",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                presetService.ApplyPreset(SelectedPreset, Path.Combine(GamePath, "gamedata"));

                var presetFileNames = SelectedPreset.Files
                    .Select(f => Path.GetFileName(f))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Reload installed mods fresh from user pc
                LoadInstalledMaps();
                LoadInstalledGenericMods();

                // Disable maps that are NOT in the preset
                var mapsToDisable = EnabledMaps
                    .Where(m => !presetFileNames.Contains(m.FileName)).ToList();
                DisableSelectedMaps(mapsToDisable);

                // Enable maps that ARE in the preset
                var mapsToEnable = DisabledMaps
                    .Where(m => presetFileNames.Contains(m.FileName)).ToList();
                EnableSelectedMaps(mapsToEnable);

                SaveMapsToGamedata();

                // Disable generic mods that are NOT in the preset
                var modsToDisable = EnabledGenericMods
                    .Where(m => !presetFileNames.Contains(m.FileName)).ToList();
                DisableSelectedGenericMods(modsToDisable);

                // Enable generic mods that ARE in the preset. All this double logic is killing me, it will be a pain to add more mod types in the future. I should really refactor this at some point to be more data driven and less hardcoded, but for now this will work.
                var modsToEnable = DisabledGenericMods
                    .Where(m => presetFileNames.Contains(m.FileName)).ToList();
                EnableSelectedGenericMods(modsToEnable);

                SaveGenericModsToGamedata();

                MessageBox.Show($"Preset '{SelectedPreset.Name}' applied.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply preset: {ex.Message}");
            }
        }

        // ================= COMPARE =================

        public void LoadCompareOptions()
        {
            var presets = presetService.LoadAllPresets();

            ComparePresetNames = new ObservableCollection<string>();
            ComparePresetNames.Add("Original Files");

            foreach (var p in presets)
                ComparePresetNames.Add(p.Name);

            OnPropertyChanged(nameof(ComparePresetNames));

            CompareLeftSelection = ComparePresetNames.FirstOrDefault();
            CompareRightSelection = ComparePresetNames.Skip(1).FirstOrDefault()
                                    ?? ComparePresetNames.FirstOrDefault();
        }

        public void RunComparison()
        {
            var leftFiles = GetFilesForSelection(CompareLeftSelection);
            var rightFiles = GetFilesForSelection(CompareRightSelection);

            var results = presetService.Compare(leftFiles, rightFiles);

            CompareResults = new ObservableCollection<CompareResultItem>(
                results.Select(r => new CompareResultItem
                {
                    FileName = r.FileName,
                    IsDifferent = r.IsDifferent
                })
            );

            OnPropertyChanged(nameof(CompareResults));
        }

        private List<string> GetFilesForSelection(string selectionName)
        {
            if (selectionName == "Original Files")
                return presetService.LoadOriginalFilesList();

            var preset = presetService.LoadAllPresets()
                .FirstOrDefault(p => p.Name == selectionName);

            return preset?.Files ?? new List<string>();
        }

        // ================= INSTALLED: SCAN =================

        private bool hasUnrecognizedFiles;
        public bool HasUnrecognizedFiles
        {
            get => hasUnrecognizedFiles;
            set { hasUnrecognizedFiles = value; OnPropertyChanged(nameof(HasUnrecognizedFiles)); }
        }

        public async Task ScanGamedataForUnknownMods()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string gameDataPath = Path.Combine(GamePath, "gamedata");

            if (!Directory.Exists(gameDataPath))
            {
                MessageBox.Show("Gamedata folder not found.");
                return;
            }

            HashSet<string> knownMods = storageService.GetAllKnownModFileNames();

            List<string> inGamedata = Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetFileName(f))
                .ToList();

            List<Map> downloadableMaps = new List<Map>();
            List<GenericGamedataMod> downloadableGenericMods = new List<GenericGamedataMod>();

            try
            {
                downloadableMaps = await storageService.GetDownloadableMapsAsync();
                downloadableGenericMods = await storageService.GetDownloadableGenericModsAsync();
            }
            catch { }

            var downloadableMapsByFile = downloadableMaps
                .ToDictionary(m => m.FileName, m => (object)m, StringComparer.OrdinalIgnoreCase);

            var downloadableGenericByFile = downloadableGenericMods
                .ToDictionary(m => m.FileName, m => (object)m, StringComparer.OrdinalIgnoreCase);

            var unknown = new List<ScanResultItem>();
            var matched = new List<ScanResultItem>();

            foreach (string file in inGamedata.OrderBy(f => f))
            {
                if (knownMods.Contains(file) || ModStorageService.IsOriginalGameFile(file))
                    continue;

                if (downloadableMapsByFile.TryGetValue(file, out var matchedMap))
                {
                    matched.Add(new ScanResultItem
                    {
                        FileName = file,
                        ResultType = ScanResultType.MatchesDownloadable,
                        MatchedMod = matchedMap,
                        IsSelected = true
                    });
                }
                else if (downloadableGenericByFile.TryGetValue(file, out var matchedMod))
                {
                    matched.Add(new ScanResultItem
                    {
                        FileName = file,
                        ResultType = ScanResultType.MatchesDownloadable,
                        MatchedMod = matchedMod,
                        IsSelected = true
                    });
                }
                else
                {
                    unknown.Add(new ScanResultItem
                    {
                        FileName = file,
                        ResultType = ScanResultType.Unknown,
                        IsSelected = true
                    });
                }
            }

            ScanResults = new ObservableCollection<ScanResultItem>(unknown);
            ScanMatchResults = new ObservableCollection<ScanResultItem>(matched);

            OnPropertyChanged(nameof(ScanResults));
            OnPropertyChanged(nameof(ScanMatchResults));

            HasUnrecognizedFiles = unknown.Any() || matched.Any();
        }

        public void DeleteUnknownFiles(IEnumerable<ScanResultItem> items)
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string gameDataPath = Path.Combine(GamePath, "gamedata");
            int deletedCount = 0;

            foreach (var item in items.Where(i => i.IsSelected))
            {
                string path = Directory.GetFiles(gameDataPath, item.FileName, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (path != null)
                {
                    File.Delete(path);
                    deletedCount++;
                }
            }

            MessageBox.Show($"{deletedCount} file(s) deleted from gamedata.");
        }

        public async Task ImportMetadataForMatchedMods(IEnumerable<ScanResultItem> items)
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string gameDataPath = Path.Combine(GamePath, "gamedata");
            int successCount = 0;
            var errors = new List<string>();

            foreach (var item in items.Where(i => i.IsSelected))
            {
                try
                {
                    string sourcePath = Directory.GetFiles(gameDataPath, item.FileName, SearchOption.AllDirectories).FirstOrDefault();

                    if (sourcePath == null)
                        throw new Exception("File not found in gamedata.");

                    if (item.MatchedMod is Map)
                        await storageService.ImportMapAsync(sourcePath);
                    else
                        await storageService.ImportGenericModAsync(sourcePath);

                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }

            LoadInstalledMaps();
            LoadInstalledGenericMods();

            if (errors.Any())
                MessageBox.Show($"Some files could not be imported:\n{string.Join("\n", errors)}");
            else
                MessageBox.Show($"{successCount} mod(s) imported with full metadata.\n\nYou can manage them from the Installed screen.");
        }

        public void DeleteMatchedMods(IEnumerable<ScanResultItem> items)
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string gameDataPath = Path.Combine(GamePath, "gamedata");
            int deletedCount = 0;

            foreach (var item in items.Where(i => i.IsSelected))
            {
                string path = Directory.GetFiles(gameDataPath, item.FileName, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (path != null)
                {
                    File.Delete(path);
                    deletedCount++;
                }
            }

            MessageBox.Show($"{deletedCount} file(s) removed from gamedata.\n\nYou can re-download them from the Download screen whenever you want.");
        }

        public async Task ImportSelectedScanResultsAsync(IEnumerable<ScanResultItem> items)
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string gameDataPath = Path.Combine(GamePath, "gamedata");
            var toImport = items.Where(i => i.IsSelected).ToList();

            if (!toImport.Any())
            {
                MessageBox.Show("No files selected.");
                return;
            }

            int successCount = 0;
            var errors = new List<string>();

            foreach (var item in toImport)
            {
                try
                {
                    string sourcePath = Directory.GetFiles(
                        gameDataPath, item.FileName, SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (sourcePath == null)
                        throw new Exception("File not found in gamedata.");

                    await storageService.ImportGenericModAsync(sourcePath);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }

            LoadInstalledGenericMods();

            if (errors.Any())
                MessageBox.Show($"Some files could not be imported:\n{string.Join("\n", errors)}");
            else
                MessageBox.Show($"{successCount} file(s) imported successfully as Generic Gamedata Mods.\n\nYou can enable or disable them from Installed → Generic Gamedata Mods.");
        }

        // ================= SORTING AND FILTERING =================
        private static DateTime ParseModDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr) || dateStr.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                return DateTime.MinValue;

            if (DateTime.TryParseExact(dateStr, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var d))
                return d;

            return DateTime.MinValue;
        }

        private IEnumerable<Map> ApplyMapSort(IEnumerable<Map> maps, ModSortViewModel sort)
        {
            return sort.Field == SortField.Name
                ? (sort.Direction == SortDirection.Ascending
                    ? maps.OrderBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
                    : maps.OrderByDescending(m => m.FileName, StringComparer.OrdinalIgnoreCase))
                : (sort.Direction == SortDirection.Ascending
                    ? maps.OrderBy(m => ParseModDate(m.LastUpdated))
                    : maps.OrderByDescending(m => ParseModDate(m.LastUpdated)));
        }

        private IEnumerable<GenericGamedataMod> ApplyGenericModSort(IEnumerable<GenericGamedataMod> mods, ModSortViewModel sort)
        {
            return sort.Field == SortField.Name
                ? (sort.Direction == SortDirection.Ascending
                    ? mods.OrderBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
                    : mods.OrderByDescending(m => m.FileName, StringComparer.OrdinalIgnoreCase))
                : (sort.Direction == SortDirection.Ascending
                    ? mods.OrderBy(m => ParseModDate(m.LastUpdated))
                    : mods.OrderByDescending(m => ParseModDate(m.LastUpdated)));
        }

        // ================= INSTALLED: MAPS =================

        public void LoadInstalledMaps()
        {
            var onDisk = storageService.GetInstalledMaps();
            var savedState = storageService.LoadMapsState();
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

            EnabledMaps = new ObservableCollection<Map>(enriched.Where(m => m.IsEnabled));
            DisabledMaps = new ObservableCollection<Map>(enriched.Where(m => !m.IsEnabled));

            OnPropertyChanged(nameof(EnabledMaps));
            OnPropertyChanged(nameof(DisabledMaps));

            RefreshInstalledMapFilters();
        }

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

        public void SaveMapsToGamedata()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                MessageBox.Show("Game path not set.");
                return;
            }

            string mapsEnabledPath = Path.Combine(
                Globals.GetDataPath(), "Mods", "Maps", "Enabled"
            );
            string gameDataPath = Path.Combine(GamePath, "gamedata");

            foreach (var map in EnabledMaps.Concat(DisabledMaps))
                gamedataService.DisableMap(map, gameDataPath);

            foreach (var map in EnabledMaps)
            {
                try { gamedataService.EnableMap(map, mapsEnabledPath, gameDataPath); }
                catch (Exception ex) { MessageBox.Show($"Could not enable {map.FileName}: {ex.Message}"); }
            }

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
            DisableAllMaps();
            foreach (var map in EnabledMaps.Concat(DisabledMaps).ToList())
            {
                storageService.DeleteMap(map);
            }

            EnabledMaps.Clear();
            DisabledMaps.Clear();
        }

        // ================= SELECTED GENERIC MOD =================

        private GenericGamedataMod selectedInstalledGenericMod;
        public GenericGamedataMod SelectedInstalledGenericMod
        {
            get => selectedInstalledGenericMod;
            set { selectedInstalledGenericMod = value; OnPropertyChanged(nameof(SelectedInstalledGenericMod)); }
        }

        private GenericGamedataMod selectedDownloadGenericMod;
        public GenericGamedataMod SelectedDownloadGenericMod
        {
            get => selectedDownloadGenericMod;
            set { selectedDownloadGenericMod = value; OnPropertyChanged(nameof(SelectedDownloadGenericMod)); }
        }


        // ================= INSTALLED: GENERIC MODS =================

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

            RefreshInstalledGenericModSort();
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
            RefreshInstalledGenericModSort();
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
            RefreshInstalledGenericModSort();
        }

        public void RefreshInstalledGenericModSort()
        {
            FilteredEnabledGenericMods = new ObservableCollection<GenericGamedataMod>(ApplyGenericModSort(EnabledGenericMods, InstalledGenericModSort));
            FilteredDisabledGenericMods = new ObservableCollection<GenericGamedataMod>(ApplyGenericModSort(DisabledGenericMods, InstalledGenericModSort));
            OnPropertyChanged(nameof(FilteredEnabledGenericMods));
            OnPropertyChanged(nameof(FilteredDisabledGenericMods));
        }

        public void RefreshDownloadGenericModSort()
        {
            FilteredDownloadableGenericMods = new ObservableCollection<GenericGamedataMod>(ApplyGenericModSort(DownloadableGenericMods, DownloadGenericModSort));
            OnPropertyChanged(nameof(FilteredDownloadableGenericMods));
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
                Globals.GetDataPath(), "Mods", "GenericMods", "Enabled"
            );
            string gameDataPath = Path.Combine(GamePath, "gamedata");

            foreach (var mod in EnabledGenericMods.Concat(DisabledGenericMods))
                gamedataService.DisableGenericMod(mod, gameDataPath);

            foreach (var mod in EnabledGenericMods)
            {
                try 
                { 
                    gamedataService.EnableGenericMod(mod, modsEnabledPath, gameDataPath); 
                }
                catch (Exception ex) 
                { 
                    MessageBox.Show($"Could not enable {mod.FileName}: {ex.Message}"); 
                }
            }

            storageService.SaveGenericModsState(EnabledGenericMods.Concat(DisabledGenericMods));
        }

        public void UninstallGenericMod(GenericGamedataMod mod)
        {
            storageService.DeleteGenericMod(mod);
            EnabledGenericMods.Remove(mod);
            DisabledGenericMods.Remove(mod);

            OnPropertyChanged(nameof(EnabledGenericMods));
            OnPropertyChanged(nameof(DisabledGenericMods));
        }

        public void UninstallAllGenericMods()
        {
            DisableAllGenericMods();

            foreach (var mod in EnabledGenericMods.Concat(DisabledGenericMods).ToList())
                storageService.DeleteGenericMod(mod);

            EnabledGenericMods.Clear();
            DisabledGenericMods.Clear();
        }

        // ================= DOWNLOAD: MAPS =================

        public async Task LoadDownloadableMapsAsync()
        {
            try
            {
                var all = await storageService.GetDownloadableMapsAsync();
                var installed = storageService.GetInstalledMaps();
                var fileNames = installed.Select(m => m.FileName).ToHashSet();

                foreach (var map in all)
                    map.IsDownloaded = fileNames.Contains(map.FileName);

                DownloadableMaps = new ObservableCollection<Map>(all);
                OnPropertyChanged(nameof(DownloadableMaps));

                RefreshDownloadMapFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load downloadable maps: {ex.Message}");
            }
        }

        public async Task DownloadSelectedMapsAsync(IEnumerable<Map> maps)
        {
            int successCount = 0;
            var errors = new List<string>();

            List<Map> installed = this.storageService.GetInstalledMaps();
            var newlyDownloaded = new List<Map>();

            foreach (var map in maps)
            {
                try
                {
                    if (installed.Any(m => m.FileName.Equals(map.FileName, StringComparison.OrdinalIgnoreCase)))
                        throw new Exception($"A mod with the filename '{map.FileName}' is already installed. Please uninstall it before downloading.");

                    await storageService.DownloadMapAsync(map);
                    map.IsDownloaded = true;
                    map.IsEnabled = false;
                    newlyDownloaded.Add(map);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{map.FileName}: {ex.Message}");
                }
            }

            if (newlyDownloaded.Any())
            {
                var existingState = storageService.LoadMapsState();
                var existingByFile = existingState.ToDictionary(m => m.FileName, m => m, StringComparer.OrdinalIgnoreCase);

                foreach (var map in newlyDownloaded)
                    existingByFile[map.FileName] = map;

                storageService.SaveMapsState(existingByFile.Values);
            }

            if (errors.Any())
                MessageBox.Show($"Failed to download:\n{string.Join("\n", errors)}");
            else
                MessageBox.Show($"{successCount} map(s) downloaded successfully.");
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

            RefreshDownloadGenericModSort();
        }

        public async Task DownloadSelectedGenericModsAsync(IEnumerable<GenericGamedataMod> mods)
        {
            int successCount = 0;
            var errors = new List<string>();
            var newlyDownloaded = new List<GenericGamedataMod>();

            List<GenericGamedataMod> installed = this.storageService.GetInstalledGenericMods();

            foreach (var mod in mods)
            {
                try
                {
                    if (installed.Any(m => m.FileName.Equals(mod.FileName, StringComparison.OrdinalIgnoreCase)))
                        throw new Exception($"A mod with the filename '{mod.FileName}' is already installed. Please uninstall it before downloading.");

                    await storageService.DownloadGenericModAsync(mod);
                    mod.IsDownloaded = true;
                    mod.IsEnabled = false;
                    newlyDownloaded.Add(mod);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{mod.FileName}: {ex.Message}");
                }
            }

            if (newlyDownloaded.Any())
            {
                var existingState = storageService.LoadGenericModsState();
                var existingByFile = existingState.ToDictionary(m => m.FileName, m => m, StringComparer.OrdinalIgnoreCase);

                foreach (var mod in newlyDownloaded)
                    existingByFile[mod.FileName] = mod;

                storageService.SaveGenericModsState(existingByFile.Values);
            }

            if (errors.Any())
                MessageBox.Show($"Failed to download:\n{string.Join("\n", errors)}");
            else
                MessageBox.Show($"{successCount} mod(s) downloaded successfully.");
        }

        // ================= MAP FILTERING =================

        public IEnumerable<Map> GetFilteredEnabledMaps()
            => EnabledMaps.Where(m => InstalledMapsFilter.Passes(m));

        public IEnumerable<Map> GetFilteredDisabledMaps()
            => DisabledMaps.Where(m => InstalledMapsFilter.Passes(m));

        public IEnumerable<Map> GetFilteredDownloadableMaps()
            => DownloadableMaps.Where(m => DownloadMapsFilter.Passes(m));

        public void RefreshInstalledMapFilters()
        {
            FilteredEnabledMaps = new ObservableCollection<Map>(ApplyMapSort(EnabledMaps.Where(m => InstalledMapsFilter.Passes(m)), InstalledMapSort));
            FilteredDisabledMaps = new ObservableCollection<Map>(ApplyMapSort(DisabledMaps.Where(m => InstalledMapsFilter.Passes(m)), InstalledMapSort));
            OnPropertyChanged(nameof(FilteredEnabledMaps));
            OnPropertyChanged(nameof(FilteredDisabledMaps));
        }

        public void RefreshDownloadMapFilters()
        {
            FilteredDownloadableMaps = new ObservableCollection<Map>(ApplyMapSort(DownloadableMaps.Where(m => DownloadMapsFilter.Passes(m)), DownloadMapSort));
            OnPropertyChanged(nameof(FilteredDownloadableMaps));
        }

        // ================= MANUAL IMPORT =================

        public async Task<bool> ImportModFromFilePickerAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a mod file",
                Filter = "All Supported Files (*.scd;*.zip)|*.scd;*.zip|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true) return false;

            return await ImportModFilesAsync(dialog.FileNames);
        }

        public async Task<bool> ImportModFilesAsync(IEnumerable<string> files)
        {
            List<GenericGamedataMod> installed = this.storageService.GetInstalledGenericMods();
            int successCount = 0;

            foreach (var file in files)
            {
                try
                {
                    if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string tempDir = Path.Combine(Path.GetTempPath(), "SC2_import_extract");
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                        Directory.CreateDirectory(tempDir);

                        await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(file, tempDir));

                        foreach (var scd in Directory.GetFiles(tempDir, "*.scd", SearchOption.AllDirectories))
                        {
                            if (!installed.Any(m => m.FileName.Equals(Path.GetFileName(scd), StringComparison.OrdinalIgnoreCase)))
                            {
                                await storageService.ImportGenericModAsync(scd);
                                successCount++;
                            }
                        }
                        Directory.Delete(tempDir, true);
                    }
                    else if (file.EndsWith(".scd", StringComparison.OrdinalIgnoreCase))
                    {
                        if (installed.Any(m => m.FileName.Equals(Path.GetFileName(file), StringComparison.OrdinalIgnoreCase)))
                            throw new Exception($"A mod with the filename '{Path.GetFileName(file)}' is already installed.");

                        await storageService.ImportGenericModAsync(file);
                        successCount++;
                    }
                    else
                    {
                        // Non-standard file type — warn the user
                        var confirm = MessageBox.Show(
                            $"'{Path.GetFileName(file)}' is not a standard .scd file. " +
                            "Importing non-standard files could cause issues with your game.\n\n" +
                            "Are you sure you want to import it into the gamedata folder?",
                            "Non-Standard File",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (confirm != MessageBoxResult.Yes) continue;

                        await storageService.ImportGenericModAsync(file);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            LoadInstalledGenericMods();
            return successCount > 0;
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
                string installPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string exeName = "SC2ModManager.exe";

                if (!File.Exists(updaterPath))
                {
                    MessageBox.Show("Updater not found.");
                    return;
                }

                await DownloadFileWithProgress(updateDownloadUrl, zipPath);
                MessageBox.Show("Download complete. Installing update...");
                //File.WriteAllText(
                //    Path.Combine(installPath, "updater_debug.txt"),
                //    $"zipPath: {zipPath}\ninstallPath: {installPath}\nexeName: {exeName}\nFull args: \"{zipPath}\" \"{installPath}\" \"{exeName}\""
                //);

                try
                {
                    string zoneFile = updaterPath + ":Zone.Identifier";
                    if (File.Exists(zoneFile))
                        File.Delete(zoneFile);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = $"\"{zipPath}\" \"{installPath}\" \"{exeName}\"",
                        UseShellExecute = true,
                        WorkingDirectory = installPath
                    });
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    MessageBox.Show(
                        $"The update was cancelled. If Windows is blocking the updater, please run SC2MMUpdater.exe manually first:\n\n" +
                        $"1. Open this folder: {Path.GetDirectoryName(updaterPath)}\n" +
                        $"2. Double-click SC2MMUpdater.exe\n" +
                        $"3. Click \"More info\"\n" +
                        $"4. Click \"Run Anyway\"\n" +
                        $"5. Close the window\n\n" +
                        $"Then try updating again.",
                        "Update Cancelled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    // Clean up the downloaded zip since we aren't updating
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }

                //Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Updater failed: {ex.Message}");
            }
        }

        public async Task DownloadFileWithProgress(string url, string outputPath)
        {
            using HttpClient client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            });

            client.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager");

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

            await fileStream.FlushAsync();

            if (canReport && totalRead != totalBytes)
                throw new Exception($"Download incomplete. Expected {totalBytes} bytes but got {totalRead}.");
        }

        // ================= SETTINGS =================
        public void Uninstall()
        {
            var confirm = MessageBox.Show(
                "This will permanently delete all SC2 Mod Manager files including your downloaded mods, presets, and configuration.\n\nAre you sure you want to uninstall?",
                "Confirm Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var installService = new InstallService();
            string installPath = Globals.GetInstallPath();

            if (!string.IsNullOrEmpty(installPath))
            {
                installService.Uninstall(installPath);
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show("Install path not found. Please delete the application folder manually.");
            }
        }

        // ================= EVENTS =================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}