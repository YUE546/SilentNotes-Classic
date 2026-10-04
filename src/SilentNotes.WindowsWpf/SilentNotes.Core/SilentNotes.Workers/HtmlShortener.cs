using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SilentNotes.Workers;

public class HtmlShortener
{
	private class TagInfo
	{
		public string Name { get; set; }

		public Match StartTag { get; set; }

		public Match EndTag { get; set; }

		public int StartTagPosition => StartTag.Index;

		public int ContentLength => (EndTag != null) ? (EndTag.Index - StartTag.Index - StartTag.Length) : 0;

		public int TagLength => (EndTag != null) ? (EndTag.Index + EndTag.Length - StartTag.Index) : 0;
	}

	private enum TagType
	{
		Opening,
		Closing,
		OpeningAndClosing,
		DocType
	}

	private static char[] _tagMarkupCharacters = new char[5] { '<', '>', '/', ' ', '\t' };

	private static Regex _findTagsRegex;

	private static string[] _emptyTagNames = new string[15]
	{
		"area", "base", "br", "col", "embed", "hr", "img", "input", "keygen", "link",
		"meta", "param", "source", "track", "wbr"
	};

	private static string[] _relevantTagNames = new string[12]
	{
		"blockquote", "dd", "div", "dt", "h1", "h2", "h3", "h4", "li", "p",
		"pre", "table"
	};

	private static string _linkTagName = "a";

	public int MinimumLengthForShortening { get; set; }

	public int WantedTagNumber { get; set; }

	public int WantedLength { get; set; }

	public HtmlShortener()
	{
		MinimumLengthForShortening = 400;
		WantedTagNumber = 8;
		WantedLength = 400;
	}

	public string Shorten(string content)
	{
		if (content == null || content.Length <= MinimumLengthForShortening)
		{
			return content;
		}
		Stack<TagInfo> branchToLastItem = new Stack<TagInfo>();
		IEnumerator<Match> enumerator = EnumerateTags(content).GetEnumerator();
		TagInfo lastItem = FindLastItemInsideLimits(enumerator, branchToLastItem);
		return BuildShortenedContent(content, lastItem, branchToLastItem);
	}

	public string DisableLinks(string content)
	{
		if (content == null)
		{
			return content;
		}
		List<Match> list = EnumerateTags(content).ToList();
		string[] array = list.Select((Match tag) => GetLowerCaseTagName(tag.Value)).ToArray();
		if (!array.Contains(_linkTagName))
		{
			return content;
		}
		StringBuilder stringBuilder = new StringBuilder(content);
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (array[num] == _linkTagName)
			{
				Match match = list[num];
				int num2 = match.Index + match.Value.IndexOf(_linkTagName);
				stringBuilder.Remove(num2, _linkTagName.Length);
				stringBuilder.Insert(num2, "span");
			}
		}
		return stringBuilder.ToString();
	}

	private static IEnumerable<Match> EnumerateTags(string content)
	{
		Regex htmlTagRegex = GetOrCreateRegex();
		Match foundTag = htmlTagRegex.Match(content, 0);
		while (foundTag.Success)
		{
			yield return foundTag;
			foundTag = htmlTagRegex.Match(content, foundTag.Index + foundTag.Length);
		}
	}

	private TagInfo FindLastItemInsideLimits(IEnumerator<Match> tagEnumerator, Stack<TagInfo> branchToLastItem)
	{
		TagInfo tagInfo = null;
		int num = 0;
		int num2 = 0;
		while (num < WantedTagNumber && num2 < WantedLength && tagEnumerator.MoveNext())
		{
			Match current = tagEnumerator.Current;
			if (string.IsNullOrEmpty(current.Value))
			{
				continue;
			}
			string lowerCaseTagName = GetLowerCaseTagName(current.Value);
			TagType tagType = GetTagType(current.Value);
			if (IsEmptyTag(lowerCaseTagName) || tagType == TagType.OpeningAndClosing)
			{
				if (IsRelevantTag(lowerCaseTagName))
				{
					num++;
				}
				continue;
			}
			switch (tagType)
			{
			case TagType.Opening:
			{
				TagInfo item = new TagInfo
				{
					Name = lowerCaseTagName,
					StartTag = current
				};
				branchToLastItem.Push(item);
				break;
			}
			case TagType.Closing:
				if (branchToLastItem.Count != 0 && string.Equals(lowerCaseTagName, branchToLastItem.Peek().Name))
				{
					tagInfo = branchToLastItem.Pop();
					tagInfo.EndTag = current;
					if (IsRelevantTag(lowerCaseTagName))
					{
						num++;
						num2 += tagInfo.ContentLength;
					}
				}
				break;
			}
		}
		return tagInfo;
	}

	private static string BuildShortenedContent(string content, TagInfo lastItem, Stack<TagInfo> branchToLastItem)
	{
		if (lastItem == null)
		{
			return content;
		}
		string text = content.Substring(0, lastItem.StartTagPosition + lastItem.TagLength);
		if (branchToLastItem.Count == 0)
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder();
		while (branchToLastItem.Count > 0)
		{
			TagInfo tagInfo = branchToLastItem.Pop();
			stringBuilder.Append("</");
			stringBuilder.Append(tagInfo.Name);
			stringBuilder.Append(">");
		}
		return text + stringBuilder.ToString();
	}

	private static string GetLowerCaseTagName(string tag)
	{
		if (string.IsNullOrEmpty(tag))
		{
			return string.Empty;
		}
		string text = tag.Trim(_tagMarkupCharacters);
		int num = text.IndexOfAny(_tagMarkupCharacters);
		if (num > 0)
		{
			text = text.Remove(num);
		}
		return text.ToLowerInvariant();
	}

	private static bool IsRelevantTag(string tagName)
	{
		return Array.BinarySearch(_relevantTagNames, tagName) >= 0;
	}

	private static bool IsEmptyTag(string tagName)
	{
		return Array.BinarySearch(_emptyTagNames, tagName) >= 0;
	}

	private static TagType GetTagType(string tag)
	{
		tag = tag.Replace(" ", "");
		if (tag.StartsWith("</"))
		{
			return TagType.Closing;
		}
		if (tag.EndsWith("/>"))
		{
			return TagType.OpeningAndClosing;
		}
		if (tag.StartsWith("<!"))
		{
			return TagType.DocType;
		}
		return TagType.Opening;
	}

	private static Regex GetOrCreateRegex()
	{
		return _findTagsRegex ?? (_findTagsRegex = new Regex("<[^>]+>", RegexOptions.Compiled));
	}
}
