using System;
using System.Collections.Generic;
using System.Linq;

namespace SilentNotes.Workers;

public class NoteFilter
{
	public enum FilterOptions
	{
		FilterByTagList,
		NotesWithoutTags
	}

	private readonly string _pattern;

	private readonly HashSet<string> _userDefinedTags;

	private readonly FilterOptions _options;

	public NoteFilter(string pattern, IEnumerable<string> userDefinedTags, FilterOptions options)
	{
		_pattern = pattern;
		if (userDefinedTags == null)
		{
			userDefinedTags = new string[0];
		}
		_userDefinedTags = new HashSet<string>(userDefinedTags.Where((string tag) => !string.IsNullOrWhiteSpace(tag)), StringComparer.InvariantCultureIgnoreCase);
		_options = options;
	}

	public bool ContainsPattern(string searchableNoteContent)
	{
		if (string.IsNullOrEmpty(_pattern))
		{
			return true;
		}
		if (!string.IsNullOrEmpty(searchableNoteContent) && searchableNoteContent.IndexOf(_pattern, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		return false;
	}

	public bool MatchTags(List<string> noteTags)
	{
		switch (_options)
		{
		case FilterOptions.FilterByTagList:
		{
			if (_userDefinedTags.Count == 0)
			{
				return true;
			}
			if (noteTags == null || !noteTags.Any())
			{
				return false;
			}
			int num = 0;
			foreach (string noteTag in noteTags)
			{
				if (_userDefinedTags.Contains(noteTag))
				{
					num++;
				}
				if (num >= _userDefinedTags.Count)
				{
					return true;
				}
			}
			return false;
		}
		case FilterOptions.NotesWithoutTags:
			return noteTags == null || !noteTags.Any();
		default:
			throw new ArgumentOutOfRangeException("FilterOptions");
		}
	}
}
