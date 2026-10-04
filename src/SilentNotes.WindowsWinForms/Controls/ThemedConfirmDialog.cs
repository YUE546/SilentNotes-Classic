using System;
using System.Drawing;
using System.Windows.Forms;
using SilentNotes.WindowsWinForms.Services;
using Sunny.UI;

namespace SilentNotes.WindowsWinForms.Controls
{
    /// <summary>
    /// Themed replacement for confirmation MessageBoxes: title, explanation and a
    /// right aligned button row. The confirm button turns danger-colored when the
    /// action is irreversible (permanent delete, empty recycle bin, backup restore).
    /// Uses SunnyUI buttons/labels so dialogs match the main window styling.
    /// </summary>
    internal class ThemedConfirmDialog : ThemedDialogForm
    {
        private ThemedConfirmDialog()
        {
        }

        /// <summary>Shows a modal confirmation and returns true when confirmed.</summary>
        public static bool Show(Form owner, WinFormsThemeService themeService, string title, string message, string confirmText, bool danger)
        {
            using (ThemedConfirmDialog dialog = new ThemedConfirmDialog())
            {
                dialog.Text = title;
                dialog.Width = 400;
                dialog.Height = 180;

                UILabel messageLabel = new UILabel
                {
                    Text = message,
                    AutoSize = false,
                    Left = 18,
                    Top = 18,
                    Width = 350,
                    Height = 70,
                    TextAlign = ContentAlignment.TopLeft,
                };

                UIButton confirmButton = new UIButton
                {
                    Text = confirmText,
                    Width = 88,
                    Height = 30,
                    Tag = danger ? "danger" : "accent",
                    DialogResult = DialogResult.OK,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                UIButton cancelButton = new UIButton
                {
                    Text = "取消",
                    Width = 72,
                    Height = 30,
                    DialogResult = DialogResult.Cancel,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                cancelButton.Left = dialog.ClientSize.Width - 18 - confirmButton.Width - 8 - cancelButton.Width;
                confirmButton.Left = dialog.ClientSize.Width - 18 - confirmButton.Width;
                cancelButton.Top = dialog.ClientSize.Height - 44;
                confirmButton.Top = cancelButton.Top;

                dialog.Controls.Add(messageLabel);
                dialog.Controls.Add(confirmButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = confirmButton;
                dialog.CancelButton = cancelButton;
                dialog.ApplyTheme(themeService);

                return dialog.ShowDialog(owner) == DialogResult.OK;
            }
        }

        /// <summary>
        /// Three-way close prompt: Yes = save, No = discard, Cancel = keep editing.
        /// </summary>
        public static DialogResult ShowSavePrompt(Form owner, WinFormsThemeService themeService, string title, string message)
        {
            using (ThemedConfirmDialog dialog = new ThemedConfirmDialog())
            {
                dialog.Text = title;
                dialog.Width = 420;
                dialog.Height = 190;

                UILabel messageLabel = new UILabel
                {
                    Text = message,
                    AutoSize = false,
                    Left = 18,
                    Top = 18,
                    Width = 370,
                    Height = 70,
                    TextAlign = ContentAlignment.TopLeft,
                };

                UIButton saveButton = new UIButton
                {
                    Text = "保存",
                    Width = 80,
                    Height = 30,
                    Tag = "accent",
                    DialogResult = DialogResult.Yes,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                UIButton discardButton = new UIButton
                {
                    Text = "不保存",
                    Width = 80,
                    Height = 30,
                    DialogResult = DialogResult.No,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                UIButton cancelButton = new UIButton
                {
                    Text = "取消",
                    Width = 72,
                    Height = 30,
                    DialogResult = DialogResult.Cancel,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                };
                cancelButton.Left = dialog.ClientSize.Width - 18 - cancelButton.Width;
                discardButton.Left = cancelButton.Left - 8 - discardButton.Width;
                saveButton.Left = discardButton.Left - 8 - saveButton.Width;
                cancelButton.Top = dialog.ClientSize.Height - 44;
                discardButton.Top = cancelButton.Top;
                saveButton.Top = cancelButton.Top;

                dialog.Controls.Add(messageLabel);
                dialog.Controls.Add(saveButton);
                dialog.Controls.Add(discardButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = saveButton;
                dialog.CancelButton = cancelButton;
                dialog.ApplyTheme(themeService);

                return dialog.ShowDialog(owner);
            }
        }
    }
}
