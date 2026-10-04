using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface IFeedbackService
{
	void ShowToast(string message, FeedbackSeverity severity = FeedbackSeverity.Unknown);

	Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButtons buttons, bool conservativeDefault);
}
