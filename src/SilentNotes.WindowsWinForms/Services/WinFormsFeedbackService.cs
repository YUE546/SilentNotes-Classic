using System;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using SilentNotes.Services;
using SilentNotes.WindowsWinForms.Controls;
using Sunny.UI;

namespace SilentNotes.WindowsWinForms.Services
{
    /// <summary>
    /// Core feedback surface for the WinForms client. Toasts go to SunnyUI's
    /// transient UIMessageTip instead of blocking MessageBoxes; modal messages use
    /// a ThemedDialogForm so Core-initiated prompts follow the app palette instead
    /// of the unthemed system MessageBox.
    /// </summary>
    internal sealed class WinFormsFeedbackService : IFeedbackService
    {
        private readonly WinFormsThemeService _theme;

        public WinFormsFeedbackService(WinFormsThemeService theme)
        {
            _theme = theme;
        }

        public void ShowToast(string message, FeedbackSeverity severity = FeedbackSeverity.Unknown)
        {
            switch (severity)
            {
                case FeedbackSeverity.Warning:
                    UIMessageTip.ShowWarning(message, 2000);
                    break;
                case FeedbackSeverity.Error:
                    UIMessageTip.ShowError(message, 2000);
                    break;
                default:
                    UIMessageTip.Show(message);
                    break;
            }
        }

        public Task<SilentNotes.Services.MessageBoxResult> ShowMessageAsync(
            string message,
            string title,
            MessageBoxButtons buttons,
            bool conservativeDefault)
        {
            SilentNotes.Services.MessageBoxResult result;
            using (ThemedDialogForm dialog = new ThemedDialogForm())
            {
                dialog.Text = string.IsNullOrEmpty(title) ? "SilentNotes" : title;
                dialog.Width = 420;
                dialog.Height = 190;

                UILabel messageLabel = new UILabel
                {
                    Text = message,
                    AutoSize = false,
                    Left = 18,
                    Top = 18,
                    Width = 370,
                    Height = 90,
                    TextAlign = System.Drawing.ContentAlignment.TopLeft,
                };
                dialog.Controls.Add(messageLabel);

                UIButton acceptButton = null;
                UIButton defaultButton;
                switch (buttons)
                {
                    case MessageBoxButtons.YesNoCancel:
                        acceptButton = CreateButton("是", Forms.DialogResult.Yes, "accent");
                        UIButton noButton = CreateButton("否", Forms.DialogResult.No, "window");
                        UIButton cancelButton = CreateButton("取消", Forms.DialogResult.Cancel, "window");
                        LayoutButtonsRight(dialog, cancelButton, noButton, acceptButton);
                        dialog.AcceptButton = acceptButton;
                        dialog.CancelButton = cancelButton;
                        defaultButton = conservativeDefault ? cancelButton : acceptButton;
                        break;
                    case MessageBoxButtons.ContinueCancel:
                        acceptButton = CreateButton("继续", Forms.DialogResult.OK, "accent");
                        UIButton cancel2Button = CreateButton("取消", Forms.DialogResult.Cancel, "window");
                        LayoutButtonsRight(dialog, cancel2Button, acceptButton);
                        dialog.AcceptButton = acceptButton;
                        dialog.CancelButton = cancel2Button;
                        defaultButton = conservativeDefault ? cancel2Button : acceptButton;
                        break;
                    default:
                        acceptButton = CreateButton("确定", Forms.DialogResult.OK, "accent");
                        LayoutButtonsRight(dialog, acceptButton);
                        dialog.AcceptButton = acceptButton;
                        dialog.CancelButton = acceptButton;
                        defaultButton = acceptButton;
                        break;
                }

                dialog.ApplyTheme(_theme);
                defaultButton.Focus();
                ShowDialogOnOwner(dialog);
                result = ToResult(dialog.DialogResult, buttons);
            }

            return Task.FromResult(result);
        }

        private static UIButton CreateButton(string text, Forms.DialogResult dialogResult, string tag)
        {
            return new UIButton
            {
                Text = text,
                Width = 80,
                Height = 30,
                Tag = tag,
                DialogResult = dialogResult,
                Anchor = Forms.AnchorStyles.Bottom | Forms.AnchorStyles.Right,
            };
        }

        // Lays the buttons out right-aligned: each button goes to the left of the
        // previous row end, so the first argument ends up rightmost.
        private static void LayoutButtonsRight(ThemedDialogForm dialog, params UIButton[] buttonsLeftToRight)
        {
            int right = dialog.ClientSize.Width - 18;
            for (int index = buttonsLeftToRight.Length - 1; index >= 0; index--)
            {
                UIButton button = buttonsLeftToRight[index];
                button.Left = right - button.Width;
                button.Top = dialog.ClientSize.Height - 44;
                right = button.Left - 8;
                dialog.Controls.Add(button);
            }
        }

        private static void ShowDialogOnOwner(Forms.Form dialog)
        {
            Forms.Form owner = Forms.Form.ActiveForm;
            if (owner != null && !owner.IsDisposed)
                dialog.ShowDialog(owner);
            else
                dialog.ShowDialog();
        }

        private static SilentNotes.Services.MessageBoxResult ToResult(
            Forms.DialogResult result,
            MessageBoxButtons buttons)
        {
            switch (result)
            {
                case Forms.DialogResult.OK:
                    return buttons == MessageBoxButtons.ContinueCancel
                        ? SilentNotes.Services.MessageBoxResult.Continue
                        : SilentNotes.Services.MessageBoxResult.Ok;
                case Forms.DialogResult.Yes:
                    return SilentNotes.Services.MessageBoxResult.Yes;
                case Forms.DialogResult.No:
                    return SilentNotes.Services.MessageBoxResult.No;
                default:
                    return SilentNotes.Services.MessageBoxResult.Cancel;
            }
        }
    }
}
