using System.Threading.Tasks;
using System.Windows;
using SilentNotes.Services;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WpfFeedbackService : IFeedbackService
    {
        public void ShowToast(string message, FeedbackSeverity severity = FeedbackSeverity.Unknown)
        {
            MessageBox.Show(message, "SilentNotes", MessageBoxButton.OK, ToImage(severity));
        }

        public Task<SilentNotes.Services.MessageBoxResult> ShowMessageAsync(
            string message,
            string title,
            MessageBoxButtons buttons,
            bool conservativeDefault)
        {
            WpfMessageBoxResult result = MessageBox.Show(
                message,
                string.IsNullOrEmpty(title) ? "SilentNotes" : title,
                ToButtons(buttons),
                MessageBoxImage.Information,
                conservativeDefault ? WpfMessageBoxResult.Cancel : WpfMessageBoxResult.OK);

            return Task.FromResult(ToResult(result, buttons));
        }

        private static MessageBoxButton ToButtons(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.ContinueCancel:
                    return MessageBoxButton.OKCancel;
                case MessageBoxButtons.YesNoCancel:
                    return MessageBoxButton.YesNoCancel;
                default:
                    return MessageBoxButton.OK;
            }
        }

        private static MessageBoxImage ToImage(FeedbackSeverity severity)
        {
            switch (severity)
            {
                case FeedbackSeverity.Warning:
                    return MessageBoxImage.Warning;
                case FeedbackSeverity.Error:
                    return MessageBoxImage.Error;
                default:
                    return MessageBoxImage.Information;
            }
        }

        private static SilentNotes.Services.MessageBoxResult ToResult(WpfMessageBoxResult result, MessageBoxButtons buttons)
        {
            switch (result)
            {
                case WpfMessageBoxResult.OK:
                    return buttons == MessageBoxButtons.ContinueCancel
                        ? SilentNotes.Services.MessageBoxResult.Continue
                        : SilentNotes.Services.MessageBoxResult.Ok;
                case WpfMessageBoxResult.Yes:
                    return SilentNotes.Services.MessageBoxResult.Yes;
                case WpfMessageBoxResult.No:
                    return SilentNotes.Services.MessageBoxResult.No;
                default:
                    return SilentNotes.Services.MessageBoxResult.Cancel;
            }
        }
    }
}
