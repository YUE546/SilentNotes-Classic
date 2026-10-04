using System.Diagnostics;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsNativeBrowserService : INativeBrowserService
    {
        public void OpenWebsite(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public void OpenWebsiteInApp(string url)
        {
            OpenWebsite(url);
        }
    }
}
