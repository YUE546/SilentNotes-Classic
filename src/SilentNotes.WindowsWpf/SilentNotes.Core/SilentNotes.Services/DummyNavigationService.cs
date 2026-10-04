namespace SilentNotes.Services;

public class DummyNavigationService : INavigationService
{
	public void NavigateTo(string uri, bool reload = false)
	{
	}

	public void NavigateReload()
	{
	}
}
