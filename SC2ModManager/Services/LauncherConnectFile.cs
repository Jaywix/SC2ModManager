using SC2ModManager.Models;
using System;
using System.IO;
using System.Text.Json;

namespace SC2ModManager.Services
{
    public static class LauncherConnectFile
    {
        public const string FileName = "sc2_launcher_connect.json";

        public static string GetPath()
            => Path.Combine(Path.GetTempPath(), FileName);

        public static void Write(LauncherConnectPayload payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetPath(), json);
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(GetPath()))
                    File.Delete(GetPath());
            }
            catch { }
        }

        public static LauncherConnectPayload? Read()
        {
            try
            {
                if (!File.Exists(GetPath()))
                    return null;
                string json = File.ReadAllText(GetPath());
                return JsonSerializer.Deserialize<LauncherConnectPayload>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
