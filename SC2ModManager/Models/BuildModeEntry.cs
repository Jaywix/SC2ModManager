/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 12, 2026
 * Author: Jacob Wixom
 * 
*/
using System.ComponentModel;

namespace SC2ModManager.Models
{
    public enum BuildModeFaction
    {
        UEF,
        Cybran,
        Illuminate
    }

    public class BuildModeEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        ///     Which faction this entry belongs to.
        /// </summary>
        public BuildModeFaction Faction { get; init; }

        /// <summary>
        ///     Category table name, e.g. "BasicEngineering", "BasicLand", "BasicAir"
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        ///     The unit or structure ID, e.g. "uub0001", "ucl0001"
        ///     uub = unit, uef, building
        ///     ucl = unit, cybran, land
        ///     uia = unit, illuminate, air
        ///     Numbers are the id
        /// </summary>
        public string UnitId { get; init; } = string.Empty;

        /// <summary>
        ///     Inline comment from the lua file, e.g. "Land Factory". May be empty if no comment was provided in the lua
        /// </summary>
        public string Comment { get; init; } = string.Empty;


        private string _key = string.Empty;

        /// <summary>
        ///     Single uppercase letter used as the hotkey, e.g. "D", "F"
        /// </summary>
        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(nameof(Key)); }
        }

        private bool _isDuplicate;
        /// <summary>
        ///     True when another entry in the same category+faction table shares the same Key.
        /// </summary>
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set { if (_isDuplicate != value) { _isDuplicate = value; OnPropertyChanged(nameof(IsDuplicate)); } }
        }
    }
}
