using SilentNotes.Models;

namespace SilentNotes.Services;

public interface ISettingsService
{
	SettingsModel LoadSettingsOrDefault();

	bool TrySaveSettingsToLocalDevice(SettingsModel model);
}
