using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

using SC2ModManager.Models;

namespace SC2ModManager
{

    public partial class SetupWindow : Window
    {
        private string selectedPath;

        private readonly string AppDataPath;
        private readonly string ConfigPath;

        public SetupWindow()
        {
            InitializeComponent();

            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Globals.LauncherName
            );

            ConfigPath = Path.Combine(AppDataPath, "config.json");
        }

        // Browse button
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FolderName;
                GamePathTextBox.Text = selectedPath;
            }
        }

        // Finish button
        private async void Finish_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                MessageBox.Show("Please select a folder.");
                return;
            }

            if (!IsValidGameFolder(selectedPath))
            {
                MessageBox.Show("Invalid game folder.");
                return;
            }

            try
            {
                StatusText.Text = "Initializing...";
                SetupProgressBar.Value = 0;

                InitializeApp(selectedPath);

                StatusText.Text = "Creating backup...";

                await Task.Run(() => CreateBackupWithProgress(selectedPath + "\\gamedata"));

                StatusText.Text = "Done!";
                SetupProgressBar.Value = 100;

                MessageBox.Show("Setup complete!");

                var main = new MainWindow();
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        // Validate game folder
        private bool IsValidGameFolder(string path)
        {
            return Directory.Exists(Path.Combine(path, "gamedata"));
        }

        // Initialize app data + config
        private void InitializeApp(string gamePath)
        {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);

            string mapsPath = Path.Combine(AppDataPath, "Maps");
            string backupPath = Path.Combine(AppDataPath, "GameBackup");

            Directory.CreateDirectory(mapsPath);
            Directory.CreateDirectory(backupPath);

            var config = new AppConfig
            {
                GamePath = gamePath
            };

            File.WriteAllText(
                ConfigPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );
        }

        // Create full backup
        //private void CreateBackup(string gamePath)
        //{
        //    string backupPath = Path.Combine(AppDataPath, "GameBackup");

        //    CopyDirectory(gamePath, backupPath);
        //}

        // Recursive copy
        //private void CopyDirectory(string sourceDir, string targetDir)
        //{
        //    Directory.CreateDirectory(targetDir);

        //    foreach (var file in Directory.GetFiles(sourceDir))
        //    {
        //        string destFile = Path.Combine(targetDir, Path.GetFileName(file));
        //        File.Copy(file, destFile, true);
        //    }

        //    foreach (var dir in Directory.GetDirectories(sourceDir))
        //    {
        //        string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
        //        CopyDirectory(dir, destDir);
        //    }
        //}

        private void CreateBackupWithProgress(string sourceDir)
        {
            string backupPath = Path.Combine(AppDataPath, "GameBackup");

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int copied = 0;

            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(backupPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                File.Copy(file, destFile, true);

                copied++;

                int progress = (int)((copied / (double)totalFiles) * 100);

                // Update UI safely
                Dispatcher.Invoke(() =>
                {
                    SetupProgressBar.Value = progress;
                    StatusText.Text = $"Copying files... ({copied}/{totalFiles})";
                });
            }
        }


    }

}
