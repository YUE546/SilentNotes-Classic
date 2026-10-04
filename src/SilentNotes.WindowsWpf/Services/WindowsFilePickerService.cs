using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsFilePickerService : IFilePickerService
    {
        private string _pickedFilePath;

        public Task<bool> PickFile(IEnumerable<string> extensions = null)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Multiselect = false,
                Filter = BuildFilter(extensions),
            };

            bool result = dialog.ShowDialog() == true;
            _pickedFilePath = result ? dialog.FileName : null;
            return Task.FromResult(result);
        }

        public Task<byte[]> ReadPickedFile()
        {
            if (string.IsNullOrEmpty(_pickedFilePath) || !File.Exists(_pickedFilePath))
                return Task.FromResult<byte[]>(null);

            return Task.FromResult(File.ReadAllBytes(_pickedFilePath));
        }

        private static string BuildFilter(IEnumerable<string> extensions)
        {
            string[] normalizedExtensions = extensions?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.StartsWith(".") ? item : "." + item)
                .Distinct()
                .ToArray();

            if ((normalizedExtensions == null) || (normalizedExtensions.Length == 0))
                return "All files (*.*)|*.*";

            string pattern = string.Join(";", normalizedExtensions.Select(item => "*" + item));
            return string.Format("Supported files ({0})|{0}|All files (*.*)|*.*", pattern);
        }
    }
}
