using SC2ModManager.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This class handles all of the gamedata manipulation
    /// </summary>
    public class GamedataService
    {
        public void EnableMap(Map map, string mapsPath, string gameDataPath)
        {
            string source = Path.Combine(mapsPath, map.FileName);
            string destination = Path.Combine(gameDataPath, map.FileName);

            if (!File.Exists(source))
                throw new Exception($"Map not found in storage: {map.FileName}");

            File.Copy(source, destination, true);
        }

        public void DisableMap(Map map, string gameDataPath)
        {
            string path = Path.Combine(gameDataPath, map.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        public void EnableGenericMod(GenericGamedataMod mod, string modsPath, string gameDataPath)
        {
            string source = Path.Combine(modsPath, mod.FileName);
            string destination = Path.Combine(gameDataPath, mod.FileName);

            if (!File.Exists(source))
                throw new Exception($"Mod not found in storage: {mod.FileName}");

            File.Copy(source, destination, true);
        }

        public void DisableGenericMod(GenericGamedataMod mod, string gameDataPath)
        {
            string path = Path.Combine(gameDataPath, mod.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        public async Task RestoreOriginalGamedataAsync(string gameDataPath)
        {
            // Ensure directory exists
            if (!Directory.Exists(gameDataPath))
                Directory.CreateDirectory(gameDataPath);

            // This will delete everything inside gamedata to reset it to the original files found at github
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

            // Delete empty directories (bottom-up)
            foreach (var dir in Directory.GetDirectories(gameDataPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(dir, false);
                }
                catch
                {
                    // ignore non-empty dirs or failures
                }
            }

            // Get the files from github and extract them to the gamedata directory
            await GetGamedataFilesFromGithub(gameDataPath);
        }

        private async Task GetGamedataFilesFromGithub(string gameDataPath)
        {
            string url1 = Globals.GameDataBackupPt1GithubReleaseUrl;
            string url2 = Globals.GameDataBackupPt2GithubReleaseUrl;

            string tempDir = Path.Combine(Path.GetTempPath(), "SC2ModManager_GameData");
            Directory.CreateDirectory(tempDir);

            string zip1Path = Path.Combine(tempDir, "gamedata1.zip");
            string zip2Path = Path.Combine(tempDir, "gamedata2.zip");

            using (HttpClient client = new HttpClient())
            {
                var task1 = DownloadToFileAsync(client, url1, zip1Path);
                var task2 = DownloadToFileAsync(client, url2, zip2Path);

                await Task.WhenAll(task1, task2);
            }

            Directory.CreateDirectory(gameDataPath);

            ExtractZipToDirectory(zip1Path, gameDataPath);
            ExtractZipToDirectory(zip2Path, gameDataPath);
        }

        private async Task DownloadToFileAsync(HttpClient client, string url, string filePath)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }
        }

        private void ExtractZipToDirectory(string zipPath, string destination)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string fullPath = Path.Combine(destination, entry.FullName);

                    // If it's a directory
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                    // Extract file (overwrite)
                    entry.ExtractToFile(fullPath, true);
                }
            }
        }
    }
}