using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class SetupService
    {
        public string AppDataPath { get; }
        public string ConfigPath => Path.Combine(AppDataPath, "config.json");

        public SetupService()
        {
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );
        }
    }
}
