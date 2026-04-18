/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 18, 2026
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

namespace SC2ModManager.ViewModels
{
    public enum SortField { Name, Date }
    public enum SortDirection { Ascending, Descending }

    public class ModSortViewModel : INotifyPropertyChanged
    {
        private SortField _field = SortField.Name;
        private SortDirection _direction = SortDirection.Ascending;

        public SortField Field
        {
            get => _field;
            set
            {
                _field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NameButtonText));
                OnPropertyChanged(nameof(DateButtonText));
            }
        }

        public SortDirection Direction
        {
            get => _direction;
            set
            {
                _direction = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NameButtonText));
                OnPropertyChanged(nameof(DateButtonText));
            }
        }

        // Button labels show arrow only on the active field
        public string NameButtonText => Field == SortField.Name
            ? (Direction == SortDirection.Ascending ? "Name ↑" : "Name ↓")
            : "Name";

        public string DateButtonText => Field == SortField.Date
            ? (Direction == SortDirection.Ascending ? "Date ↑" : "Date ↓")
            : "Date";

        /// <summary>
        ///     If already sorting by Name, toggle direction. Otherwise switch to Name Ascending.
        /// </summary>
        public void SortByName()
        {
            if (Field == SortField.Name)
                Direction = Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            else
            {
                Field = SortField.Name;
                Direction = SortDirection.Ascending;
            }
        }

        /// <summary>
        ///     If already sorting by Date, toggle direction. Otherwise switch to Date Ascending.
        /// </summary>
        public void SortByDate()
        {
            if (Field == SortField.Date)
                Direction = Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            else
            {
                Field = SortField.Date;
                Direction = SortDirection.Ascending;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
