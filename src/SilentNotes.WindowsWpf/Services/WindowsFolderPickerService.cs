using System.IO;
using System.Threading.Tasks;
using SilentNotes.Services;
using Forms = System.Windows.Forms;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsFolderPickerService : IFolderPickerService
    {
        private string _pickedFolderPath;

        public Task<bool> PickFolder()
        {
            using (Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog())
            {
                Forms.DialogResult result = dialog.ShowDialog();
                _pickedFolderPath = result == Forms.DialogResult.OK ? dialog.SelectedPath : null;
                return Task.FromResult(result == Forms.DialogResult.OK);
            }
        }

        public Task<bool> TrySaveFileToPickedFolder(string fileName, byte[] content)
        {
            if (string.IsNullOrEmpty(_pickedFolderPath) || !Directory.Exists(_pickedFolderPath))
                return Task.FromResult(false);

            try
            {
                string filePath = Path.Combine(_pickedFolderPath, Path.GetFileName(fileName));
                File.WriteAllBytes(filePath, content ?? new byte[0]);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
