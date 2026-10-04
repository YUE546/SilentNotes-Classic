using System;
using System.Collections.Generic;

namespace SilentNotes.Models;

public static class NoteListModelExtensions
{
	public static int IndexToInsertNewNote(this List<NoteModel> notes, NoteInsertionMode insertionMode)
	{
		switch (insertionMode)
		{
		case NoteInsertionMode.AtTop:
		{
			int num = notes.FindLastIndex((NoteModel note) => note.IsPinned);
			if (num >= 0)
			{
				return num + 1;
			}
			return 0;
		}
		case NoteInsertionMode.AtBottom:
			return notes.Count;
		default:
			throw new ArgumentOutOfRangeException("insertionMode");
		}
	}
}
