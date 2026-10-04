using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SilentNotes.WindowsWpf.Workers
{
    internal sealed class ChecklistHtmlConverter
    {
        public string ToHtml(IEnumerable<string> items)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<ul data-type=\"taskList\">");
            foreach (string item in items)
            {
                builder.Append("<li data-type=\"taskItem\" data-checked=\"false\"><p>")
                    .Append(WebUtility.HtmlEncode(item))
                    .Append("</p></li>");
            }
            builder.Append("</ul>");
            return builder.ToString();
        }
    }
}
