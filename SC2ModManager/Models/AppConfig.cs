using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class AppConfig
    {
        public string GamePath { get; set; }
        public List<string> EnabledMaps { get; set; } = new();
    }
}
