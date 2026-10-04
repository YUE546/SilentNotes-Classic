using System;
using System.IO;
using System.Threading.Tasks;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsLanguageServiceResourceReader : ILanguageServiceResourceReader
    {
        public Task<Stream> TryOpenResourceStream(string domain, string languageCode)
        {
            string fileName = string.Format("Lng.{0}.{1}", domain, languageCode);
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization", fileName);

            if (!File.Exists(filePath))
                return Task.FromResult<Stream>(null);

            return Task.FromResult<Stream>(File.OpenRead(filePath));
        }
    }
}
