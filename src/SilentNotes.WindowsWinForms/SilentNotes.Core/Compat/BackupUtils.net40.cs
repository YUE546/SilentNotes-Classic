// Compat copy of SilentNotes.AllPlatforms\Workers\BackupUtils.cs for .NET 4.0.
// The original uses async/await (needs Microsoft.Bcl.Async on net40). This copy keeps the
// same behaviour with ContinueWith on the captured synchronization context, so the file/folder
// picker dialogs are still invoked from the UI thread.
using System;
using System.Threading.Tasks;
using SilentNotes.Models;
using SilentNotes.Services;

namespace SilentNotes.Workers
{
    /// <summary>
    /// Contains functions to create and restore a backup.
    /// </summary>
    public static class BackupUtils
    {
        /// <summary>
        /// Lets the user pick an output folder and saves a backup to the selected folder.
        /// </summary>
        /// <param name="folderPickerService">Service which can let the user pick a folder.</param>
        /// <param name="repositoryService">Service which can load the repository.</param>
        /// <returns>Task for async calls.</returns>
        public static Task CreateBackup(IFolderPickerService folderPickerService, IRepositoryStorageService repositoryService)
        {
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            return folderPickerService.PickFolder().ContinueWith(t =>
            {
                if (t.Result)
                {
                    // Create a zip file, so that in future, attachements can be added as well.
                    CompressUtils.CompressEntry repositoryEntry = new CompressUtils.CompressEntry
                    {
                        Name = NoteRepositoryModel.RepositoryFileName,
                        Data = repositoryService.LoadRepositoryFile()
                    };

                    byte[] zipArchiveContent = CompressUtils.CreateZipArchive(new[] { repositoryEntry });
                    return folderPickerService.TrySaveFileToPickedFolder(
                        CreateBackupFileName(), zipArchiveContent);
                }
                return CompletedTask();
            }, uiScheduler).Unwrap();
        }

        private static string CreateBackupFileName()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            return NoteRepositoryModel.BackupFileMask.Replace("*", datePart);
        }

        /// <summary>
        /// Lets the user pick a backup file and loads this backup as the current repository.
        /// </summary>
        /// <remarks>
        /// For safety reasons, repositories with no notes are rejected. This should rule out any
        /// valid xml file which isn't a valid repository.
        /// </remarks>
        /// <param name="filePickerService">Service which can let the user pick a backup file.</param>
        /// <param name="repositoryService">Service which can store the repository.</param>
        /// <returns>Returns false if the selected backup file was invalid, returns true if the
        /// backup was loaded successfully or the user canceled loading of the backup.</returns>
        public static Task<bool> TryRestoreBackup(IFilePickerService filePickerService, IRepositoryStorageService repositoryService)
        {
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            string[] extensions = new[] { Path.GetExtension(NoteRepositoryModel.BackupFileMask) };
            return filePickerService.PickFile(extensions).ContinueWith(t =>
            {
                if (t.Result)
                {
                    return filePickerService.ReadPickedFile().ContinueWith(t2 =>
                    {
                        try
                        {
                            byte[] fileContent = t2.Result;
                            var repositoryEntries = CompressUtils.OpenZipArchive(fileContent);
                            var repositoryEntry = repositoryEntries.Find(item => NoteRepositoryModel.RepositoryFileName.Equals(item.Name, StringComparison.InvariantCultureIgnoreCase));

                            NoteRepositoryModel noteRepository;
                            if ((repositoryService.TryLoadRepositoryFromFile(repositoryEntry.Data, out noteRepository)) &&
                                (noteRepository.Notes.Count > 0))
                            {
                                repositoryService.TrySaveRepository(noteRepository);
                                return true;
                            }
                        }
                        catch (Exception)
                        { // returns false
                        }
                        return false;
                    }, uiScheduler);
                }
                else
                {
                    return CompletedTask(true); // User canceled file selection, this is no error
                }
            }, uiScheduler).Unwrap();
        }

        private static Task CompletedTask()
        {
            TaskCompletionSource<object> result = new TaskCompletionSource<object>();
            result.SetResult(null);
            return result.Task;
        }

        private static Task<bool> CompletedTask(bool value)
        {
            TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();
            result.SetResult(value);
            return result.Task;
        }
    }
}
