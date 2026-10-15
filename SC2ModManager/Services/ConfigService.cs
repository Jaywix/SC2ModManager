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

        // config.json.bak is always the last config that saved fine (File.Replace makes it on every save)
        private string BackupPath => ConfigPath + ".bak";

        private static readonly object FileLock = new();

        public AppConfig Load()
        {
            lock (FileLock)
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                // Normal case, config.json reads fine
                if (TryRead(ConfigPath, out var config))
                    return config!;

                // config.json is broken (the app got killed mid-save, disk problem, someone edited it by hand).
                // This used to silently reset every setting. Try the backup from the last good save first
                if (TryRead(BackupPath, out var backup))
                {
                    try { File.Copy(BackupPath, ConfigPath, overwrite: true); } catch { }
                    TellUser("Your settings file was damaged, so the mod manager restored it from the last good backup. " +
                             "Anything you changed right before this might need to be set again.");
                    return backup!;
                }

                // Both are broken. Move the bad one out of the way (not deleted, in case someone wants to look at it)
                // so this only happens once, and start fresh
                try { File.Move(ConfigPath, ConfigPath + ".broken", overwrite: true); } catch { }
                TellUser("Your settings file was damaged and couldn't be recovered, so the mod manager is starting with default settings. " +
                         "You may need to set your game path and theme again.\n\n" +
                         $"The damaged file was kept here:\n{ConfigPath}.broken");
                return new AppConfig();
            }
        }

        private bool TryRead(string path, out AppConfig? config)
        {
            config = null;
            try
            {
                if (!File.Exists(path))
                    return false;

                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path));
                if (config == null)
                    return false;

                // Older versions could save the game path with mixed slashes (from the Steam registry),
                // so clean it up on load. It gets written back clean the next time the config is saved
                config.GamePath = NormalizePath(config.GamePath);
                return true;
            }
            catch
            {
                config = null;
                return false;
            }
        }

        // Load gets called from all over, sometimes not on the UI thread, so queue the message instead of showing it right here
        private static void TellUser(string message)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show(message, "Settings Restored",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning));
            }
            catch
            {
                // nothing we can do if there's no UI to show it on
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                lock (FileLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

                    // Write the whole thing to a temp file first, then swap it in. If the app gets killed
                    // halfway through a write, the real config.json is still the old good one instead of
                    // an empty or half written file
                    string tempPath = ConfigPath + ".tmp";
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(ConfigPath))
                    {
                        try
                        {
                            // Swaps the new file in and keeps the old one as config.json.bak in one step
                            File.Replace(tempPath, ConfigPath, BackupPath);
                        }
                        catch (IOException)
                        {
                            // File.Replace doesn't work on some drives (network/FAT), do it the long way
                            File.Copy(ConfigPath, BackupPath, overwrite: true);
                            File.Move(tempPath, ConfigPath, overwrite: true);
                        }
                    }
                    else
                    {
                        File.Move(tempPath, ConfigPath);
                    }
                }
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

            config.GamePath = NormalizePath(newPath);
            Save(config);
        }

        /// <summary>
        ///     Tries to flip any forward slashes to backslashes and get the full path
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) 
                return path;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private string TryGetFromSteam()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string steamPath = key?.GetValue("SteamPath")?.ToString();
            if (string.IsNullOrEmpty(steamPath)) return null;

            // Steam stores SteamPath with forward slashes (d:/program files (x86)/steam), so GetFullPath
            // flips them to backslashes. The file dialogs throw on mixed slashes otherwise
            string gamePath = NormalizePath(Path.Combine(steamPath, "steamapps", "common", "Supreme Commander 2"));
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