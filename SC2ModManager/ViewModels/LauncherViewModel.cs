using SC2ModManager.Models;
using SC2ModManager.Services;
using SC2ModManager.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SC2ModManager.ViewModels
{
    public class LauncherViewModel : INotifyPropertyChanged
    {
        private readonly LauncherLaunchService _launch;
        private readonly ModStorageService _storage;
        private readonly LobbySyncService _sync;
        private readonly LauncherFilesService _files = new();
        private readonly Func<string?> _getGamePath;
        private Dictionary<string, string> _knownNamesByHash = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _loadModsCts;
        private bool _lastSyncHadChanges;
        private volatile bool _isWarmingKnownNames;
        private volatile bool _isWarmingLocalHashIndex;
        private readonly ConcurrentDictionary<string, (DateTime LastWriteTimeUtc, long Length, string Hash)> _localFileHashCache = new(StringComparer.OrdinalIgnoreCase);

        private string _ownOwnerId = "";
        public string OwnOwnerId
        {
            get => _ownOwnerId;
            set { _ownOwnerId = value ?? ""; OnPropertyChanged(); RefreshFilteredLobbies(); }
        }

        private bool _hideOwnLobby = false;
        public bool HideOwnLobby
        {
            get => _hideOwnLobby;
            set { _hideOwnLobby = value; OnPropertyChanged(); RefreshFilteredLobbies(); }
        }

        public LauncherViewModel(
            LauncherLaunchService launch,
            LobbySyncService sync,
            ModStorageService storage,
            Func<string?> getGamePath)
        {
            _launch = launch;
            _sync = sync;
            _storage = storage;
            _getGamePath = getGamePath;
        }

        public ObservableCollection<LobbyInfo> Lobbies { get; } = new();
        public ObservableCollection<LobbyInfo> FilteredLobbies { get; } = new();

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanScan)); }
        }

        public bool CanScan => !IsScanning;
        public bool IsIpcOnline => _launch.IsIpcAvailable;

        private bool _onlyOurLauncher = true;
        public bool OnlyOurLauncher
        {
            get => _onlyOurLauncher;
            set { _onlyOurLauncher = value; OnPropertyChanged(); RefreshFilteredLobbies(); }
        }

        private string _searchFilter = "";
        public string SearchFilter
        {
            get => _searchFilter;
            set { _searchFilter = value; OnPropertyChanged(); RefreshFilteredLobbies(); }
        }

        private LobbyInfo? _selectedLobby;
        public LobbyInfo? SelectedLobby
        {
            get => _selectedLobby;
            set
            {
                if (_selectedLobby?.SteamId == value?.SteamId)
                    return;

                _selectedLobby = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedLobby));
                OnPropertyChanged(nameof(SelectedLobbySummary));
                OnPropertyChanged(nameof(CanSync));
                _ = LoadModDetailsForSelectionAsync(value);
            }
        }

        public bool HasSelectedLobby => SelectedLobby != null;

        public string SelectedLobbySummary
        {
            get
            {
                if (SelectedLobby == null) return "";
                var pw = SelectedLobby.HasPassword ? " 🔒" : "";
                return $"{SelectedLobby.Name}{pw} — {SelectedLobby.MemberCount}/{SelectedLobby.MaxMembers}";
            }
        }

        public ObservableCollection<LobbyModDetail> LobbyMods { get; } = new();

        private async Task EnsureKnownNamesCacheAsync(bool includeRemoteCatalog = true, CancellationToken token = default)
        {
            var names = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var localNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var gm in _storage.GetInstalledGenericMods())
                {
                    token.ThrowIfCancellationRequested();
                    var hash = GetComputedHashCached(gm.ModFilePath, gm.ModHash);
                    if (string.IsNullOrWhiteSpace(hash)) continue;
                    localNames[hash] = !string.IsNullOrWhiteSpace(gm.DisplayName) ? gm.DisplayName : gm.FileName;
                }

                foreach (var m in _storage.GetInstalledMaps())
                {
                    token.ThrowIfCancellationRequested();
                    var hash = GetComputedHashCached(m.ModFilePath, m.ModHash);
                    if (string.IsNullOrWhiteSpace(hash)) continue;
                    localNames[hash] = !string.IsNullOrWhiteSpace(m.MapName) && m.MapName != "Unknown" ? m.MapName : m.FileName;
                }

                return localNames;
            }, token);

            if (includeRemoteCatalog)
            {
                token.ThrowIfCancellationRequested();
                var dlMods = await _storage.GetDownloadableGenericModsAsync();
                foreach (var gm in dlMods.Where(x => !string.IsNullOrWhiteSpace(x.ModHash)))
                    names[gm.ModHash!] = !string.IsNullOrWhiteSpace(gm.DisplayName) ? gm.DisplayName : gm.FileName;

                token.ThrowIfCancellationRequested();
                var dlMaps = await _storage.GetDownloadableMapsAsync();
                foreach (var m in dlMaps.Where(x => !string.IsNullOrWhiteSpace(x.ModHash)))
                    names[m.ModHash!] = !string.IsNullOrWhiteSpace(m.MapName) && m.MapName != "Unknown" ? m.MapName : m.FileName;
            }

            _knownNamesByHash = names;
        }

        private string GetComputedHashCached(string? filePath, string? knownHash)
        {
            if (!string.IsNullOrWhiteSpace(knownHash))
                return knownHash;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "";

            try
            {
                var fi = new FileInfo(filePath);
                var stamp = fi.LastWriteTimeUtc;
                var len = fi.Length;

                if (_localFileHashCache.TryGetValue(filePath, out var cached)
                    && cached.LastWriteTimeUtc == stamp
                    && cached.Length == len)
                {
                    return cached.Hash;
                }

                var hash = GenericGamedataMod.ComputeFileHash(filePath);
                _localFileHashCache[filePath] = (stamp, len, hash);
                return hash;
            }
            catch
            {
                return "";
            }
        }

        private async Task<Dictionary<string, (string name, bool isEnabled)>> BuildLocalByHashAsync(CancellationToken token)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var result = new Dictionary<string, (string name, bool isEnabled)>(StringComparer.OrdinalIgnoreCase);

                foreach (var gm in _storage.GetInstalledGenericMods())
                {
                    token.ThrowIfCancellationRequested();
                    var hash = GetComputedHashCached(gm.ModFilePath, gm.ModHash);
                    if (string.IsNullOrWhiteSpace(hash)) continue;
                    result[hash] = (gm.FileName, gm.IsEnabled);
                }

                foreach (var m in _storage.GetInstalledMaps())
                {
                    token.ThrowIfCancellationRequested();
                    var hash = GetComputedHashCached(m.ModFilePath, m.ModHash);
                    if (string.IsNullOrWhiteSpace(hash)) continue;
                    result[hash] = (m.FileName, m.IsEnabled);
                }

                return result;
            }, token);
        }

        private void WarmLocalHashIndexInBackground()
        {
            if (_isWarmingLocalHashIndex)
                return;

            _isWarmingLocalHashIndex = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource();
                    await BuildLocalByHashAsync(cts.Token);
                }
                catch
                {
                    // ignore warmup failures
                }
                finally
                {
                    _isWarmingLocalHashIndex = false;
                }
            });
        }

        private async Task LoadModDetailsAsync(CancellationToken token)
        {
            LobbyMods.Clear();
            if (SelectedLobby == null) return;

            if (SelectedLobby.RequiredMods == null || SelectedLobby.RequiredMods.Count == 0)
            {
                OnPropertyChanged(nameof(SyncStatusText));
                return;
            }

            var localByHash = await BuildLocalByHashAsync(token);
            token.ThrowIfCancellationRequested();

            foreach (var mod in SelectedLobby.RequiredMods)
            {
                token.ThrowIfCancellationRequested();
                localByHash.TryGetValue(mod.Hash, out var local);
                var displayName = _knownNamesByHash.GetValueOrDefault(mod.Hash);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = local.name;

                LobbyMods.Add(new LobbyModDetail
                {
                    Hash = mod.Hash,
                    IsEnabledInLobby = true,
                    IsLocal = localByHash.ContainsKey(mod.Hash),
                    IsLocalEnabled = localByHash.TryGetValue(mod.Hash, out var state) && state.isEnabled,
                    LocalFileName = local.name ?? "",
                    DisplayName = displayName ?? ""
                });
            }

            OnPropertyChanged(nameof(SyncStatusText));
        }

        private Task LoadModDetailsAsync()
            => LoadModDetailsAsync(CancellationToken.None);

        private async Task LoadModDetailsForSelectionAsync(LobbyInfo? selection)
        {
            _loadModsCts?.Cancel();
            _loadModsCts?.Dispose();
            _loadModsCts = new CancellationTokenSource();
            var token = _loadModsCts.Token;

            try
            {
                await Task.Delay(80, token);
                if (token.IsCancellationRequested || selection == null)
                    return;
                if (!ReferenceEquals(selection, SelectedLobby))
                    return;

                await LoadModDetailsAsync(token);

                if (!_isWarmingKnownNames)
                {
                    _isWarmingKnownNames = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await EnsureKnownNamesCacheAsync(includeRemoteCatalog: true, token: token);
                            if (ReferenceEquals(selection, SelectedLobby))
                                await LoadModDetailsAsync(token);
                        }
                        catch { }
                        finally { _isWarmingKnownNames = false; }
                    });
                }

                WarmLocalHashIndexInBackground();
            }
            catch (OperationCanceledException)
            {
                // Ignore rapid-selection cancellation
            }
        }

        public string SyncStatusText
        {
            get
            {
                if (SelectedLobby == null) return "";
                int present = LobbyMods.Count(m => m.IsLocalEnabled);
                int total = LobbyMods.Count;
                if (total == 0) return "No mods in this lobby";
                return $"{present}/{total} mods match (enabled locally)";
            }
        }

        public async Task InstallMissingModsAsync()
        {
            if (SelectedLobby == null) return;
            IsSyncing = true;
            try
            {
                var downloadableMods = await _storage.GetDownloadableGenericModsAsync();
                var downloadableMaps = await _storage.GetDownloadableMapsAsync();
                var syncResult = await _sync.CompareAsync(SelectedLobby, downloadableMods, downloadableMaps);

                if (syncResult.HasUnknownMods)
                {
                    StatusMessage = "There are unknown mods — auto-install is not possible";
                    return;
                }

                if (!syncResult.ModsToDownload.Any() && !syncResult.MapsToDownload.Any())
                {
                    StatusMessage = "No missing mods to install";
                    return;
                }

                foreach (var mod in syncResult.ModsToDownload)
                    await _storage.DownloadGenericModAsync(mod);
                foreach (var map in syncResult.MapsToDownload)
                    await _storage.DownloadMapAsync(map);

                StatusMessage = "Missing mods installed";
                await LoadModDetailsAsync();
                OnPropertyChanged(nameof(SyncStatusText));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        public async Task EnableRequiredModsAsync()
        {
            if (SelectedLobby == null) return;
            IsSyncing = true;
            try
            {
                var downloadableMods = await _storage.GetDownloadableGenericModsAsync();
                var downloadableMaps = await _storage.GetDownloadableMapsAsync();
                var syncResult = await _sync.CompareAsync(SelectedLobby, downloadableMods, downloadableMaps);

                var allMods = _storage.GetInstalledGenericMods();
                var allMaps = _storage.GetInstalledMaps();

                foreach (var mod in allMods.Where(m => syncResult.ModsToEnable.Contains(m.FileName) && !m.IsEnabled))
                    _storage.MoveGenericModToEnabled(mod);
                foreach (var map in allMaps.Where(m => syncResult.ModsToEnable.Contains(m.FileName) && !m.IsEnabled))
                    _storage.MoveMapToEnabled(map);

                // Preserve the saved mod metadata; only the enabled/disabled flags changed here.
                _storage.SyncGenericModsStateWithDisk();
                _storage.SyncMapsStateWithDisk();

                StatusMessage = syncResult.ModsToEnable.Any() ? "Required mods enabled" : "All required mods are already enabled";
                await LoadModDetailsAsync();
                OnPropertyChanged(nameof(SyncStatusText));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        public async Task DisableExtraModsAsync()
        {
            if (SelectedLobby == null) return;
            IsSyncing = true;
            try
            {
                var downloadableMods = await _storage.GetDownloadableGenericModsAsync();
                var downloadableMaps = await _storage.GetDownloadableMapsAsync();
                var syncResult = await _sync.CompareAsync(SelectedLobby, downloadableMods, downloadableMaps);

                var allMods = _storage.GetInstalledGenericMods();
                var allMaps = _storage.GetInstalledMaps();

                foreach (var mod in allMods.Where(m => syncResult.ModsToRemove.Contains(m.FileName) && m.IsEnabled))
                    _storage.MoveGenericModToDisabled(mod);
                foreach (var map in allMaps.Where(m => syncResult.ModsToRemove.Contains(m.FileName) && m.IsEnabled))
                    _storage.MoveMapToDisabled(map);

                // Preserve the saved mod metadata; only the enabled/disabled flags changed here.
                _storage.SyncGenericModsStateWithDisk();
                _storage.SyncMapsStateWithDisk();

                StatusMessage = syncResult.ModsToRemove.Any() ? "Extra mods disabled" : "No extra enabled mods found";
                await LoadModDetailsAsync();
                OnPropertyChanged(nameof(SyncStatusText));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set
            {
                _isSyncing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSync));
                OnPropertyChanged(nameof(CanLaunchGame));
            }
        }

        public bool CanLaunchGame => !IsSyncing;
        // Sync/Join should be available when lobby is selected.
        // IPC can be offline here because launch flow can start game + inject DLL.
        public bool CanSync => !IsSyncing && HasSelectedLobby;

        private string _password = "";
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public void RefreshIpcStatus()
        {
            OnPropertyChanged(nameof(IsIpcOnline));
            OnPropertyChanged(nameof(CanSync));
        }

        public async Task PushHostTagsAsync()
        {
            RefreshIpcStatus();
            if (!IsIpcOnline)
            {
                StatusMessage = "Launch the game via Launcher first.";
                return;
            }

            bool ok = await _launch.PushHostTagsAsync();
            // StatusMessage = ok ? "Теги модов отправлены (хост)" : "Ошибка set_tags";
            StatusMessage = ok ? "" : "";
        }

        private void RefreshFilteredLobbies()
        {
            FilteredLobbies.Clear();
            var query = Lobbies.AsEnumerable();
            if (OnlyOurLauncher)
                query = query.Where(l => l.IsOurLauncher);

            query = query.Where(l => !(l.IsPrivateGame && !l.IsOurLauncher));

            if (HideOwnLobby && !string.IsNullOrWhiteSpace(OwnOwnerId))
                query = query.Where(l => !string.Equals(l.OwnerId, OwnOwnerId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(SearchFilter))
                query = query.Where(l =>
                    l.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    l.SteamId.Contains(SearchFilter));
            foreach (var l in query)
                FilteredLobbies.Add(l);

            if (SelectedLobby != null)
            {
                var stillExists = FilteredLobbies.Any(x => x.SteamId == SelectedLobby.SteamId);
                if (!stillExists)
                    SelectedLobby = FilteredLobbies.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasSelectedLobby));
            OnPropertyChanged(nameof(CanSync));
        }

        // ======================  Launcher support files ======================

        public bool AreLauncherFilesInstalled
        {
            get
            {
                string? gamePath = _getGamePath();
                return !string.IsNullOrEmpty(gamePath) && _files.AreFilesInstalled(gamePath!);
            }
        }

        /// <summary>
        ///     Makes sure the launcher support files are there. If they aren't, asks the user (like
        ///     the hotkey mod does) whether to download them, and downloads/installs on yes. Returns
        ///     true when the files are installed and the launcher is good to go.
        /// </summary>
        public async Task<bool> EnsureLauncherFilesInstalledAsync()
        {
            string? gamePath = _getGamePath();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Set the game path in Settings first.", "Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_files.AreFilesInstalled(gamePath))
                return true;

            var choice = MessageBox.Show(
                "The launcher needs some support files that aren't installed yet.\n\n" +
                "Do you want to download and install them now?",
                "Launcher Files Needed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (choice != MessageBoxResult.Yes)
                return false;

            IsSyncing = true;
            StatusMessage = "Downloading launcher files...";
            try
            {
                await _files.DownloadAndInstallAsync(gamePath);
                OnPropertyChanged(nameof(AreLauncherFilesInstalled));
                StatusMessage = "Launcher files installed.";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = "";
                MessageBox.Show($"Failed to download launcher files: {ex.Message}", "Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsSyncing = false;
            }
        }

        /// <summary>
        ///     Removes the launcher support files (the dlls in the game bin folder plus the files
        ///     next to the mod manager exe).
        /// </summary>
        public void UninstallLauncherFiles()
        {
            string? gamePath = _getGamePath();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Set the game path in Settings first.", "Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "This will remove the launcher support files from your game folder and the mod manager folder. Continue?",
                "Uninstall Launcher Files",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _files.Uninstall(gamePath);
            OnPropertyChanged(nameof(AreLauncherFilesInstalled));
            StatusMessage = "Launcher files removed.";
        }

        public async Task LaunchGameAsync()
        {
            string? gamePath = _getGamePath();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Set the game path in Settings.", "Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSyncing = true;
            try
            {
                var progress = new Progress<string>(s => StatusMessage = s);
                await _launch.LaunchGameWithDllAsync(
                    gamePath,
                    restartIfRunning: false,
                    forceInject: false,
                    replayContext: null,
                    progress: progress);
                RefreshIpcStatus();

                if (IsIpcOnline)
                    await AutoScanLobbiesWithRetryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        public async Task ScanLobbiesAsync()
        {
            RefreshIpcStatus();
            if (!IsIpcOnline)
            {
                StatusMessage = "Launch the game via Launcher, then scan again.";
                return;
            }

            IsScanning = true;
            StatusMessage = "Scanning lobbies...";
            string? previousSelectedId = SelectedLobby?.SteamId;
            Lobbies.Clear();

            try
            {
                var ipc = new IPCService();
                var result = await ipc.GetLobbiesAsync(100);
                if (result != null && result.Count > 0)
                {
                    foreach (var lobby in result)
                        Lobbies.Add(lobby);
                    StatusMessage = $"Found {result.Count} lobbies";
                }
                else
                {
                    StatusMessage = "No lobbies found or scan failed";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                RefreshFilteredLobbies();

                if (!string.IsNullOrWhiteSpace(previousSelectedId))
                    SelectedLobby = FilteredLobbies.FirstOrDefault(l => l.SteamId == previousSelectedId) ?? FilteredLobbies.FirstOrDefault();
                else
                    SelectedLobby = FilteredLobbies.FirstOrDefault();

                OnPropertyChanged(nameof(HasSelectedLobby));
                OnPropertyChanged(nameof(CanSync));
            }
        }

        public async Task<bool> SyncModsOnlyAsync()
        {
            _lastSyncHadChanges = false;
            if (SelectedLobby == null) return false;

            IsSyncing = true;
            StatusMessage = "Synchronizing mods...";

            try
            {
                var downloadableMods = await _storage.GetDownloadableGenericModsAsync();
                var downloadableMaps = await _storage.GetDownloadableMapsAsync();
                var syncResult = await _sync.CompareAsync(SelectedLobby, downloadableMods, downloadableMaps);

                if (syncResult.HasUnknownMods)
                {
                    MessageBox.Show(
                        "This lobby has mods that are not in the catalog:\n\n" +
                        string.Join("\n", syncResult.UnknownHashes) +
                        "\n\nInstall them manually or choose another lobby.",
                        "Unknown mods",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "Sync canceled: unknown mods";
                    return false;
                }

                if (syncResult.NeedsChanges)
                {
                    string msg = "The following changes will be applied:\n\n";
                    if (syncResult.ModsToDownload.Any() || syncResult.ModsToInstall.Any() || syncResult.ModsToEnable.Any())
                        msg += $"📥 Install/enable: {string.Join(", ", syncResult.ModsToInstall.Concat(syncResult.ModsToEnable).Distinct())}\n";
                    if (syncResult.ModsToRemove.Any())
                        msg += $"🗑 Disable: {string.Join(", ", syncResult.ModsToRemove)}\n";

                    if (MessageBox.Show(msg + "\nContinue?", "Mod synchronization",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        StatusMessage = "Canceled by user";
                        return false;
                    }

                    await _sync.ApplySyncAsync(syncResult);
                    StatusMessage = "Mod synchronization completed";
                    _lastSyncHadChanges = true;
                }
                else
                {
                    StatusMessage = "Mods are already synchronized";
                    _lastSyncHadChanges = false;
                }

                await LoadModDetailsAsync();
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsSyncing = false;
            }
        }

        public async Task SyncAndLaunchAsync()
        {
            if (SelectedLobby == null) return;

            string? gamePath = _getGamePath();
            if (string.IsNullOrEmpty(gamePath))
            {
                MessageBox.Show("Set the game path in Settings.", "Launcher",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool syncOk = await SyncModsOnlyAsync();
                if (!syncOk)
                    return;

                if (_lastSyncHadChanges)
                {
                    StatusMessage = "Applying mods to gamedata...";
                    _launch.ApplyEnabledModsToGamedata(gamePath);
                }

                if (SelectedLobby.HasPassword)
                {
                    var dlg = new LobbyPasswordWindow(Password)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.EnteredPassword))
                    {
                        StatusMessage = "Password was not entered";
                        return;
                    }
                    Password = dlg.EnteredPassword;

                    var expectedPassword = SelectedLobby.LobbyPassword;
                    if (!string.IsNullOrEmpty(expectedPassword) &&
                        !string.Equals(Password, expectedPassword, StringComparison.Ordinal))
                    {
                        StatusMessage = "Incorrect lobby password";
                        MessageBox.Show("Incorrect lobby password.", "Launcher",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (!ulong.TryParse(SelectedLobby.SteamId, out ulong lobbyId))
                    throw new InvalidOperationException("Invalid lobby Steam ID");

                if (!_lastSyncHadChanges && IsIpcOnline)
                {
                    StatusMessage = "Connecting to lobby...";
                    var ipc = new IPCService();
                    bool connectedFast = await ipc.ConnectToLobbyAsync(lobbyId);
                    // if (!connectedFast)
                    //     throw new InvalidOperationException("connect_lobby returned an error.");
                }
                else
                {
                    var progress = new Progress<string>(s => StatusMessage = s);
                    bool restartGame = _lastSyncHadChanges;
                    await _launch.LaunchAndConnectAsync(
                        gamePath, lobbyId, SelectedLobby.HasPassword, restartGame, SelectedLobby.Name, progress);
                }
                await LoadModDetailsAsync();

                RefreshIpcStatus();
                if (IsIpcOnline)
                    await AutoScanLobbiesWithRetryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AutoScanLobbiesWithRetryAsync()
        {
            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                RefreshIpcStatus();
                if (!IsIpcOnline)
                {
                    await Task.Delay(500);
                    continue;
                }

                await ScanLobbiesAsync();

                // Если IPC онлайн, но лобби пусто, даем сети/клиенту еще немного времени.
                if (Lobbies.Count > 0 || attempt == maxAttempts)
                    return;

                StatusMessage = $"Auto-scan: attempt {attempt}/{maxAttempts}, no lobbies yet...";
                await Task.Delay(700);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LobbyModDetail
    {
        public string Hash { get; set; } = "";
        public bool IsEnabledInLobby { get; set; }
        public bool IsLocal { get; set; }
        public bool IsLocalEnabled { get; set; }
        public string LocalFileName { get; set; } = "";

        // Display name resolved from repository by mod hash.
        public string DisplayName { get; set; } = "";

        public string StatusIcon => IsLocalEnabled ? "✅" : (IsLocal ? "🟡" : "❌");
        public string ShortHash => Hash.Length > 8 ? Hash[..8] + "..." : Hash;

        public string DisplayNameOrHash =>
            string.IsNullOrWhiteSpace(DisplayName) ? Hash : DisplayName;

        public string StateText => IsLocalEnabled
            ? "enabled"
            : (IsLocal ? "installed (disabled)" : "missing");
    }

}
