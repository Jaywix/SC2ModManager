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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    /// <summary>
    ///     This service is extremely important. This will allow the application to check if there is a new version available and prompt the user to download it.
    ///     DO NOT BREAK THIS SERVICE PLEEEEEEASE
    /// </summary>
    public class UpdateService
    {
        /// <summary>
        ///     Finishes an updater self-update that got stuck. The updater can't replace itself while it's running,
        ///     so it copies the new one in as SC2MMUpdater_new.exe and leaves a little .bat to rename it over
        ///     SC2MMUpdater.exe a couple seconds later. If that .bat fails (it used to for anyone with an accent in
        ///     their install path, like C:\Users\José), the new updater just sits there and they keep using the old
        ///     one forever. The app itself still updated fine, it's only the updater that gets stuck.
        ///     This runs when the mod manager starts. If SC2MMUpdater_new.exe is still there, it does the rename
        ///     itself in C#, no .bat, so the path encoding problem can't happen.
        ///     It's careful on purpose since the updater is the one thing that can't break:
        ///     - never touches anything while the updater is running
        ///     - waits a minute after the new file showed up so a normal swap gets to finish first
        ///     - won't replace the working updater with an empty or obviously broken file
        ///     - never throws, if anything goes wrong it just tries again next time the app starts
        /// </summary>
        /// <returns>True if it finished a stuck swap.</returns>
        public static bool RepairStuckUpdaterSwap()
        {
            try
            {
                string installDir = AppContext.BaseDirectory;
                string newUpdater = Path.Combine(installDir, "SC2MMUpdater_new.exe");
                string currentUpdater = Path.Combine(installDir, Globals.UpdaterExecutableName);

                if (!File.Exists(newUpdater))
                    return false;

                // The updater starts the mod manager right after it starts the swap .bat, which waits 2 seconds.
                // Give that normal swap plenty of time before we step in
                var newInfo = new FileInfo(newUpdater);
                if (DateTime.Now - newInfo.CreationTime < TimeSpan.FromMinutes(1))
                    return false;

                // Never swap while any updater is still running
                var running = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Globals.UpdaterExecutableName));
                try
                {
                    if (running.Length > 0)
                        return false;
                }
                finally
                {
                    foreach (var p in running) p.Dispose();
                }

                // A real updater is tens of MB. Something tiny is a broken copy, so don't put it in place of
                // the working one. Throw the broken leftover away so this doesn't keep coming up
                if (newInfo.Length < 1024 * 1024)
                {
                    File.Delete(newUpdater);
                    return false;
                }

                File.Move(newUpdater, currentUpdater, overwrite: true);
                return true;
            }
            catch
            {
                // Locked file, permissions, whatever. Leave everything as it is and try again next launch
                return false;
            }
        }

        public async Task<(Version version, string downloadUrl)> GetLatestRelease()
        {
            using HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager");

            var json = await client.GetStringAsync(Globals.RepoUrl);

            using JsonDocument doc = JsonDocument.Parse(json);

            string tag = doc.RootElement.GetProperty("tag_name").GetString();

            var assets = doc.RootElement.GetProperty("assets");

            string downloadUrl = null;

            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString();
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            return (new Version(tag.TrimStart('v')), downloadUrl);
        }

        public async Task<List<ReleaseInfo>> GetAllReleasesAsync()
        {
            var minimumVersion = new Version(1, 6, 0);
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager");

            var json = await client.GetStringAsync(Globals.ReleasesListUrl);

            using JsonDocument doc = JsonDocument.Parse(json);

            var results = new List<ReleaseInfo>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                // Skip drafts and pre-releases
                if (element.GetProperty("draft").GetBoolean()) 
                    continue;

                string tag = element.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tag)) 
                    continue;

                if (!Version.TryParse(tag.TrimStart('v'), out Version releaseVersion)) 
                    continue;

                // Only show versions >= 1.6.0. I think 1.6.0 is a good cutoff because that's where I changed the updater
                if (releaseVersion < minimumVersion)
                    continue;

                // The running version stays in the list but gets marked so its card says "Currently Installed".
                // Compare major.minor.build only, the exe says 1.6.4.0 but the tag is v1.6.4, so comparing the
                // whole Version never matched and the current version showed up as restorable
                bool isCurrent = currentVersion != null &&
                    releaseVersion.Major == currentVersion.Major &&
                    releaseVersion.Minor == currentVersion.Minor &&
                    Math.Max(releaseVersion.Build, 0) == Math.Max(currentVersion.Build, 0);

                string body = element.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

                string downloadUrl = null;
                foreach (var asset in element.GetProperty("assets").EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString();
                    if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (downloadUrl == null) continue;

                results.Add(new ReleaseInfo
                {
                    Version = releaseVersion,
                    TagName = tag,
                    Body = body,
                    DownloadUrl = downloadUrl,
                    IsCurrentVersion = isCurrent
                });
            }

            // Sort newest first
            results.Sort((a, b) => b.Version.CompareTo(a.Version));
            return results;
        }
    }
}
