using System.IO;
using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface ILanguageServiceResourceReader
{
	Task<Stream> TryOpenResourceStream(string domain, string languageCode);
}
