using SC2ModManager.ViewModels;
using System.Windows;
using System.IO;
using SC2ModManager.Models;


namespace SC2ModManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel vm;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            vm = (MainViewModel)DataContext;
        }

        private void ShowHome()
        {
            HomeView.Visibility = Visibility.Visible;
            ModsView.Visibility = Visibility.Collapsed;
            MapsView.Visibility = Visibility.Collapsed;
        }

        private void LaunchGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                vm.LaunchGame();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}");
            }
        }

        private void ShowMods()
        {
            HomeView.Visibility = Visibility.Collapsed;
            ModsView.Visibility = Visibility.Visible;
            MapsView.Visibility = Visibility.Collapsed;
        }

        private void ShowMaps()
        {
            HomeView.Visibility = Visibility.Collapsed;
            ModsView.Visibility = Visibility.Collapsed;
            MapsView.Visibility = Visibility.Visible;
        }

        private void GoToMods(object sender, RoutedEventArgs e) => ShowMods();
        private void GoHome(object sender, RoutedEventArgs e) => ShowHome();
        private void GoToMaps(object sender, RoutedEventArgs e) => ShowMaps();
        private void GoToHotkeys(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hotkeys not implemented yet.");
        }

        private void Maps_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (var file in files)
                {
                    if (file.EndsWith(".zip"))
                    {
                        vm.ExtractZip(file);
                    }
                    else if (file.EndsWith(".scd"))
                    {
                        vm.AddMap(file);
                    }
                }

                vm.LoadMaps();
            }
        }

        private void SelectAll(object sender, RoutedEventArgs e)
        {
            vm.SelectAllMaps();
        }

        private void RemoveAll(object sender, RoutedEventArgs e)
        {
            vm.RemoveAllMaps();
        }

        private void SaveMaps(object sender, RoutedEventArgs e)
        {
            vm.SaveMaps();
        }

        private void ImportMaps(object sender, RoutedEventArgs e)
        {
            vm.ImportMaps();
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            await vm.RunUpdater();
        }




    }
}