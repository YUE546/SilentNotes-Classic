using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface IFolderPickerService
{
	Task<bool> PickFolder();

	Task<bool> TrySaveFileToPickedFolder(string fileName, byte[] content);
}
