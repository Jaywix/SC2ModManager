/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public static class Globals
    {
        public static string LauncherName = "SC2ModManager";
        public static string ModManagerExecutableName = "SC2ModManager.exe";
        public static string UpdaterExecutableName = "SC2MMUpdater.exe";

        // Github URLs
        public static string RepoUrl = "https://api.github.com/repos/Jaywix/SC2ModManager/releases/latest";
        public static string GameDataBackupPt1GithubReleaseUrl = "https://github.com/Jaywix/SC2Mods/releases/download/GamedataBackupFilesPt1/gamedataBackupPt1.zip";
        public static string GameDataBackupPt2GithubReleaseUrl = "https://github.com/Jaywix/SC2Mods/releases/download/GamedataBackupFilesPt2/gamedataBackupPt2.zip";
        public static string MapsListUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/maps.json";
        public static string GenericModsListUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/genericMods.json";
        public static string MapImagesBaseUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/main/Images/";
        public static string NewsUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/news.json";
        public static string NewsImagesBaseUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/main/NewsImages/";



        // ================= INSTALL LOCATION =================

        // The pointer file lives in AppData/Roaming/SC2ModManager — this is the ONLY thing in AppData
        private static readonly string PointerDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LauncherName
        );

        private static readonly string PointerFilePath = Path.Combine(
            PointerDirectory,
            "install_location.txt"
        );

        public static string DefaultInstallPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherName
        );

        /// <summary>
        /// Returns the user-chosen install folder, or null if not yet set up.
        /// </summary>
        public static string GetInstallPath()
        {
            if (!File.Exists(PointerFilePath))
                return null;

            string path = File.ReadAllText(PointerFilePath).Trim();
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        /// Saves the chosen install path to the pointer file.
        /// </summary>
        public static void SetInstallPath(string path)
        {
            Directory.CreateDirectory(PointerDirectory);
            File.WriteAllText(PointerFilePath, path);
        }

        /// <summary>
        /// Returns true if first-run setup has been completed.
        /// </summary>
        public static bool IsSetupComplete()
        {
            string path = GetInstallPath();
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        /// <summary>
        /// Returns the data folder path (mods, config, presets) inside the install folder.
        /// </summary>
        public static string GetDataPath()
        {
            string install = GetInstallPath() ?? DefaultInstallPath;
            return Path.Combine(install, "Data");
        }
    }



    public static class AppTheme
    {
        public const string Standard = "standard";
        public const string UEF = "uef";
        public const string Cybran = "cybran";
        public const string Aeon = "aeon";
    }
}
