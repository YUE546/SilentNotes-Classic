using System;
using SilentNotes.Models;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsSettingsService : SettingsServiceBase
    {
        public WindowsSettingsService(IXmlFileService xmlFileService, IDataProtectionService dataProtectionService)
            : base(xmlFileService, dataProtectionService)
        {
        }

        protected override string GetDirectoryPath()
        {
            return WindowsDataDirectoryService.GetEffectiveDirectory();
        }

        public override bool TrySaveSettingsToLocalDevice(SettingsModel model)
        {
            string newPath = model.DataDirectory;
            if (!string.IsNullOrEmpty(newPath))
            {
                WindowsDataDirectoryService.WriteToRegistry(newPath);
            }

            return base.TrySaveSettingsToLocalDevice(model);
        }
    }
}
