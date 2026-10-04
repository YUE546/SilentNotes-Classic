using System;
using System.Threading.Tasks;

namespace SilentNotes;

public class RelayMarkdownConverter : IMarkdownConverter
{
	private readonly Func<string, System.Threading.Tasks.ValueTask<string>> _htmlToMarkdown;

	private readonly Func<string, System.Threading.Tasks.ValueTask<string>> _markdownToHtml;

	public RelayMarkdownConverter(Func<string, System.Threading.Tasks.ValueTask<string>> htmlToMarkdown, Func<string, System.Threading.Tasks.ValueTask<string>> markdownToHtml)
	{
		_htmlToMarkdown = htmlToMarkdown;
		_markdownToHtml = markdownToHtml;
	}

	public System.Threading.Tasks.ValueTask<string> HtmlToMarkdown(string html)
	{
		return _htmlToMarkdown(html);
	}

	public System.Threading.Tasks.ValueTask<string> MarkdownToHtml(string markdown)
	{
		return _markdownToHtml(markdown);
	}
}
