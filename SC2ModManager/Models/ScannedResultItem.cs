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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public enum ScanResultType
    {
        Unknown,
        MatchesDownloadable
    }

    public class ScanResultItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public ScanResultType ResultType { get; set; } = ScanResultType.Unknown;

        public object MatchedMod { get; set; }

        private bool isSelected = true;
        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
