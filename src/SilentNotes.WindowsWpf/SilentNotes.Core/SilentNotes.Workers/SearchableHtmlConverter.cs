using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace SilentNotes.Workers;

public class SearchableHtmlConverter
{
	private const char TagReplacement = '\u00a0';

	private readonly Regex _stripTagsRegex;

	public SearchableHtmlConverter()
	{
		_stripTagsRegex = new Regex("<[^>]+>", RegexOptions.None);
	}

	public bool TryConvertHtml(string html, out string searchableText)
	{
		searchableText = string.Empty;
		if (string.IsNullOrWhiteSpace(html))
		{
			return true;
		}
		try
		{
			string s = _stripTagsRegex.Replace(html, '\u00a0'.ToString());
			string text = HttpUtility.HtmlDecode(s);
			searchableText = NormalizeWhitespaces(text);
			return true;
		}
		catch (Exception)
		{
			searchableText = html;
			return false;
		}
	}

	public static string NormalizeWhitespaces(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		foreach (char c in text)
		{
			if (char.IsWhiteSpace(c))
			{
				if (c != '\u00a0')
				{
					flag2 = true;
				}
				else if (flag)
				{
					flag3 = true;
				}
				continue;
			}
			flag = true;
			if (flag2 || flag3)
			{
				stringBuilder.Append(" ");
			}
			stringBuilder.Append(c);
			flag2 = false;
			flag3 = false;
		}
		if (flag2)
		{
			stringBuilder.Append(" ");
		}
		return stringBuilder.ToString();
	}
}
