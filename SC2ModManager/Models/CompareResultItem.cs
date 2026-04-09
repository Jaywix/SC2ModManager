/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class CompareResultItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public bool IsDifferent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
