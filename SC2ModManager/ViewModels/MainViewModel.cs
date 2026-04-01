using Microsoft.Win32;
using SC2ModManager.Models;
using SC2ModManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SC2ModManager.ViewModels
{

    public enum MainView
    {
        Home,
        Mods,
        CustomMaps,
        Hotkeys
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // Data properties
        private ModService modService;
        private GameService gameService;

        private ObservableCollection<Map> maps;
        public ObservableCollection<Map> Maps
        {
            get => this.maps;
            set
            {
                this.maps = value;
                OnPropertyChanged(nameof(Maps));
            }
        }

        // View management
        private MainView currentView;
        public MainView CurrentView
        {
            get { return currentView; }
            set
            {
                currentView = value;
                OnPropertyChanged("CurrentView");
            }
        }

        // Update checking properties
        private UpdateService updateService;

        private bool updateAvailable;
        public bool UpdateAvailable
        {
            get => updateAvailable;
            set
            {
                updateAvailable = value;
                OnPropertyChanged(nameof(UpdateAvailable));
            }
        }

        private string updateDownloadUrl;

        // Download progress properties
        private double downloadProgress;
        public double DownloadProgress
        {
            get => downloadProgress;
            set
            {
                downloadProgress = value;
                OnPropertyChanged(nameof(DownloadProgress));
            }
        }



        public MainViewModel()
        {
            try
            {
                ConfigService configService = new ConfigService();

                this.modService = new ModService(configService);
                this.gameService = new GameService(configService);

                LoadMaps();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}");
            }

            this.CurrentView = MainView.Home;

            this.updateService = new UpdateService();
            _ = CheckForUpdates();
        }

        public void LoadMaps()
        {
            var mapList = this.modService.GetAllMaps();
            this.Maps = new ObservableCollection<Map>(mapList);
        }

        public void Save()
        {
            try
            {
                this.modService.SaveMaps(this.Maps);
                MessageBox.Show("Maps saved successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving maps: {ex.Message}");
            }
        }

        public void LaunchGame()
        {
            try
            {
                this.gameService.LaunchGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}");
            }
        }

        public void AddMap(string filePath)
        {
            try
            {
                this.modService.AddMap(filePath);
                LoadMaps();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding map: {ex.Message}");
            }
        }


        public void SelectAllMaps()
        {
            foreach (var map in Maps)
                map.IsEnabled = true;
        }

        public void RemoveAllMaps()
        {
            foreach (var map in Maps)
                map.IsEnabled = false;
        }

        public void SaveMaps()
        {
            modService.SaveMaps(Maps);
        }

        public void ImportMaps()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Maps (*.scd;*.zip)|*.scd;*.zip",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (file.EndsWith(".zip"))
                    {
                        ExtractZip(file);
                    }
                    else
                    {
                        modService.AddMap(file);
                    }
                }

                LoadMaps();
            }
        }

        public void ExtractZip(string zipPath)
        {
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, temp);

            foreach (var file in Directory.GetFiles(temp, "*.scd", SearchOption.AllDirectories))
            {
                modService.AddMap(file);
            }

            Directory.Delete(temp, true);
        }

        public async Task CheckForUpdates()
        {
            try
            {
                var (latestVersion, downloadUrl) = await updateService.GetLatestRelease();

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (latestVersion.CompareTo(currentVersion) > 0)
                {
                    UpdateAvailable = true;
                    updateDownloadUrl = downloadUrl;
                }
            }
            catch
            {
                // possibly failed, but that's ok - just don't show update notification
            }
        }

        public async Task RunUpdater()
        {
            try
            {
                if (string.IsNullOrEmpty(updateDownloadUrl))
                {
                    MessageBox.Show("No update URL found.");
                    return;
                }

                // Paths
                string zipPath = Path.Combine(Path.GetTempPath(), "SC2_update.zip");
                string installPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                string updaterPath = Path.Combine(installPath, Globals.UpdaterExecutableName);
                string exeName = Globals.ModManagerExecutableName;

                // Ensure updater exists
                if (!File.Exists(updaterPath))
                {
                    MessageBox.Show($"Updater not found:\n{updaterPath}");
                    return;
                }

                // Optional: create backup BEFORE downloading/installing
                CreateBackup(installPath);

                // Download with progress
                await DownloadFileWithProgress(updateDownloadUrl, zipPath);

                MessageBox.Show("Download complete. Starting updater...");

                // Start updater process
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{zipPath}\" \"{installPath}\" \"{exeName}\"",
                    UseShellExecute = true,
                    WorkingDirectory = installPath
                });

                // Small delay to ensure updater launches cleanly
                MessageBox.Show("Updating... The app will restart automatically.");
                await Task.Delay(500);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed:\n{ex.Message}");
            }
        }

        public async Task DownloadFileWithProgress(string url, string outputPath)
        {
            using HttpClient client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReport = totalBytes != -1;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);

                totalRead += read;

                if (canReport)
                {
                    DownloadProgress = (double)totalRead / totalBytes * 100;
                }
            }
        }

        public void CreateBackup(string installPath)
        {
            string backupDir = Path.Combine(installPath, "backup");

            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);

            Directory.CreateDirectory(backupDir);

            foreach (var file in Directory.GetFiles(installPath))
            {
                var dest = Path.Combine(backupDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
        }

        public void RestoreBackup(string installPath)
        {
            string backupDir = Path.Combine(installPath, "backup");

            foreach (var file in Directory.GetFiles(backupDir))
            {
                var dest = Path.Combine(installPath, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
