using SilentNotes.Models;

namespace SilentNotes.Services;

public interface IRepositoryStorageService
{
	RepositoryStorageLoadResult LoadRepositoryOrDefault(out NoteRepositoryModel repositoryModel);

	bool TrySaveRepository(NoteRepositoryModel repositoryModel);

	void ClearCache();

	byte[] LoadRepositoryFile();

	bool TryLoadRepositoryFromFile(byte[] fileContent, out NoteRepositoryModel repository);

	string GetLocation();
}
