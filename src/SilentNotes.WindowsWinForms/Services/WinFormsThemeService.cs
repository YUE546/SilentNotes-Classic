using System;
using System.Drawing;
using System.Windows.Forms;
using SilentNotes.Models;
using SilentNotes.WindowsWinForms.Controls;
using Sunny.UI;

namespace SilentNotes.WindowsWinForms.Services
{
    /// <summary>
    /// Application palette for the WinForms client, derived from the "Minimalism &
    /// Swiss Style" design pass (teal focus color + single orange action color).
    /// Light and dark variants are both token based; controls are styled by walking
    /// the control tree. Controls steer their appearance with a Tag containing
    /// tokens: "accent" (teal), "action" (orange CTA), "danger", "ghost" (icon-only
    /// button), "secondary", "paper", "panel", "window", "bare" (no background).
    /// </summary>
    internal class WinFormsThemeService
    {
        public bool IsDarkMode { get; private set; }

        public Color SurfaceWindow { get; private set; }
        public Color SurfacePanel { get; private set; }
        public Color SurfacePaper { get; private set; }
        public Color BorderSubtle { get; private set; }
        public Color TextPrimary { get; private set; }
        public Color TextSecondary { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentHover { get; private set; }
        public Color AccentSoft { get; private set; }
        public Color Action { get; private set; }
        public Color ActionHover { get; private set; }
        public Color Danger { get; private set; }
        public Color DangerSoft { get; private set; }
        public Color ListHover { get; private set; }

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

        public WinFormsThemeService()
        {
            // The palette must be populated up front: ApplyTheme early-returns when the
            // requested mode matches the initial dark flag, and the editor relies on
            // IsDarkMode before the first ApplyTheme call.
            SetPalette(false);
        }

        public void ApplyTheme(bool dark)
        {
            if (IsDarkMode == dark)
                return;

            IsDarkMode = dark;
            SetPalette(dark);
        }

        private void SetPalette(bool dark)
        {
            if (dark)
            {
                SurfaceWindow = FromHex("#17211F");
                SurfacePanel = FromHex("#1D2927");
                SurfacePaper = FromHex("#223030");
                BorderSubtle = FromHex("#33443F");
                TextPrimary = FromHex("#D9E6E2");
                TextSecondary = FromHex("#8FA8A0");
                Accent = FromHex("#2DD4BF");
                AccentHover = FromHex("#14B8A6");
                AccentSoft = FromHex("#1E3D38");
                // The dark Action stays a deep orange: the light-mode orange reads
                // neon-bright against the dark surfaces (only 保存/新建笔记 use it).
                Action = FromHex("#C2410C");
                ActionHover = FromHex("#EA580C");
                Danger = FromHex("#F87171");
                DangerSoft = FromHex("#3B2222");
                ListHover = FromHex("#263532");
            }
            else
            {
                SurfaceWindow = FromHex("#F0F7F5");
                SurfacePanel = FromHex("#E6F0EC");
                SurfacePaper = FromHex("#FFFFFF");
                BorderSubtle = FromHex("#CFE0DA");
                TextPrimary = FromHex("#12433E");
                // Darkened from #5B776E: 4.18:1 on the sidebar panel failed WCAG 4.5:1
                // for the 8.5F secondary text (status bar, info row, tag caption).
                TextSecondary = FromHex("#4E6A61");
                Accent = FromHex("#0D9488");
                AccentHover = FromHex("#0A7E73");
                AccentSoft = FromHex("#CFF2EB");
                // Darkened from #EA580C: white 9F text was 3.58:1; #C94D0B reaches 4.6:1.
                Action = FromHex("#C94D0B");
                ActionHover = FromHex("#B24409");
                Danger = FromHex("#DC2626");
                DangerSoft = FromHex("#FEE2E2");
                ListHover = FromHex("#EDF5F2");
            }
        }

        /// <summary>Recursively applies the current palette to the control tree.</summary>
        public void Apply(Control root)
        {
            if (root == null)
                return;

            ApplyControl(root);
            foreach (Control child in root.Controls)
                Apply(child);
        }

        private void ApplyControl(Control control)
        {
            // Forms (including the SunnyUI UIForm) style themselves via ApplyThemeToUi
            if (control is Form)
                return;

            string tag = (control.Tag as string) ?? string.Empty;
            bool hasAccent = tag.IndexOf("accent", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasAction = tag.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasDanger = tag.IndexOf("danger", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasSecondary = tag.IndexOf("secondary", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasPaper = tag.IndexOf("paper", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasWindow = tag.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasTool = tag.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0;
            bool bare = tag.IndexOf("bare", StringComparison.OrdinalIgnoreCase) >= 0;

            // Custom SegmentedToggle paints itself from these token colors; it must
            // be matched before the generic fallback, which would overwrite its
            // transparent background with the panel fill.
            SegmentedToggle segmentedToggle = control as SegmentedToggle;
            if (segmentedToggle != null)
            {
                // Recessed track (window tone) with a raised paper pill on the
                // selected segment — the classic segmented-control pairing.
                // Opaque base matching the sidebar; the track is painted on top.
                segmentedToggle.BackColor = SurfacePanel;
                segmentedToggle.TrackFill = SurfaceWindow;
                segmentedToggle.TrackBorder = BorderSubtle;
                segmentedToggle.SegmentFill = SurfacePaper;
                segmentedToggle.SegmentText = Accent;
                segmentedToggle.TrackText = TextSecondary;
                segmentedToggle.TrackTextHover = TextPrimary;
                return;
            }

            // SunnyUI controls must be matched before their standard base classes;
            // UIButton/UISymbolButton/UICheckBox derive from UIControl, not from Button.
            UISymbolButton symbolButton = control as UISymbolButton;
            if (symbolButton != null)
            {
                StyleSymbolButton(symbolButton, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare);
                return;
            }

            UIButton uiButton = control as UIButton;
            if (uiButton != null)
            {
                StyleButton(uiButton, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare);
                return;
            }

            UILabel uiLabel = control as UILabel;
            if (uiLabel != null)
            {
                uiLabel.Style = UIStyle.Custom;
                uiLabel.ForeColor = hasDanger ? Danger : (hasSecondary ? TextSecondary : TextPrimary);
                return;
            }

            UITextBox uiTextBox = control as UITextBox;
            if (uiTextBox != null)
            {
                uiTextBox.Style = UIStyle.Custom;
                uiTextBox.FillColor = SurfacePaper;
                uiTextBox.ForeColor = TextPrimary;
                uiTextBox.RectColor = BorderSubtle;
                uiTextBox.WatermarkColor = TextSecondary;
                uiTextBox.WatermarkActiveColor = TextSecondary;
                uiTextBox.SymbolColor = TextSecondary;
                // ReadOnly UITextBoxes (data directory) switch to a dedicated
                // read-only palette whose near-white defaults glow in dark mode.
                uiTextBox.FillReadOnlyColor = SurfacePaper;
                uiTextBox.RectReadOnlyColor = BorderSubtle;
                uiTextBox.ForeReadOnlyColor = TextSecondary;
                return;
            }

            UIListBox uiListBox = control as UIListBox;
            if (uiListBox != null)
            {
                uiListBox.Style = UIStyle.Custom;
                uiListBox.FillColor = SurfacePaper;
                uiListBox.RectColor = SurfacePaper;
                uiListBox.ListBox.BackColor = SurfacePaper;
                uiListBox.ItemSelectBackColor = AccentSoft;
                uiListBox.ItemSelectForeColor = Accent;
                uiListBox.HoverColor = ListHover;
                uiListBox.ScrollBarBackColor = SurfacePaper;
                uiListBox.ScrollBarColor = BorderSubtle;
                // Keep the listbox's own default item text invisible: we repaint items
                // in the DrawItem handler, and visible default text flickers on hover.
                uiListBox.ForeColor = SurfacePaper;
                return;
            }

            UIComboBox uiComboBox = control as UIComboBox;
            if (uiComboBox != null)
            {
                // The dropdown arrow is drawn with RectColor over FillColor; leaving
                // these at defaults makes the arrow disappear into the background.
                uiComboBox.Style = UIStyle.Custom;
                uiComboBox.FillColor = SurfacePaper;
                uiComboBox.RectColor = BorderSubtle;
                uiComboBox.ForeColor = TextPrimary;
                uiComboBox.ItemFillColor = SurfacePaper;
                uiComboBox.ItemForeColor = TextPrimary;
                uiComboBox.ItemSelectBackColor = AccentSoft;
                uiComboBox.ItemSelectForeColor = TextPrimary;
                uiComboBox.ItemHoverColor = ListHover;
                return;
            }

            UIPanel uiPanel = control as UIPanel;
            if (uiPanel != null)
            {
                uiPanel.Style = UIStyle.Custom;
                Color panelFill = hasWindow ? SurfaceWindow : (hasPaper ? SurfacePaper : SurfacePanel);
                uiPanel.FillColor = panelFill;
                uiPanel.RectColor = panelFill;
                return;
            }

            UILine uiLine = control as UILine;
            if (uiLine != null)
            {
                uiLine.Style = UIStyle.Custom;
                uiLine.LineColor = BorderSubtle;
                return;
            }

            UICheckBox uiCheckBox = control as UICheckBox;
            if (uiCheckBox != null)
            {
                uiCheckBox.Style = UIStyle.Custom;
                uiCheckBox.ForeColor = TextPrimary;
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                if (hasAccent)
                {
                    button.BackColor = Accent;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Accent;
                    button.FlatAppearance.MouseOverBackColor = AccentHover;
                }
                else if (hasDanger)
                {
                    button.BackColor = DangerSoft;
                    button.ForeColor = Danger;
                    button.FlatAppearance.BorderColor = Danger;
                    button.FlatAppearance.MouseOverBackColor = DangerSoft;
                }
                else
                {
                    button.BackColor = hasPaper ? SurfacePaper : SurfaceWindow;
                    button.ForeColor = hasDanger ? Danger : TextPrimary;
                    button.FlatAppearance.BorderColor = BorderSubtle;
                    button.FlatAppearance.MouseOverBackColor = AccentSoft;
                }
                return;
            }

            TextBox textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = SurfacePaper;
                textBox.ForeColor = TextPrimary;
                // SunnyUI wrappers (UITextBox) draw their own frame; forcing a border
                // on their inner TextBox paints stray brackets into the control.
                if (!(textBox.Parent is UITextBox) && !(textBox.Parent is UIComboBox))
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = SurfacePaper;
                comboBox.ForeColor = TextPrimary;
                return;
            }

            ListBox listBox = control as ListBox;
            if (listBox != null)
            {
                if (listBox.Parent is UIListBox)
                {
                    // Inner listbox of a SunnyUI UIListBox: SunnyUI draws its own frame
                    // and items are repainted in the DrawItem handler; forcing a border
                    // here paints a stray frame, and visible ForeColor causes flicker.
                    listBox.BackColor = SurfacePaper;
                    listBox.ForeColor = SurfacePaper;
                    listBox.BorderStyle = BorderStyle.None;
                    return;
                }

                listBox.BackColor = SurfacePaper;
                listBox.ForeColor = TextPrimary;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            Label label = control as Label;
            if (label != null)
            {
                if (!bare)
                    label.BackColor = Color.Transparent;
                label.ForeColor = hasDanger ? Danger : (hasSecondary ? TextSecondary : TextPrimary);
                return;
            }

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                if (!bare)
                    checkBox.BackColor = control.Parent != null ? control.Parent.BackColor : SurfacePanel;
                checkBox.ForeColor = TextPrimary;
                return;
            }

            ToolStrip toolStrip = control as ToolStrip;
            if (toolStrip != null)
            {
                toolStrip.BackColor = SurfacePanel;
                toolStrip.ForeColor = TextPrimary;
                return;
            }

            StatusStrip statusStrip = control as StatusStrip;
            if (statusStrip != null)
            {
                statusStrip.BackColor = SurfacePanel;
                statusStrip.ForeColor = TextSecondary;
                return;
            }

            SplitContainer splitContainer = control as SplitContainer;
            if (splitContainer != null)
            {
                splitContainer.BackColor = BorderSubtle;
                return;
            }

            if (!bare)
                control.BackColor = hasPaper ? SurfacePaper : SurfacePanel;

            if (hasSecondary)
                control.ForeColor = TextSecondary;
        }

        private void StyleButton(UIButton button, bool hasAccent, bool hasAction, bool hasDanger, bool hasPaper, bool hasWindow, bool hasTool, bool bare)
        {
            // SunnyUI cascades the global style over every control whose Style is
            // still "Inherited"; marking the control Custom opts it out.
            button.Style = UIStyle.Custom;
            if (hasAction)
            {
                button.FillColor = Action;
                button.ForeColor = Color.White;
                button.RectColor = Action;
                button.FillHoverColor = ActionHover;
                button.RectHoverColor = ActionHover;
                button.ForeHoverColor = Color.White;
                button.FillPressColor = ActionHover;
                button.RectPressColor = ActionHover;
                return;
            }

            if (hasAccent)
            {
                button.FillColor = Accent;
                button.ForeColor = Color.White;
                button.RectColor = Accent;
                button.FillHoverColor = AccentHover;
                button.RectHoverColor = AccentHover;
                button.ForeHoverColor = Color.White;
                button.FillPressColor = AccentHover;
                button.RectPressColor = AccentHover;
                return;
            }

            if (hasDanger)
            {
                button.FillColor = DangerSoft;
                button.ForeColor = Danger;
                button.RectColor = Danger;
                button.FillHoverColor = DangerSoft;
                button.RectHoverColor = Danger;
                button.ForeHoverColor = Danger;
                return;
            }

            Color fill = hasWindow ? SurfaceWindow : (hasPaper ? SurfacePaper : SurfacePanel);
            button.FillColor = fill;
            button.ForeColor = TextPrimary;
            button.RectColor = bare ? fill : BorderSubtle;

            // Toolbar ("tool") buttons keep a quiet hover: a full AccentSoft/Accent
            // highlight on 16 buttons in a row outshouts the ghost buttons above.
            if (hasTool)
            {
                button.FillHoverColor = ListHover;
                button.RectHoverColor = ListHover;
                button.FillPressColor = ListHover;
                button.RectPressColor = ListHover;
                button.ForeHoverColor = TextPrimary;
                return;
            }

            button.FillHoverColor = AccentSoft;
            button.RectHoverColor = Accent;
            button.ForeHoverColor = TextPrimary;
        }

        private void StyleSymbolButton(UISymbolButton button, bool hasAccent, bool hasAction, bool hasDanger, bool hasPaper, bool hasWindow, bool hasTool, bool bare)
        {
            StyleButton(button, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare);
            if (hasAction)
            {
                button.SymbolColor = Color.White;
                button.SymbolHoverColor = Color.White;
                button.SymbolPressColor = Color.White;
                return;
            }

            if (hasAccent)
            {
                button.SymbolColor = Color.White;
                button.SymbolHoverColor = Color.White;
                button.SymbolPressColor = Color.White;
                return;
            }

            if (hasDanger)
            {
                button.SymbolColor = Danger;
                button.SymbolHoverColor = Danger;
                button.SymbolPressColor = Danger;
                return;
            }

            // Ghost icon buttons: quiet symbol that wakes up on hover
            button.SymbolColor = TextSecondary;
            button.SymbolHoverColor = Accent;
            button.SymbolPressColor = AccentHover;
        }

        public void ApplyWindowTheme(Form form)
        {
            if (form == null)
                return;

            TrySetImmersiveDarkMode(form, IsDarkMode);
        }

        private static Color FromHex(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        private static void TrySetImmersiveDarkMode(Form form, bool dark)
        {
            try
            {
                IntPtr handle = form.Handle;
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
                    var value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    if (value is int)
                        return ((int)value) == 0;
                }
            }
            catch { }
            return false;
        }
    }
}
