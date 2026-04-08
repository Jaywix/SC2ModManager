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
