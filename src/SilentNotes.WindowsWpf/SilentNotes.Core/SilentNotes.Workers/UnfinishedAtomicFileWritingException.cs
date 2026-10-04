using System;

namespace SilentNotes.Workers;

public class UnfinishedAtomicFileWritingException : Exception
{
	public string FilePath { get; }

	public UnfinishedAtomicFileWritingException(string filePath)
		: base(CreateErrorMessage(filePath))
	{
		FilePath = filePath;
	}

	private static string CreateErrorMessage(string filePath)
	{
		return $"The file '{filePath}' cannot be written, because there is an unfinished file writing operation pending.";
	}
}
