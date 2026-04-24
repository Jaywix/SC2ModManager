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
                try
                {
                    foreach (var process in Process.GetProcessesByName("SC2ModManager"))
                    {
                        try
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
                        catch
                        {
                            // Process was already exited or inaccessible, skip it
                        }
                    }
                }
                catch
                {
                    // Could not enumerate processes, SC2ModManager is likely already closed
                }

                Thread.Sleep(3000); // let the OS release file locks

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

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                Directory.CreateDirectory(extractPath);

                // Extract ZIP
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Detect if publish folder exists
                string sourcePath = extractPath;
                string publishPath = Path.Combine(extractPath, "publish");

                if (Directory.Exists(publishPath))
                    sourcePath = publishPath;

                // ================= DELETE MANIFEST =================

                string manifestPath = Path.Combine(sourcePath, "delete_manifest.txt");
                if (File.Exists(manifestPath))
                {
                    foreach (string line in File.ReadAllLines(manifestPath))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        string targetFile = Path.Combine(installPath, trimmed);
                        if (File.Exists(targetFile))
                            File.Delete(targetFile);
                    }
                }

                // ================= COPY FILES =================

                var skipFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "SC2MMUpdater.exe",
                    "SC2MMUpdater.dll",
                    "SC2MMUpdater.pdb",
                    "SC2MMUpdater.runtimeconfig.json",
                    "SC2MMUpdater.deps.json",
                    "delete_manifest.txt"
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

                // ================= SELF-UPDATE =================

                string newUpdaterPath = Path.Combine(sourcePath, "SC2MMUpdater.exe");
                string currentUpdaterPath = Path.Combine(installPath, "SC2MMUpdater.exe");

                if (File.Exists(newUpdaterPath))
                {
                    string tempUpdater = Path.Combine(installPath, "SC2MMUpdater_new.exe");
                    File.Copy(newUpdaterPath, tempUpdater, true);

                    string batchPath = Path.Combine(Path.GetTempPath(), "sc2mm_updater_swap.bat");
                    string batch = $@"@echo off
                        timeout /t 2 /nobreak >nul
                        move /y ""{tempUpdater}"" ""{currentUpdaterPath}""
                        del ""%~f0""
                        ";
                    File.WriteAllText(batchPath, batch);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{batchPath}\"",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }

                // ================= CLEANUP =================

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                // ================= RESTART =================

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