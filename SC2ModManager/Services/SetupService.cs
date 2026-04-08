/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: 2024-01-01
 * Last updated: 2024-06-01
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This is a very basic service that is responsible for setting up the application's data directory and providing paths to important files like the config file.
    /// </summary>
    public class SetupService
    {
        public string AppDataPath { get; }
        public string ConfigPath => Path.Combine(AppDataPath, "config.json");

        public SetupService()
        {
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );
        }
    }
}
