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

                // Only show versions >= 1.6.0 and not the currently running version. I think 1.6.0 is a good cutoff because that's where I changed the updater
                if (releaseVersion < minimumVersion) 
                    continue;
                if (currentVersion != null && releaseVersion == currentVersion) 
                    continue;

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
                    DownloadUrl = downloadUrl
                });
            }

            // Sort newest first
            results.Sort((a, b) => b.Version.CompareTo(a.Version));
            return results;
        }
    }
}
