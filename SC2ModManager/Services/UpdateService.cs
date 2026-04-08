/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: 2024-01-01
 * Last updated: 2024-06-01
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

            if (assets.GetArrayLength() > 0)
            {
                downloadUrl = assets[0].GetProperty("browser_download_url").GetString();
            }

            return (new Version(tag.TrimStart('v')), downloadUrl);
        }
    }
}
