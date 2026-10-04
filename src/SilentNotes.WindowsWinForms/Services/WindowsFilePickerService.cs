using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SilentNotes.Services;
using Forms = System.Windows.Forms;

namespace SilentNotes.WindowsWinForms.Services
{
    internal sealed class WindowsFilePickerService : IFilePickerService
    {
        private string _pickedFilePath;

        public Task<bool> PickFile(IEnumerable<string> extensions = null)
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                dialog.Filter = BuildFilter(extensions);

                bool result = dialog.ShowDialog() == Forms.DialogResult.OK;
                _pickedFilePath = result ? dialog.FileName : null;
                return Task.FromResult(result);
            }
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
