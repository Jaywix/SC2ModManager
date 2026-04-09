/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System;
using SC2ModManager.Models;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service is responsible for launching the game. 
    ///     It reads the game path from the configuration and starts the game's executable. 
    ///     If the game path is not configured or the executable is not found, it throws an exception.
    ///     IMPORTANT: If the configuration changes, a new instance of this service must be created
    /// </summary>
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
