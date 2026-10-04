using System.Net;
using System.Text;
using System.Xml.Linq;

namespace SilentNotes.Workers;

public class NoteRepositoryUpdater : INoteRepositoryUpdater
{
	private readonly int _newestSupportedRevision;

	public NoteRepositoryUpdater()
		: this(9)
	{
	}

	internal NoteRepositoryUpdater(int newestSupportedRevision)
	{
		_newestSupportedRevision = newestSupportedRevision;
	}

	public bool IsTooNewForThisApp(XDocument repository)
	{
		XElement root = repository.Root;
		XAttribute xAttribute = root.Attribute("revision");
		int num = int.Parse(xAttribute.Value);
		return num > _newestSupportedRevision;
	}

	public bool Update(XDocument repository)
	{
		XElement root = repository.Root;
		XAttribute xAttribute = root.Attribute("revision");
		int num = int.Parse(xAttribute.Value);
		if (num <= 1)
		{
			UpdateRepositoryFrom1To2(root);
		}
		bool flag = num < 9;
		if (flag)
		{
			root.SetAttributeValue("revision", 9);
		}
		return flag;
	}

	private void UpdateRepositoryFrom1To2(XElement root)
	{
		StringBuilder stringBuilder = new StringBuilder();
		XElement xElement = root.Element("notes");
		foreach (XElement item in xElement.Elements())
		{
			stringBuilder.Clear();
			XElement xElement2 = item.Element("title");
			string value = xElement2?.Value;
			XElement xElement3 = item.Element("content");
			string value2 = xElement3?.Value;
			if (!string.IsNullOrWhiteSpace(value))
			{
				value = WebUtility.HtmlEncode(value);
				stringBuilder.Append("<h1>");
				stringBuilder.Append(value);
				stringBuilder.Append("</h1>");
			}
			if (!string.IsNullOrWhiteSpace(value2))
			{
				value2 = WebUtility.HtmlEncode(value2);
				value2 = value2.Replace("\n", "</p><p>");
				stringBuilder.Append("<p>");
				stringBuilder.Append(value2);
				stringBuilder.Append("</p>");
			}
			xElement2?.Remove();
			xElement3?.Remove();
			item.Add(new XElement("html_content", stringBuilder.ToString()));
		}
	}
}
