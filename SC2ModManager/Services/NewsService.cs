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
    public class NewsService
    {
        private readonly HttpClient httpClient = new HttpClient();

        public NewsService()
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.0");
        }

        public async Task<List<NewsItem>> GetNewsAsync()
        {
            try
            {
                string json = await httpClient.GetStringAsync(Globals.NewsUrl);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<NewsItem>>(json, options) ?? new List<NewsItem>();

                // Resolve image filenames to full URLs
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.ImageUrl) && !item.ImageUrl.StartsWith("http"))
                        item.ImageUrl = Globals.NewsImagesBaseUrl + item.ImageUrl;
                }

                // Pinned items first, then by date descending, take top 5
                return items
                    .OrderByDescending(n => n.Pinned)
                    .ThenByDescending(n => n.Date)
                    .Take(5)
                    .ToList();
            }
            catch
            {
                return new List<NewsItem>();
            }
        }
    }
}
