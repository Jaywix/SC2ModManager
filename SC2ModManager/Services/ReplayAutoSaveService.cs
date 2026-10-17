/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 *
 * Created on: July 7, 2026
 * Author: Jacob Wixom
 *
*/
using SC2ModManager.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     Auto-saves replays into the user's chosen replays folder. While a game session is running
    ///     it watches LastGame.SC2Replay* and copies each finished game's replay out, so you get
    ///     every game in a session, not just the last one, without restarting the game.
    ///
    ///     Two things keep this safe: we only copy a replay once it has stopped changing for a poll
    ///     interval (so we don't grab a half-written file), and we copy with permissive file sharing
    ///     (FileShare.ReadWrite | Delete) so we never lock the game out of its own replay file.
    ///
    ///     Runs whenever the mod manager is open. The destination is read live from a callback so
    ///     changing the "read from" folder takes effect right away.
    /// </summary>
    public class ReplayAutoSaveService : IDisposable
    {
        private const int PollMs = 2000;

        private Func<string> _getDestFolder;
        private CancellationTokenSource _cts;
        private string _lastCopiedSignature;   // don't copy the same replay twice

        public bool IsRunning => _cts != null;

        public void Start(Func<string> getDestFolder)
        {
            Stop();
            _getDestFolder = getDestFolder;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(() => MonitorLoopAsync(token));
            Log("auto-save monitor started");
        }

        public void Stop()
        {
            if (_cts == null)
                return;

            try { _cts.Cancel(); _cts.Dispose(); }
            catch { }

            _cts = null;
            Log("auto-save monitor stopped");
        }

        public void Dispose() => Stop();

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            bool wasRunning = false;
            bool firstPass = true;
            string lastSeenSig = null;

            // The last replay we saved, remembered between launches. This used to only live in memory and got
            // reset to "whatever LastGame is now" every time a game session started, so a game that finished
            // while the mod manager was closed (or before it was opened) never got saved
            _lastCopiedSignature = LoadRememberedSignature();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool running = DllInjectionService.IsGameRunning();

                    if (running && !wasRunning)
                        Log("game session started");

                    // Look while a game session is going, right after it ends, and once when the mod manager
                    // starts (that last one is what catches games that finished while we weren't open)
                    if (running || wasRunning || firstPass)
                    {
                        var f = FindCurrentLastGame();
                        if (f != null)
                        {
                            string sig = Signature(f);

                            if (_lastCopiedSignature == null)
                            {
                                // Nothing remembered yet (first run with this version, or auto-save was just
                                // turned on). Don't copy whatever old replay is sitting there, just start
                                // remembering from here, same as it used to work
                                Remember(sig);
                                lastSeenSig = sig;
                            }
                            else if (sig != _lastCopiedSignature)
                            {
                                if (running && sig != lastSeenSig)
                                    lastSeenSig = sig;   // the game's open and the file just changed, wait a poll for it to settle
                                else
                                    TrySave(f, sig);     // stable for a poll, or the game is closed so the file is done
                            }
                        }
                    }

                    if (wasRunning && !running)
                        Log("game session ended");

                    firstPass = false;
                    wasRunning = running;
                    await Task.Delay(PollMs, token);
                }
            }
            catch (OperationCanceledException) { /* Stop() was called */ }
            catch (Exception ex)
            {
                Log($"monitor loop error: {ex.Message}");
            }
        }

        private void TrySave(FileInfo replay, string sig)
        {
            if (TryCopyReplay(replay))
                Remember(sig);
        }

        // ======= Remembering the last saved replay between launches (stored in config.json) =======

        private static string LoadRememberedSignature()
        {
            try { return new ConfigService().Load().LastAutoSavedReplaySignature; }
            catch { return null; }
        }

        private void Remember(string sig)
        {
            _lastCopiedSignature = sig;
            try
            {
                var configService = new ConfigService();
                var config = configService.Load();
                config.LastAutoSavedReplaySignature = sig;
                configService.Save(config);
            }
            catch (Exception ex)
            {
                // Still remembered for this session, it just won't carry over to the next launch
                Log($"couldn't save last auto-saved replay: {ex.Message}");
            }
        }

        private bool TryCopyReplay(FileInfo src)
        {
            try
            {
                string dest = _getDestFolder?.Invoke();
                if (string.IsNullOrWhiteSpace(dest))
                {
                    Log("skip: no destination folder set");
                    return false;
                }

                Directory.CreateDirectory(dest);

                string ext = src.Extension;   // .SC2ReplayDLC or .SC2Replay
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string target = NextUniquePath(dest, $"AutoSave_{stamp}", ext);

                // Copy with permissive sharing so we never block the game from reading, writing, or
                // deleting its own replay file — locking it out could crash a game that has it open.
                using (var s = new FileStream(src.FullName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var d = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(d);
                }

                Log($"auto-saved '{src.FullName}' -> '{target}'");
                return true;
            }
            catch (Exception ex)
            {
                Log($"auto-save failed for '{src.FullName}': {ex.Message}");
                return false;
            }
        }

        // Newest LastGame replay under the game's replays folder. AutoSave_ copies never match this
        // pattern, so we never react to our own output even if the destination is in the same tree.
        private static FileInfo FindCurrentLastGame()
        {
            string root = Globals.DefaultReplaysBasePath;
            if (!Directory.Exists(root))
                return null;

            try
            {
                return new DirectoryInfo(root)
                    .GetFiles("LastGame.SC2Replay*", SearchOption.AllDirectories)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string Signature(FileInfo f) => $"{f.FullName}|{f.LastWriteTimeUtc.Ticks}|{f.Length}";

        private static string NextUniquePath(string folder, string baseName, string ext)
        {
            string candidate = Path.Combine(folder, baseName + ext);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}_{suffix}{ext}");
                suffix++;
            }
            return candidate;
        }

        private static void Log(string message)
        {
            try
            {
                string logDir = Path.Combine(Globals.GetDataPath(), "Logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "replay-autosave.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // never throw from logging
            }
        }
    }
}
