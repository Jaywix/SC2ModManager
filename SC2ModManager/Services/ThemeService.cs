/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 18, 2026
 * Author: Jacob Wixom
 * 
*/
using SC2ModManager.Models;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SC2ModManager.Services
{
    public class ThemeService
    {
        private readonly ConfigService configService;

        public ThemeService(ConfigService configService)
        {
            this.configService = configService;
        }

        public void ApplyTheme(string theme)
        {
            var config = configService.Load();
            config.Theme = theme;
            configService.Save(config);

            ApplyThemeResources(theme);
        }

        public void ApplyThemeResources(string theme)
        {
            var res = Application.Current.Resources;

            switch (theme)
            {
                case AppTheme.UEF:
                    res["ThemeAccentColor"] = Color.FromRgb(0x1E, 0x90, 0xFF);
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x12, 0x70, 0xCC);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1A, 0x25, 0x35);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x1E, 0x35, 0x50);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x0D, 0x11, 0x17);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0A, 0x0F, 0x18);
                    res["ListBoxSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x90, 0xFF));
                    res["ListBoxHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x90, 0xFF));
                    res["ThemeMainBgImage"] = MakeBitmapImage("/Assets/uef.png");
                    res["ThemeSidebarImage"] = MakeBitmapImage("/Assets/uefacu.png");
                    break;

                case AppTheme.Cybran:   // maybe a little dark, come back to visit, but for now it's fine
                    res["ThemeAccentColor"] = Color.FromRgb(0xCC, 0x22, 0x00);
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x99, 0x11, 0x00);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1F, 0x0A, 0x08);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x3D, 0x0F, 0x0A);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x10, 0x06, 0x05);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0C, 0x04, 0x03);
                    res["ListBoxSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x33, 0xCC, 0x22, 0x00));
                    res["ListBoxHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0xCC, 0x22, 0x00));
                    res["ThemeMainBgImage"] = MakeBitmapImage("/Assets/cybran.png");
                    res["ThemeSidebarImage"] = MakeBitmapImage("/Assets/cybranacu.png");
                    break;

                case AppTheme.Aeon:
                    res["ThemeAccentColor"] = Color.FromRgb(0x00, 0xBF, 0xA5);
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x00, 0x8C, 0x78);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x0A, 0x1A, 0x18);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x0F, 0x2A, 0x26);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x08, 0x12, 0x10);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x05, 0x0E, 0x0C);
                    res["ListBoxSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xBF, 0xA5));
                    res["ListBoxHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0xBF, 0xA5));
                    res["ThemeMainBgImage"] = MakeBitmapImage("/Assets/aeon.png");
                    res["ThemeSidebarImage"] = MakeBitmapImage("/Assets/aeonacu.png");
                    break;

                default: // The goated theme #numberoneuefplayerxd
                    res["ThemeAccentColor"] = Color.FromRgb(0x1E, 0x90, 0xFF);
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x12, 0x70, 0xCC);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1A, 0x25, 0x35);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x1E, 0x35, 0x50);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x0D, 0x11, 0x17);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0A, 0x0F, 0x18);
                    res["ListBoxSelectionBrush"] = new SolidColorBrush(Color.FromArgb(0x33, 0x1E, 0x90, 0xFF));
                    res["ListBoxHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0x1E, 0x90, 0xFF));
                    res["ThemeMainBgImage"] = MakeBitmapImage("/Assets/uef.png");
                    res["ThemeSidebarImage"] = MakeBitmapImage("/Assets/spoiler_profile.png");
                    break;
            }

            // Update the Color keys so StaticResource references in styles also update
            var accent = (Color)res["ThemeAccentColor"];
            var accentDark = (Color)res["ThemeAccentDarkColor"];
            var btnBase = (Color)res["ThemeButtonBaseColor"];
            var btnHover = (Color)res["ThemeButtonHoverColor"];
            var panelBg = (Color)res["ThemePanelBgColor"];
            var sidebarBg = (Color)res["ThemeSidebarBgColor"];

            res["AccentColor"] = accent;
            res["AccentDarkColor"] = accentDark;
            res["ButtonBaseColor"] = btnBase;
            res["ButtonHoverColor"] = btnHover;
            res["ButtonPressedColor"] = accentDark;

            // Rebuild brushes
            res["AccentBrush"] = new SolidColorBrush(accent);
            res["AccentDarkBrush"] = new SolidColorBrush(accentDark);
            res["ButtonBaseBrush"] = new SolidColorBrush(btnBase);
            res["ButtonHoverBrush"] = new SolidColorBrush(btnHover);
            res["ButtonPressedBrush"] = new SolidColorBrush(accentDark);
            res["PanelBackgroundBrush"] = new SolidColorBrush(panelBg);
            res["SidebarOverlayBrush"] = new SolidColorBrush(sidebarBg);
        }

        private BitmapImage MakeBitmapImage(string assetPath)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri($"pack://application:,,,{assetPath}", UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        public string GetCurrentTheme()
        {
            return configService.Load().Theme ?? AppTheme.Standard;
        }
    }
}
