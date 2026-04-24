/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 23, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.ViewModels
{
    public class NewsItemViewModel : INotifyPropertyChanged
    {
        private readonly NewsItem item;

        public string ID => item.ID;
        public string Type => item.Type;
        public string Title => item.Title;
        public string Date => item.Date;
        public string Body => item.Body;
        //public string LinkText => item.LinkText;
        //public string LinkUrl => item.LinkUrl;
        public IEnumerable<NewsLink> Links => item.Links ?? Enumerable.Empty<NewsLink>();
        public bool HasLinks => Links.Any();

        public string ImageUrl => item.ImageUrl;
        public bool Pinned => item.Pinned;

        private bool isExpanded = true;
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToggleIcon));
            }
        }

        public string ToggleIcon => IsExpanded ? "▲" : "▼";

        public void ToggleExpanded() => IsExpanded = !IsExpanded;

        public NewsItemViewModel(NewsItem item) { this.item = item; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
