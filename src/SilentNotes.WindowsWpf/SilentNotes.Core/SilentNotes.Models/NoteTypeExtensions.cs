using System;

namespace SilentNotes.Models;

public static class NoteTypeExtensions
{
	public static string GetRouteName(this NoteType noteType)
	{
		return noteType switch
		{
			NoteType.Text => "/note", 
			NoteType.Checklist => "/checklist", 
			_ => throw new ArgumentOutOfRangeException("NoteType"), 
		};
	}
}
