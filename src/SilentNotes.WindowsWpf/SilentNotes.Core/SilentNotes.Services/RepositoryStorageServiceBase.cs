using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using SilentNotes.Models;
using SilentNotes.Workers;

namespace SilentNotes.Services;

public abstract class RepositoryStorageServiceBase : IRepositoryStorageService
{
	public readonly string InstanceId = Guid.NewGuid().ToString();

	protected readonly IXmlFileService _xmlFileService;

	protected readonly ILanguageService _languageService;

	protected readonly INoteRepositoryUpdater _updater;

	private NoteRepositoryModel _cachedRepository;

	public RepositoryStorageServiceBase(IXmlFileService xmlFileService, ILanguageService languageService)
	{
		_xmlFileService = xmlFileService;
		_languageService = languageService;
		_updater = new NoteRepositoryUpdater();
	}

	public RepositoryStorageLoadResult LoadRepositoryOrDefault(out NoteRepositoryModel repositoryModel)
	{
		if (_cachedRepository != null)
		{
			repositoryModel = _cachedRepository;
			return RepositoryStorageLoadResult.SuccessfullyLoaded;
		}
		repositoryModel = null;
		bool flag = false;
		RepositoryStorageLoadResult result;
		try
		{
			string text = Path.Combine(GetLocation(), "silentnotes_repository.silentnotes");
			if (_xmlFileService.Exists(text))
			{
				if (!_xmlFileService.TryLoad(text, out var xml) && !TryRecoverRepositoryFromLegacyWriter(_xmlFileService, text, out xml))
				{
					throw new Exception("Invalid XML");
				}
				result = RepositoryStorageLoadResult.SuccessfullyLoaded;
				flag = _updater.Update(xml);
				repositoryModel = XmlUtils.DeserializeFromXmlDocument<NoteRepositoryModel>(xml);
			}
			else
			{
				result = RepositoryStorageLoadResult.CreatedNewEmptyRepository;
				repositoryModel = new NoteRepositoryModel();
				repositoryModel.Revision = 9;
				AddWelcomeNote(repositoryModel);
				flag = true;
			}
		}
		catch (Exception)
		{
			result = RepositoryStorageLoadResult.InvalidRepository;
			repositoryModel = NoteRepositoryModel.InvalidRepository;
			flag = false;
		}
		if (flag)
		{
			TrySaveRepository(repositoryModel);
		}
		_cachedRepository = repositoryModel;
		return result;
	}

	private bool TryRecoverRepositoryFromLegacyWriter(IXmlFileService xmlFileService, string xmlFilePath, out XDocument xml)
	{
		bool flag = false;
		xml = null;
		long minValidFileSize = 22L;
		if (FileExistsAndHasValidSize(xmlFilePath + ".old", minValidFileSize))
		{
			File.Copy(xmlFilePath + ".old", xmlFilePath, overwrite: true);
			flag = _xmlFileService.TryLoad(xmlFilePath, out xml);
		}
		if (!flag && FileExistsAndHasValidSize(xmlFilePath + ".new", minValidFileSize))
		{
			File.Copy(xmlFilePath + ".new", xmlFilePath, overwrite: true);
			flag = _xmlFileService.TryLoad(xmlFilePath, out xml);
		}
		return flag;
	}

	private static bool FileExistsAndHasValidSize(string filePath, long minValidFileSize)
	{
		if (!File.Exists(filePath))
		{
			return false;
		}
		long length = new FileInfo(filePath).Length;
		return length >= minValidFileSize;
	}

	public bool TrySaveRepository(NoteRepositoryModel repositoryModel)
	{
		if (NoteRepositoryModel.InvalidRepository == repositoryModel)
		{
			return false;
		}
		try
		{
			string filePath = Path.Combine(GetLocation(), "silentnotes_repository.silentnotes");
			bool flag = _xmlFileService.TrySerializeAndSave(filePath, repositoryModel);
			if (flag)
			{
				_cachedRepository = repositoryModel;
			}
			return flag;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public void ClearCache()
	{
		_cachedRepository = null;
	}

	public byte[] LoadRepositoryFile()
	{
		try
		{
			string path = Path.Combine(GetLocation(), "silentnotes_repository.silentnotes");
			return File.ReadAllBytes(path);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public bool TryLoadRepositoryFromFile(byte[] fileContent, out NoteRepositoryModel repositoryModel)
	{
		try
		{
			using (Stream stream = new MemoryStream(fileContent))
			{
				XDocument xDocument = XDocument.Load(stream, LoadOptions.None);
				_updater.Update(xDocument);
				repositoryModel = XmlUtils.DeserializeFromXmlDocument<NoteRepositoryModel>(xDocument);
			}
			return true;
		}
		catch (Exception)
		{
			repositoryModel = null;
			return false;
		}
	}

	public abstract string GetLocation();

	private void AddWelcomeNote(NoteRepositoryModel repositoryModel)
	{
		NoteModel[] collection = new NoteModel[3]
		{
			new NoteModel
			{
				HtmlContent = _languageService.LoadText("welcome_note"),
				BackgroundColorHex = "#fbf4c1"
			},
			new NoteModel
			{
				HtmlContent = _languageService.LoadText("welcome_note_2"),
				BackgroundColorHex = "#d9f8c8"
			},
			new NoteModel
			{
				HtmlContent = _languageService.LoadText("welcome_note_3"),
				BackgroundColorHex = "#d0f8f9",
				NoteType = NoteType.Checklist,
				Tags = new List<string> { _languageService.LoadText("welcome_note_tag") }
			}
		};
		repositoryModel.Notes.AddRange(collection);
	}
}
