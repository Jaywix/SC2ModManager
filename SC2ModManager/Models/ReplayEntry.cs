/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 19, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.IO;

namespace SC2ModManager.Models
{
    public class ReplayEntry
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public string FolderName { get; set; }
        public DateTime LastModified { get; set; }
        public string DisplayName => Path.GetFileNameWithoutExtension(FilePath);
    }
}
