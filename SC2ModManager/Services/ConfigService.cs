using SC2ModManager.Models;
using System;
using System.IO;
using System.Text.Json;

namespace SC2ModManager.Services
{
    public class ConfigService
    {
        private readonly string configPath;

        public ConfigService()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            Directory.CreateDirectory(appDataPath);

            this.configPath = Path.Combine(appDataPath, "config.json");
        }

        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(configPath))
                    return new AppConfig();

                string json = File.ReadAllText(configPath);

                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                // If config is corrupted, return default
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                // You could log this instead
                throw new Exception("Failed to save config", ex);
            }
        }

        public bool ConfigExists()
        {
            return File.Exists(configPath);
        }

        public string DetectGamePath()
        {
            string path = TryGetFromSteam();
            if (!string.IsNullOrEmpty(path))
                return path;

            return TryCommonPaths();
        }

        private string TryGetFromSteam()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            string steamPath = key?.GetValue("SteamPath")?.ToString();

            if (string.IsNullOrEmpty(steamPath))
                return null;

            string gamePath = Path.Combine(
                steamPath,
                "steamapps",
                "common",
                "Supreme Commander 2"
            );

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
            {
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }
    }
}