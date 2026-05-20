using System.Collections.Generic;

namespace ReplayParser.SC2
{
    public class SC2ReplayData
    {
        public string Version { get; set; }
        public string ReplayVersion { get; set; }
        public string MapName { get; set; }
        public List<SC2Player> Players { get; set; } = new();
        public List<string> Mods { get; set; } = new();
        public Dictionary<string, object> GameOptions { get; set; } = new();
        public Dictionary<string, object> ScenarioData { get; set; } = new();
        public Dictionary<string, object> ExtraData { get; set; } = new();
        public byte[] RawData { get; set; }
    }

    public class SC2Player
    {
        public string Name { get; set; }
        public uint Id { get; set; }
        public int ArmyIndex { get; set; }
        public int Team { get; set; }
        public float PlayerColor { get; set; }
        public float ArmyColor { get; set; }
        public float Faction { get; set; }
        public string ArmyName { get; set; }
        public string CustomName { get; set; }
        public bool IsHuman { get; set; }
        public string AIPersonality { get; set; }
        public Dictionary<string, object> ArmyConfig { get; set; } = new();
    }
}