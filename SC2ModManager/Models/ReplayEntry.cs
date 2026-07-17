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

        public long FileSizeBytes { get; set; }

        /// <summary>File size as "1.4 MB" / "312 KB".</summary>
        public string FileSizeDisplay =>
            FileSizeBytes >= 1024 * 1024
                ? $"{FileSizeBytes / (1024.0 * 1024.0):0.0} MB"
                : $"{Math.Max(1, FileSizeBytes / 1024)} KB";

        public ReplayMetadata Metadata { get; set; }
    }
}
