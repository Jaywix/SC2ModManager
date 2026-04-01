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
                throw new Exception("Config file not found.");

            return JsonSerializer.Deserialize<AppConfig>(
                File.ReadAllText(this.configPath)
            );
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
    }
}
