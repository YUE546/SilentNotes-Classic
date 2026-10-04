namespace SilentNotes.Services;

public interface IEnvironmentService
{
	OperatingSystem Os { get; }

	bool InDarkMode { get; }

	IKeepScreenOn KeepScreenOn { get; }

	IScreenshots Screenshots { get; }
}
