using System;

namespace SC2ModManager.Models
{
    public class ReleaseInfo
    {
        public Version Version { get; set; }
        public string TagName { get; set; }
        public string Body { get; set; }
        public string DownloadUrl { get; set; }

        /// <summary>True for the version that's running right now. Its card shows "Currently Installed" instead of a restore button.</summary>
        public bool IsCurrentVersion { get; set; }
    }
}
