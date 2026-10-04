using System;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace SilentNotes.WindowsWpf.Controls
{
    public class ThemedDialogWindow : Window
    {
        public ThemedDialogWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = (System.Windows.Media.Brush)Application.Current.Resources["SilentNotesBackgroundBrush"];
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SilentNotesTextBrush"];
            Loaded += ThemedDialogWindow_Loaded;
        }

        public void ApplyTitleBarTheme(bool dark)
        {
            if (!TrySetImmersiveDarkMode(this, dark))
                return;
        }

        private void ThemedDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var themeService = App.Services.GetRequiredService<Services.WpfThemeService>();
            ApplyTitleBarTheme(themeService.IsDarkMode);
        }

        private static bool TrySetImmersiveDarkMode(Window window, bool dark)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                int attribute = 20;
                int value = dark ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
    }
}
