using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using SilentNotes.Workers;

namespace SilentNotes.Models;

[XmlRoot(ElementName = "silentnotes")]
public class NoteRepositoryModel
{
	public const int CurrentSavingRevision = 9;

	public const int NewestSupportedRevision = 9;

	public readonly string InstanceId = Guid.NewGuid().ToString();

	public const string RepositoryFileName = "silentnotes_repository.silentnotes";

	public const string BackupFileMask = "silentnotes_*.slnbackup";

	public static NoteRepositoryModel InvalidRepository = new NoteRepositoryModel();

	private Guid _id;

	private NoteListModel _notes;

	private DeletedNoteListModel _deletedNotes;

	private SafeListModel _safes;

	[XmlAttribute(AttributeName = "id")]
	public Guid Id
	{
		get
		{
			return (_id != Guid.Empty) ? _id : (_id = Guid.NewGuid());
		}
		set
		{
			_id = value;
		}
	}

	[XmlAttribute(AttributeName = "revision")]
	public int Revision { get; set; }

	[XmlAttribute(AttributeName = "order_modified_at")]
	public DateTime OrderModifiedAt { get; set; }

	[XmlArray("notes")]
	[XmlArrayItem("note")]
	public NoteListModel Notes
	{
		get
		{
			return _notes ?? (_notes = new NoteListModel());
		}
		set
		{
			_notes = value;
		}
	}

	[XmlArray("deleted_notes")]
	[XmlArrayItem("deleted_note")]
	public DeletedNoteListModel DeletedNotes
	{
		get
		{
			return _deletedNotes ?? (_deletedNotes = new DeletedNoteListModel());
		}
		set
		{
			_deletedNotes = value;
		}
	}

	[XmlArray("safes")]
	[XmlArrayItem("safe")]
	public SafeListModel Safes
	{
		get
		{
			return _safes ?? (_safes = new SafeListModel());
		}
		set
		{
			_safes = value;
		}
	}

	public NoteRepositoryModel()
	{
		OrderModifiedAt = DateTime.UtcNow;
	}

	public void RefreshOrderModifiedAt()
	{
		OrderModifiedAt = DateTime.UtcNow;
	}

	public List<string> CollectActiveTags()
	{
		List<string> list = new List<string>();
		foreach (NoteModel note in Notes)
		{
			if (note.InRecyclingBin)
			{
				continue;
			}
			foreach (string tag in note.Tags)
			{
				if (!list.Contains(tag, StringComparer.InvariantCultureIgnoreCase))
				{
					list.Add(tag);
				}
			}
		}
		list.Sort(StringComparer.InvariantCultureIgnoreCase);
		return list;
	}

	public long GetModificationFingerprint()
	{
		List<long> list = new List<long>();
		list.Add(Id.GetHashCode());
		list.Add(Revision);
		list.Add(OrderModifiedAt.GetHashCode());
		foreach (NoteModel note in Notes)
		{
			list.Add(note.ModifiedAt.GetHashCode());
			if (note.MetaModifiedAt.HasValue)
			{
				list.Add(note.MetaModifiedAt.GetHashCode());
			}
			list.Add(note.InRecyclingBin.GetHashCode());
		}
		foreach (DeletedNoteModel deletedNote in DeletedNotes)
		{
			list.Add(deletedNote.Id.GetHashCode());
		}
		foreach (SafeModel safe in Safes)
		{
			list.Add(safe.ModifiedAt.GetHashCode());
			if (safe.MaintainedAt.HasValue)
			{
				list.Add(safe.MaintainedAt.GetHashCode());
			}
		}
		return ModificationDetector.CombineHashCodes(list, 0L);
	}

	public void RemoveUnusedSafes()
	{
		List<Guid> usedSafeIds = (from note in Notes
			where note.SafeId.HasValue
			select note.SafeId.Value).ToList();
		Safes.RemoveAll((SafeModel safe) => !usedSafeIds.Contains(safe.Id));
	}
}
