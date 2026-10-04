using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsEnvironmentService : IEnvironmentService
    {
        public OperatingSystem Os => OperatingSystem.Windows;

        public bool InDarkMode => false;

        public IKeepScreenOn KeepScreenOn => null;

        public IScreenshots Screenshots => null;
    }
}
