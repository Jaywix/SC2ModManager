using System;

namespace SC2ModManager.Models
{
    public class ReleaseInfo
    {
        public Version Version { get; set; }
        public string TagName { get; set; }
        public string Body { get; set; }
        public string DownloadUrl { get; set; }
    }
}
