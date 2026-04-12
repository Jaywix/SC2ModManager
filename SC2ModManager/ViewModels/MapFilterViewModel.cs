/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SC2ModManager.ViewModels
{
    public enum PlayerCountOperator { Equal, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual, Any }

    public class TagFilterItem : INotifyPropertyChanged
    {
        public string Tag { get; set; }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MapFilterViewModel : INotifyPropertyChanged
    {
        // ================= TAG FILTERS =================

        public ObservableCollection<TagFilterItem> TagFilters { get; set; }

        // ================= PLAYER COUNT FILTER =================

        private PlayerCountOperator playerCountOperator = PlayerCountOperator.Any;
        public PlayerCountOperator PlayerCountOperator
        {
            get => playerCountOperator;
            set { playerCountOperator = value; OnPropertyChanged(); }
        }

        private int playerCountValue = 2;
        public int PlayerCountValue
        {
            get => playerCountValue;
            set { playerCountValue = value; OnPropertyChanged(); }
        }

        public List<PlayerCountOperator> AvailableOperators { get; } = new()
        {
            PlayerCountOperator.Any,
            PlayerCountOperator.Equal,
            PlayerCountOperator.GreaterThan,
            PlayerCountOperator.LessThan,
            PlayerCountOperator.GreaterThanOrEqual,
            PlayerCountOperator.LessThanOrEqual
        };

        public List<int> AvailablePlayerCounts { get; } = new() { 2, 3, 4, 5, 6, 7, 8 };

        // ================= INIT =================

        public MapFilterViewModel()
        {
            TagFilters = new ObservableCollection<TagFilterItem>(
                MapTagConstants.AllTags.Select(t => new TagFilterItem { Tag = t, IsSelected = false })
            );
        }

        // ================= FILTER LOGIC =================

        /// <summary>
        /// Returns true if the map passes all active filters.
        /// </summary>
        public bool Passes(Map map)
        {
            var selectedTags = TagFilters.Where(t => t.IsSelected).Select(t => t.Tag).ToList();
            if (!selectedTags.Any()) return true;
            if (map.Tags == null) return false;
            return selectedTags.All(t => map.Tags.Contains(t));
        }

        public void ClearAll()
        {
            foreach (var tag in TagFilters)
                tag.IsSelected = false;

            PlayerCountOperator = PlayerCountOperator.Any;
            PlayerCountValue = 2;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}