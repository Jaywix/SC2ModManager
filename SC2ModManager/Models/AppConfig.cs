/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 18, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    /// <summary>
    ///     This model represents the application's configuration, including the game path, enabled maps, and enabled generic mods. 
    ///     It is used to store and retrieve user preferences and settings for the mod manager.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        ///     This is the path to Supreme Commander 2's installation directory. It is used to locate the game's gamedata folder where mods will be enabled/disabled.
        /// </summary>
        public string GamePath { get; set; } = string.Empty;

        /// <summary>
        ///     This defines the applications color theme
        /// </summary>
        public string Theme { get; set; } = "standard";

        /// <summary>
        ///     This is a json representation of all the maps installed. This json must be of the same format as the Map model 
        ///     and the github repository's maps.json file. This is used to keep track of which maps are enabled and to display map information in the UI.
        /// </summary>
        public List<string> EnabledMaps { get; set; } = new();

        /// <summary>
        ///     This is a json representation of all the generic gamedata mods installed. This json must be of the same format as the GenericGamedataMod model 
        ///     and the github repository's generic gamedata mods' .json file. This is used to keep track of which maps are enabled and to display map information in the UI.
        /// </summary>
        public List<string> EnabledGenericMods { get; set; } = new();

        /// <summary>
        ///     User-chosen path to the SC2 replays folder. Defaults to the standard My Documents location.
        /// </summary>
        public string ReplayFolderPath { get; set; } = string.Empty;

        /// <summary>
        ///     When true, the replay launch warning dialog is suppressed.
        /// </summary>
        public bool SuppressReplayWarning { get; set; } = false;
    }
}
