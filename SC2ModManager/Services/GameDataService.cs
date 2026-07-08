/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using SC2ModManager.Views;
using SC2ModManager.ViewModels;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service does everything that has to do with the gamedata folder.
    ///     For now, each type of mod is going to need its own enable/disable methods.
    ///     Each mod is split up into labled sections, so whenever adding a mod type, copy one 
    ///     of the existing ones and modify it to fit the new mod type. The restore method is just for the original gamedata backup
    /// </summary>
    public class GamedataService
    {
        // ================= MAPS =================

        /// <summary>
        ///     Copies the map from Mods/Maps/Enabled into the game's gamedata folder.
        /// </summary>
        public void EnableMap(Map map, string mapsEnabledPath, string gameDataPath)
        {
            string source = Path.Combine(mapsEnabledPath, map.FileName);
            string destination = Path.Combine(gameDataPath, map.FileName);

            if (!File.Exists(source))
                throw new Exception($"Map not found in Enabled folder: {map.FileName}");

            File.Copy(source, destination, true);
        }

        /// <summary>
        ///     Removes the map from the game's gamedata folder.
        /// </summary>
        public void DisableMap(Map map, string gameDataPath)
        {
            string path = Path.Combine(gameDataPath, map.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        // ================= GENERIC MODS =================

        /// <summary>
        ///     Copies the mod from Mods/GenericMods/Enabled into the game's gamedata folder.
        /// </summary>
        public void EnableGenericMod(GenericGamedataMod mod, string genericModsEnabledPath, string gameDataPath)
        {
            string source = Path.Combine(genericModsEnabledPath, mod.FileName);
            string destination = Path.Combine(gameDataPath, mod.FileName);

            if (!File.Exists(source))
                throw new Exception($"Mod not found in Enabled folder: {mod.FileName}");

            File.Copy(source, destination, true);
        }

        /// <summary>
        ///     Removes the mod from the game's gamedata folder.
        /// </summary>
        public void DisableGenericMod(GenericGamedataMod mod, string gameDataPath)
        {
            string path = Path.Combine(gameDataPath, mod.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        // ================= RESTORE =================

        /// <summary>
        ///     Restores the original gamedata from GitHub. Nothing in the existing gamedata
        ///     folder is deleted until BOTH backup archives have been fully downloaded and
        ///     validated — a failed or interrupted download leaves the current gamedata intact.
        /// </summary>
        public async Task RestoreOriginalGamedataAsync(string gameDataPath)
        {
            var progressVm = new ProgressViewModel { Status = "Restoring original gamedata..." };
            var progressWindow = new ProgressWindow { DataContext = progressVm };

            progressWindow.Show();

            string tempDir = Path.Combine(Path.GetTempPath(), "SC2ModManager_GameData");
            string zip1Path = Path.Combine(tempDir, "gamedata1.zip");
            string zip2Path = Path.Combine(tempDir, "gamedata2.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                // 1. Download both archives completely BEFORE touching gamedata
                var progress = new Progress<int>(value =>
                {
                    progressVm.Progress = value;
                    progressVm.Status = $"Downloading... {value}%";
                });

                using (HttpClient client = new HttpClient())
                {
                    await Task.WhenAll(
                        DownloadToFileAsync(client, Globals.GameDataBackupPt1GithubReleaseUrl, zip1Path, progress),
                        DownloadToFileAsync(client, Globals.GameDataBackupPt2GithubReleaseUrl, zip2Path, progress)
                    );
                }

                // 2. Verify both archives are readable zips before deleting anything
                progressVm.Status = "Validating downloads...";
                ValidateZipArchive(zip1Path);
                ValidateZipArchive(zip2Path);

                // 3. Now it's safe to wipe the current gamedata
                progressVm.Status = "Deleting current gamedata...";

                if (!Directory.Exists(gameDataPath))
                    Directory.CreateDirectory(gameDataPath);

                foreach (var file in Directory.GetFiles(gameDataPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to delete file: {file}", ex);
                    }
                }

                foreach (var dir in Directory.GetDirectories(gameDataPath, "*", SearchOption.AllDirectories))
                {
                    try { Directory.Delete(dir, false); }
                    catch { }
                }

                // 4. Extract the validated archives
                progressVm.Status = "Extracting files...";
                progressVm.Progress = 0;

                ExtractZipToDirectory(zip1Path, gameDataPath);
                ExtractZipToDirectory(zip2Path, gameDataPath);

                // 5. The restore brings back the original lua.scd, so the hotkey mod isn't active
                //    anymore. Clean up the lua/luo/toc files, mainly to remove a leftover
                //    toc.win.bdf from the game root which would break the game.
                new HotkeyService().ReconcileNormalHotkeyState(gameDataPath);

                await Task.Delay(500);
            }
            finally
            {
                try
                {
                    if (File.Exists(zip1Path)) File.Delete(zip1Path);
                    if (File.Exists(zip2Path)) File.Delete(zip2Path);
                }
                catch { }

                // Always close the progress window, even when the restore fails
                progressWindow.Close();
            }
        }

        /// <summary>
        ///     Throws if the file isn't a readable zip with stuff in it (like when a failed download
        ///     saves an html error page instead of the actual zip).
        /// </summary>
        private static void ValidateZipArchive(string zipPath)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0)
                throw new InvalidDataException($"Downloaded archive is empty: {Path.GetFileName(zipPath)}");
        }

        private async Task DownloadToFileAsync(HttpClient client, string url, string filePath, IProgress<int> progress)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalRead = 0L;
            var buffer = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes > 0)
                    progress?.Report((int)(totalRead * 100 / totalBytes));
            }
        }

        private void ExtractZipToDirectory(string zipPath, string destination)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                string fullPath = Path.Combine(destination, entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                entry.ExtractToFile(fullPath, true);
            }
        }
    }
}