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
using System.Windows.Data;

namespace SC2ModManager.ViewModels
{
    public class HotkeyEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly HotkeyService _service = new();

        // The gamedata folder from the last time the editor was opened. Reloads after a save use it
        private string? _gamedataPath;

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

            SetUpHotkeySearch();
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

        // ======================  Normal hotkeys — search ======================
        // The search only hides rows in the grids (through the collections' default views). The
        // collections themselves still have everything, so saving and validation always see every hotkey

        private string _searchKey = string.Empty;
        private string _searchCommand = string.Empty;
        private string _searchDescription = string.Empty;

        public bool IsHotkeySearchActive =>
            _searchKey.Length > 0 || _searchCommand.Length > 0 || _searchDescription.Length > 0;

        private void SetUpHotkeySearch()
        {
            foreach (var collection in new[] { MainHotkeys, TooltipHotkeys, DebugHotkeys })
                CollectionViewSource.GetDefaultView(collection).Filter = item => item is HotkeyEntry e && MatchesHotkeySearch(e);
        }

        /// <summary>
        ///     Filters the three hotkey grids. Empty boxes are ignored, filled ones all have to match.
        ///     Returns how many hotkeys are showing in each section.
        /// </summary>
        public (int main, int tooltip, int debug) SearchHotkeys(string? key, string? command, string? description)
        {
            _searchKey = key?.Trim() ?? string.Empty;
            _searchCommand = command?.Trim() ?? string.Empty;
            _searchDescription = description?.Trim() ?? string.Empty;

            static int RefreshAndCount(ObservableCollection<HotkeyEntry> collection)
            {
                var view = CollectionViewSource.GetDefaultView(collection);
                view.Refresh();
                return view.Cast<object>().Count();
            }

            return (RefreshAndCount(MainHotkeys), RefreshAndCount(TooltipHotkeys), RefreshAndCount(DebugHotkeys));
        }

        private bool MatchesHotkeySearch(HotkeyEntry e)
        {
            if (_searchKey.Length > 0 && !KeyMatchesSearch(e.KeyCombo, _searchKey))
                return false;
            if (_searchCommand.Length > 0 && !(e.Command ?? string.Empty).Contains(_searchCommand, StringComparison.OrdinalIgnoreCase))
                return false;
            if (_searchDescription.Length > 0 && !(e.Description ?? string.Empty).Contains(_searchDescription, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        // Searching "R" should find R, Ctrl-R and Shift-R, but not every key that has an r in it like
        // Ctrl-1. So each part of the key gets checked on its own. If they type a dash (Ctrl-R) we just
        // look for that whole thing in the key
        private static bool KeyMatchesSearch(string? keyCombo, string search)
        {
            keyCombo ??= string.Empty;

            if (search.Contains('-'))
                return keyCombo.Contains(search, StringComparison.OrdinalIgnoreCase);

            return keyCombo.Split('-').Any(part => part.StartsWith(search, StringComparison.OrdinalIgnoreCase));
        }

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

        // ======================  Game running check ======================

        /// <summary>
        ///     Shows a message and returns false if the game is open. Every action that changes the
        ///     game's hotkey files checks this first.
        /// </summary>
        private static bool CheckGameClosed(string whatYouWereDoing)
        {
            if (!DllInjectionService.IsGameRunning())
                return true;

            MessageBox.Show(
                $"Supreme Commander 2 is running. Close the game before you {whatYouWereDoing}.\n\n" +
                "The game has its hotkey files open while it runs, so changing them now could break it.",
                "Close the Game First", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        /// <summary>
        ///     Stops a save if the file in the game got changed or replaced after the editor loaded it
        ///     (another tool, or a modder dropping in a new luo.scd while the mod manager was open).
        ///     Saving would put our older copy over theirs and they'd lose whatever they changed. Instead
        ///     we reload their new file and have them make the hotkey change again.
        /// </summary>
        private bool CheckGameFileUnchanged(HotkeyModType modType, string gamedataPath)
        {
            if (!_service.GamedataChangedSinceLoad(modType, gamedataPath))
                return true;

            string fileName = modType == HotkeyModType.NormalHotkey ? Globals.NormalHotkeyScdName : Globals.BuildModeScdName;

            MessageBox.Show(
                $"The {fileName} in your game was changed after the hotkey editor loaded it, maybe by another tool or by replacing it yourself.\n\n" +
                "So it doesn't get overwritten, nothing was saved. The editor has reloaded the version that's in your game now, " +
                "so make your hotkey changes again and save.",
                "Game File Changed", MessageBoxButton.OK, MessageBoxImage.Warning);

            if (modType == HotkeyModType.NormalHotkey)
                LoadNormalHotkeys(gamedataPath);
            else
                LoadBuildModeHotkeys(gamedataPath);

            return false;
        }

        // ======================  Load ======================

        public void LoadNormalHotkeys(string? gamedataPath = null)
        {
            if (!string.IsNullOrEmpty(gamedataPath))
                _gamedataPath = gamedataPath;

            // Pick up whatever luo.scd is actually installed in gamedata before loading, so the
            // editor edits (and later re-applies) the user's real file and not a stale local copy
            string? newLuoBackup = null;
            if (!string.IsNullOrEmpty(_gamedataPath))
            {
                try { newLuoBackup = _service.SyncLocalFromGamedata(HotkeyModType.NormalHotkey, _gamedataPath); }
                catch { /* if the sync fails, saving gets blocked by the "game file changed" check so nothing gets overwritten */ }
            }

            // Installed means luo.scd is actually in the game, not just that we have a local copy. If
            // someone took it out of gamedata themselves, the next save shouldn't sneak it back in
            IsNormalModInstalled = _service.IsInstalledInGame(HotkeyModType.NormalHotkey, _gamedataPath)
                && _service.IsModInstalled(HotkeyModType.NormalHotkey);

            if (!IsNormalModInstalled)
            {
                MainHotkeys.Clear();
                TooltipHotkeys.Clear();
                DebugHotkeys.Clear();
                return;
            }

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

                // Changing a main hotkey updates its tooltip too, so the tooltip doesn't keep showing the old key
                HotkeyService.LinkTooltipsToMainHotkeys(MainHotkeys, TooltipHotkeys);

                RawDefaultKeyMapText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, HotkeyService.DefaultKeyMapLuaPath);
                RawKeyActionsText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, HotkeyService.KeyActionsLuaPath);
                RawKeyDescriptionsText = _service.ReadRawLuaFile(HotkeyModType.NormalHotkey, HotkeyService.KeyDescriptionsLuaPath);

                // First time we've seen this luo.scd (their own, or one that got swapped in), so tell them
                // there's a copy of it from before we touched anything, and how to use it
                if (newLuoBackup != null)
                    MessageBox.Show(
                        "The mod manager found a luo.scd in your game it hasn't seen before, so it saved a copy of it before making any hotkey changes:\n\n" +
                        $"{newLuoBackup}\n\n" +
                        "If anything ever goes wrong with your hotkeys, copy that file into your gamedata folder and rename it to luo.scd (with the game closed).",
                        "luo.scd Backed Up", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadBuildModeHotkeys(string? gamedataPath = null)
        {
            if (!string.IsNullOrEmpty(gamedataPath))
                _gamedataPath = gamedataPath;

            if (!string.IsNullOrEmpty(_gamedataPath))
            {
                try { _service.SyncLocalFromGamedata(HotkeyModType.BuildModeHotkey, _gamedataPath); }
                catch { /* if the sync fails the editor still works on the local copy */ }
            }

            IsBuildModeModInstalled = _service.IsInstalledInGame(HotkeyModType.BuildModeHotkey, _gamedataPath)
                && _service.IsModInstalled(HotkeyModType.BuildModeHotkey);

            if (!IsBuildModeModInstalled)
            {
                _allBuildModeEntries.Clear();
                FilterBuildModeEntries();
                return;
            }

            try
            {
                _allBuildModeEntries = _service.ReadBuildModeData();
                FilterBuildModeEntries();
                RawBuildModeText = _service.ReadRawLuaFile(HotkeyModType.BuildModeHotkey, HotkeyService.BuildModeLuaPath);
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

        // ======================  Validation ======================

        /// <summary>
        ///     Checks the normal hotkeys before saving. Every hotkey needs a key, and any key that got
        ///     changed has to be one the game knows. Keys that came from the file are left alone even if
        ///     they're weird, that's how the mod shipped. Duplicates get a warning you can save past.
        /// </summary>
        private bool ValidateNormalHotkeys()
        {
            var problems = new List<string>();
            var all = MainHotkeys.Concat(TooltipHotkeys).Concat(DebugHotkeys).ToList();

            // Right click > Paste can sneak spaces in ("R "), and the save writes exactly what's in the box.
            // Trim everything first so what gets checked is exactly what gets saved
            foreach (var e in all)
            {
                string trimmed = e.KeyCombo?.Trim() ?? string.Empty;
                if (e.KeyCombo != trimmed)
                    e.KeyCombo = trimmed;
            }

            foreach (var e in all)
            {
                string key = e.KeyCombo;

                if (key.Length == 0)
                    problems.Add($"• {e.Command} has no key. Every hotkey needs one.");
                else if (key != e.OriginalKeyCombo && !HotkeyService.IsValidGameKey(key))
                    problems.Add($"• {e.Command}: \"{key}\" isn't a key the game recognizes.");
            }

            if (problems.Count > 0)
            {
                ShowCantSave(problems);
                return false;
            }

            // Duplicates only matter in Main and Debug. In a lua table the last one wins, so the other
            // action quietly loses its hotkey. Only warn about ones that involve something you changed
            var dupes = FindChangedDuplicates(MainHotkeys).Concat(FindChangedDuplicates(DebugHotkeys)).ToList();
            return dupes.Count == 0 || ConfirmDuplicates(dupes);
        }

        private static IEnumerable<string> FindChangedDuplicates(IEnumerable<HotkeyEntry> entries) =>
            entries
                .Where(e => !string.IsNullOrWhiteSpace(e.KeyCombo))
                .GroupBy(e => e.KeyCombo.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Any(e => e.KeyCombo != e.OriginalKeyCombo))
                .Select(g => $"• {g.Key}: {string.Join(", ", g.Select(e => e.Command))}");

        private bool ValidateBuildModeHotkeys()
        {
            var problems = new List<string>();

            // Same as the normal hotkeys, trim first so what gets checked is what gets saved
            foreach (var e in _allBuildModeEntries)
            {
                string trimmed = e.Key?.Trim() ?? string.Empty;
                if (e.Key != trimmed)
                    e.Key = trimmed;
            }

            foreach (var e in _allBuildModeEntries)
            {
                string key = e.Key;
                string name = string.IsNullOrEmpty(e.Comment) ? e.UnitId : e.Comment;

                if (key.Length == 0)
                    problems.Add($"• {e.Faction} {name} has no key. Every hotkey needs one.");
                else if (key != e.OriginalKey && !HotkeyService.IsValidBuildModeKey(key))
                    problems.Add($"• {e.Faction} {name}: \"{key}\" can't be used. Build mode keys have to be a single letter A-Z.");
            }

            if (problems.Count > 0)
            {
                ShowCantSave(problems);
                return false;
            }

            var dupes = _allBuildModeEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .GroupBy(e => (e.Faction, e.Category, Key: e.Key.Trim().ToUpperInvariant()))
                .Where(g => g.Count() > 1 && g.Any(e => e.Key != e.OriginalKey))
                .Select(g => $"• {g.Key.Faction} {g.Key.Category} {g.Key.Key}: {string.Join(", ", g.Select(e => string.IsNullOrEmpty(e.Comment) ? e.UnitId : e.Comment))}")
                .ToList();

            return dupes.Count == 0 || ConfirmDuplicates(dupes);
        }

        private static void ShowCantSave(List<string> problems)
        {
            string list = string.Join("\n", problems.Take(15));
            if (problems.Count > 15)
                list += $"\n...and {problems.Count - 15} more";

            MessageBox.Show($"These hotkeys can't be saved:\n\n{list}\n\nPress Escape on a key to put it back to how it was.",
                "Can't Save Hotkeys", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static bool ConfirmDuplicates(List<string> dupes)
        {
            string list = string.Join("\n", dupes.Take(15));
            if (dupes.Count > 15)
                list += $"\n...and {dupes.Count - 15} more";

            var result = MessageBox.Show(
                $"These keys are used more than once in the same section:\n\n{list}\n\n" +
                "The game only keeps one of them, so the other action won't have a working hotkey. Save anyway?",
                "Duplicate Hotkeys", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes;
        }

        // ======================  Save ======================

        /// <summary>
        ///     Saves the normal hotkeys and applies them to the game. Returns true only if it all worked.
        /// </summary>
        public bool SaveNormalHotkeys(string gamedataPath)
        {
            if (!IsNormalModInstalled)
                return false;

            if (!CheckGameClosed("save hotkeys"))
                return false;

            if (!CheckGameFileUnchanged(HotkeyModType.NormalHotkey, gamedataPath))
                return false;

            // Advanced mode is raw lua, which the warning when switching to it covers
            if (!IsNormalAdvancedMode && !ValidateNormalHotkeys())
                return false;

            try
            {
                if (IsNormalAdvancedMode)
                {
                    _service.WriteRawLuaFiles(HotkeyModType.NormalHotkey, new Dictionary<string, string>
                    {
                        [HotkeyService.DefaultKeyMapLuaPath] = RawDefaultKeyMapText,
                        [HotkeyService.KeyActionsLuaPath] = RawKeyActionsText,
                        [HotkeyService.KeyDescriptionsLuaPath] = RawKeyDescriptionsText
                    });
                }
                else
                {
                    var all = MainHotkeys.Concat(TooltipHotkeys).Concat(DebugHotkeys).ToList();
                    _service.WriteDefaultKeyMap(all);
                }

                _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);

                // Always reload from the saved file. Each entry remembers the key it had when it was
                // loaded and that's how the save finds its line in the file. Without the reload, a second
                // save would look for the old keys, miss the line, and quietly skip your change
                LoadNormalHotkeys(gamedataPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        ///     Saves the build mode hotkeys and applies them to the game. Returns true only if it all worked.
        /// </summary>
        public bool SaveBuildModeHotkeys(string gamedataPath)
        {
            if (!IsBuildModeModInstalled)
                return false;

            if (!CheckGameClosed("save hotkeys"))
                return false;

            if (!CheckGameFileUnchanged(HotkeyModType.BuildModeHotkey, gamedataPath))
                return false;

            if (!IsBuildModeAdvancedMode && !ValidateBuildModeHotkeys())
                return false;

            try
            {
                if (IsBuildModeAdvancedMode)
                {
                    _service.WriteRawLuaFiles(HotkeyModType.BuildModeHotkey,
                        new Dictionary<string, string> { [HotkeyService.BuildModeLuaPath] = RawBuildModeText });
                }
                else
                {
                    _service.WriteBuildModeData(_allBuildModeEntries);
                }

                _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys(gamedataPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save build mode hotkeys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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
                "This will put all of your normal hotkeys back to the originals from when the mod was first installed.\n\n" +
                "Only the hotkeys change. Everything else in luo.scd stays exactly how it is. Continue?",
                "Restore Originals", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!CheckGameClosed("restore your hotkeys"))
                return;

            if (!CheckGameFileUnchanged(HotkeyModType.NormalHotkey, gamedataPath))
                return;

            try
            {
                _service.RestoreDefaultKeyMapFromBackup();
                _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                LoadNormalHotkeys(gamedataPath);
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

            if (!CheckGameClosed("restore your hotkeys"))
                return;

            if (!CheckGameFileUnchanged(HotkeyModType.BuildModeHotkey, gamedataPath))
                return;

            try
            {
                _service.RestoreFromBackups(HotkeyModType.BuildModeHotkey);
                _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys(gamedataPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ======================  Uninstall ======================

        /// <summary>
        ///     The confirmation text for uninstalling the normal hotkey mod. Shared with uninstalling the
        ///     whole mod manager so both say the same thing.
        /// </summary>
        public static string GetNormalUninstallDescription(bool keepGameFiles) => keepGameFiles
            ? "'Keep hotkey mod game files' is enabled, so this will:\n" +
              "• Leave luo.scd and toc.win.bdf in your game exactly as they are (the original lua.scd will NOT be restored)\n" +
              "• Delete the mod manager's local copy and backups\n\n" +
              "Your game files stay altered and are yours to maintain."
            : "This removes all of your hotkey edits and puts your game's keymap back how it was before the mod manager installed the hotkey mod:\n" +
              "• The original lua.scd is restored\n" +
              "• luo.scd and the hotkey mod's toc.win.bdf are deleted from the game\n" +
              "• The mod manager's local copy and backups are deleted\n\n" +
              "If your luo.scd has changes from other mods (not just hotkeys), a copy is saved as luo.scd.bak in your game folder first.\n\n" +
              "If you already had your own luo.scd before using the mod manager, it stays in your game and only its hotkeys are put back.";

        /// <summary>
        ///     The message to show after an uninstall, or null if there's nothing extra to say.
        /// </summary>
        public static string? DescribeUninstallResult(HotkeyUninstallResult result)
        {
            var parts = new List<string>();

            if (result.KeptUserLuo)
                parts.Add("You had your own luo.scd before the mod manager, so it was left in your game with its hotkeys put back.");

            if (!string.IsNullOrEmpty(result.LuoBackupPath))
                parts.Add($"Your luo.scd had changes besides hotkeys, so a copy was saved here before anything changed:\n{result.LuoBackupPath}");

            return parts.Count == 0 ? null : string.Join("\n\n", parts);
        }

        public void UninstallNormalMod(string gamedataPath)
        {
            // Advanced setting for modders: leave luo.scd / toc.win.bdf in the game untouched
            bool keepLuo = new ConfigService().Load()?.KeepLuoOnUninstall ?? false;

            var confirm = MessageBox.Show(
                "Uninstall the hotkey mod?\n\n" + GetNormalUninstallDescription(keepLuo),
                "Uninstall Hotkey Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!CheckGameClosed("uninstall the hotkey mod"))
                return;

            try
            {
                var result = _service.UninstallMod(HotkeyModType.NormalHotkey, gamedataPath, keepLuo);

                // Don't reload here. If luo.scd stayed in the game (keep files setting, or it was theirs to
                // begin with) a reload would pick it right back up and the editor would look installed again
                MainHotkeys.Clear();
                TooltipHotkeys.Clear();
                DebugHotkeys.Clear();
                RawDefaultKeyMapText = string.Empty;
                RawKeyActionsText = string.Empty;
                RawKeyDescriptionsText = string.Empty;
                IsNormalModInstalled = false;

                string? extra = DescribeUninstallResult(result);
                if (extra != null)
                    MessageBox.Show(extra, "Hotkey Mod Uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstall failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UninstallBuildModeMod(string gamedataPath)
        {
            var confirm = MessageBox.Show(
                "Uninstall the build mode hotkey mod?\n\nThis will:\n• Delete BuildmodeHotkeys.scd from the game's gamedata folder\n• Delete your local copy and backup",
                "Uninstall Build Mode Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!CheckGameClosed("uninstall the build mode hotkey mod"))
                return;

            try
            {
                _service.UninstallMod(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys(gamedataPath);
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

        private static async Task DownloadToFileAsync(string url, string destPath)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var file = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file);
        }

        private static void DeleteQuietly(string path)
        {
            try 
            { 
                if (File.Exists(path)) 
                    File.Delete(path); 
            } 
            catch { }
        }

        /// <summary>
        ///     Makes sure we have a local toc.win.bdf, downloading and checking it if we don't.
        /// </summary>
        private static async Task EnsureLocalTocAsync()
        {
            if (HotkeyService.HasLocalToc())
                return;

            Directory.CreateDirectory(Globals.GetHotkeyModsPath());
            string bdfPath = Globals.GetLocalTocBdfPath();
            string temp = bdfPath + ".download";

            try
            {
                await Task.Run(() => DownloadToFileAsync(Globals.NormalHotkeyTocBdfDirectDownloadUrl, temp));
                HotkeyService.VerifyDownloadedToc(temp);
                File.Move(temp, bdfPath, overwrite: true);
            }
            finally
            {
                DeleteQuietly(temp);
            }
        }

        public async Task DownloadAndInstallNormalMod(string gamedataPath)
        {
            if (!CheckGameClosed("install the hotkey mod"))
                return;

            if (!Directory.Exists(gamedataPath))
            {
                MessageBox.Show($"Couldn't find the game's gamedata folder:\n{gamedataPath}\n\nCheck your game path in Settings.",
                    "Gamedata Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If luo.scd is already in the game (a modder's own, or one kept after an uninstall), never
            // download over it. Use theirs instead, same as when the editor opens and finds one
            if (_service.IsInstalledInGame(HotkeyModType.NormalHotkey, gamedataPath))
            {
                await UseExistingLuoAsync(gamedataPath);
                return;
            }

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

            // A toc that isn't ours with no luo.scd means some other setup is using it. Ask before
            // moving it out of the way
            string gameRoot = Path.GetDirectoryName(gamedataPath)!;
            string tocPath = Path.Combine(gameRoot, Globals.TocWinBdfName);
            bool otherToc = File.Exists(tocPath) && !HotkeyService.IsOurToc(tocPath);

            if (otherToc)
            {
                var tocConfirm = MessageBox.Show(
                    "A toc.win.bdf file was found in your game folder, but luo.scd isn't in gamedata.\n\n" +
                    "If you continue, your toc.win.bdf is renamed to toc.win.bdf.bak in the game folder and the hotkey mod's toc.win.bdf is used instead.\n\n" +
                    "The mod manager won't put your old toc.win.bdf back on its own, even if you uninstall the hotkey mod later. " +
                    "If you want it back, rename toc.win.bdf.bak back to toc.win.bdf yourself.\n\nContinue?",
                    "Existing toc.win.bdf Found", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if (tocConfirm != MessageBoxResult.OK)
                    return;
            }

            IsNormalDownloading = true;

            string destDir = Globals.GetHotkeyModsPath();
            string scdPath = Path.Combine(destDir, Globals.NormalHotkeyScdName);
            string bdfPath = Globals.GetLocalTocBdfPath();
            string scdTemp = scdPath + ".download";
            string bdfTemp = bdfPath + ".download";
            string? movedToc = null;

            try
            {
                Directory.CreateDirectory(destDir);

                // Download to temp files first and check them. Our real copies only get replaced once
                // both downloads finished and look right, so a failed download can't leave half a mod behind
                await Task.Run(async () =>
                {
                    await DownloadToFileAsync(Globals.NormalHotkeyDirectDownloadUrl, scdTemp);
                    await DownloadToFileAsync(Globals.NormalHotkeyTocBdfDirectDownloadUrl, bdfTemp);
                });

                HotkeyService.VerifyDownloadedLuo(scdTemp);
                HotkeyService.VerifyDownloadedToc(bdfTemp);

                // The game could have been opened while it was downloading
                if (!CheckGameClosed("install the hotkey mod"))
                    return;

                File.Move(scdTemp, scdPath, overwrite: true);
                File.Move(bdfTemp, bdfPath, overwrite: true);

                // Remember what the downloaded luo.scd looks like so uninstall can tell later if
                // anything besides hotkeys got changed in it
                _service.RecordDownloadedLuo(scdPath);
                _service.CreateBackupsIfAbsent(HotkeyModType.NormalHotkey);

                movedToc = otherToc ? _service.MoveExistingTocAside(gamedataPath) : null;

                _service.ApplyToGamedata(HotkeyModType.NormalHotkey, gamedataPath);
                LoadNormalHotkeys(gamedataPath);

                string done = "Normal hotkey mod downloaded and installed successfully.";
                if (movedToc != null)
                    done += $"\n\nYour old toc.win.bdf was saved as:\n{movedToc}";

                MessageBox.Show(done, "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string failed = $"Install failed: {ex.Message}";

                // Their toc already got renamed before the failure, so make sure they know where it went
                if (movedToc != null)
                    failed += $"\n\nYour old toc.win.bdf was already renamed before this happened. It's here:\n{movedToc}\n\n" +
                              "If you want it back, rename it to toc.win.bdf.";

                MessageBox.Show(failed, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DeleteQuietly(scdTemp);
                DeleteQuietly(bdfTemp);
                IsNormalDownloading = false;
            }
        }

        /// <summary>
        ///     luo.scd is already in the game, so instead of downloading one over it we use theirs. If the
        ///     toc is missing, the game check offers to put ours there, then the editor loads their file.
        /// </summary>
        private async Task UseExistingLuoAsync(string gamedataPath)
        {
            MessageBox.Show(
                "luo.scd is already installed in your game, so the mod manager will use that one instead of downloading a new one. " +
                "Your own luo.scd is never replaced.",
                "Hotkey Mod Already Installed", MessageBoxButton.OK, MessageBoxImage.Information);

            await CheckGameKeymapFilesAsync(gamedataPath);
            LoadNormalHotkeys(gamedataPath);

            if (!IsNormalModInstalled)
                MessageBox.Show(
                    "The mod manager couldn't load the luo.scd that's in your game, so nothing was changed. " +
                    "Make sure the game is closed and try opening the Hotkeys page again.",
                    "Couldn't Load luo.scd", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public async Task DownloadAndInstallBuildModeMod(string gamedataPath)
        {
            if (!CheckGameClosed("install the build mode hotkey mod"))
                return;

            if (!Directory.Exists(gamedataPath))
            {
                MessageBox.Show($"Couldn't find the game's gamedata folder:\n{gamedataPath}\n\nCheck your game path in Settings.",
                    "Gamedata Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBuildModeDownloading = true;

            string destDir = Globals.GetHotkeyModsPath();
            string destPath = Path.Combine(destDir, Globals.BuildModeScdName);
            string temp = destPath + ".download";

            try
            {
                Directory.CreateDirectory(destDir);

                await Task.Run(() => DownloadToFileAsync(Globals.BuildModeDirectDownloadUrl, temp));
                HotkeyService.VerifyDownloadedBuildMode(temp);

                if (!CheckGameClosed("install the build mode hotkey mod"))
                    return;

                File.Move(temp, destPath, overwrite: true);

                _service.CreateBackupsIfAbsent(HotkeyModType.BuildModeHotkey);
                // Actually place the .scd in gamedata — without this the mod only exists in the data
                // folder and the game never sees it until the user happens to save an edit
                _service.ApplyToGamedata(HotkeyModType.BuildModeHotkey, gamedataPath);
                LoadBuildModeHotkeys(gamedataPath);
                MessageBox.Show("Build mode hotkey mod downloaded and installed successfully.", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Install failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DeleteQuietly(temp);
                IsBuildModeDownloading = false;
            }
        }

        // ======================  Game file check ======================

        /// <summary>
        ///     Looks for a lua/luo/toc combination that would stop the game from launching and offers to
        ///     fix it. Runs when the mod manager opens and when the Hotkeys page opens. Things like Steam's
        ///     "verify files" or a game update can cause these without the mod manager doing anything.
        /// </summary>
        public async Task CheckGameKeymapFilesAsync(string? gamedataPath)
        {
            if (string.IsNullOrEmpty(gamedataPath) || !Directory.Exists(gamedataPath))
                return;

            // Can't safely fix anything with the game open. We'll check again next time
            if (DllInjectionService.IsGameRunning())
                return;

            try
            {
                switch (_service.Diagnose(gamedataPath))
                {
                    case HotkeyGamedataProblem.BothLuaAndLuo:
                    {
                        var choice = MessageBox.Show(
                            "Your gamedata folder has both lua.scd and luo.scd in it. The game can only have one of them, so it won't launch like this.\n\n" +
                            "This usually happens after Steam's \"Verify integrity of game files\" or a game update puts lua.scd back.\n\n" +
                            "Yes: keep the hotkey mod (lua.scd is backed up and removed)\n" +
                            "No: go back to the original keymap (luo.scd and the hotkey mod's toc.win.bdf are removed)\n" +
                            "Cancel: leave it for now",
                            "Game Files Need Fixing", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                        if (choice == MessageBoxResult.Yes)
                        {
                            _service.KeepLuoRemoveLua(gamedataPath);

                            // Keeping luo.scd might still leave the toc missing, so check again
                            if (_service.Diagnose(gamedataPath) == HotkeyGamedataProblem.LuoWithoutToc)
                                await FixMissingTocAsync(gamedataPath);
                        }
                        else if (choice == MessageBoxResult.No)
                        {
                            string? backup = _service.RemoveLuoKeepLua(gamedataPath);
                            if (backup != null)
                                MessageBox.Show($"Your luo.scd had changes besides hotkeys, so a copy was saved here:\n{backup}",
                                    "luo.scd Backed Up", MessageBoxButton.OK, MessageBoxImage.Information);

                            // We only ever delete our own toc. If a different one is still there, it was
                            // probably for the luo.scd we just took out, and a toc with no luo.scd can stop the
                            // game from launching. We can't tell for sure, so ask
                            string tocPath = Path.Combine(Path.GetDirectoryName(gamedataPath)!, Globals.TocWinBdfName);
                            if (File.Exists(tocPath) && !HotkeyService.IsOurToc(tocPath))
                            {
                                var tocChoice = MessageBox.Show(
                                    "There's still a toc.win.bdf in your game folder that didn't come from the mod manager. " +
                                    "It was probably for the luo.scd that was just removed, and a toc.win.bdf without luo.scd can stop the game from launching.\n\n" +
                                    "Rename it to toc.win.bdf.bak so it's out of the way? You can always rename it back later.",
                                    "Leftover toc.win.bdf", MessageBoxButton.YesNo, MessageBoxImage.Question);

                                if (tocChoice == MessageBoxResult.Yes)
                                {
                                    string? movedToc = _service.MoveExistingTocAside(gamedataPath);
                                    if (movedToc != null)
                                        MessageBox.Show($"Your toc.win.bdf was renamed to:\n{movedToc}",
                                            "toc.win.bdf Renamed", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                        }
                        break;
                    }

                    case HotkeyGamedataProblem.LuoWithoutToc:
                    {
                        var choice = MessageBox.Show(
                            "luo.scd is in your gamedata folder but toc.win.bdf is missing from your game folder, so the game won't launch.\n\n" +
                            "Put the hotkey mod's toc.win.bdf back now?",
                            "Game Files Need Fixing", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (choice == MessageBoxResult.Yes)
                            await FixMissingTocAsync(gamedataPath);
                        break;
                    }

                    case HotkeyGamedataProblem.LeftoverToc:
                    {
                        var choice = MessageBox.Show(
                            "The hotkey mod's toc.win.bdf is still in your game folder, but luo.scd isn't in gamedata. That leftover file stops the game from launching.\n\n" +
                            "Remove it now?",
                            "Game Files Need Fixing", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (choice == MessageBoxResult.Yes)
                            _service.RemoveLeftoverToc(gamedataPath);
                        break;
                    }

                    case HotkeyGamedataProblem.NoKeymapFile:
                    {
                        if (_service.HasUsableLuaBackup(gamedataPath))
                        {
                            var choice = MessageBox.Show(
                                "Neither lua.scd nor luo.scd is in your gamedata folder, so the game has no keymap and won't launch.\n\n" +
                                "The mod manager has a backup of your original lua.scd. Put it back now?",
                                "Game Files Need Fixing", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (choice == MessageBoxResult.Yes)
                                _service.RestoreLuaFromBackup(gamedataPath);
                        }
                        else
                        {
                            MessageBox.Show(
                                "Neither lua.scd nor luo.scd is in your gamedata folder, so the game has no keymap and won't launch. " +
                                "The mod manager doesn't have a backup of lua.scd to put back.\n\n" +
                                "You can fix this with Restore Original Game Files in the mod manager, or with Steam's \"Verify integrity of game files\".",
                                "Game Files Need Fixing", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't fix the game's hotkey files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FixMissingTocAsync(string gamedataPath)
        {
            await EnsureLocalTocAsync();
            _service.PlaceToc(gamedataPath);
        }
    }
}
