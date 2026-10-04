using System.Collections.Generic;
using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface IFilePickerService
{
	Task<bool> PickFile(IEnumerable<string> extensions = null);

	Task<byte[]> ReadPickedFile();
}
