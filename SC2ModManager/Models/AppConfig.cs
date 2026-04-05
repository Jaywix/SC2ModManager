using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class AppConfig
    {
        public string GamePath { get; set; } = string.Empty;

        public List<string> EnabledMaps { get; set; } = new();

        public List<string> EnabledGenericMods { get; set; } = new();
    }
}
