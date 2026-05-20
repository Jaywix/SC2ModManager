
using System;
using System.IO;
using ReplayParser.SC2;

namespace ReplayParser.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Supreme Commander 2 Replay Dumper ===");
            Console.WriteLine();

            Console.Write("Enter path to replay file (.SC2Replay or .SC2ReplayDLC): ");
            string replayPath = Console.ReadLine()?.Trim().Trim('"');

            if (string.IsNullOrEmpty(replayPath))
            {
                Console.WriteLine("No path entered.");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            // Checking file extension
            string extension = Path.GetExtension(replayPath).ToLower();
            if (extension != ".sc2replay" && extension != ".sc2replaydlc")
            {
                Console.WriteLine($"Warning: File extension '{extension}' is not a typical SC2 replay extension.");
                Console.WriteLine("Expected: .SC2Replay or .SC2ReplayDLC");
                Console.Write("Continue anyway? (y/n): ");
                var response = Console.ReadLine()?.Trim().ToLower();
                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Exiting...");
                    Console.ReadKey();
                    return;
                }
            }

            if (!File.Exists(replayPath))
            {
                Console.WriteLine($"File not found: {replayPath}");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            try
            {
                Console.WriteLine("\nParsing replay...");
                var replay = SC2ReplayParser.Parse(replayPath);

                ReplayDumper.DumpToConsole(replay);

                var outputPath = Path.ChangeExtension(replayPath, ".dump.txt");
                ReplayDumper.DumpToFile(replay, outputPath);

                Console.WriteLine($"\n✓ Dump saved to: {outputPath}");
                Console.WriteLine($"\n✓ Parsing complete!");
            }
            catch (EndOfStreamException ex)
            {
                Console.WriteLine($"\n✗ Error: File appears to be corrupted or not a valid SC2 replay file.");
                Console.WriteLine($"  Details: {ex.Message}");
                Console.WriteLine("\n  Make sure you are opening a .SC2Replay or .SC2ReplayDLC file");
                Console.WriteLine("  and not a source code file or other text file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}