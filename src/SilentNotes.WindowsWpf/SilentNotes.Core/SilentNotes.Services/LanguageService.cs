using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SilentNotes.Services;

public class LanguageService : ILanguageService, ILanguageTestService
{
	private readonly ILanguageServiceResourceReader _resourceReader;

	private readonly string _domain;

	private readonly string _languageCode;

	private readonly string _fallbackLanguageCode;

	private Dictionary<string, string> _textResources;

	private bool _alwaysEnglish;

	public string this[string id] => LoadText(id);

	public LanguageService(ILanguageServiceResourceReader resourceReader, string domain, string languageCode, string fallbackLanguageCode = "en")
	{
		_resourceReader = resourceReader;
		_domain = domain;
		_languageCode = languageCode;
		_fallbackLanguageCode = fallbackLanguageCode;
		_alwaysEnglish = false;
	}

	public string LoadText(string id)
	{
		if (LazyLoadTextResources() && _textResources.TryGetValue(id, out var value))
		{
			return value;
		}
		if (Debugger.IsAttached)
		{
			throw new Exception($"Could not find text resource {id}");
		}
		return "Translated text not found";
	}

	public string LoadTextFmt(string id, params object[] args)
	{
		string text = LoadText(id);
		try
		{
			return string.Format(text, args);
		}
		catch
		{
			return text;
		}
	}

	public string FormatDateTime(DateTime dateTime, string format)
	{
		CultureInfo cultureInfo;
		try
		{
			cultureInfo = CultureInfo.GetCultureInfo(_languageCode);
		}
		catch (Exception)
		{
			cultureInfo = CultureInfo.InvariantCulture;
		}
		return dateTime.ToString(format, cultureInfo);
	}

	private bool LazyLoadTextResources()
	{
		if (_textResources == null)
		{
			try
			{
				_textResources = LoadTextResources(_domain, _languageCode);
			}
			catch (Exception)
			{
				_textResources = null;
			}
		}
		if (_textResources == null)
		{
			_textResources = LoadTextResources(_domain, _fallbackLanguageCode);
		}
		return _textResources != null;
	}

	private Dictionary<string, string> LoadTextResources(string domain, string languageCode)
	{
		Dictionary<string, string> result = null;
		if (_alwaysEnglish)
		{
			languageCode = "en";
		}
		Stream result2 = Task.Run(async () => await _resourceReader.TryOpenResourceStream(domain, languageCode)).Result;
		if (result2 != null)
		{
			using (result2)
			{
				using StreamReader languageResourceStream = new StreamReader(result2);
				result = ReadFromStream(languageResourceStream);
			}
		}
		return result;
	}

	internal Dictionary<string, string> ReadFromStream(StreamReader languageResourceStream)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		string line;
		while ((line = languageResourceStream.ReadLine()) != null)
		{
			if (!IsComment(line) && TrySplitLine(line, out var key, out var text))
			{
				text = (dictionary[key] = ReplaceSpecialTags(text));
			}
		}
		return dictionary;
	}

	private static bool IsComment(string line)
	{
		return line.TrimStart().StartsWith("//");
	}

	private static bool TrySplitLine(string line, out string key, out string text)
	{
		key = null;
		text = null;
		if (line == null)
		{
			return false;
		}
		line = line.Trim();
		int num = line.IndexOf('=');
		if (num < 1)
		{
			return false;
		}
		key = line.Substring(0, num).TrimEnd();
		text = line.Substring(num + 1).TrimStart();
		return true;
	}

	protected static string ReplaceSpecialTags(string resText)
	{
		string text = resText;
		if (text.Contains("\\n"))
		{
			text = text.Replace("\\r\\n", "\r\n");
			text = text.Replace("\\n", "\r\n");
		}
		return text;
	}

	public void OverrideWithTestResourceFile(byte[] customResourceFile)
	{
		_textResources = null;
		LazyLoadTextResources();
		using MemoryStream stream = new MemoryStream(customResourceFile);
		using StreamReader streamReader = new StreamReader(stream);
		string line;
		while ((line = streamReader.ReadLine()) != null)
		{
			if (!IsComment(line) && TrySplitLine(line, out var key, out var text) && _textResources.ContainsKey(key))
			{
				text = ReplaceSpecialTags(text);
				if (text.Length > 1000)
				{
					text.Substring(0, 1000);
				}
				_textResources[key] = text;
			}
		}
	}

	public void SetAlwaysEnglish(bool alwaysEnglish)
	{
		if (alwaysEnglish != _alwaysEnglish)
		{
			_alwaysEnglish = alwaysEnglish;
			_textResources = null;
		}
	}
}
