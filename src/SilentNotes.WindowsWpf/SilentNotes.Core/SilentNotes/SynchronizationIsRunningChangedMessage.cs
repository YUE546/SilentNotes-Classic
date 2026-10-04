namespace SilentNotes;

public class SynchronizationIsRunningChangedMessage
{
	public bool IsRunning { get; }

	public SynchronizationIsRunningChangedMessage(bool isRunning)
	{
		IsRunning = isRunning;
	}
}
