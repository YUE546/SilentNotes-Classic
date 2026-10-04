using System;

namespace SilentNotes;

internal class BringNoteIntoViewMessage
{
	public Guid NoteId { get; }

	public bool Smooth { get; }

	public BringNoteIntoViewMessage(Guid noteId, bool smooth)
	{
		NoteId = noteId;
		Smooth = smooth;
	}
}
