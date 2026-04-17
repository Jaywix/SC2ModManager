using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class NewsItem
    {
        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } // "tournament", "update", "discord", "general"

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("linkText")]
        public string LinkText { get; set; }

        [JsonPropertyName("linkUrl")]
        public string LinkUrl { get; set; }

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; }
    }
}
