using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface IClipboardService
{
	Task SetTextAsync(string text);
}
