using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class Map : ModBase
    {
        public string Author { get; set; }
        public int PlayerCount { get; set; }
        public int Size { get; set; }
        public string MapName { get; set; }
        public string Description { get; set; }



        public Map() : base("", "", false)
        {
            this.Author = "";
            this.PlayerCount = 0;
            this.Size = 0;
            this.MapName = "";
            this.Description = "";
        }
        public Map(string author, int playerCount, int size, string mapName, string description) : base(mapName, "", false)
        {
            this.Author = author;
            this.PlayerCount = playerCount;
            this.Size = size;
            this.MapName = mapName;
            this.Description = description;
        }
    }
}

