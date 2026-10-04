using System.Collections.Generic;

namespace SilentNotes.Models;

public class DeletedNoteModelIdComparer : IComparer<DeletedNoteModel>
{
	public int Compare(DeletedNoteModel x, DeletedNoteModel y)
	{
		return x.Id.CompareTo(y.Id);
	}
}
