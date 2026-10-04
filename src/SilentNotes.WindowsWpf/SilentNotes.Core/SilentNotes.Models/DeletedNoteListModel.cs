using System;
using System.Collections.Generic;

namespace SilentNotes.Models;

public class DeletedNoteListModel : List<DeletedNoteModel>
{
	public DeletedNoteModel FindById(Guid id)
	{
		return Find((DeletedNoteModel item) => item.Id == id);
	}

	public void AddIdOrRefreshDeletedAt(Guid id)
	{
		DeletedNoteModel deletedNoteModel = FindById(id);
		if (deletedNoteModel != null)
		{
			deletedNoteModel.DeletedAt = DateTime.UtcNow;
		}
		else
		{
			Add(new DeletedNoteModel(id));
		}
	}
}
