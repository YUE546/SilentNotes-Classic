namespace SilentNotes.Services;

public interface INavigationService
{
	void NavigateTo(string uri, bool reload = false);

	void NavigateReload();
}
