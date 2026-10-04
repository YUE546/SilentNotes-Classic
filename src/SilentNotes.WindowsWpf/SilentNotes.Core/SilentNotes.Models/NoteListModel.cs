using System;
using System.Collections.Generic;

namespace SilentNotes.Models;

public class NoteListModel : List<NoteModel>
{
	public int IndexOfById(Guid id)
	{
		for (int i = 0; i < base.Count; i++)
		{
			if (id == base[i].Id)
			{
				return i;
			}
		}
		return -1;
	}

	public bool ContainsById(Guid id)
	{
		int num = IndexOfById(id);
		return num >= 0;
	}

	public NoteModel FindById(Guid id)
	{
		return Find((NoteModel item) => item.Id == id);
	}

	public int IndexOfFirstUnpinnedNote()
	{
		for (int i = 0; i < base.Count; i++)
		{
			if (!base[i].IsPinned)
			{
				return i;
			}
		}
		return -1;
	}
}
