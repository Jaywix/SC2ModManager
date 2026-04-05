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
    public class Map : INotifyPropertyChanged
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



        [JsonPropertyName("playerCount")]
        public string PlayerCount { get; set; }

        [JsonPropertyName("mapTeamStyle")]
        public string MapTeamStyle { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }

        [JsonPropertyName("mapName")]
        public string MapName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("mapPictureFileName")]
        public string MapPictureFileName { get; set; }


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

        private bool isDownloaded;
        [JsonIgnore]
        public bool IsDownloaded { 
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
        public Map()
        {
            this.ID = string.Empty;
            this.FileName = "Unknown";
            this.Author = "Unknown";
            this.Type = "Unknown";
            this.Version = "Unknown";
            this.LastUpdated = "Unknown";
            this.DownloadURL = string.Empty;
            this.PlayerCount = "Unknown";
            this.MapTeamStyle = "Unknown";
            this.Size = "Unknown";
            this.MapName = "Unknown";
            this.Description = "No description available.";
            this.MapPictureFileName = string.Empty;
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

            this.OnPropertyChanged(nameof(ID));
        }


        /// <summary>
        ///     This is for when users manually import a map
        /// </summary>
        /// <param name="fileName"></param>
        public Map(string fileName)
        {
            this.ID = fileName;
            this.FileName = fileName;
            this.Author = "Unknown";
            this.Type = "Unknown";
            this.Version = "Unknown";
            this.LastUpdated = "Unknown";
            this.DownloadURL = string.Empty;
            this.PlayerCount = "Unknown";
            this.MapTeamStyle = "Unknown";
            this.Size = "Unknown";
            this.MapName = "Unknown";
            this.Description = "No description available.";
            this.MapPictureFileName = string.Empty;
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

            this.OnPropertyChanged(nameof(ID));
        }



        public List<Map> BuildMapState(
                                        List<Map> availableMaps,
                                        List<string> downloadedMaps,
                                        List<string> enabledMaps)
        {
            foreach (var map in availableMaps)
            {
                map.IsDownloaded = downloadedMaps.Contains(map.FileName);
                map.IsEnabled = enabledMaps.Contains(map.FileName);
            }

            return availableMaps;
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

