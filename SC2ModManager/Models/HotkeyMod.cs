/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: May 12, 2026
 * Author: Jacob Wixom
 * 
*/
using System.IO;

namespace SC2ModManager.Models
{
    public enum HotkeyModType
    {
        NormalHotkey,
        BuildModeHotkey
    }

    public class HotkeyMod
    {
        public HotkeyModType ModType { get; set; }

        /// <summary>
        ///     Full path to the local copy of the .scd file inside {DataPath}/HotkeyMods/
        /// </summary>
        public string LocalScdPath { get; set; }

        /// <summary>
        ///     Full path where the .scd file lives in the game's gamedata folder.
        /// </summary>
        public string GamedataScdPath { get; set; }

        /// <summary>
        ///     Browser URL for the Google Drive folder containing the .scd download.
        /// </summary>
        public string DownloadPageUrl { get; set; }

        /// <summary>
        ///     True if the mod has been imported and had its first save (backup files exist in the Backups folder).
        /// </summary>
        public bool IsInstalled => File.Exists(LocalScdPath);

        public HotkeyMod(HotkeyModType modType, string localScdPath, string gamedataScdPath, string downloadPageUrl)
        {
            ModType = modType;
            LocalScdPath = localScdPath;
            GamedataScdPath = gamedataScdPath;
            DownloadPageUrl = downloadPageUrl;
        }
    }
}
