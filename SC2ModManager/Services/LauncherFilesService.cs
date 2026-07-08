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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     Handles downloading, installing, and removing the support files Maksing's launcher needs
    ///     (ipc_dll.dll and friends). Works like the hotkey mod: check if they're already there, and
    ///     if not offer to download them. These files don't replace any game files, so no backups.
    /// </summary>
    public class LauncherFilesService
    {
        private static readonly HttpClient _httpClient = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.0");
            client.Timeout = TimeSpan.FromMinutes(5);
            return client;
        }

        // Where each support file goes. The game dlls go in <gamePath>\bin, everything else sits
        // next to the mod manager exe (the launcher folder).
        private static List<(string FileName, string DestDir)> GetTargets(string gamePath)
        {
            string gameBin = Path.Combine(gamePath, "bin");
            string launcherFolder = Globals.GetLauncherFolderPath();

            return new List<(string, string)>
            {
                (Globals.VMProtectDllName,     gameBin),
                (Globals.LibCryptoDllName,     gameBin),
                (Globals.LibSslDllName,        gameBin),
                (Globals.IpcDllName,           launcherFolder),
                (Globals.Injector32HelperName, launcherFolder),
                (Globals.BannedListName,       launcherFolder),
            };
        }

        /// <summary>
        ///     True if every launcher support file is present at its destination.
        /// </summary>
        public bool AreFilesInstalled(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return false;

            return GetTargets(gamePath).All(t => File.Exists(Path.Combine(t.DestDir, t.FileName)));
        }

        /// <summary>
        ///     Downloads launcherfiles.zip and drops each file into its spot. Files are matched by
        ///     name inside the zip, so the zip's internal folder layout doesn't matter.
        /// </summary>
        public async Task DownloadAndInstallAsync(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                throw new InvalidOperationException("Game path is not set.");

            string tempDir = Path.Combine(Path.GetTempPath(), "SC2ModManager_LauncherFiles");
            string zipPath = Path.Combine(tempDir, "launcherfiles.zip");
            string extractDir = Path.Combine(tempDir, "extracted");

            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(extractDir);

                // Download the zip
                using (var response = await _httpClient.GetAsync(Globals.LauncherFilesDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var file = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(file);
                }

                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                // Route each known file to its destination, found by name anywhere in the zip
                var extracted = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
                var missing = new List<string>();

                foreach (var (fileName, destDir) in GetTargets(gamePath))
                {
                    string source = extracted.FirstOrDefault(
                        f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

                    if (source == null)
                    {
                        missing.Add(fileName);
                        continue;
                    }

                    Directory.CreateDirectory(destDir);
                    File.Copy(source, Path.Combine(destDir, fileName), overwrite: true);
                }

                if (missing.Count > 0)
                    throw new FileNotFoundException(
                        "The launcher download was missing these files: " + string.Join(", ", missing));
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch { }
            }
        }

        /// <summary>
        ///     Deletes every launcher support file from its destination. Safe to call even if some
        ///     of them aren't there. The game bin dlls matter most here, since wiping our own data
        ///     folder on uninstall wouldn't catch files that live in the game folder.
        /// </summary>
        public void Uninstall(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return;

            foreach (var (fileName, destDir) in GetTargets(gamePath))
            {
                string path = Path.Combine(destDir, fileName);
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }
    }
}
