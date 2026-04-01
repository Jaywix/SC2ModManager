using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SC2ModManager.Models;

namespace SC2ModManager.Services
{
    class ModService
    {
        private readonly string appDataPath;
        private readonly string mapsPath;
        private readonly string backupPath;
        private readonly ConfigService configService;

        public ModService(ConfigService configService)
        {
            this.configService = configService;

            this.appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            this.mapsPath = Path.Combine(this.appDataPath, "Maps");
            this.backupPath = Path.Combine(this.appDataPath, "GameBackup");

            EnsureDirectories();
        }



        public List<Map> GetAllMaps()
        {
            var config = this.configService.Load();

            var enabledSet = new HashSet<string>(
                config.EnabledMaps ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase
            );

            return Directory.GetFiles(this.mapsPath, "*.scd")
                .Select(file => new Map
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    IsEnabled = enabledSet.Contains(Path.GetFileName(file))
                })
                .ToList();
        }

        public void SaveMaps(IEnumerable<Map> maps)
        {
            var config = this.configService.Load();

            config.EnabledMaps = maps
                .Where(m => m.IsEnabled)
                .Select(m => m.Name)
                .ToList();

            this.configService.Save(config);

            RebuildGameData(config);
        }

        public void AddMap(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new Exception("Map file does not exist.");

            string fileName = Path.GetFileName(sourceFilePath);
            string destPath = Path.Combine(this.mapsPath, fileName);

            File.Copy(sourceFilePath, destPath, true);
        }

        public void RemoveMap(Map map)
        {
            if (File.Exists(map.Path))
            {
                File.Delete(map.Path);
            }
        }



        // ------------ Helpers ---------------

        private void RebuildGameData(AppConfig config)
        {
            string gameDataPath = Path.Combine(config.GamePath, "gamedata");

            if (!Directory.Exists(gameDataPath))
                throw new Exception("gamedata folder not found.");

            // Clear existing gamedata
            ClearDirectory(gameDataPath);

            // Restore from backup
            CopyDirectory(this.backupPath, gameDataPath);

            // Add enabled maps
            if (config.EnabledMaps == null)
                return;

            foreach (var mapName in config.EnabledMaps)
            {
                string source = Path.Combine(this.mapsPath, mapName);
                string dest = Path.Combine(gameDataPath, mapName);

                if (File.Exists(source))
                {
                    File.Copy(source, dest, true);
                }
            }
        }




        private void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(this.appDataPath))
                    Directory.CreateDirectory(this.appDataPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create app data directory: {ex.Message}");
            }

            try
            {
                if (!Directory.Exists(this.mapsPath))
                    Directory.CreateDirectory(this.mapsPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create maps directory: {ex.Message}");
            }

            try
            {
                if (!Directory.Exists(this.backupPath))
                    Directory.CreateDirectory(this.backupPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create backup directory: {ex.Message}");
            }
        }

        private void ClearDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                Directory.Delete(dir, true);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }
        }



    }
}
