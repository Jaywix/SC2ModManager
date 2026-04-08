/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: 2024-01-01
 * Last updated: 2024-06-01
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



    }
}
