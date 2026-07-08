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
            string lastSeenSig = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool running = DllInjectionService.IsGameRunning();

                    if (running && !wasRunning)
                    {
                        // Session just started. Seed with whatever LastGame already exists so we
                        // don't re-copy a replay from before this session — only new games count.
                        var current = FindCurrentLastGame();
                        _lastCopiedSignature = current != null ? Signature(current) : null;
                        lastSeenSig = _lastCopiedSignature;
                        Log("game session started");
                    }

                    if (running)
                    {
                        // During the session: copy each game's replay once it settles. LastGame gets
                        // overwritten after every game, so this catches all of them, not just the last.
                        var f = FindCurrentLastGame();
                        if (f != null)
                        {
                            string sig = Signature(f);
                            if (sig != lastSeenSig)
                                lastSeenSig = sig;                 // still changing, wait for it to settle
                            else if (sig != _lastCopiedSignature)  // stable for a poll and not yet saved
                                TrySave(f, ref _lastCopiedSignature, sig);
                        }
                    }
                    else if (wasRunning)
                    {
                        // Session ended. Grab the final game's replay if we didn't already — the game
                        // has exited so the file is fully released now.
                        var f = FindCurrentLastGame();
                        if (f != null)
                        {
                            string sig = Signature(f);
                            if (sig != _lastCopiedSignature)
                                TrySave(f, ref _lastCopiedSignature, sig);
                        }
                        Log("game session ended");
                    }

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

        private void TrySave(FileInfo replay, ref string lastCopiedSig, string sig)
        {
            if (TryCopyReplay(replay))
                lastCopiedSig = sig;
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
