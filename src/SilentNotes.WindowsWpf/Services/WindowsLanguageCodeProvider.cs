using System.Globalization;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsLanguageCodeProvider : ILanguageCodeProvider
    {
        public string GetSystemLanguageCode()
        {
            string name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return string.IsNullOrWhiteSpace(name) ? "en" : name.ToLowerInvariant();
        }
    }
}
