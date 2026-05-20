/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 19, 2026
 * Author: Jacob Wixom
 * 
*/
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
        // ── Local paths ─────────────────────────────────────────────────────────

        private string ReplayToolsPath => Globals.GetReplayToolsPath();
        private string LocalLuaReplayPath => Path.Combine(ReplayToolsPath, Globals.LuaReplayFileName);
        private string LocalZLuaDlc1ReplayPath => Path.Combine(ReplayToolsPath, Globals.ZLuaDlc1ReplayFileName);

        // ── Installation detection ───────────────────────────────────────────────

        public bool AreReplayToolsInstalled()
        {
            return File.Exists(LocalLuaReplayPath) && File.Exists(LocalZLuaDlc1ReplayPath);
        }

        // ── Download ─────────────────────────────────────────────────────────────

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

        // ── Replay discovery ─────────────────────────────────────────────────────

        /// <summary>
        ///     Scans the base replay folder and all numeric subdirectories (account folders)
        ///     for .SC2Replay and .SC2ReplayDLC files. Returns them sorted newest-first.
        /// </summary>
        public List<ReplayEntry> GetReplays(string baseFolderPath)
        {
            var result = new List<ReplayEntry>();
            if (string.IsNullOrEmpty(baseFolderPath) || !Directory.Exists(baseFolderPath))
                return result;

            // Numeric subdirectories are account ID folders (e.g. 72823206)
            var numericDirs = Directory.GetDirectories(baseFolderPath)
                .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+$"))
                .ToList();

            IEnumerable<string> searchDirs = numericDirs.Any() ? numericDirs : new[] { baseFolderPath };

            foreach (string dir in searchDirs)
            {
                string folderName = Path.GetFileName(dir);

                var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f);
                        return ext.Equals(".SC2Replay", StringComparison.OrdinalIgnoreCase) ||
                               ext.Equals(".SC2ReplayDLC", StringComparison.OrdinalIgnoreCase);
                    });

                foreach (string file in files)
                {
                    result.Add(new ReplayEntry
                    {
                        FilePath = file,
                        FolderName = folderName,
                        LastModified = File.GetLastWriteTime(file)
                    });
                }
            }

            return result.OrderByDescending(r => r.LastModified).ToList();
        }

        // ── Backup / restore helpers ─────────────────────────────────────────────

        private static string GetLuaBackupPath(string gamedataPath) =>
            Path.Combine(gamedataPath,
                Path.GetFileNameWithoutExtension(Globals.LuaScdName) + Globals.ReplayBackupSuffix);

        private static string GetZLuaDlc1BackupPath(string gamedataPath) =>
            Path.Combine(gamedataPath,
                Path.GetFileNameWithoutExtension(Globals.ZLuaDlc1ScdName) + Globals.ReplayBackupSuffix);

        // ── Crash recovery ───────────────────────────────────────────────────────

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

        // ── Launch replay ────────────────────────────────────────────────────────

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
    }
}
