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
    public enum HotkeySection
    {
        Main,
        Tooltip,
        Debug
    }

    public class HotkeyEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        ///     The action/command name, e.g. "repair", "overcharge", "set_group1" (Determined by the "ability_id" field in orderstable.lua)
        /// </summary>
        public string Command { get; init; } = string.Empty;

        /// <summary>
        ///     Human-readable description loaded from keydescriptions.lua.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Which block this entry came from inside defaultKeyMap.lua.
        /// </summary>
        public HotkeySection Section { get; init; }

        /// <summary>
        ///     The key combo as it was read from the file. Never changes after initial load.
        ///     Used by RebuildDefaultKeyMap to uniquely identify the line to rewrite, so that
        ///     two different bindings for the same command (e.g. B and Shift-B both mapped to
        ///     'build') are each updated independently rather than trampling each other.
        /// </summary>
        public string OriginalKeyCombo { get; init; } = string.Empty;

        private string _keyCombo = string.Empty;
        /// <summary>
        ///     The key binding string in game format, e.g. "R", "Ctrl-1", "Shift-M", "Alt-B"
        /// </summary>
        public string KeyCombo
        {
            get => _keyCombo;
            set { _keyCombo = value; OnPropertyChanged(nameof(KeyCombo)); }
        }

        private bool _isDuplicate;
        /// <summary>
        ///     True when another entry in the same section shares the same KeyCombo.
        /// </summary>
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set { if (_isDuplicate != value) { _isDuplicate = value; OnPropertyChanged(nameof(IsDuplicate)); } }
        }
    }
}
