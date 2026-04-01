using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    class ConfigService
    {
        private readonly string configPath;

        public string GamePath { get; set; }

        public ConfigService()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            this.configPath = Path.Combine(appDataPath, "config.json");
        }

        public AppConfig Load()
        {
            if (!File.Exists(this.configPath))
            {
                return new AppConfig();
            }

            string json = File.ReadAllText(this.configPath);

            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }

        public void Save(AppConfig config)
        {
            File.WriteAllText(
                this.configPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );
        }

        public bool ConfigExists()
        {
            return File.Exists(this.configPath);
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
