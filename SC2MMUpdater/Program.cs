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
                        }
                    }
                }



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

                // Copy files to install directory
                foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourcePath, file);
                    string destinationPath = Path.Combine(installPath, relativePath);

                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

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