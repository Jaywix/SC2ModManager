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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public static class MapTagConstants
    {
        public const string TwoPlayer = "2 Players";
        public const string ThreePlayer = "3 Players";
        public const string FourPlayer = "4 Players";
        public const string FivePlayer = "5 Players";
        public const string SixPlayer = "6 Players";
        public const string SevenPlayer = "7 Players";
        public const string EightPlayer = "8 Players";

        public const string Team = "Team";
        public const string FFA = "FFA";

        public const string MapPack = "Map Pack";

        public const string Water = "Water";

        public const string Large = "Large";
        public const string Medium = "Medium";
        public const string Small = "Small";
        public static readonly List<string> AllTags = new List<string>
        {
            TwoPlayer, ThreePlayer, FourPlayer, FivePlayer, SixPlayer, SevenPlayer, EightPlayer,
            Team, FFA,
            MapPack,
            Water,
            Large, Medium, Small
        };
    }


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

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }



        [JsonIgnore]
        public string MapPictureUrl =>
        string.IsNullOrEmpty(MapPictureFileName)
            ? null
            : Globals.MapImagesBaseUrl + MapPictureFileName;

        /// <summary>
        ///     MD5 hash of the mod file. Used to match against lobby tags (mod_<hash>).
        /// </summary>
        [JsonPropertyName("modHash")]
        public string? ModHash { get; set; }

        /// <summary>
        ///     Returns path to this mod file (Enabled > Disabled).
        /// </summary>
        [JsonIgnore]
        public string? ModFilePath
        {
            get
            {
                string appData = Globals.GetDataPath();
                string root = Path.Combine(appData, "Mods", "Maps");
                string enabled = Path.Combine(root, "Enabled", FileName);
                if (File.Exists(enabled)) return enabled;
                string disabled = Path.Combine(root, "Disabled", FileName);
                if (File.Exists(disabled)) return disabled;
                return null;
            }
        }

        [JsonIgnore]
        public string ComputedHash
        {
            get
            {
                if (!string.IsNullOrEmpty(ModHash))
                    return ModHash;
                var path = ModFilePath;
                if (path != null && File.Exists(path))
                    return ComputeFileHash(path);
                return "";
            }
        }

        public static string ComputeFileHash(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
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

        private bool isQueued;
        [JsonIgnore]
        public bool IsQueued
        {
            get => isQueued;
            set
            {
                if (isQueued != value)
                {
                    isQueued = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isDownloading;
        [JsonIgnore]
        public bool IsDownloading
        {
            get => isDownloading;
            set
            {
                if (isDownloading != value)
                {
                    isDownloading = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool downloadFailed;
        [JsonIgnore]
        public bool DownloadFailed
        {
            get => downloadFailed;
            set
            {
                if (downloadFailed != value)
                {
                    downloadFailed = value;
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
            this.Tags = new List<string>();
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
            this.Tags = new List<string>();
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

