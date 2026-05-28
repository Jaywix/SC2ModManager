using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SC2ModManager.Models
{
    /// <summary>
    ///     Represents a lobby returned from the IPC get_lobbies command.
    /// </summary>
    public class LobbyInfo
    {
        [JsonPropertyName("steamId")]
        public string SteamId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; } = "";

        [JsonPropertyName("memberCount")]
        public int MemberCount { get; set; }

        [JsonPropertyName("maxMembers")]
        public int MaxMembers { get; set; }

        [JsonPropertyName("lobbyReady")]
        public string LobbyReady { get; set; } = "";

        [JsonPropertyName("dataCount")]
        public int DataCount { get; set; }

        [JsonPropertyName("data")]
        public Dictionary<string, string> Data { get; set; } = new();

        /// <summary>
        ///     Returns all mod tags (keys starting with "mod_") from this lobby's data.
        /// </summary>
        [JsonIgnore]
        public List<LobbyModEntry> Mods
        {
            get
            {
                var mods = new List<LobbyModEntry>();
                foreach (var kv in Data)
                {
                    if (kv.Key.StartsWith("mod_") & kv.Key != "mod_has_mods")
                    {
                        mods.Add(new LobbyModEntry
                        {
                            Hash = kv.Key.Substring(4),
                            IsEnabled = kv.Value == "1"
                        });
                    }
                }
                return mods;
            }
        }

        /// <summary>
        ///     Checks if this lobby is from our launcher (BuildVersion doesn't contain "Supreme").
        /// </summary>
        [JsonIgnore]
        public bool IsOurLauncher
        {
            get
            {
                if (Data.TryGetValue("LauncherId", out var lid) &&
                    lid.Equals("SC2ModManager", System.StringComparison.OrdinalIgnoreCase))
                    return true;

                if (Data.TryGetValue("BuildVersion", out var bv))
                    return !bv.Contains("Supreme", System.StringComparison.OrdinalIgnoreCase);

                return false;
            }
        }

        [JsonIgnore]
        public bool IsDlcEnabled
        {
            get
            {
                if (Data.TryGetValue("DLC1Enabled", out var lid) &&
                    lid.Equals("1", System.StringComparison.OrdinalIgnoreCase))
                    return true;

                if (Data.TryGetValue("DLC1Enabled", out var bv))
                    return !bv.Contains("0", System.StringComparison.OrdinalIgnoreCase);

                return false;
            }
        }

        /// <summary>
        ///     Short status based on tags to show in lobby list.
        /// </summary>
        [JsonIgnore]
        public string LobbyTagStatus => IsDlcEnabled ? "DLC Enabled" : "DLC Disabled";

        /// <summary>Mods required in lobby (mod_* with value 1).</summary>
        [JsonIgnore]
        public List<LobbyModEntry> RequiredMods =>
            Mods.Where(m => m.IsEnabled).ToList();

        [JsonIgnore]
        public bool HasPassword
        {
            get
            {
                if (Data.TryGetValue("HasPassword", out var hp))
                    return hp == "1";
                return false;
            }
        }

        [JsonIgnore]
        public bool IsPrivateGame
        {
            get
            {
                if (Data.TryGetValue("PrivateGame", out var pg))
                    return pg == "1";
                return false;
            }
        }

        [JsonIgnore]
        public string LobbyPassword
        {
            get
            {
                if (Data.TryGetValue("Password", out var p))
                    return p ?? "";
                return "";
            }
        }
    }

    /// <summary>
    ///     A single mod entry parsed from lobby tags (mod_<hash> = 1/0).
    /// </summary>
    public class LobbyModEntry
    {
        public string Hash { get; set; } = "";
        public bool IsEnabled { get; set; }
    }
}