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
    public class ModStorageService
    {
        private readonly string mapsPath;
        private readonly string genericModsPath;

        private readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        ///     This class is how we manage the AppData folder stuff
        /// </summary>
        public ModStorageService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            mapsPath = Path.Combine(appData, "Maps");
            genericModsPath = Path.Combine(appData, "GenericMods");

            Directory.CreateDirectory(mapsPath);
            Directory.CreateDirectory(genericModsPath);
        }

        // ---------------- MAPS ----------------

        public List<string> GetDownloadedMaps()
        {
            return Directory.GetFiles(mapsPath, "*.scd")
                            .Select(Path.GetFileName)
                            .ToList();
        }

        public async Task DownloadMapAsync(Map map)
        {
            if (string.IsNullOrEmpty(map.DownloadURL))
                throw new Exception("Map has no download URL.");

            var data = await httpClient.GetByteArrayAsync(map.DownloadURL);

            string path = Path.Combine(mapsPath, map.FileName);

            await File.WriteAllBytesAsync(path, data);
        }

        public void DeleteMap(Map map)
        {
            string path = Path.Combine(mapsPath, map.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }

        public async Task ExtractAndAddMapsAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new Exception("Zip file not found.");

            string tempExtractPath = Path.Combine(Path.GetTempPath(), "SC2_map_extract");

            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
            }

            Directory.CreateDirectory(tempExtractPath);

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);
            });

            var scdFiles = Directory.GetFiles(tempExtractPath, "*.scd", SearchOption.AllDirectories);

            foreach (var file in scdFiles)
            {
                string fileName = Path.GetFileName(file);
                string destination = Path.Combine(mapsPath, fileName);

                File.Copy(file, destination, true);
            }

            Directory.Delete(tempExtractPath, true);
        }

        public async Task AddMapAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("Map file not found.");

            string fileName = Path.GetFileName(filePath);
            string destination = Path.Combine(mapsPath, fileName);

            await Task.Run(() =>
            {
                File.Copy(filePath, destination, true);
            });
        }

        // ---------------- GENERIC MODS ----------------

        public List<string> GetDownloadedGenericMods()
        {
            return Directory.GetFiles(genericModsPath, "*.scd")
                            .Select(Path.GetFileName)
                            .ToList();
        }

        public async Task DownloadGenericModAsync(GenericGamedataMod mod)
        {
            if (string.IsNullOrEmpty(mod.DownloadURL))
                throw new Exception("Mod has no download URL.");

            var data = await httpClient.GetByteArrayAsync(mod.DownloadURL);

            string path = Path.Combine(genericModsPath, mod.FileName);

            await File.WriteAllBytesAsync(path, data);
        }

        public void DeleteGenericMod(GenericGamedataMod mod)
        {
            string path = Path.Combine(genericModsPath, mod.FileName);

            if (File.Exists(path))
                File.Delete(path);
        }
    }
}