using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    class HotkeyMod
    {
        public string ConfigFilePath { get; set; }

        public HotkeyMod(string name, string path, bool isEnabled)
        {
                this.ConfigFilePath = path;
        }
    }
}
