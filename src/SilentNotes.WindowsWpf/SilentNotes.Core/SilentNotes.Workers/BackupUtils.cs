using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SilentNotes.Services;

namespace SilentNotes.Workers;

public static class BackupUtils
{
	public static async Task CreateBackup(IFolderPickerService folderPickerService, IRepositoryStorageService repositoryService)
	{
		if (await folderPickerService.PickFolder())
		{
			CompressUtils.CompressEntry repositoryEntry = new CompressUtils.CompressEntry
			{
				Name = "silentnotes_repository.silentnotes",
				Data = repositoryService.LoadRepositoryFile()
			};
			await folderPickerService.TrySaveFileToPickedFolder(content: CompressUtils.CreateZipArchive(new CompressUtils.CompressEntry[1] { repositoryEntry }), fileName: CreateBackupFileName());
		}
	}

	private static string CreateBackupFileName()
	{
		string newValue = DateTime.Now.ToString("yyyyMMdd-HHmmss");
		return "silentnotes_*.slnbackup".Replace("*", newValue);
	}

	public static async Task<bool> TryRestoreBackup(IFilePickerService filePickerService, IRepositoryStorageService repositoryService)
	{
		string[] extensions = new string[1] { Path.GetExtension("silentnotes_*.slnbackup") };
		if (await filePickerService.PickFile(extensions))
		{
			try
			{
				List<CompressUtils.CompressEntry> repositoryEntries = CompressUtils.OpenZipArchive(await filePickerService.ReadPickedFile());
				CompressUtils.CompressEntry repositoryEntry = repositoryEntries.Find((CompressUtils.CompressEntry item) => "silentnotes_repository.silentnotes".Equals(item.Name, StringComparison.InvariantCultureIgnoreCase));
				if (repositoryService.TryLoadRepositoryFromFile(repositoryEntry.Data, out var noteRepository) && noteRepository.Notes.Count > 0)
				{
					repositoryService.TrySaveRepository(noteRepository);
					return true;
				}
			}
			catch (Exception)
			{
			}
			return false;
		}
		return true;
	}
}
