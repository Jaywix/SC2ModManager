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
using System;
using System.IO;
using System.Text.Json;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service is responsible for loading and saving the application's configuration, which includes the game path, enabled maps, and enabled generic mods.
    /// </summary>
    public class ConfigService
    {
        private string ConfigPath => Path.Combine(Globals.GetDataPath(), "config.json");

        public ConfigService()
        {
            Directory.CreateDirectory(Globals.GetDataPath());
        }

        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save config", ex);
            }
        }

        public bool ConfigExists() => File.Exists(ConfigPath);

        public string DetectGamePath()
        {
            string path = TryGetFromSteam();
            if (!string.IsNullOrEmpty(path)) return path;
            return TryCommonPaths();
        }

        public void UpdateGamePath(AppConfig config, string newPath)
        {
            if (string.IsNullOrEmpty(newPath) || !Directory.Exists(newPath))
                throw new DirectoryNotFoundException("The specified game path does not exist.");
            if (!IsValidGamePath(newPath))
                throw new InvalidDataException("The specified path does not appear to be a valid Supreme Commander 2 installation.");

            config.GamePath = newPath;
            Save(config);
        }

        private string TryGetFromSteam()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string steamPath = key?.GetValue("SteamPath")?.ToString();
            if (string.IsNullOrEmpty(steamPath)) return null;

            string gamePath = Path.Combine(steamPath, "steamapps", "common", "Supreme Commander 2");
            return Directory.Exists(gamePath) ? gamePath : null;
        }

        private string TryCommonPaths()
        {
            string[] paths =
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Supreme Commander 2",
                @"C:\Program Files\Steam\steamapps\common\Supreme Commander 2",
                @"D:\Steam\steamapps\common\Supreme Commander 2",
                @"D:\Program Files (x86)\Steam\steamapps\common\Supreme Commander 2"
            };

            foreach (var path in paths)
                if (Directory.Exists(path)) return path;

            return null;
        }

        private bool IsValidGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "bin")) &&
                   File.Exists(Path.Combine(path, "bin", "SupremeCommander2.exe")) &&
                   Directory.Exists(Path.Combine(path, "gamedata"));
        }
    }
}