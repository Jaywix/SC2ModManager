
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ReplayParser.SC2
{
    public static class ReplayDumper
    {
        public static void DumpToConsole(SC2ReplayData replay)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    SUPREME COMMANDER 2 REPLAY DUMP               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            DumpSection("BASIC INFORMATION");
            Console.WriteLine($"  File Size:        {replay.RawData.Length:N0} bytes");
            Console.WriteLine($"  Version:          {replay.Version}");
            Console.WriteLine($"  Replay Version:   {replay.ReplayVersion}");
            Console.WriteLine($"  Map Name:         {replay.MapName}");
            Console.WriteLine();
            
            DumpSection("PLAYERS");
            for (int i = 0; i < replay.Players.Count; i++)
            {
                var player = replay.Players[i];
                Console.WriteLine($"  Player {i + 1}:");
                Console.WriteLine($"    Name:           {player.Name}");
                Console.WriteLine($"    ID:             {player.Id}");
                Console.WriteLine($"    Army Index:     {player.ArmyIndex}");
                Console.WriteLine($"    Team:           {player.Team}");
                Console.WriteLine($"    Color:          {GetColorName((int)player.PlayerColor)}");
                Console.WriteLine($"    Faction:        {GetFactionName(player.Faction)}");
                if (!string.IsNullOrEmpty(player.CustomName))
                    Console.WriteLine($"    Custom Name:    {player.CustomName}");
                if (!string.IsNullOrEmpty(player.ArmyName))
                    Console.WriteLine($"    Army Name:      {player.ArmyName}");
                Console.WriteLine($"    Is Human:       {player.IsHuman}");
                if (!string.IsNullOrEmpty(player.AIPersonality))
                    Console.WriteLine($"    AI Personality: {player.AIPersonality}");
                if (player.PlayerColor > 0)
                    Console.WriteLine($"    Color Value:    {player.PlayerColor}");
                if (player.ArmyColor > 0)
                    Console.WriteLine($"    Army Color:     {player.ArmyColor}");
            }
            Console.WriteLine();
            
            if (replay.Mods.Count > 0)
            {
                DumpSection("MODS");
                foreach (var mod in replay.Mods)
                {
                    Console.WriteLine($"  {mod}");
                }
                Console.WriteLine();
            }
            
            DumpSection("GAME OPTIONS");
            DumpDictionaryDetailed(replay.GameOptions, "  ");
            Console.WriteLine();
        }
        
        public static void DumpToFile(SC2ReplayData replay, string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            
            writer.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            writer.WriteLine("║                    SUPREME COMMANDER 2 REPLAY DUMP               ║");
            writer.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            writer.WriteLine();
            
            writer.WriteLine("=== BASIC INFORMATION ===");
            writer.WriteLine($"File Size: {replay.RawData.Length:N0} bytes");
            writer.WriteLine($"Version: {replay.Version}");
            writer.WriteLine($"Replay Version: {replay.ReplayVersion}");
            writer.WriteLine($"Map Name: {replay.MapName}");
            writer.WriteLine();
            
            writer.WriteLine("=== PLAYERS ===");
            writer.WriteLine($"Total Players: {replay.Players.Count}");
            writer.WriteLine();
            
            for (int i = 0; i < replay.Players.Count; i++)
            {
                var player = replay.Players[i];
                writer.WriteLine($"┌─────────────────────────────────────────────────────────────────┐");
                writer.WriteLine($"│ Player {i + 1}                                                          │");
                writer.WriteLine($"├─────────────────────────────────────────────────────────────────┤");
                writer.WriteLine($"│ Name:           {player.Name,-46} │");
                writer.WriteLine($"│ ID:             {player.Id,-46} │");
                writer.WriteLine($"│ Army Index:     {player.ArmyIndex,-46} │");
                writer.WriteLine($"│ Team:           {player.Team,-46} │");
                writer.WriteLine($"│ Color:          {GetColorName((int)player.PlayerColor),-46} │");
                writer.WriteLine($"│ Color Value:    {player.PlayerColor,-46} │");
                writer.WriteLine($"│ Army Color:     {player.ArmyColor,-46} │");
                writer.WriteLine($"│ Faction:        {GetFactionName(player.Faction),-46} │");
                writer.WriteLine($"│ Faction Value:  {player.Faction,-46} │");
                if (!string.IsNullOrEmpty(player.CustomName))
                    writer.WriteLine($"│ Custom Name:    {player.CustomName,-46} │");
                else
                    writer.WriteLine($"│ Custom Name:    (none),-46 │");
                if (!string.IsNullOrEmpty(player.ArmyName))
                    writer.WriteLine($"│ Army Name:      {player.ArmyName,-46} │");
                writer.WriteLine($"│ Is Human:       {player.IsHuman,-46} │");
                if (!string.IsNullOrEmpty(player.AIPersonality))
                    writer.WriteLine($"│ AI Personality: {player.AIPersonality,-46} │");
                writer.WriteLine($"└─────────────────────────────────────────────────────────────────┘");
                writer.WriteLine();
            }
            
            if (replay.Mods.Count > 0)
            {
                writer.WriteLine("=== MODS ===");
                foreach (var mod in replay.Mods)
                {
                    writer.WriteLine($"  {mod}");
                }
                writer.WriteLine();
            }
            
            writer.WriteLine("=== GAME OPTIONS ===");
            DumpDictionaryDetailedToWriter(replay.GameOptions, "  ", writer);
            writer.WriteLine();
            
            if (replay.ScenarioData.Count > 0)
            {
                writer.WriteLine("=== SCENARIO DATA ===");
                DumpDictionaryDetailedToWriter(replay.ScenarioData, "  ", writer);
                writer.WriteLine();
            }
            
            writer.WriteLine("=== END OF DUMP ===");
        }
        
        private static string GetColorName(int colorIndex)
        {
            var colors = new Dictionary<int, string>
            {
                { 0, "Blue" },
                { 1, "Green" },
                { 2, "Red" },
                { 3, "Purple" },
                { 4, "Tan" },
                { 5, "Grey" },
                { 6, "Olive" },
                { 7, "Cyan" },
                { 8, "Yellow" },
                { 9, "Orange" }
            };
            
            return colors.ContainsKey(colorIndex) ? colors[colorIndex] : $"Unknown ({colorIndex})";
        }
        
        private static string GetFactionName(float factionValue)
        {
            var factions = new Dictionary<float, string>
            {
                { 1f, "UEF" },
                { 2f, "Cybran" },
                { 3f, "Illuminate" }
            };
            
            return factions.ContainsKey(factionValue) ? factions[factionValue] : $"Unknown ({factionValue})";
        }
        
        private static void DumpSection(string title)
        {
            Console.WriteLine($"\n┌───────────────────────── {title} ─────────────────────────┐");
        }
        
        private static void DumpDictionaryDetailed(Dictionary<string, object> dict, string indent)
        {
            if (dict == null || dict.Count == 0)
            {
                Console.WriteLine($"{indent}(empty)");
                return;
            }
            
            foreach (var kvp in dict.OrderBy(k => k.Key))
            {
                DumpObject(kvp.Key, kvp.Value, indent);
            }
        }
        
        private static void DumpObject(string key, object value, string indent)
        {
            if (value == null)
            {
                Console.WriteLine($"{indent}{key}: null");
                return;
            }
            
            if (value is Dictionary<object, object> dictObj)
            {
                Console.WriteLine($"{indent}{key}:");
                foreach (var subKvp in dictObj.OrderBy(k => k.Key?.ToString()))
                {
                    DumpObject(subKvp.Key?.ToString() ?? "unknown", subKvp.Value, indent + "  ");
                }
                return;
            }
            
            if (value is Dictionary<string, object> dictStr)
            {
                Console.WriteLine($"{indent}{key}:");
                DumpDictionaryDetailed(dictStr, indent + "  ");
                return;
            }
            
            if (value is IEnumerable list && !(value is string) && !(value is byte[]))
            {
                var listItems = list.Cast<object>().ToList();
                Console.WriteLine($"{indent}{key}: [{listItems.Count} items]");
                for (int i = 0; i < Math.Min(20, listItems.Count); i++)
                {
                    Console.WriteLine($"{indent}  [{i}] {listItems[i]}");
                }
                if (listItems.Count > 20)
                    Console.WriteLine($"{indent}  ... and {listItems.Count - 20} more");
                return;
            }
            
            if (value is byte[] bytes)
            {
                Console.WriteLine($"{indent}{key}: [{bytes.Length} bytes] {BitConverter.ToString(bytes, 0, Math.Min(16, bytes.Length))}...");
                return;
            }
            
            var valueStr = value.ToString();
            if (valueStr.Length > 200)
                valueStr = valueStr.Substring(0, 200) + "...";
            Console.WriteLine($"{indent}{key}: {valueStr}");
        }
        
        private static void DumpDictionaryDetailedToWriter(Dictionary<string, object> dict, string indent, StreamWriter writer)
        {
            if (dict == null || dict.Count == 0)
            {
                writer.WriteLine($"{indent}(empty)");
                return;
            }
            
            foreach (var kvp in dict.OrderBy(k => k.Key))
            {
                DumpObjectToWriter(kvp.Key, kvp.Value, indent, writer);
            }
        }
        
        private static void DumpObjectToWriter(string key, object value, string indent, StreamWriter writer)
        {
            if (value == null)
            {
                writer.WriteLine($"{indent}{key}: null");
                return;
            }
            
            if (value is Dictionary<object, object> dictObj)
            {
                writer.WriteLine($"{indent}{key}:");
                foreach (var subKvp in dictObj.OrderBy(k => k.Key?.ToString()))
                {
                    DumpObjectToWriter(subKvp.Key?.ToString() ?? "unknown", subKvp.Value, indent + "  ", writer);
                }
                return;
            }
            
            if (value is Dictionary<string, object> dictStr)
            {
                writer.WriteLine($"{indent}{key}:");
                DumpDictionaryDetailedToWriter(dictStr, indent + "  ", writer);
                return;
            }
            
            if (value is IEnumerable list && !(value is string) && !(value is byte[]))
            {
                var listItems = list.Cast<object>().ToList();
                writer.WriteLine($"{indent}{key}: [{listItems.Count} items]");
                for (int i = 0; i < Math.Min(20, listItems.Count); i++)
                {
                    writer.WriteLine($"{indent}  [{i}] {listItems[i]}");
                }
                if (listItems.Count > 20)
                    writer.WriteLine($"{indent}  ... and {listItems.Count - 20} more");
                return;
            }
            
            if (value is byte[] bytes)
            {
                writer.WriteLine($"{indent}{key}: [{bytes.Length} bytes]");
                return;
            }
            
            writer.WriteLine($"{indent}{key}: {value}");
        }
    }
}