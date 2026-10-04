using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace SilentNotes.Models;

[DebuggerDisplay("{Id} {DeletedAt}")]
public class DeletedNoteModel
{
	[XmlText]
	public Guid Id { get; set; }

	[XmlAttribute(AttributeName = "deleted_at")]
	public DateTime DeletedAt { get; set; }

	public DeletedNoteModel()
	{
		DeletedAt = DateTime.UtcNow;
	}

	public DeletedNoteModel(Guid id)
		: this()
	{
		Id = id;
	}

	public void CloneTo(DeletedNoteModel target)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		if (this != target)
		{
			target.Id = Id;
			target.DeletedAt = DeletedAt;
		}
	}

	public DeletedNoteModel Clone()
	{
		DeletedNoteModel deletedNoteModel = new DeletedNoteModel();
		CloneTo(deletedNoteModel);
		return deletedNoteModel;
	}
}
