/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 12, 2026
 * Author: Jacob Wixom
 * 
*/
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
using System.Threading.Tasks;
using System.Windows;

namespace SC2ModManager.ViewModels
{
    public class HotkeyEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly HotkeyService _service = new();

        public HotkeyEditorViewModel()
        {
            // Wire up duplicate detection once — handles all loads/reloads automatically
            AttachDuplicateWatcher(MainHotkeys);
            AttachDuplicateWatcher(TooltipHotkeys);
            AttachDuplicateWatcher(DebugHotkeys);
            AttachBuildModeDuplicateWatcher(BasicEngineering);
            AttachBuildModeDuplicateWatcher(AdvancedEngineering);
            AttachBuildModeDuplicateWatcher(ExperimentalEngineering);
            AttachBuildModeDuplicateWatcher(BasicLand);
            AttachBuildModeDuplicateWatcher(BasicAir);
            AttachBuildModeDuplicateWatcher(Sea);
            AttachBuildModeDuplicateWatcher(Experimental);
            AttachBuildModeDuplicateWatcher(ExperimentalLand);
            AttachBuildModeDuplicateWatcher(ExperimentalAir);
            AttachBuildModeDuplicateWatcher(FactoryAddons);
        }

        private static void AttachDuplicateWatcher(ObservableCollection<HotkeyEntry> collection)
        {
            collection.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (HotkeyEntry entry in e.NewItems)
                        entry.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(HotkeyEntry.KeyCombo))
                                RefreshDuplicates(collection);
                        };
                RefreshDuplicates(collection);
            };
        }

        private static void RefreshDuplicates(ObservableCollection<HotkeyEntry> collection)
        {
            var counts = collection
                .Where(e => !string.IsNullOrWhiteSpace(e.KeyCombo))
                .GroupBy(e => e.KeyCombo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var e in collection)
                e.IsDuplicate = !string.IsNullOrWhiteSpace(e.KeyCombo)
                    && counts.TryGetValue(e.KeyCombo.Trim(), out int c) && c > 1;
        }

        private static void AttachBuildModeDuplicateWatcher(ObservableCollection<BuildModeEntry> collection)
        {
            collection.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (BuildModeEntry entry in e.NewItems)
                        entry.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(BuildModeEntry.Key))
                                RefreshBuildModeDuplicates(collection);
                        };
                RefreshBuildModeDuplicates(collection);
            };
        }

        private static void RefreshBuildModeDuplicates(ObservableCollection<BuildModeEntry> collection)
        {
            var counts = collection
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .GroupBy(e => e.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var e in collection)
                e.IsDuplicate = !string.IsNullOrWhiteSpace(e.Key)
                    && counts.TryGetValue(e.Key.Trim(), out int c) && c > 1;
        }

        // ======================  Installation state ====================== 

        private bool _isNormalModInstalled;
        public bool IsNormalModInstalled
        {
            get => _isNormalModInstalled;
            private set { _isNormalModInstalled = value; OnPropertyChanged(nameof(IsNormalModInstalled)); }
        }

        private bool _isBuildModeModInstalled;
        public bool IsBuildModeModInstalled
        {
            get => _isBuildModeModInstalled;
            private set { _isBuildModeModInstalled = value; OnPropertyChanged(nameof(IsBuildModeModInstalled)); }
        }

        private bool _isNormalDownloading;
        public bool IsNormalDownloading
        {
            get => _isNormalDownloading;
            private set { _isNormalDownloading = value; OnPropertyChanged(nameof(IsNormalDownloading)); }
        }

        private bool _isBuildModeDownloading;
        public bool IsBuildModeDownloading
        {
            get => _isBuildModeDownloading;
            private set { _isBuildModeDownloading = value; OnPropertyChanged(nameof(IsBuildModeDownloading)); }
        }

        // ======================  Normal hotkeys — mode toggle ====================== 

        private bool _isNormalAdvancedMode;
        public bool IsNormalAdvancedMode
        {
            get => _isNormalAdvancedMode;
            set { _isNormalAdvancedMode = value; OnPropertyChanged(nameof(IsNormalAdvancedMode)); }
        }

        // ======================  Normal hotkeys — entry collections ====================== 

        public ObservableCollection<HotkeyEntry> MainHotkeys { get; } = new();
        public ObservableCollection<HotkeyEntry> TooltipHotkeys { get; } = new();
        public ObservableCollection<HotkeyEntry> DebugHotkeys { get; } = new();

        // ======================  Normal hotkeys — raw lua text (advanced mode) ====================== 

        private string _rawDefaultKeyMapText = string.Empty;
        public string RawDefaultKeyMapText
        {
            get => _rawDefaultKeyMapText;
            set { _rawDefaultKeyMapText = value; OnPropertyChanged(nameof(RawDefaultKeyMapText)); }
        }

        private string _rawKeyActionsText = string.Empty;
        public string RawKeyActionsText
        {
            get => _rawKeyActionsText;
            set { _rawKeyActionsText = value; OnPropertyChanged(nameof(RawKeyActionsText)); }
        }

        private string _rawKeyDescriptionsText = string.Empty;
        public string RawKeyDescriptionsText
        {
            get => _rawKeyDescriptionsText;
            set { _rawKeyDescriptionsText = value; OnPropertyChanged(nameof(RawKeyDescriptionsText)); }
        }

        // ======================  Build mode — mode toggle ======================

        private bool _isBuildModeAdvancedMode;
        public bool IsBuildModeAdvancedMode
        {
            get => _isBuildModeAdvancedMode;
            set { _isBuildModeAdvancedMode = value; OnPropertyChanged(nameof(IsBuildModeAdvancedMode)); }
        }

        // ======================  Build mode — faction selection ====================== 

        private BuildModeFaction _selectedFaction = BuildModeFaction.UEF;
        public BuildModeFaction SelectedFaction
        {
            get => _selectedFaction;
            set
            {
                _selectedFaction = value;
                OnPropertyChanged(nameof(SelectedFaction));
                OnPropertyChanged(nameof(IsUEFSelected));
                OnPropertyChanged(nameof(IsCybranSelected));
                OnPropertyChanged(nameof(IsIlluminateSelected));
                FilterBuildModeEntries();
            }
        }

        public bool IsUEFSelected
        {
            get => SelectedFaction == BuildModeFaction.UEF;
            set { if (value) SelectedFaction = BuildModeFaction.UEF; }
        }
        public bool IsCybranSelected
        {
            get => SelectedFaction == BuildModeFaction.Cybran;
            set { if (value) SelectedFaction = BuildModeFaction.Cybran; }
        }
        public bool IsIlluminateSelected
        {
            get => SelectedFaction == BuildModeFaction.Illuminate;
            set { if (value) SelectedFaction = BuildModeFaction.Illuminate; }
        }

        // ======================  Build mode — entry collections ====================== 

        private List<BuildModeEntry> _allBuildModeEntries = new();

        // Category -> observable collection for that faction's entries in that category
        public ObservableCollection<BuildModeEntry> BasicEngineering { get; } = new();
        public ObservableCollection<BuildModeEntry> AdvancedEngineering { get; } = new();
        public ObservableCollection<BuildModeEntry> ExperimentalEngineering { get; } = new();
        public ObservableCollection<BuildModeEntry> BasicLand { get; } = new();
        public ObservableCollection<BuildModeEntry> BasicAir { get; } = new();
        public ObservableCollection<BuildModeEntry> Sea { get; } = new();
        public ObservableCollection<BuildModeEntry> Experimental { get; } = new();
        public ObservableCollection<BuildModeEntry> ExperimentalLand { get; } = new();
        public ObservableCollection<BuildModeEntry> ExperimentalAir { get; } = new();
        public ObservableCollection<BuildModeEntry> FactoryAddons { get; } = new();

        // ======================  Build mode — raw lua text ======================

        private string _rawBuildModeText = string.Empty;
        public string RawBuildModeText
        {
            get => _rawBuildModeText;
            set { _rawBuildModeText = value; OnPropertyChanged(nameof(RawBuildModeText)); }
        }

        // ======================  Load ======================

        public void LoadNormalHotkeys(string? gamedataPath = null)
        {
            // Pick up whatever luo.scd is actually installed in gamedata before loading, so the
            // editor edits (and later re-applies) the user's real file and not a stale local copy
            if (!string.IsNullOrEmpty(gamedataPath))
            {
                try { _service.SyncLocalFromGamedata(HotkeyModType.NormalHotkey, gamedataPath); }
                catch { /* if the sync fails the editor still works on the local copy */ }
            }

            IsNormalModInstalled = _service.IsModInstalled(HotkeyModType.NormalHotkey);
            if (!IsNormalModInstalled)
                return;

            try
            {
                var entries = _service.ReadDefaultKeyMap();
                MainHotkeys.Clear();
                TooltipHotkeys.Clear();
                DebugHotkeys.Clear();

                foreach (var e in entries)
                {
                    switch (e.Section)
                    {
                        case HotkeySection.Main: MainHotkeys.Add(e); break;
                        case HotkeySection.Tooltip: TooltipHotkeys.Add(e); break;
                        case HotkeySection.Debug: DebugHotkeys.Add(e); break;
                    }
                }

                RawDefaultKeyMapText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/defaultKeyMap.lua");
                RawKeyActionsText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keyactions.lua");
                RawKeyDescriptionsText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keydescriptions.lua");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadBuildModeHotkeys(string? gamedataPath = null)
        {
            if (!string.IsNullOrEmpty(gamedataPath))
            {
                try { _service.SyncLocalFromGamedata(HotkeyModType.BuildModeHotkey, gamedataPath); }
                catch { /* if the sync fails the editor still works on the local copy */ }
            }

            IsBuildModeModInstalled = _service.IsModInstalled(HotkeyModType.BuildModeHotkey);
            if (!IsBuildModeModInstalled)
                return;

            try
            {
                _allBuildModeEntries = _service.ReadBuildModeData();
                FilterBuildModeEntries();
                RawBuildModeText = _service.ReadRawLuaFile(HotkeyModType.BuildModeHotkey, "mods/DLC1/shadow/lua/ui/game/buildmodedata.lua");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load build mode hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterBuildModeEntries()
        {
            var collections = new Dictionary<string, ObservableCollection<BuildModeEntry>>(StringComparer.OrdinalIgnoreCase)
            {
                { "BasicEngineering",       BasicEngineering },
                { "AdvancedEngineering",    AdvancedEngineering },
                { "ExperimentalEngineering",ExperimentalEngineering },
                { "BasicLand",              BasicLand },
                { "BasicAir",               BasicAir },
                { "Sea",                    Sea },
                { "Experimental",           Experimental },
                { "ExperimentalLand",       ExperimentalLand },
                { "ExperimentalAir",        ExperimentalAir },
                { "FactoryAddons",          FactoryAddons },
            };

            foreach (var col in collections.Values) 
                col.Clear();

            foreach (var entry in _allBuildModeEntries.Where(e => e.Faction == _selectedFaction))
            {
                if (collections.TryGetValue(entry.Category, out var col))
                    col.Add(entry);
            }
        }

        // ======================  Save ======================

        public void SaveNormalHotkeys(string gamedataPath)
        {
            if (!IsNormalModInstalled) 
                return;

            try
            {
                if (IsNormalAdvancedMode)
                {
                    _service.WriteRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/defaultKeyMap.lua", RawDefaultKeyMapText);
                    _service.WriteRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keyactions.lua", RawKeyActionsText);
                    _service.WriteRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keydescriptions.lua", RawKeyDescriptionsText);
                    _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                    // In advanced mode, reload the grid from the saved file so it stays in sync
                    LoadNormalHotkeys();
                }
                else
                {
                    var all = MainHotkeys.Concat(TooltipHotkeys).Concat(DebugHotkeys).ToList();
                    _service.WriteDefaultKeyMap(all);
                    _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                    // In normal mode the collections already reflect the user's edits via two-way binding.
                    // Just refresh the raw text fields so switching to Advanced mode shows the saved state.
                    RawDefaultKeyMapText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/defaultKeyMap.lua");
                    RawKeyActionsText    = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keyactions.lua");
                    RawKeyDescriptionsText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, "lua/keymap/keydescriptions.lua");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveBuildModeHotkeys(string gamedataPath)
        {
            if (!IsBuildModeModInstalled) 
                return;

            try
            {
                if (IsBuildModeAdvancedMode)
                {
                    _service.WriteRawLuaFile(HotkeyModType.BuildModeHotkey, "mods/DLC1/shadow/lua/ui/game/buildmodedata.lua", RawBuildModeText);
                    _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                    LoadBuildModeHotkeys();
                }
                else
                {
                    _service.WriteBuildModeData(_allBuildModeEntries);
                    _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                    // In normal mode the collections already reflect the user's edits.
                    // Just refresh raw text for if the user switches to Advanced mode.
                    RawBuildModeText = _service.ReadRawLuaFile(HotkeyModType.BuildModeHotkey, "mods/DLC1/shadow/lua/ui/game/buildmodedata.lua");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save build mode hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ======================  Restore originals ======================

        public void RestoreNormalDefaults(string gamedataPath)
        {
            if (!IsNormalModInstalled) 
                return;

            if (!_service.HasBackups(HotkeyModType.NormalHotkey))
            {
                MessageBox.Show("No original backup found. Try re-downloading the mod to restore it.", "No Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "This will restore all normal hotkeys to the originals from when you first imported the mod. Continue?",
                "Restore Originals", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) 
                return;

            try
            {
                _service.RestoreFromBackups(HotkeyModType.NormalHotkey);
                _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                LoadNormalHotkeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RestoreBuildModeDefaults(string gamedataPath)
        {
            if (!IsBuildModeModInstalled) 
                return;

            if (!_service.HasBackups(HotkeyModType.BuildModeHotkey))
            {
                MessageBox.Show("No original backup found. Try re-downloading the mod to restore it.", "No Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "This will restore all build mode hotkeys to the originals from when you first imported the mod. Continue?",
                "Restore Originals", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) 
                return;

            try
            {
                _service.RestoreFromBackups(HotkeyModType.BuildModeHotkey);
                _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ======================  Uninstall ======================

        public void UninstallNormalMod(string gamedataPath)
        {
            // Advanced setting for modders: leave luo.scd / toc.win.bdf in the game untouched
            bool keepLuo = new ConfigService().Load()?.KeepLuoOnUninstall ?? false;

            string message = keepLuo
                ? "Uninstall the hotkey mod?\n\n'Keep hotkey mod game files' is enabled, so this will:\n\u2022 Leave luo.scd and toc.win.bdf in your game exactly as they are (the original lua.scd will NOT be restored)\n\u2022 Delete the mod manager's local copy and backups\n\nYour game files stay altered and are yours to maintain."
                : "Uninstall the hotkey mod?\n\nThis will:\n\u2022 Restore your game's keymap files to how they were before the mod was installed through the mod manager (the original lua.scd, or your own luo.scd if you had the mod before using the mod manager)\n\u2022 Delete the mod manager's local copy and backup";

            var confirm = MessageBox.Show(
                message,
                "Uninstall Hotkey Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _service.UninstallMod(HotkeyModType.NormalHotkey, gamedataPath, keepLuo);
                MainHotkeys.Clear();
                TooltipHotkeys.Clear();
                DebugHotkeys.Clear();
                RawDefaultKeyMapText = string.Empty;
                RawKeyActionsText = string.Empty;
                RawKeyDescriptionsText = string.Empty;
                IsNormalModInstalled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstall failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UninstallBuildModeMod(string gamedataPath)
        {
            var confirm = MessageBox.Show(
                "Uninstall the build mode hotkey mod?\n\nThis will:\n\u2022 Delete BuildmodeHotkeys.scd from the game's gamedata folder\n\u2022 Delete your local copy and backup",
                "Uninstall Build Mode Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (confirm != MessageBoxResult.Yes) 
                return;

            try
            {
                _service.UninstallMod(HotkeyModType.BuildModeHotkey, gamedataPath);
                _allBuildModeEntries.Clear();
                FilterBuildModeEntries();
                RawBuildModeText = string.Empty;
                IsBuildModeModInstalled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstall failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ======================  Import ======================

        public void ImportNormalModFile(string sourcePath)
        {
            try
            {
                _service.ImportModFile(sourcePath, HotkeyModType.NormalHotkey);
                LoadNormalHotkeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ImportBuildModeModFile(string sourcePath)
        {
            try
            {
                _service.ImportModFile(sourcePath, HotkeyModType.BuildModeHotkey);
                LoadBuildModeHotkeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ======================  Download & install ======================

        private static readonly HttpClient _httpClient = new HttpClient();

        static HotkeyEditorViewModel()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.0");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task DownloadAndInstallNormalMod(string gamedataPath)
        {
            // Make sure the user knows this touches files the game needs before anything downloads
            var confirm = MessageBox.Show(
                "Installing the hotkey mod edits game files that are required to run the game:\n\n" +
                "• lua.scd is backed up and replaced with luo.scd\n" +
                "• toc.win.bdf is added to the game folder\n\n" +
                "If you ever want the original files back, uninstall the hotkey mod from this screen and they will be restored. " +
                "Uninstalling the mod manager from Settings will also restore them.\n\nContinue?",
                "Install Hotkey Mod", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK)
                return;

            IsNormalDownloading = true;
            try
            {
                string destDir = Globals.GetHotkeyModsPath();
                Directory.CreateDirectory(destDir);
                string scdPath = Path.Combine(destDir, Globals.NormalHotkeyScdName);
                string bdfPath = Globals.GetLocalTocBdfPath();

                await Task.Run(async () =>
                {
                    // Download luo.scd
                    using var r1 = await _httpClient.GetAsync(Globals.NormalHotkeyDirectDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    r1.EnsureSuccessStatusCode();
                    using var s1 = await r1.Content.ReadAsStreamAsync();
                    using var f1 = new FileStream(scdPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await s1.CopyToAsync(f1);

                    // Download toc.win.bdf
                    using var r2 = await _httpClient.GetAsync(Globals.NormalHotkeyTocBdfDirectDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    r2.EnsureSuccessStatusCode();
                    using var s2 = await r2.Content.ReadAsStreamAsync();
                    using var f2 = new FileStream(bdfPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await s2.CopyToAsync(f2);
                });

                // Backup the original lua.scd (only once) and apply the mod directly to the game
                _service.CreateBackupsIfAbsent(HotkeyModType.NormalHotkey);
                _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                LoadNormalHotkeys();
                MessageBox.Show("Normal hotkey mod downloaded and installed successfully.", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsNormalDownloading = false;
            }
        }

        public async Task DownloadAndInstallBuildModeMod(string gamedataPath)
        {
            IsBuildModeDownloading = true;
            try
            {
                string destDir = Globals.GetHotkeyModsPath();
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Globals.BuildModeScdName);

                await Task.Run(async () =>
                {
                    using var response = await _httpClient.GetAsync(Globals.BuildModeDirectDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream);
                });

                _service.CreateBackupsIfAbsent(HotkeyModType.BuildModeHotkey);
                // Actually place the .scd in gamedata — without this the mod only exists in the data
                // folder and the game never sees it until the user happens to save an edit
                _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys();
                MessageBox.Show("Build mode hotkey mod downloaded and installed successfully.", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBuildModeDownloading = false;
            }
        }
    }
}
