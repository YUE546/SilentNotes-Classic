namespace SilentNotes.Services;

public interface ILanguageTestService
{
	void OverrideWithTestResourceFile(byte[] customResourceFile);

	void SetAlwaysEnglish(bool alwaysEnglish);
}
