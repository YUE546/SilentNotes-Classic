using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsRepositoryStorageService : RepositoryStorageServiceBase
    {
        public WindowsRepositoryStorageService(IXmlFileService xmlFileService, ILanguageService languageService)
            : base(xmlFileService, languageService)
        {
        }

        public override string GetLocation()
        {
            return WindowsDataDirectoryService.GetEffectiveDirectory();
        }
    }
}
