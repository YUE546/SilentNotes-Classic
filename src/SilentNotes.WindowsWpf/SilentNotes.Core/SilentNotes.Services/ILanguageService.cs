using System;

namespace SilentNotes.Services;

public interface ILanguageService
{
	string this[string id] { get; }

	string LoadText(string id);

	string LoadTextFmt(string id, params object[] args);

	string FormatDateTime(DateTime dateTime, string format);
}
