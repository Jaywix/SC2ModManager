using SC2ModManager.Models;
using System.Windows;
using System.Windows.Media;

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
                    // UEF
                    res["ThemeAccentColor"] = Color.FromRgb(0x1E, 0x90, 0xFF); // UEF Blue
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x12, 0x70, 0xCC);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1A, 0x25, 0x35);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x1E, 0x35, 0x50);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x0D, 0x11, 0x17);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0A, 0x0F, 0x18);
                    res["ThemeMainBgImage"] = "/Assets/uef.png";
                    res["ThemeSidebarImage"] = "/Assets/uef_acu.png";
                    break;

                case AppTheme.Cybran:
                    // Cybran
                    res["ThemeAccentColor"] = Color.FromRgb(0xCC, 0x22, 0x00); // Cybran Red
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x99, 0x11, 0x00);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1F, 0x0A, 0x08);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x3D, 0x0F, 0x0A);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x10, 0x06, 0x05);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0C, 0x04, 0x03);
                    res["ThemeMainBgImage"] = "/Assets/cybran.png";
                    res["ThemeSidebarImage"] = "/Assets/cybran_acu.png";
                    break;

                case AppTheme.Aeon:
                    // Aeon
                    res["ThemeAccentColor"] = Color.FromRgb(0x00, 0xBF, 0xA5); // Aeon Teal
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x00, 0x8C, 0x78);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x0A, 0x1A, 0x18);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x0F, 0x2A, 0x26);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x08, 0x12, 0x10);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x05, 0x0E, 0x0C);
                    res["ThemeMainBgImage"] = "/Assets/aeon.png";
                    res["ThemeSidebarImage"] = "/Assets/aeon_acu.png";
                    break;

                default: // Standard
                    res["ThemeAccentColor"] = Color.FromRgb(0x1E, 0x90, 0xFF);
                    res["ThemeAccentDarkColor"] = Color.FromRgb(0x12, 0x70, 0xCC);
                    res["ThemeButtonBaseColor"] = Color.FromRgb(0x1A, 0x25, 0x35);
                    res["ThemeButtonHoverColor"] = Color.FromRgb(0x1E, 0x35, 0x50);
                    res["ThemePanelBgColor"] = Color.FromArgb(0xCC, 0x0D, 0x11, 0x17);
                    res["ThemeSidebarBgColor"] = Color.FromArgb(0xCC, 0x0A, 0x0F, 0x18);
                    res["ThemeMainBgImage"] = "/Assets/uef.png";
                    res["ThemeSidebarImage"] = "/Assets/spoiler_profile.png";
                    break;
            }

            // Rebuild brushes from updated colors
            res["AccentBrush"] = new SolidColorBrush((Color)res["ThemeAccentColor"]);
            res["AccentDarkBrush"] = new SolidColorBrush((Color)res["ThemeAccentDarkColor"]);
            res["ButtonBaseBrush"] = new SolidColorBrush((Color)res["ThemeButtonBaseColor"]);
            res["ButtonHoverBrush"] = new SolidColorBrush((Color)res["ThemeButtonHoverColor"]);
            res["PanelBackgroundBrush"] = new SolidColorBrush((Color)res["ThemePanelBgColor"]);
            res["SidebarOverlayBrush"] = new SolidColorBrush((Color)res["ThemeSidebarBgColor"]);
        }

        public string GetCurrentTheme()
        {
            return configService.Load().Theme ?? AppTheme.Standard;
        }
    }
}
