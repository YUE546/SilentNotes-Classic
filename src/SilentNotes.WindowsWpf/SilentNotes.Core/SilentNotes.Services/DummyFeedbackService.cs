using System.Threading.Tasks;

namespace SilentNotes.Services;

public class DummyFeedbackService : IFeedbackService
{
	public Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButtons buttons, bool conservativeDefault)
	{
		return Task.FromResult(buttons switch
		{
			MessageBoxButtons.Ok => MessageBoxResult.Ok, 
			MessageBoxButtons.ContinueCancel => MessageBoxResult.Continue, 
			_ => MessageBoxResult.Ok, 
		});
	}

	public void ShowToast(string message, FeedbackSeverity severity = FeedbackSeverity.Unknown)
	{
	}
}
