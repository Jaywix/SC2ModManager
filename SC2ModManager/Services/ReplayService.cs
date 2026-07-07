/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 19, 2026
 * Author: Jacob Wixom
 * 
*/
using ReplayParser.SC2;
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class ReplayService
    {
        // ============================== Replay tools (DISABLED) ==============================
        // The replay-tools file swap was removed: replays now launch directly via
        // LaunchReplayDirectAsync with no gamedata changes, so the patched .replay files are no
        // longer downloaded or stored. The original logic (local paths, install detection,
        // download) is kept commented out below in case I decide to bring it back sometime
        /*
        private string ReplayToolsPath => Globals.GetReplayToolsPath();
        private string LocalLuaReplayPath => Path.Combine(ReplayToolsPath, Globals.LuaReplayFileName);
        private string LocalZLuaDlc1ReplayPath => Path.Combine(ReplayToolsPath, Globals.ZLuaDlc1ReplayFileName);

        public bool AreReplayToolsInstalled()
        {
            return File.Exists(LocalLuaReplayPath) && File.Exists(LocalZLuaDlc1ReplayPath);
        }

        public async Task DownloadReplayToolsAsync()
        {
            Directory.CreateDirectory(ReplayToolsPath);

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager");

            await DownloadFileAsync(client, Globals.ReplayToolsLuaReplayDownloadUrl, LocalLuaReplayPath);
            await DownloadFileAsync(client, Globals.ReplayToolsZLuaDlc1ReplayDownloadUrl, LocalZLuaDlc1ReplayPath);
        }

        private static async Task DownloadFileAsync(HttpClient client, string url, string outputPath)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            int bytes;
            while ((bytes = await stream.ReadAsync(buffer)) > 0)
                await file.WriteAsync(buffer, 0, bytes);
        }
        */

        // ============================== Replay discovery ==============================

        /// <summary>
        ///     Recursively scans the selected folder for .SC2Replay and .SC2ReplayDLC files —
        ///     replays directly in the folder, in numeric account-ID subfolders (e.g. 72823206),
        ///     or in any other subdirectory the user organizes them into.
        ///     Returns them sorted newest-first.
        /// </summary>
        public List<ReplayEntry> GetReplays(string baseFolderPath)
        {
            var result = new List<ReplayEntry>();
            if (string.IsNullOrEmpty(baseFolderPath) || !Directory.Exists(baseFolderPath))
                return result;

            foreach (string file in EnumerateReplayFilesSafe(baseFolderPath))
            {
                string dir = Path.GetDirectoryName(file) ?? baseFolderPath;
                string relativeDir = Path.GetRelativePath(baseFolderPath, dir);

                var entry = new ReplayEntry
                {
                    FilePath = file,
                    FolderName = relativeDir == "." ? string.Empty : relativeDir,
                    LastModified = File.GetLastWriteTime(file)
                };
                entry.Metadata = ParseReplayMetadata(file);
                result.Add(entry);
            }

            return result.OrderByDescending(r => r.LastModified).ToList();
        }

        /// <summary>
        ///     Walks the folder tree yielding every replay file. Inaccessible subdirectories
        ///     are skipped instead of failing the whole scan.
        /// </summary>
        private static IEnumerable<string> EnumerateReplayFilesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                string[] subDirs;
                string[] files;

                try
                {
                    subDirs = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir);
                }
                catch
                {
                    continue; // no access — skip this folder
                }

                foreach (string sub in subDirs)
                    pending.Push(sub);

                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file);
                    if (ext.Equals(".SC2Replay", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".SC2ReplayDLC", StringComparison.OrdinalIgnoreCase))
                        yield return file;
                }
            }
        }

        // ============================== Replay metadata parsing ==============================

        private static readonly Dictionary<float, string> FactionNames = new()
        {
            { 1f, "UEF" },
            { 2f, "Cybran" },
            { 3f, "Illuminate" }
        };

        private static readonly Dictionary<int, string> ColorNames = new()
        {
            { 0, "Blue" }, { 1, "Green" }, { 2, "Red" }, { 3, "Purple" },
            { 4, "Tan" }, { 5, "Grey" }, { 6, "Olive" }, { 7, "Cyan" },
            { 8, "Yellow" }, { 9, "Orange" }
        };

        private static readonly Dictionary<string, string> ExclusionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ADDONS",             "No Structure Add-ons" },
            { "AIR",               "No Air Units" },
            { "ALL_RESEARCH",      "No Research / All Research and Units Unlocked" },
            { "ALL_RESEARCH_UNITS","No Research / All Units Unlocked" },
            { "ARTILLERY",         "No Artillery Structures" },
            { "EXPERIMENTALS",     "No Experimentals" },
            { "INTEL",             "No Intel Structures" },
            { "LAND",              "No Land Units" },
            { "MASSCONVERT",       "No Mass Conversion" },
            { "NAVAL",             "No Naval Units" },
            { "NO_DLC",            "No DLC Units" },
            { "NUKE",              "No Nukes" },
            { "SHIELDS",           "No Shield Structures" },
            { "SLOW_RESEARCH",     "Slow Research (No Research Stations)" }
        };

        public ReplayMetadata ParseReplayMetadata(string filePath)
        {
            var meta = new ReplayMetadata();
            try
            {
                var data = SC2ReplayParser.Parse(filePath);

                meta.MapRawPath = data.MapName;
                meta.GameVersion = data.Version;
                meta.ReplayVersion = data.ReplayVersion;

                // Extract readable map name from GameOptions["name"], stripping <LOC ...> prefix, the map name at the top is not the correct map name
                if (data.GameOptions.TryGetValue("name", out var rawName) && rawName != null)
                {
                    string nameStr = rawName.ToString();
                    // Strip <LOC TAG_NAME> prefix pattern
                    nameStr = Regex.Replace(nameStr, @"<LOC\s+\S+>", string.Empty).Trim();
                    meta.MapDisplayName = nameStr;
                }
                else
                {
                    meta.MapDisplayName = Path.GetFileNameWithoutExtension(data.MapName ?? string.Empty);
                }

                // Build player list, excluding ARMY_EXTRA/civilian slots
                foreach (var p in data.Players)
                {
                    if (string.Equals(p.ArmyName, "ARMY_EXTRA", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!p.IsHuman && string.IsNullOrWhiteSpace(p.Name))
                        continue;

                    meta.Players.Add(new ReplayPlayerInfo
                    {
                        Name = string.IsNullOrWhiteSpace(p.Name) ? "(AI)" : p.Name,
                        Faction = FactionNames.TryGetValue(p.Faction, out var fn) ? fn : $"({p.Faction})",
                        Color = ColorNames.TryGetValue((int)p.PlayerColor, out var cn) ? cn : $"({p.PlayerColor})",
                        Team = p.Team,
                        IsHuman = p.IsHuman,
                        AIPersonality = p.AIPersonality
                    });
                }

                // Game options
                meta.CheatsEnabled = data.GameOptions.TryGetValue("CheatsEnabled", out var cheat) && cheat is bool b && b;


                string rawVictory = GetOption(data.GameOptions, "Options.Victory");
                meta.VictoryCondition = rawVictory?.ToLowerInvariant() switch
                {
                    "demoralization" => "Assassination",
                    "domination"     => "Supremacy",
                    "sandbox"        => "Infinite War",
                    // Unknown/missing value: just don't show this piece of metadata.
                    // Without a default arm this throws SwitchExpressionException, which
                    // marked the whole replay as parse-failed and dropped everything below.
                    _ => null
                };
                meta.FogOfWar = GetOption(data.GameOptions, "Options.FogOfWar");
                meta.TeamSpawn = GetOption(data.GameOptions, "Options.TeamSpawn");

                if (data.GameOptions.TryGetValue("Options.UnitCap", out var uc) && uc is float ucf)
                    meta.UnitCap = (int)ucf;
                if (data.GameOptions.TryGetValue("Options.InitialMass", out var im) && im is float imf)
                    meta.InitialMass = (int)imf;
                if (data.GameOptions.TryGetValue("Options.InitialEnergy", out var ie) && ie is float ief)
                    meta.InitialEnergy = (int)ief;
                if (data.GameOptions.TryGetValue("Options.InitialResearch", out var ir) && ir is float irf)
                    meta.InitialResearch = (int)irf;
                if (data.GameOptions.TryGetValue("Options.Ranked", out var ranked))
                    meta.Ranked = ranked is bool rb && rb;

                // Collect RestrictedCategories — may live at the top level or nested under Options.
                // Two possible table formats seen in the wild:
                //   A) {1 = "ADDONS", 2 = "AIR", ...}  - iterate VALUES for category names
                //   B) {ADDONS = true, AIR = true, ...} - iterate KEYS for category names
                Dictionary<object, object> rcDict = null;

                if (data.GameOptions.TryGetValue("RestrictedCategories", out var rcRaw1)
                    && rcRaw1 is Dictionary<object, object> d1)
                    rcDict = d1;
                else if (data.GameOptions.TryGetValue("Options.RestrictedCategories", out var rcRaw2)
                    && rcRaw2 is Dictionary<object, object> d2)
                    rcDict = d2;
                else
                {
                    // try any key that ends with "RestrictedCategories"
                    var matchKey = data.GameOptions.Keys.FirstOrDefault(
                        k => k.EndsWith("RestrictedCategories", StringComparison.OrdinalIgnoreCase));
                    if (matchKey != null && data.GameOptions[matchKey] is Dictionary<object, object> d3)
                        rcDict = d3;
                }

                if (rcDict != null)
                {
                    foreach (var kvp2 in rcDict)
                    {
                        string rawCategory;

                        // Format A: key is a number, value is the category name string
                        if (kvp2.Key is float || kvp2.Key is double || kvp2.Key is int)
                            rawCategory = kvp2.Value?.ToString() ?? string.Empty;
                        // Format B: key IS the category name, value is true/false
                        else
                            rawCategory = kvp2.Key?.ToString() ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(rawCategory))
                        {
                            string displayName = ExclusionNames.TryGetValue(rawCategory, out var dn) ? dn : rawCategory;
                            if (!meta.Exclusions.Contains(displayName))
                                meta.Exclusions.Add(displayName);
                        }
                    }
                    meta.Exclusions.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                meta.ParseFailed = true;
                meta.ParseError = ex.Message;
            }
            return meta;
        }

        private static string GetOption(Dictionary<string, object> opts, string key)
        {
            if (opts.TryGetValue(key, out var val) && val != null)
                return val.ToString();
            return null;
        }

        // ============================== Rename ==============================

        /// <summary>
        ///     Renames a replay file on disk. Returns the updated ReplayEntry on success,
        ///     or throws on failure (caller handles UI feedback).
        /// </summary>
        public ReplayEntry RenameReplay(ReplayEntry entry, string newDisplayName)
        {
            string dir       = Path.GetDirectoryName(entry.FilePath);
            string extension = Path.GetExtension(entry.FilePath);
            string newPath   = Path.Combine(dir, newDisplayName + extension);

            if (File.Exists(newPath) && !string.Equals(newPath, entry.FilePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"A replay named \"{newDisplayName}\" already exists in that folder.");

            File.Move(entry.FilePath, newPath);

            return new ReplayEntry
            {
                FilePath     = newPath,
                FolderName   = entry.FolderName,
                LastModified = File.GetLastWriteTime(newPath),
                Metadata     = entry.Metadata
            };
        }

        // ============================== Backup / restore + crash recovery (DISABLED) ==============================
        // Tied to the replay-tools file swap, which no longer runs. Direct launch never creates
        // backups, so there is nothing to restore. Kept commented out for possible future revival.
        /*
        private static string GetLuaBackupPath(string gamedataPath) =>
            Path.Combine(gamedataPath,
                Path.GetFileNameWithoutExtension(Globals.LuaScdName) + Globals.ReplayBackupSuffix);

        private static string GetZLuaDlc1BackupPath(string gamedataPath) =>
            Path.Combine(gamedataPath,
                Path.GetFileNameWithoutExtension(Globals.ZLuaDlc1ScdName) + Globals.ReplayBackupSuffix);

        /// <summary>
        ///     Returns true if orphaned replay backup files exist from a previous (crashed) session.
        /// </summary>
        public bool HasOrphanedBackups(string gamedataPath)
        {
            return File.Exists(GetLuaBackupPath(gamedataPath)) ||
                   File.Exists(GetZLuaDlc1BackupPath(gamedataPath));
        }

        /// <summary>
        ///     Restores .scd files from their replay backups and deletes the backup files.
        ///     Safe to call even if only one backup exists.
        /// </summary>
        public void RestoreOrphanedBackups(string gamedataPath)
        {
            string luaBackup = GetLuaBackupPath(gamedataPath);
            string zLuaBackup = GetZLuaDlc1BackupPath(gamedataPath);
            string luaScd = Path.Combine(gamedataPath, Globals.LuaScdName);
            string zLuaScd = Path.Combine(gamedataPath, Globals.ZLuaDlc1ScdName);

            if (File.Exists(luaBackup))
            {
                File.Copy(luaBackup, luaScd, overwrite: true);
                File.Delete(luaBackup);
            }

            if (File.Exists(zLuaBackup))
            {
                File.Copy(zLuaBackup, zLuaScd, overwrite: true);
                File.Delete(zLuaBackup);
            }
        }
        */

        // ============================== Launch replay ==============================

        /// <summary>
        ///     Launches the replay WITHOUT touching any gamedata files — no backup, no .replay
        ///     swap, no restore. Just runs the game with the /replay flag against whatever lua
        ///     is currently in gamedata, waits for it to exit.
        ///
        ///     This is now the only launch path. The old swap-based approach (and the whole
        ///     replay-tools download/backup/restore machinery) is disabled — see the commented-out
        ///     regions in this file.
        /// </summary>
        public async Task LaunchReplayDirectAsync(ReplayEntry replay, string gamePath)
        {
            string exePath = Path.Combine(gamePath, "bin", "SupremeCommander2.exe");
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Game executable not found: {exePath}");

            // DIAGNOSTIC (temporary): a before/after snapshot can't see a file that is created and
            // then deleted within the session. So in addition to the snapshot, watch the gamedata
            // and profile folders LIVE and log every create/delete/change/rename as it happens,
            // and log the real game process name(s) to confirm we're tracking the right process.
            // Remove all of this once we've identified what poisons the replay state.
            string gamedataPath = Path.Combine(gamePath, "gamedata");
            string profilePath = GetGameProfileFolder(replay.FilePath);
            string logPath = Path.Combine(Globals.GetDataPath(), "replay_gamedata_diff.log");
            Directory.CreateDirectory(Globals.GetDataPath());

            var logLock = new object();
            void Log(string msg)
            {
                lock (logLock)
                {
                    try { File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff}  {msg}{Environment.NewLine}"); }
                    catch { }
                }
            }

            Log("==================================================================");
            Log($"LAUNCH replay: {replay.FilePath}");

            var beforeGame = SnapshotFolder(gamePath);
            var beforeProfile = SnapshotFolder(profilePath);

            using var watchGamedata = MakeWatcher(gamedataPath, "gamedata", Log);
            using var watchProfile = MakeWatcher(profilePath, "profile", Log);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"/replay \"{replay.FilePath}\"",
                WorkingDirectory = Path.GetDirectoryName(exePath)
            });

            if (process != null)
                await process.WaitForExitAsync();

            // SupremeCommander2.exe may be a thin launcher that spawns the real game and exits
            // immediately, so the process we started can return long before the game closes.
            // Wait until the game process is gone so we observe the whole session.
            await WaitForNoGameProcessAsync(Log);

            try
            {
                var afterGame = SnapshotFolder(gamePath);
                var afterProfile = SnapshotFolder(profilePath);
                WriteGamedataDiffLog(replay, beforeGame, afterGame, profilePath, beforeProfile, afterProfile);
            }
            catch { /* diagnostics must never break launch */ }
        }

        /// <summary>
        ///     Creates a recursive FileSystemWatcher that logs every change under <paramref name="path"/>.
        ///     Returns null (and logs) if the folder does not exist.
        /// </summary>
        private static FileSystemWatcher? MakeWatcher(string path, string tag, Action<string> log)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                log($"[{tag}] watcher NOT started (folder missing: {path})");
                return null;
            }

            var w = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
            };
            w.Created += (s, e) => log($"[{tag}] CREATED  {e.FullPath}");
            w.Deleted += (s, e) => log($"[{tag}] DELETED  {e.FullPath}");
            w.Changed += (s, e) => log($"[{tag}] CHANGED  {e.FullPath}");
            w.Renamed += (s, e) => log($"[{tag}] RENAMED  {e.OldFullPath} -> {e.FullPath}");
            w.Error   += (s, e) => log($"[{tag}] WATCHER ERROR: {e.GetException()?.Message}");
            w.EnableRaisingEvents = true;
            return w;
        }

        /// <summary>
        ///     Waits until no SupCom2 game process is running. Logs the game-like process names
        ///     once so we can confirm which process to track. Capped so it can never hang forever.
        /// </summary>
        private static async Task WaitForNoGameProcessAsync(Action<string> log)
        {
            await Task.Delay(3000); // let a launcher hand off to the real game first

            bool loggedNames = false;
            for (int i = 0; i < 600; i++) // ~10 min safety cap
            {
                // Diagnostic only: log game-like process names once (excluding our own app).
                if (!loggedNames)
                {
                    var names = Process.GetProcesses()
                        .Select(p => { try { return p.ProcessName; } catch { return string.Empty; } })
                        .Where(n => (n.Contains("supreme", StringComparison.OrdinalIgnoreCase)
                                  || n.Contains("command", StringComparison.OrdinalIgnoreCase)
                                  || n.Contains("sc2", StringComparison.OrdinalIgnoreCase))
                                 && !n.Contains("SC2ModManager", StringComparison.OrdinalIgnoreCase))
                        .Distinct();
                    log("game-like processes running: " + (names.Any() ? string.Join(", ", names) : "(none)"));
                    loggedNames = true;
                }

                // Gate ONLY on the actual game process, never our own app.
                var procs = Process.GetProcessesByName("SupremeCommander2");
                bool anyRunning = procs.Length > 0;
                foreach (var p in procs) p.Dispose();

                if (!anyRunning)
                    return;

                await Task.Delay(1000);
            }
        }

        // ============================== Gamedata change diagnostics (temporary) ==============================

        /// <summary>
        ///     Builds a manifest of a folder: relative path -> "length|lastWriteUtcTicks".
        /// </summary>
        private static Dictionary<string, string> SnapshotFolder(string folderPath, bool recursive = true)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return map;

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string file in Directory.GetFiles(folderPath, "*", option))
            {
                var info = new FileInfo(file);
                string rel = Path.GetRelativePath(folderPath, file);
                map[rel] = $"{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            }
            return map;
        }

        /// <summary>
        ///     Walks up from a replay file path to the SupCom2 user profile folder
        ///     (…\Documents\My Games\SquareEnix\Supreme Commander 2), i.e. the parent of the
        ///     "replays" directory. Returns empty if not found.
        /// </summary>
        private static string GetGameProfileFolder(string replayFilePath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(replayFilePath) ?? string.Empty);
            while (dir != null)
            {
                if (string.Equals(dir.Name, "replays", StringComparison.OrdinalIgnoreCase))
                    return dir.Parent?.FullName ?? string.Empty;
                dir = dir.Parent;
            }
            return string.Empty;
        }

        private static void WriteGamedataDiffLog(
            ReplayEntry replay,
            Dictionary<string, string> beforeGame,
            Dictionary<string, string> afterGame,
            string profilePath,
            Dictionary<string, string> beforeProfile,
            Dictionary<string, string> afterProfile)
        {
            var lines = new List<string>();

            void DiffInto(string label, Dictionary<string, string> b, Dictionary<string, string> a)
            {
                var added    = a.Keys.Where(k => !b.ContainsKey(k)).OrderBy(k => k).ToList();
                var removed  = b.Keys.Where(k => !a.ContainsKey(k)).OrderBy(k => k).ToList();
                var modified = a.Keys.Where(k => b.ContainsKey(k) && b[k] != a[k]).OrderBy(k => k).ToList();

                lines.Add($"[{label}]  added={added.Count}  removed={removed.Count}  modified={modified.Count}");
                foreach (var f in added)    lines.Add($"    + ADDED    {f}");
                foreach (var f in removed)  lines.Add($"    - REMOVED  {f}");
                foreach (var f in modified) lines.Add($"    ~ MODIFIED {f}  (was {b[f]}  now {a[f]})");
            }

            lines.Add("==================================================================");
            lines.Add($"Replay: {replay.FilePath}");
            DiffInto("game install (recursive)", beforeGame, afterGame);
            lines.Add($"profile folder: {(string.IsNullOrEmpty(profilePath) ? "(not found)" : profilePath)}");
            DiffInto("user profile (recursive)", beforeProfile, afterProfile);
            lines.Add("");

            string logPath = Path.Combine(Globals.GetDataPath(), "replay_gamedata_diff.log");
            Directory.CreateDirectory(Globals.GetDataPath());
            File.AppendAllText(logPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        // ============================== Swap-based launch (DISABLED) ==============================
        // The original launch backed up lua.scd / z_lua_dlc1.scd, copied the patched .replay
        // files over them, launched, then restored. Replaced by LaunchReplayDirectAsync (above).
        // Kept commented out for possible future revival.
        /*
        /// <summary>
        ///     Backs up the .scd files, copies the .replay files over them, launches the game
        ///     with the /replay flag, waits for the game to exit, then restores the originals.
        ///     If lua.scd does not exist, that pair is skipped entirely.
        /// </summary>
        public async Task LaunchReplayAsync(ReplayEntry replay, string gamePath)
        {
            string gamedataPath = Path.Combine(gamePath, "gamedata");
            string luaScd = Path.Combine(gamedataPath, Globals.LuaScdName);
            string zLuaScd = Path.Combine(gamedataPath, Globals.ZLuaDlc1ScdName);
            string luaBackup = GetLuaBackupPath(gamedataPath);
            string zLuaBackup = GetZLuaDlc1BackupPath(gamedataPath);

            bool luaExisted = File.Exists(luaScd);
            bool zLuaExisted = File.Exists(zLuaScd);

            try
            {
                // 1. Backup and replace lua.scd (only if it exists)
                if (luaExisted)
                {
                    File.Copy(luaScd, luaBackup, overwrite: true);
                    File.Copy(LocalLuaReplayPath, luaScd, overwrite: true);
                }

                // 2. Backup and replace z_lua_dlc1.scd (only if it exists)
                if (zLuaExisted)
                {
                    File.Copy(zLuaScd, zLuaBackup, overwrite: true);
                    File.Copy(LocalZLuaDlc1ReplayPath, zLuaScd, overwrite: true);
                }

                // 3. Launch the game with the /replay flag
                string exePath = Path.Combine(gamePath, "bin", "SupremeCommander2.exe");
                if (!File.Exists(exePath))
                    throw new FileNotFoundException($"Game executable not found: {exePath}");

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"/replay \"{replay.FilePath}\"",
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                });

                // 4. Wait for the game to exit, then restore in the finally block
                if (process != null)
                    await process.WaitForExitAsync();
            }
            finally
            {
                // 5. Always restore originals, even if an exception occurred
                if (luaExisted && File.Exists(luaBackup))
                {
                    File.Copy(luaBackup, luaScd, overwrite: true);
                    File.Delete(luaBackup);
                }

                if (zLuaExisted && File.Exists(zLuaBackup))
                {
                    File.Copy(zLuaBackup, zLuaScd, overwrite: true);
                    File.Delete(zLuaBackup);
                }
            }
        }
        */
    }
}
