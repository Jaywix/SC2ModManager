using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System;

using SC2ModManager.Models;

namespace SC2ModManager.Services
{
    class GameService
    {
        private readonly ConfigService configService;

        public GameService(ConfigService configService)
        {
            this.configService = configService;
        }

        public void LaunchGame()
        {
            var config = this.configService.Load();

            if (config == null || string.IsNullOrEmpty(config.GamePath))
            {
                throw new Exception("Game path not configured.");
            }

            string exePath = Path.Combine(
                config.GamePath,
                "bin",
                "SupremeCommander2.exe"
            );

            if (!File.Exists(exePath))
            {
                throw new Exception($"Game executable not found at: {exePath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            });
        }





    }
}
