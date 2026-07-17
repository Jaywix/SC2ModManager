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
        // The replay tools file swap is gone. Replays launch directly through LaunchReplayDirectAsync
        // now with no gamedata changes, so the patched .replay files don't get downloaded or stored
        // anymore. Keeping the original logic commented out below in case I decide to bring it back sometime
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
        ///     Scans the selected folder and everything under it for .SC2Replay and .SC2ReplayDLC
        ///     files. Replays right in the folder, in the numeric account ID folders (e.g. 72823206),
        ///     or in any other subfolder the user made all get picked up. Sorted newest first.
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
                    LastModified = File.GetLastWriteTime(file),
                    FileSizeBytes = new FileInfo(file).Length
                };
                entry.Metadata = ParseReplayMetadata(file);
                result.Add(entry);
            }

            return result.OrderByDescending(r => r.LastModified).ToList();
        }

        /// <summary>
        ///     Walks the whole folder tree returning every replay file. Folders we can't access just
        ///     get skipped instead of failing the whole scan.
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
                    continue; // no access, skip it
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

        /// <summary>
        ///     Permanently deletes the replay file. No recycle bin — gone is gone, which is why the
        ///     UI confirms first.
        /// </summary>
        public void DeleteReplay(ReplayEntry entry)
        {
            if (File.Exists(entry.FilePath))
                File.Delete(entry.FilePath);
        }

        public ReplayMetadata ParseReplayMetadata(string filePath)
        {
            var meta = new ReplayMetadata();
            try
            {
                var data = SC2ReplayParser.Parse(filePath);

                meta.MapRawPath = data.MapName;
                meta.GameVersion = data.Version;
                meta.ReplayVersion = data.ReplayVersion;

                // Match length: the body scan counts sim ticks, which run at 10 per second
                meta.DurationSeconds = data.SimTicks / 10.0;

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
        // This only existed for the replay tools file swap which doesn't run anymore. Direct launch
        // never makes backups so there's nothing to restore. Keeping it in case the tools come back.
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
        ///     Launches the replay without touching any gamedata files. No backup, no .replay swap,
        ///     no restore. Just runs the game with the /replay flag against whatever lua is currently
        ///     in gamedata and waits for it to exit. This is the only launch path now, the old swap
        ///     based stuff is disabled in the commented out sections in this file.
        /// </summary>
        public async Task LaunchReplayDirectAsync(ReplayEntry replay, string gamePath)
        {
            string exePath = Path.Combine(gamePath, "bin", "SupremeCommander2.exe");
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Game executable not found: {exePath}");

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"/replay \"{replay.FilePath}\"",
                WorkingDirectory = Path.GetDirectoryName(exePath)
            });

            if (process != null)
                await process.WaitForExitAsync();
        }

        // ============================== Swap-based launch (DISABLED) ==============================
        // The original launch backed up lua.scd / z_lua_dlc1.scd, copied the patched .replay files
        // over them, launched, then restored everything after. Replaced by LaunchReplayDirectAsync
        // above. Keeping it around in case the replay tools ever come back.
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
