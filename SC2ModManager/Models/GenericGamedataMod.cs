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
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class GenericGamedataMod : INotifyPropertyChanged
    {

        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("lastUpdatedDate")]
        public string LastUpdated { get; set; }

        [JsonPropertyName("downloadURL")]
        public string DownloadURL { get; set; }




        private bool isEnabled;
        [JsonIgnore]
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isChecked;
        [JsonIgnore]
        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isDownloaded;
        [JsonIgnore]
        public bool IsDownloaded
        {
            get => isDownloaded;
            set
            {
                if (isDownloaded != value)
                {
                    isDownloaded = value;
                    OnPropertyChanged();
                }
            }
        }




        /// <summary>
        ///     This should only be necessary for the JSON. Do not use in the code
        /// </summary>
        public GenericGamedataMod()
        {
            this.ID = string.Empty;
            this.FileName = "Unknown";
            this.Author = "Unknown";
            this.Type = "Unknown";
            this.Version = "Unknown";
            this.LastUpdated = "Unknown";
            this.DownloadURL = string.Empty;
            this.IsEnabled = false;
            this.IsDownloaded = true;

            this.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ID))
                {
                    this.FileName = this.ID;
                    OnPropertyChanged(nameof(FileName));
                }
            };

            //this.OnPropertyChanged(nameof(ID));
        }


        /// <summary>
        ///     This is for when users manually import a map
        /// </summary>
        /// <param name="fileName"></param>
        public GenericGamedataMod(string fileName)
        {
            this.ID = fileName;
            this.FileName = fileName;
            this.Author = "Unknown";
            this.Type = "Unknown";
            this.Version = "Unknown";
            this.LastUpdated = "Unknown";
            this.DownloadURL = string.Empty;
            this.IsEnabled = false;
            this.IsDownloaded = true;

            this.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ID))
                {
                    this.FileName = this.ID;
                    OnPropertyChanged(nameof(FileName));
                }
            };

            //this.OnPropertyChanged(nameof(ID));
        }


        public List<GenericGamedataMod> BuildModState(List<GenericGamedataMod> availableMods, List<string> downloadedMods, List<string> enabledMods)
        {
            foreach (var mod in availableMods)
            {
                mod.IsDownloaded = downloadedMods.Contains(mod.FileName);
                mod.IsEnabled = enabledMods.Contains(mod.FileName);
            }

            return availableMods;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
