using System;
using System.Drawing;
using System.Windows.Forms;
using SilentNotes.WindowsWinForms.Services;

namespace SilentNotes.WindowsWinForms.Controls
{
    /// <summary>
    /// Base class for themed secondary windows: a native-frame dialog whose caption
    /// buttons and window animation come from the system (DWM immersive dark mode
    /// follows the theme), while the content controls are styled by
    /// WinFormsThemeService. ApplyTheme must be called after the content is added.
    /// </summary>
    public class ThemedDialogForm : Form
    {
        /// <summary>The palette applied last; lets subclasses color dynamic elements.</summary>
        internal WinFormsThemeService Theme;

        public ThemedDialogForm()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 9F);
        }

        /// <summary>Applies the current palette to the dialog and its controls.</summary>
        internal void ApplyTheme(WinFormsThemeService themeService)
        {
            if (themeService == null)
                return;

            Theme = themeService;
            BackColor = themeService.SurfaceWindow;
            ForeColor = themeService.TextPrimary;
            themeService.Apply(this);
            themeService.ApplyWindowTheme(this);
        }
    }
}
