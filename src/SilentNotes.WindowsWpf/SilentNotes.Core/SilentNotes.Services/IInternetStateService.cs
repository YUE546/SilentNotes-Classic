namespace SilentNotes.Services;

public interface IInternetStateService
{
	bool IsInternetConnected();

	bool IsInternetCostFree();
}
