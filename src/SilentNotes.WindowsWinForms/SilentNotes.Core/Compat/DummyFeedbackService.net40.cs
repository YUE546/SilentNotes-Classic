// Compat copy of SilentNotes.AllPlatforms\Services\DummyFeedbackService.cs for .NET 4.0.
// The original uses Task.FromResult (.NET 4.5+), replaced by a TaskCompletionSource.
using System.Threading.Tasks;

namespace SilentNotes.Services
{
    /// <summary>
    /// Dummy implementation of the <see cref="IFeedbackService"/> interface. This implementation
    /// provides no functionallity and can be used when all feedback should be ignored.
    /// </summary>
    public class DummyFeedbackService : IFeedbackService
    {
        /// <inheritdoc/>
        public Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButtons buttons, bool conservativeDefault)
        {
            MessageBoxResult result;
            switch (buttons)
            {
                case MessageBoxButtons.Ok:
                    result = MessageBoxResult.Ok;
                    break;
                case MessageBoxButtons.ContinueCancel:
                    result = MessageBoxResult.Continue;
                    break;
                default:
                    result = MessageBoxResult.Ok;
                    break;
            }
            TaskCompletionSource<MessageBoxResult> completedTask = new TaskCompletionSource<MessageBoxResult>();
            completedTask.SetResult(result);
            return completedTask.Task;
        }

        /// <inheritdoc/>
        public void ShowToast(string message, FeedbackSeverity severity = FeedbackSeverity.Unknown)
        {
        }
    }
}
