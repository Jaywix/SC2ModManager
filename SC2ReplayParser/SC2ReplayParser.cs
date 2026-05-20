
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReplayParser.SC2
{
    public class SC2ReplayParser
    {
        private const int MAX_RECURSION_DEPTH = 100;
        
        public static SC2ReplayData Parse(string filePath)
        {
            var data = new SC2ReplayData();
            var bytes = File.ReadAllBytes(filePath);
            data.RawData = bytes;
            
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            
            ParseHeader(reader, data);
            
            return data;
        }

        private static void ParseHeader(BinaryReader reader, SC2ReplayData data)
        {
            data.Version = ReadNullTerminatedString(reader);
            reader.ReadBytes(3);
            
            var replayInfo = ReadNullTerminatedString(reader);
            var parts = replayInfo.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                data.ReplayVersion = parts[0];
                data.MapName = parts[1];
            }
            
            reader.ReadBytes(4);
            
            var modsSize = reader.ReadUInt32();
            if (modsSize > 0 && modsSize < reader.BaseStream.Length - reader.BaseStream.Position)
            {
                var modsData = ReadLuaDataSafe(reader, 0);
                data.Mods = ParseMods(modsData);
            }
            
            var scenarioSize = reader.ReadUInt32();
            if (scenarioSize > 0 && scenarioSize < reader.BaseStream.Length - reader.BaseStream.Position)
            {
                var scenarioData = ReadLuaDataSafe(reader, 0);
                ParseScenario(scenarioData, data);
            }
            
            var playersCount = reader.ReadByte();
            

            var humanPlayerNames = new Dictionary<int, string>();
            for (int i = 0; i < playersCount && reader.BaseStream.Position < reader.BaseStream.Length; i++)
            {
                var playerName = ReadNullTerminatedString(reader);
                var playerId = reader.ReadUInt32();
                humanPlayerNames[i] = playerName;
            }
            
            if (reader.BaseStream.Position < reader.BaseStream.Length)
                data.GameOptions["CheatsEnabled"] = reader.ReadByte() != 0;
            
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var armiesCount = reader.ReadByte();
                
                for (int armyIdx = 0; armyIdx < armiesCount && reader.BaseStream.Position < reader.BaseStream.Length; armyIdx++)
                {
                    var armyDataSize = reader.ReadUInt32();
                    var armyData = ReadLuaDataSafe(reader, 0);
                    var armySource = reader.ReadByte();
                    
                    var player = new SC2Player
                    {
                        ArmyIndex = armySource,
                        IsHuman = false 
                    };
                    
                    
                    if (armyData is Dictionary<object, object> dict)
                    {
                        ParsePlayerInfo(dict, player);
                    }
                    
                   
                    if (humanPlayerNames.ContainsKey(armySource))
                    {
                        player.Name = humanPlayerNames[armySource];
                        player.IsHuman = true;
                    }
                    
                    data.Players.Add(player);
                    
                    if (armySource != 255 && reader.BaseStream.Position < reader.BaseStream.Length)
                        reader.ReadByte();
                }
            }
            

            data.Players.Sort((a, b) => a.ArmyIndex.CompareTo(b.ArmyIndex));
            
            if (reader.BaseStream.Position < reader.BaseStream.Length)
                data.GameOptions["RandomSeed"] = reader.ReadUInt32();
        }

        private static void ParsePlayerInfo(Dictionary<object, object> playerData, SC2Player player)
        {
            if (playerData.ContainsKey("PlayerName"))
            {
                string name = playerData["PlayerName"]?.ToString();
                if (!string.IsNullOrEmpty(name) && name != "Unnamed")
                    player.Name = name;
            }
            
            if (playerData.ContainsKey("PlayerColor") && playerData["PlayerColor"] is float pc)
                player.PlayerColor = pc;
            if (playerData.ContainsKey("ArmyColor") && playerData["ArmyColor"] is float ac)
                player.ArmyColor = ac;
            if (playerData.ContainsKey("Faction") && playerData["Faction"] is float f)
                player.Faction = f;
            if (playerData.ContainsKey("Team") && playerData["Team"] is float t)
                player.Team = (int)t;
            if (playerData.ContainsKey("ArmyName"))
                player.ArmyName = playerData["ArmyName"]?.ToString();
            

            if (playerData.ContainsKey("Human"))
            {
                if (playerData["Human"] is bool humanBool)
                    player.IsHuman = humanBool;
                else if (playerData["Human"] is byte humanByte)
                    player.IsHuman = humanByte != 0;
                else if (playerData["Human"] is double humanDouble)
                    player.IsHuman = humanDouble != 0;
                else if (playerData["Human"] is float humanFloat)
                    player.IsHuman = humanFloat != 0;
            }
            
            if (playerData.ContainsKey("AIPersonality") && playerData["AIPersonality"] is string ai)
            {
                player.AIPersonality = ai;
                if (!string.IsNullOrEmpty(ai) && ai != "Human")
                {
                    player.IsHuman = false;
                }
            }
            

            if (playerData.ContainsKey("Civilian") && playerData["Civilian"] is bool civilian)
            {
                if (civilian)
                    player.IsHuman = false;
            }
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            try
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    b = reader.ReadByte();
                    if (b == 0) break;
                    bytes.Add(b);
                }
            }
            catch
            {
                return "";
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static object ReadLuaDataSafe(BinaryReader reader, int depth)
        {
            if (depth > MAX_RECURSION_DEPTH || reader.BaseStream.Position >= reader.BaseStream.Length)
                return null;
                
            try
            {
                var type = reader.ReadByte();
                return ReadLuaValueSafe(reader, type, depth);
            }
            catch
            {
                return null;
            }
        }

        private static object ReadLuaValueSafe(BinaryReader reader, byte type, int depth)
        {
            if (depth > MAX_RECURSION_DEPTH || reader.BaseStream.Position >= reader.BaseStream.Length)
                return null;
                
            try
            {
                switch (type)
                {
                    case DataType.NUMBER:
                        if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
                            return reader.ReadSingle();
                        return null;
                    case DataType.STRING:
                        return ReadNullTerminatedString(reader);
                    case DataType.NIL:
                        return null;
                    case DataType.BOOL:
                        if (reader.BaseStream.Position + 1 <= reader.BaseStream.Length)
                            return reader.ReadByte() != 0;
                        return null;
                    case DataType.TABLE:
                        return ReadLuaTableSafe(reader, depth + 1);
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<object, object> ReadLuaTableSafe(BinaryReader reader, int depth)
        {
            if (depth > MAX_RECURSION_DEPTH || reader.BaseStream.Position >= reader.BaseStream.Length)
                return new Dictionary<object, object>();
                
            var table = new Dictionary<object, object>();
            int maxItems = 10000;
            int itemsCount = 0;
            
            try
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length && itemsCount < maxItems)
                {
                    byte keyType;
                    try
                    {
                        keyType = reader.ReadByte();
                    }
                    catch
                    {
                        break;
                    }
                    
                    if (keyType == DataType.END)
                        break;
                    
                    var key = ReadLuaValueSafe(reader, keyType, depth + 1);
                    if (key == null) 
                    {
                        if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
                            reader.ReadBytes(4);
                        continue;
                    }
                    
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        break;
                        
                    byte valueType;
                    try
                    {
                        valueType = reader.ReadByte();
                    }
                    catch
                    {
                        break;
                    }
                    
                    var value = ReadLuaValueSafe(reader, valueType, depth + 1);
                    
                    try
                    {
                        table[key] = value;
                        itemsCount++;
                    }
                    catch
                    {

                    }
                }
            }
            catch
            {

            }
            
            return table;
        }

        private static List<string> ParseMods(object modsData)
        {
            var mods = new List<string>();
            if (modsData is Dictionary<object, object> dict)
            {
                foreach (var kvp in dict)
                {
                    mods.Add(kvp.Key?.ToString() ?? "unknown");
                }
            }
            return mods;
        }

        private static void ParseScenario(object scenarioData, SC2ReplayData data)
        {
            if (scenarioData is Dictionary<object, object> dict)
            {
                foreach (var kvp in dict)
                {
                    var key = kvp.Key?.ToString() ?? "unknown";
                    var value = kvp.Value;
                    data.GameOptions[key] = value;
                    
                    if (value is Dictionary<object, object> nestedDict)
                    {
                        foreach (var nested in nestedDict)
                        {
                            data.GameOptions[$"{key}.{nested.Key}"] = nested.Value;
                        }
                    }
                }
            }
        }
    }
}