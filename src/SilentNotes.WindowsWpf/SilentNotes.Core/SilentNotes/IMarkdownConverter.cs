using System.Threading.Tasks;

namespace SilentNotes;

public interface IMarkdownConverter
{
	System.Threading.Tasks.ValueTask<string> MarkdownToHtml(string markdown);

	System.Threading.Tasks.ValueTask<string> HtmlToMarkdown(string html);
}
