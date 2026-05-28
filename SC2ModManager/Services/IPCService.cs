using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SC2ModManager.Models;

namespace SC2ModManager.Services
{
    public class IPCService
    {
        private const string PipeName = "SC2FURRY";
        private const int ConnectTimeoutMs = 4000;
        private const int ReadTimeoutMs = 15000;

        public bool IsPipeAvailable()
        {
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                pipe.Connect(500);
                return pipe.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> WaitForPipeAsync(TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (IsPipeAvailable())
                    return true;
                await Task.Delay(500);
            }
            return false;
        }

        public async Task<List<LobbyInfo>?> GetLobbiesAsync(int maxResults = 100)
        {
            var request = new { cmd = "get_lobbies", maxResults };
            string json = JsonSerializer.Serialize(request);
            string resp = await SendRawCommandAsync(json);

            try
            {
                using var doc = JsonDocument.Parse(resp);
                var root = doc.RootElement;
                if (root.GetProperty("status").GetString() == "ok" && root.TryGetProperty("lobbies", out var lobbies))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<List<LobbyInfo>>(lobbies.GetRawText(), options)
                           ?? new List<LobbyInfo>();
                }
            }
            catch { }

            return null;
        }

        public async Task<bool> SetTagsAsync(List<TagEntry> tags)
        {
            var request = new { cmd = "set_tags", tags };
            string json = JsonSerializer.Serialize(request);
            string resp = await SendRawCommandAsync(json);
            return resp.Contains("\"status\":\"ok\"");
        }

        /// <summary>
        ///     Подключение только по ID. Пароль сюда не передаётся — игра его игнорирует.
        /// </summary>
        public async Task<bool> ConnectToLobbyAsync(ulong lobbyId)
        {
            var request = new { cmd = "connect_lobby", lobbyId = lobbyId.ToString() };
            string json = JsonSerializer.Serialize(request);
            string resp = await SendRawCommandAsync(json);
            return resp.Contains("\"status\":\"ok\"");
        }

        public async Task<string> SendRawCommandAsync(string jsonCommand)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous,
                    System.Security.Principal.TokenImpersonationLevel.None);

                await pipe.ConnectAsync(ConnectTimeoutMs);
                pipe.ReadMode = PipeTransmissionMode.Message;

                byte[] sendBytes = Encoding.UTF8.GetBytes(jsonCommand);
                await pipe.WriteAsync(sendBytes, 0, sendBytes.Length);
                await pipe.FlushAsync();

                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 8192, true);
                using var cts = new CancellationTokenSource(ReadTimeoutMs);
                string? response = await reader.ReadLineAsync(cts.Token);
                return response ?? "{\"status\":\"error\",\"message\":\"null response\"}";
            }
            catch (OperationCanceledException)
            {
                return "{\"status\":\"error\",\"message\":\"read timeout\"}";
            }
            catch (TimeoutException)
            {
                return "{\"status\":\"error\",\"message\":\"connect timeout\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"status\":\"error\",\"message\":\"{ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}";
            }
        }
    }

    public class TagEntry
    {
        public string key { get; set; } = "";
        public string value { get; set; } = "";
    }
}
