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
