/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 23, 2026
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
        public static string ReleasesListUrl = "https://api.github.com/repos/Jaywix/SC2ModManager/releases";
        public static string GameDataBackupPt1GithubReleaseUrl = "https://github.com/Jaywix/SC2Mods/releases/download/GamedataBackupFilesPt1/gamedataBackupPt1.zip";
        public static string GameDataBackupPt2GithubReleaseUrl = "https://github.com/Jaywix/SC2Mods/releases/download/GamedataBackupFilesPt2/gamedataBackupPt2.zip";
        public static string MapsListUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/maps.json";
        public static string GenericModsListUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/genericMods.json";
        public static string MapImagesBaseUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/main/Images/";
        public static string NewsUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/master/news.json";
        public static string NewsImagesBaseUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/main/NewsImages/";
        public static string GenericModImagesBaseUrl = "https://raw.githubusercontent.com/Jaywix/SC2Mods/main/Images/GenericMods/";




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
        ///     Returns the user-chosen install folder, or null if not yet set up.
        /// </summary>
        public static string GetInstallPath()
        {
            if (!File.Exists(PointerFilePath))
                return null;

            string path = File.ReadAllText(PointerFilePath).Trim();
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        ///     Saves the chosen install path to the pointer file.
        /// </summary>
        public static void SetInstallPath(string path)
        {
            Directory.CreateDirectory(PointerDirectory);
            File.WriteAllText(PointerFilePath, path);
        }

        /// <summary>
        ///     Returns true if first-run setup has been completed.
        /// </summary>
        public static bool IsSetupComplete()
        {
            string path = GetInstallPath();
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        /// <summary>
        ///     Returns the data folder path (mods, config, presets) inside the install folder.
        /// </summary>
        public static string GetDataPath()
        {
            string install = GetInstallPath() ?? DefaultInstallPath;
            return Path.Combine(install, "Data");
        }

        // ================= MAKSING HOTKEY MOD =================

        public static string LuaScdName = "lua.scd";
        public static string NormalHotkeyScdName = "luo.scd";
        public static string BuildModeScdName = "BuildmodeHotkeys.scd";

        // toc.win.bdf must be placed in the SC2 game root directory (alongside the gamedata folder)
        public static string TocWinBdfName = "toc.win.bdf";

        // Direct download URLs (GitHub releases)
        // I should probably change the code tot ake the base URL and filename separately, but this is fine for now since this will probably never change
        public static string NormalHotkeyDirectDownloadUrl = "https://github.com/Jaywix/SC2Mods/releases/download/MaksingHotkeyModFiles/luo.scd";
        public static string NormalHotkeyTocBdfDirectDownloadUrl = "https://github.com/Jaywix/SC2Mods/releases/download/MaksingHotkeyModFiles/toc.win.bdf";
        public static string BuildModeDirectDownloadUrl = "https://github.com/Jaywix/SC2Mods/releases/download/MaksingHotkeyModFiles/BuildmodeHotkeys.scd";

        // Suffix appended to backup lua files so we how they were made. This may come in handy when I implement the balance mods or any other mod that changes original files
        public const string HotkeyModBackupSuffix = "_Original_MadeBy_HotkeyMod";

        /// <summary>
        ///     Gamedata files owned exclusively by the hotkey mod system. The preset system
        ///     must never capture, add, or delete these — lua.scd / luo.scd are a coupled pair
        ///     (see <see cref="Services.HotkeyService"/>) and removing one without restoring the
        ///     other leaves gamedata in a combination that crashes the game. Hotkey state is
        ///     toggled only from the Hotkeys tab, independently of presets.
        /// </summary>
        public static readonly string[] HotkeyManagedGamedataFiles =
        {
            LuaScdName,           // lua.scd  (original keymap)
            NormalHotkeyScdName,  // luo.scd  (modified keymap)
            BuildModeScdName      // BuildmodeHotkeys.scd
        };

        /// <summary>
        ///     True if the given file name or relative path refers to a hotkey-managed gamedata file.
        /// </summary>
        public static bool IsHotkeyManagedFile(string fileNameOrRelativePath)
        {
            string name = Path.GetFileName(fileNameOrRelativePath);
            return HotkeyManagedGamedataFiles.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetHotkeyModsPath()
        {
            return Path.Combine(GetDataPath(), "HotkeyMods");
        }

        public static string GetLocalTocBdfPath()
        {
            return Path.Combine(GetHotkeyModsPath(), TocWinBdfName);
        }

        public static string GetHotkeyModsBackupPath()
        {
            return Path.Combine(GetHotkeyModsPath(), "Backups");
        }

        // ================= REPLAY TOOLS (DISABLED) =================
        // Replays now launch directly with no support files, so these are no longer used.
        // Kept commented out for possible future revival.
        /*
        public static string LuaReplayFileName = "lua.replay";
        public static string ZLuaDlc1ReplayFileName = "z_lua_dlc1.replay";
        public static string ZLuaDlc1ScdName = "z_lua_dlc1.scd";
        public const string ReplayBackupSuffix = "_replaybackup.bkup";

        public static string ReplayToolsLuaReplayDownloadUrl = "https://github.com/Jaywix/SC2Mods/releases/download/ReplayTools_v1.0.0/lua.replay";
        public static string ReplayToolsZLuaDlc1ReplayDownloadUrl = "https://github.com/Jaywix/SC2Mods/releases/download/ReplayTools_v1.0.0/z_lua_dlc1.replay";

        public static string GetReplayToolsPath()
        {
            return Path.Combine(GetDataPath(), "replaytools");
        }
        */

        public static string DefaultReplaysBasePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "SquareEnix", "Supreme Commander 2", "replays"
            );
    }



    public static class AppTheme
    {
        public const string Standard = "standard";
        public const string UEF = "uef";
        public const string Cybran = "cybran";
        public const string Aeon = "aeon";
    }
}
