using SC2ModManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Services
{
    public class InstallService
    {
        /// <summary>
        /// Copies all files from the current running location to the chosen install folder.
        /// Reports progress as 0-100.
        /// </summary>
        public async Task InstallToFolderAsync(string installFolder, IProgress<(int percent, string status)> progress)
        {
            string sourceDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string dataDir = Path.Combine(installFolder, "Data");

            Directory.CreateDirectory(installFolder);
            Directory.CreateDirectory(dataDir);

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            int total = files.Length;
            int copied = 0;

            foreach (string file in files)
            {
                string relative = Path.GetRelativePath(sourceDir, file);
                string dest = Path.Combine(installFolder, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                await Task.Run(() => File.Copy(file, dest, true));

                copied++;
                int percent = (int)((copied / (double)total) * 100);
                progress?.Report((percent, $"Copying {Path.GetFileName(file)}..."));
            }

            // Save the install location pointer
            Globals.SetInstallPath(installFolder);

            progress?.Report((100, "Done!"));
        }

        /// <summary>
        /// Creates a desktop shortcut pointing to SC2ModManager.exe in the install folder.
        /// </summary>
        public void CreateDesktopShortcut(string installFolder, string iconPath = null)
        {
            string exePath = Path.Combine(installFolder, Globals.ModManagerExecutableName);
            string shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "SC2 Mod Manager.lnk"
            );

            // Use Windows Script Host to create the shortcut
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = installFolder;
            shortcut.Description = "SC2 Mod Manager";

            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                shortcut.IconLocation = iconPath;
            else if (File.Exists(exePath))
                shortcut.IconLocation = exePath;

            shortcut.Save();
        }

        /// <summary>
        /// Deletes the entire install folder and clears the pointer file.
        /// Launches a small cleanup script to delete itself after the app closes.
        /// </summary>
        public void Uninstall(string installFolder)
        {
            string pointerDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            // Write a batch script to delete the folder after process exits
            string batchPath = Path.Combine(Path.GetTempPath(), "sc2mm_uninstall.bat");
            string batchContent = $@"@echo off
                    timeout /t 2 /nobreak >nul
                    rd /s /q ""{installFolder}""
                    rd /s /q ""{pointerDir}""
                    del ""{shortcutPath()}""
                    del ""%~f0""
                    ";
            File.WriteAllText(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private string shortcutPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "SC2 Mod Manager.lnk"
        );
    }
}
