using Microsoft.Win32;
using SC2ModManager.Models;
using SC2ModManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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



        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
