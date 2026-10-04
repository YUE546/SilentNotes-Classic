namespace SilentNotes.Services;

public interface INativeBrowserService
{
	void OpenWebsite(string url);

	void OpenWebsiteInApp(string url);
}
