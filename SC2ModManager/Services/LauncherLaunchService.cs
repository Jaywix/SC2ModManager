using SC2ModManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class LauncherLaunchService
    {
        private const int InjectDelayMs = 500;

        private readonly ModStorageService _storage;
        private readonly GamedataService _gamedata;
        private readonly ConfigService _config;
        private readonly ReplayArchiveService _replays;
        private readonly IPCService _ipc = new();

        public LauncherLaunchService(
            ModStorageService storage,
            GamedataService gamedata,
            ConfigService config,
            ReplayArchiveService? replays = null)
        {
            _storage = storage;
            _gamedata = gamedata;
            _config = config;
            _replays = replays ?? new ReplayArchiveService();
        }

        public bool IsIpcAvailable => _ipc.IsPipeAvailable();

        public static string GetGameExePath(string gamePath)
        {
            string exe = Path.Combine(gamePath, "bin", "SupremeCommander2.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException(
                    "SupremeCommander2.exe not found. Check the game path in settings.", exe);
            return exe;
        }

        public static string GetIpcDllPath()
        {
            string path = DllInjectionService.ResolveDllPath(null);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "ipc_dll.dll not found.", path);
            return path;
        }

        public void ApplyEnabledModsToGamedata(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                throw new InvalidOperationException("Game path is not set.");

            string gameDataPath = Path.Combine(gamePath, "gamedata");
            string mapsEnabled = Path.Combine(Globals.GetDataPath(), "Mods", "Maps", "Enabled");
            string modsEnabled = Path.Combine(Globals.GetDataPath(), "Mods", "GenericMods", "Enabled");

            var allMaps = _storage.GetInstalledMaps();
            var allMods = _storage.GetInstalledGenericMods();

            foreach (var map in allMaps)
                _gamedata.DisableMap(map, gameDataPath);

            foreach (var mod in allMods)
                _gamedata.DisableGenericMod(mod, gameDataPath);

            foreach (var map in allMaps.Where(m => m.IsEnabled))
            {
                try { _gamedata.EnableMap(map, mapsEnabled, gameDataPath); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to enable map {map.FileName}: {ex.Message}", ex);
                }
            }

            foreach (var mod in allMods.Where(m => m.IsEnabled))
            {
                try { _gamedata.EnableGenericMod(mod, modsEnabled, gameDataPath); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to enable mod {mod.FileName}: {ex.Message}", ex);
                }
            }

            // Persist the enabled/disabled flags without clobbering the saved mod metadata —
            // allMaps/allMods here are filename-only, so saving them directly would wipe names,
            // hashes, images, etc.
            _storage.SyncMapsStateWithDisk();
            _storage.SyncGenericModsStateWithDisk();
        }

        public async Task<bool> PushHostTagsAsync()
        {
            var tags = LobbyModTagBuilder.BuildHostTags(
                _storage.GetInstalledGenericMods());
            return await _ipc.SetTagsAsync(tags);
        }

        /// <summary>
        ///     Launches SupremeCommander2.exe and injects ipc_dll.dll from the launcher folder.
        /// </summary>
        public async Task LaunchGameWithDllAsync(
            string gamePath,
            bool restartIfRunning = true,
            bool forceInject = false,
            ReplayCaptureContext? replayContext = null,
            IProgress<string>? progress = null)
        {
            if (string.IsNullOrEmpty(gamePath))
                throw new InvalidOperationException("Game path is not set.");

            string exe = GetGameExePath(gamePath);
            string dllPath = GetIpcDllPath();

            // Only kill the running game when the caller actually asked to restart it. Killing
            // unconditionally would take down a game the user launched some other way (e.g. a
            // replay from the replay browser).
            if (restartIfRunning)
            {
                progress?.Report("Terminating previous game process...");
                DllInjectionService.TryKillGameProcess();
                await Task.Delay(1500);
            }

            Process? process = DllInjectionService.FindGameProcess();
            if (process == null || restartIfRunning)
            {
                progress?.Report("Starting SupremeCommander2.exe...");
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                    UseShellExecute = true
                });
            }

            if (process == null)
                throw new InvalidOperationException("Failed to start the game.");

            progress?.Report("Waiting for game process...");
            process = await WaitForGameProcessAsync(process.Id, TimeSpan.FromSeconds(90))
                ?? throw new TimeoutException("Game did not start in time.");

            progress?.Report("Waiting before injection...");
            await Task.Delay(InjectDelayMs);

            progress?.Report("Injecting ipc_dll.dll...");
            if (!DllInjectionService.Inject(process.Id, dllPath))
                throw new InvalidOperationException("ipc_dll.dll injection failed.");

            await Task.Delay(InjectDelayMs);

            progress?.Report("Waiting for IPC...");
            bool pipeReady = await _ipc.WaitForPipeAsync(TimeSpan.FromSeconds(60));
            if (!pipeReady)
                throw new TimeoutException("IPC pipe did not respond after injection.");

            progress?.Report("Sending mod tags...");
            if (!await PushHostTagsAsync())
                throw new InvalidOperationException("set_tags failed.");

            var captureContext = replayContext ?? BuildReplayContext(null, null);
            captureContext.GamePath ??= gamePath;
            captureContext.DebugLogPath = ResolveDebugLogPath(gamePath);
            captureContext.DebugLogOffset = GetDebugLogLengthSafe(captureContext.DebugLogPath);
            captureContext.SessionStartedAtUtc = DateTime.UtcNow;

            _replays.TrackProcessForReplay(process, captureContext);

            progress?.Report("Game launched, tags applied.");
        }

        /// <summary>
        ///     Syncs mods, restarts, injects and connects via connect_lobby by lobby id.
        /// </summary>
        public async Task LaunchAndConnectAsync(
            string gamePath,
            ulong lobbyId,
            bool lobbyHasPassword,
            bool restartGame,
            string? lobbyName = null,
            IProgress<string>? progress = null)
        {
            LauncherConnectFile.Write(new LauncherConnectPayload
            {
                LobbyId = lobbyId.ToString(),
                AutoConnect = true
            });

            await LaunchGameWithDllAsync(
                gamePath,
                restartIfRunning: restartGame,
                forceInject: true,
                replayContext: BuildReplayContext(lobbyId.ToString(), lobbyName),
                progress: progress);

            progress?.Report("Connecting to lobby...");
            await Task.Delay(1500);
            bool connected = await _ipc.ConnectToLobbyAsync(lobbyId);
            // if (!connected)
            //     throw new InvalidOperationException("connect_lobby returned an error.");
        }

        private ReplayCaptureContext BuildReplayContext(string? lobbyId, string? lobbyName)
        {
            var cfg = _config.Load();
            var configuredReplaysPath = string.IsNullOrWhiteSpace(cfg.ReplaysPath)
                ? null
                : cfg.ReplaysPath.Trim();

            return new ReplayCaptureContext
            {
                ConfiguredReplaysPath = configuredReplaysPath,
                LobbyId = lobbyId,
                LobbyName = lobbyName,
                EnabledGenericMods = _storage.GetInstalledGenericMods().Where(m => m.IsEnabled).Select(m => m.FileName).ToList(),
                EnabledMaps = _storage.GetInstalledMaps().Where(m => m.IsEnabled).Select(m => m.FileName).ToList()
            };
        }

        private static async Task<Process?> WaitForGameProcessAsync(int startedPid, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var p = Process.GetProcessById(startedPid);
                    if (!p.HasExited)
                        return p;
                }
                catch { }

                var found = DllInjectionService.FindGameProcess();
                if (found != null)
                    return found;

                await Task.Delay(500);
            }
            return DllInjectionService.FindGameProcess();
        }

        private static string ResolveDebugLogPath(string gamePath)
            => Path.Combine(gamePath, "bin", "debug.log");

        private static long GetDebugLogLengthSafe(string? debugLogPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(debugLogPath) || !File.Exists(debugLogPath))
                    return 0;

                using var fs = new FileStream(
                    debugLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                return fs.Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}