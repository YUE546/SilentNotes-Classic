using System;

namespace SilentNotes.WindowsWpf.Workers
{
    internal sealed class HtmlCompatibilityInspector
    {
        public bool CanEditWithoutConversion(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return true;

            return html.IndexOf("<script", StringComparison.OrdinalIgnoreCase) < 0
                && html.IndexOf("<iframe", StringComparison.OrdinalIgnoreCase) < 0
                && html.IndexOf("<object", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
