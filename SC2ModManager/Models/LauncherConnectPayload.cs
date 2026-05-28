using System.Text.Json.Serialization;

namespace SC2ModManager.Models
{
    /// <summary>
    ///     Written before game launch; read by the IPC DLL for auto-connect.
    /// </summary>
    public class LauncherConnectPayload
    {
        [JsonPropertyName("lobbyId")]
        public string LobbyId { get; set; } = "";

        [JsonPropertyName("autoConnect")]
        public bool AutoConnect { get; set; } = true;
    }
}
