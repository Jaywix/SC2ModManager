using SC2ModManager.Models;
using SC2ModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SC2ModManager.Views
{
    public partial class PreviousVersionsWindow : Window
    {
        private readonly MainViewModel _vm;

        public PreviousVersionsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var releases = await _vm.GetPreviousReleasesAsync();

                LoadingText.Visibility = Visibility.Collapsed;

                if (releases == null || releases.Count == 0)
                {
                    ErrorText.Text = "No previous versions are available for restore.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                foreach (var release in releases)
                {
                    VersionListPanel.Children.Add(BuildReleaseCard(release));
                }
            }
            catch (Exception ex)
            {
                LoadingText.Visibility = Visibility.Collapsed;
                ErrorText.Text = $"Failed to load versions: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private UIElement BuildReleaseCard(ReleaseInfo release)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 14)
            };

            var outer = new Grid();
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Version header row
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var versionLabel = new TextBlock
            {
                Text = release.TagName,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(versionLabel, 0);

            var restoreBtn = new Button
            {
                Content = "Restore this Version",
                Tag = release,
                Height = 32,
                MinWidth = 150,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x25, 0x35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 4, 12, 4)
            };

            var restoreBtnTemplate = new ControlTemplate(typeof(Button));
            var btnBorder = new FrameworkElementFactory(typeof(Border));
            btnBorder.Name = "border";
            btnBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            btnBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            btnBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            btnBorder.AppendChild(contentPresenter);
            restoreBtnTemplate.VisualTree = btnBorder;
            restoreBtn.Template = restoreBtnTemplate;

            restoreBtn.Click += RestoreButton_Click;
            Grid.SetColumn(restoreBtn, 1);

            headerRow.Children.Add(versionLabel);
            headerRow.Children.Add(restoreBtn);
            Grid.SetRow(headerRow, 0);

            // Separator
            var sep = new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
                Margin = new Thickness(0, 8, 0, 8),
                Height = 1
            };
            Grid.SetRow(sep, 1);

            // Release notes
            var bodyText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(release.Body) ? "No release notes provided." : release.Body.Replace("\r\n", "\n"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };
            Grid.SetRow(bodyText, 2);

            outer.Children.Add(headerRow);
            outer.Children.Add(sep);
            outer.Children.Add(bodyText);

            card.Child = outer;
            return card;
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ReleaseInfo release)
            {
                var confirm = MessageBox.Show(
                    $"You are about to restore SC2 Mod Manager to {release.TagName}.\n\n" +
                    "⚠ Warning: Rolling back to an older version may cause issues:\n" +
                    "  • Some mods may stop working correctly\n" +
                    "  • Features added in newer versions will be unavailable\n" +
                    "  • Configuration saved by newer versions may not be compatible\n\n" +
                    "The application will close and restart after the restore.\n\n" +
                    "Are you sure you want to restore this version?",
                    $"Restore {release.TagName}?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                btn.IsEnabled = false;
                btn.Content = "Restoring...";

                await _vm.RestoreVersionAsync(release.DownloadUrl, release.TagName);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
