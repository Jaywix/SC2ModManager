/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: May 20, 2026
 * Author: Jacob Wixom
 * 
*/
using System.Collections.Generic;
using System.Linq;

namespace SC2ModManager.Models
{

    public class ReplayMetadata
    {
        public string MapDisplayName { get; set; }    // e.g., "[4] Way Station Zeta (2v2)"     This is the one we want if possible, because the other one doesn't mean anything, but I'm saving the other one just in case of needs in the future
        public string MapRawPath { get; set; }        // e.g., "/maps/SC2_D1_001/SC2_D1_001.scmap"
        public string GameVersion { get; set; }
        public string ReplayVersion { get; set; }

        public List<ReplayPlayerInfo> Players { get; set; } = new();

        public bool HasAI => Players.Any(p => !p.IsHuman);
        public int HumanPlayerCount => Players.Count(p => p.IsHuman);
        public int TotalPlayerCount => Players.Count;

        // Game options
        public string VictoryCondition { get; set; }
        public int UnitCap { get; set; }
        public string FogOfWar { get; set; }
        public int InitialMass { get; set; }
        public int InitialEnergy { get; set; }
        public int InitialResearch { get; set; }
        public bool Ranked { get; set; }
        public bool CheatsEnabled { get; set; }
        public string TeamSpawn { get; set; }

        public List<string> Exclusions { get; set; } = new();
        public bool HasExclusions => Exclusions.Count > 0;

        public bool ParseFailed { get; set; }
        public string ParseError { get; set; }
    }

    public class ReplayPlayerInfo
    {
        public string Name { get; set; }
        public string Faction { get; set; }
        public string Color { get; set; }
        public int Team { get; set; }
        public bool IsHuman { get; set; }
        public string AIPersonality { get; set; }
    }
}
