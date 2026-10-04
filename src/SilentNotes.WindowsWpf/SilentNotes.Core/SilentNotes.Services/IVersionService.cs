namespace SilentNotes.Services;

public interface IVersionService
{
	string GetApplicationVersion(string format = "{0}.{1}.{2}");
}
