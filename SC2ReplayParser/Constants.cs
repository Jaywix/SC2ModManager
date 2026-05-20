
using System;

namespace ReplayParser.SC2
{
    public static class DataType
    {
        public const byte NUMBER = 0;
        public const byte STRING = 1;
        public const byte NIL = 2;
        public const byte BOOL = 3;
        public const byte TABLE = 4;
        public const byte END = 5;
    }

    public static class TargetType
    {
        public const byte NONE = 0;
        public const byte Entity = 1;
        public const byte Position = 2;
    }

    public static class CommandStates
    {
        public const byte Advance = 0;
        public const byte SetCommandSource = 1;
        public const byte CommandSourceTerminated = 2;
        public const byte VerifyChecksum = 3;
        public const byte RequestPause = 4;
        public const byte Resume = 5;
        public const byte SingleStep = 6;
        public const byte CreateUnit = 7;
        public const byte CreateProp = 8;
        public const byte DestroyEntity = 9;
        public const byte WarpEntity = 10;
        public const byte ProcessInfoPair = 11;
        public const byte IssueCommand = 12;
        public const byte IssueFactoryCommand = 13;
        public const byte IncreaseCommandCount = 14;
        public const byte DecreaseCommandCount = 15;
        public const byte SetCommandTarget = 16;
        public const byte SetCommandType = 17;
        public const byte SetCommandCells = 18;
        public const byte RemoveCommandFromQueue = 19;
        public const byte DebugCommand = 20;
        public const byte ExecuteLuaInSim = 21;
        public const byte LuaSimCallback = 22;
        public const byte EndGame = 23;
    }

    public static class CommandStateNames
    {
        public static readonly string[] Names = 
        {
            "Advance", "SetCommandSource", "CommandSourceTerminated", "VerifyChecksum",
            "RequestPause", "Resume", "SingleStep", "CreateUnit", "CreateProp",
            "DestroyEntity", "WarpEntity", "ProcessInfoPair", "IssueCommand",
            "IssueFactoryCommand", "IncreaseCommandCount", "DecreaseCommandCount",
            "SetCommandTarget", "SetCommandType", "SetCommandCells", "RemoveCommandFromQueue",
            "DebugCommand", "ExecuteLuaInSim", "LuaSimCallback", "EndGame"
        };
    }
}