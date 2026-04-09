/*
 * SC2 Mod Manager Updater
 * The updater for the SC2 Mod Manager
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace SC2MMUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("SC2ModManager"))
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        process.WaitForExit(3000);
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                    }
                }

                Thread.Sleep(2000); // let the OS release file locks



                // Validate arguments
                if (args.Length < 3)
                {
                    File.WriteAllText("update_error.txt", $"Invalid arguments: {args.Length}");
                    return;
                }

                string zipPath = args[0];
                string installPath = args[1];
                string exeName = args[2];

                // Validate ZIP exists
                if (!File.Exists(zipPath))
                {
                    File.WriteAllText("update_error.txt", $"Zip not found: {zipPath}");
                    return;
                }

                // Create temp extraction folder
                string extractPath = Path.Combine(Path.GetTempPath(), "SC2_update_extract");

                // Clean old extract if exists
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                Directory.CreateDirectory(extractPath);

                // Extract ZIP
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Detect if "publish" folder exists
                string sourcePath = extractPath;
                string publishPath = Path.Combine(extractPath, "publish");

                if (Directory.Exists(publishPath))
                {
                    sourcePath = publishPath;
                }

                var skipFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "SC2MMUpdater.exe",
                    "SC2MMUpdater.dll",
                    "SC2MMUpdater.pdb",
                    "SC2MMUpdater.runtimeconfig.json",
                    "SC2MMUpdater.deps.json"
                };

                foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourcePath, file);

                    if (skipFiles.Contains(Path.GetFileName(relativePath)))
                        continue;

                    string destinationPath = Path.Combine(installPath, relativePath);
                    string destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Copy(file, destinationPath, true);
                }

                // Clean up temp files
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                // Restart the main app
                string exePath = Path.Combine(installPath, exeName);

                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("update_error.txt", ex.ToString());
            }
        }
    }
}