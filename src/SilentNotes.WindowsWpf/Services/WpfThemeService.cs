using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SilentNotes.Models;

namespace SilentNotes.WindowsWpf.Services
{
    internal class WpfThemeService
    {
        public bool IsDarkMode { get; private set; }

        public void ApplyTheme(ThemeMode mode)
        {
            bool shouldBeDark;
            if (mode == ThemeMode.Dark)
                shouldBeDark = true;
            else if (mode == ThemeMode.Light)
                shouldBeDark = false;
            else
                shouldBeDark = IsSystemDarkMode();

            ApplyTheme(shouldBeDark);
        }

        public void ApplyTheme(bool dark)
        {
            if (IsDarkMode == dark)
                return;

            IsDarkMode = dark;
            var resources = Application.Current.Resources;

            if (dark)
            {
                SetColor(resources, "ColorSurfaceWindow", "#1E1E1E");
                SetColor(resources, "ColorSurfacePanel", "#252526");
                SetColor(resources, "ColorSurfacePaper", "#2D2D2D");
                SetColor(resources, "ColorBorderSubtle", "#3E3E3E");
                SetColor(resources, "ColorTextPrimary", "#E0E0E0");
                SetColor(resources, "ColorTextSecondary", "#9E9E9E");
                SetColor(resources, "ColorAccent", "#6C8CFF");
                SetColor(resources, "ColorAccentHover", "#5A7AE6");
                SetColor(resources, "ColorAccentSoft", "#2A3040");
                SetColor(resources, "ColorDanger", "#F44336");
                SetColor(resources, "ColorDangerSoft", "#3D2020");
            }
            else
            {
                SetColor(resources, "ColorSurfaceWindow", "#F6F7F9");
                SetColor(resources, "ColorSurfacePanel", "#EEF1F5");
                SetColor(resources, "ColorSurfacePaper", "#FFFFFF");
                SetColor(resources, "ColorBorderSubtle", "#D9DEE7");
                SetColor(resources, "ColorTextPrimary", "#1F2933");
                SetColor(resources, "ColorTextSecondary", "#667085");
                SetColor(resources, "ColorAccent", "#4B6BFB");
                SetColor(resources, "ColorAccentHover", "#3F5DE0");
                SetColor(resources, "ColorAccentSoft", "#E9EDFF");
                SetColor(resources, "ColorDanger", "#B42318");
                SetColor(resources, "ColorDangerSoft", "#FEE4E2");
            }

            // Replace brush resources (frozen brushes can't be modified)
            ReplaceBrush(resources, "SilentNotesBackgroundBrush", GetColor(resources, "ColorSurfaceWindow"));
            ReplaceBrush(resources, "SilentNotesPanelBrush", GetColor(resources, "ColorSurfacePanel"));
            ReplaceBrush(resources, "SilentNotesPaperBrush", GetColor(resources, "ColorSurfacePaper"));
            ReplaceBrush(resources, "SilentNotesBorderBrush", GetColor(resources, "ColorBorderSubtle"));
            ReplaceBrush(resources, "SilentNotesTextBrush", GetColor(resources, "ColorTextPrimary"));
            ReplaceBrush(resources, "SilentNotesSecondaryTextBrush", GetColor(resources, "ColorTextSecondary"));
            ReplaceBrush(resources, "SilentNotesPrimaryBrush", GetColor(resources, "ColorAccent"));
            ReplaceBrush(resources, "SilentNotesPrimaryHoverBrush", GetColor(resources, "ColorAccentHover"));
            ReplaceBrush(resources, "SilentNotesAccentSoftBrush", GetColor(resources, "ColorAccentSoft"));
            ReplaceBrush(resources, "SilentNotesDangerBrush", GetColor(resources, "ColorDanger"));
            ReplaceBrush(resources, "SilentNotesDangerSoftBrush", GetColor(resources, "ColorDangerSoft"));
            ReplaceBrush(resources, "SilentNotesPrimaryTextBrush", GetColor(resources, "ColorTextPrimary"));
        }

        public void ApplyWindowTheme(Window window)
        {
            if (window == null)
                return;

            TrySetImmersiveDarkMode(window, IsDarkMode);
        }

        private static void SetColor(ResourceDictionary resources, string key, string hex)
        {
            resources[key] = (Color)ColorConverter.ConvertFromString(hex);
        }

        private static Color GetColor(ResourceDictionary resources, string key)
        {
            object obj = resources[key];
            if (obj is Color)
                return (Color)obj;
            return Colors.Transparent;
        }

        private static void ReplaceBrush(ResourceDictionary resources, string key, Color color)
        {
            resources[key] = new SolidColorBrush(color);
        }

        private static void TrySetImmersiveDarkMode(Window window, bool dark)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                    return;

                int attribute = 20;
                int value = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
            }
            catch
            {
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int)
                        return ((int)value) == 0;
                }
            }
            catch { }
            return false;
        }
    }
}
