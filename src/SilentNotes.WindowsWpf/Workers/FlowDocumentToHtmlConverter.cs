using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SilentNotes.WindowsWpf.Workers
{
    internal sealed class FlowDocumentToHtmlConverter
    {
        public string Convert(FlowDocument document)
        {
            StringBuilder builder = new StringBuilder();
            List<Block> blocks = new List<Block>();
            foreach (Block block in document.Blocks)
                blocks.Add(block);

            for (int i = 0; i < blocks.Count; i++)
            {
                bool unusedChecked;
                if (IsChecklistParagraph(blocks[i], out unusedChecked))
                {
                    bool isChecked;
                    while (i < blocks.Count && IsChecklistParagraph(blocks[i], out isChecked))
                    {
                        Paragraph p = (Paragraph)blocks[i];
                        string text = ExtractChecklistText(p);
                        if (isChecked)
                            builder.Append("<p class=\"done\">")
                                .Append(WebUtility.HtmlEncode(text))
                                .Append("</p>");
                        else
                            builder.Append("<p>")
                                .Append(WebUtility.HtmlEncode(text))
                                .Append("</p>");
                        i++;
                    }
                    i--;
                }
                else
                {
                    AppendBlock(builder, blocks[i]);
                }
            }

            return builder.ToString();
        }

        private static bool IsChecklistParagraph(Block block, out bool isChecked)
        {
            Paragraph p = block as Paragraph;
            if (p == null)
            {
                isChecked = false;
                return false;
            }
            string tag = p.Tag as string;
            if (tag != "checklist" && tag != "checklist-done")
            {
                isChecked = false;
                return false;
            }

            isChecked = false;
            foreach (Inline inline in p.Inlines)
            {
                InlineUIContainer icu = inline as InlineUIContainer;
                CheckBox cb = icu?.Child as CheckBox;
                if (cb != null)
                {
                    isChecked = cb.IsChecked == true;
                    break;
                }
            }
            return true;
        }

        private static string ExtractChecklistText(Paragraph p)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Inline inline in p.Inlines)
            {
                InlineUIContainer icu = inline as InlineUIContainer;
                if (icu != null)
                    continue;
                Run run = inline as Run;
                if (run != null)
                    sb.Append(run.Text);
            }
            return sb.ToString().Trim();
        }

        private static void AppendBlock(StringBuilder builder, Block block)
        {
            if (block.Tag as string == "hr")
            {
                builder.Append("<hr />");
                return;
            }

            Paragraph paragraph = block as Paragraph;
            if (paragraph != null)
            {
                string tag = paragraph.Tag as string;

                if (tag == "pre")
                {
                    builder.Append("<pre><code>");
                    AppendInlines(builder, paragraph.Inlines);
                    builder.Append("</code></pre>");
                    return;
                }

                if (tag == "blockquote")
                {
                    builder.Append("<blockquote>");
                    AppendInlines(builder, paragraph.Inlines);
                    builder.Append("</blockquote>");
                    return;
                }

                // Heading detection: prefer Tag, fallback to font size
                string headingTag = paragraph.Tag as string;
                if (headingTag != null && headingTag.StartsWith("h"))
                {
                    builder.Append("<").Append(headingTag).Append(">");
                    AppendInlines(builder, paragraph.Inlines);
                    builder.Append("</").Append(headingTag).Append(">");
                    return;
                }
                else
                {
                    // Fallback: detect by font size
                    double fs = paragraph.FontSize;
                    if (Math.Abs(fs - 22) < 0.1) headingTag = "h1";
                    else if (Math.Abs(fs - 20) < 0.1) headingTag = "h2";
                    else if (Math.Abs(fs - 18) < 0.1) headingTag = "h3";

                    // If detected by font size, also set Tag for next round-trip
                    if (headingTag != null)
                        paragraph.Tag = headingTag;
                }

                if (headingTag != null && headingTag.StartsWith("h"))
                {
                    builder.Append("<").Append(headingTag).Append(">");
                    AppendInlines(builder, paragraph.Inlines);
                    builder.Append("</").Append(headingTag).Append(">");
                    return;
                }

                builder.Append("<p>");
                AppendInlines(builder, paragraph.Inlines);
                builder.Append("</p>");
                return;
            }

            List list = block as List;
            if (list != null)
            {
                bool ordered = list.MarkerStyle == System.Windows.TextMarkerStyle.Decimal;
                builder.Append(ordered ? "<ol>" : "<ul>");
                foreach (ListItem item in list.ListItems)
                {
                    builder.Append("<li>");
                    foreach (Block itemBlock in item.Blocks)
                    {
                        Paragraph itemParagraph = itemBlock as Paragraph;
                        if (itemParagraph != null)
                            AppendInlines(builder, itemParagraph.Inlines);
                    }
                    builder.Append("</li>");
                }
                builder.Append(ordered ? "</ol>" : "</ul>");
            }
        }

        private static void AppendInlines(StringBuilder builder, InlineCollection inlines)
        {
            foreach (Inline inline in inlines)
                AppendInline(builder, inline);
        }

        private static void AppendInline(StringBuilder builder, Inline inline)
        {
            InlineUIContainer icu = inline as InlineUIContainer;
            if (icu != null)
                return;

            Run run = inline as Run;
            if (run != null)
            {
                builder.Append(WebUtility.HtmlEncode(run.Text));
                return;
            }

            if (inline is LineBreak)
            {
                builder.Append("<br />");
                return;
            }

            Hyperlink hyperlink = inline as Hyperlink;
            if (hyperlink != null)
            {
                builder.Append("<a");
                if (hyperlink.NavigateUri != null)
                    builder.Append(" href=\"").Append(WebUtility.HtmlEncode(hyperlink.NavigateUri.ToString())).Append("\"");
                builder.Append(">");
                AppendInlines(builder, hyperlink.Inlines);
                builder.Append("</a>");
                return;
            }

            // Check Bold BEFORE Span (Bold inherits from Span)
            Bold bold = inline as Bold;
            if (bold != null)
            {
                builder.Append("<strong>");
                AppendInlines(builder, bold.Inlines);
                builder.Append("</strong>");
                return;
            }

            // Check Italic BEFORE Span (Italic inherits from Span)
            Italic italic = inline as Italic;
            if (italic != null)
            {
                builder.Append("<em>");
                AppendInlines(builder, italic.Inlines);
                builder.Append("</em>");
                return;
            }

            Underline underline = inline as Underline;
            if (underline != null)
            {
                builder.Append("<u>");
                AppendInlines(builder, underline.Inlines);
                builder.Append("</u>");
                return;
            }

            Span span = inline as Span;
            if (span != null)
            {
                string tag = span.Tag as string;
                if (tag == "s")
                {
                    builder.Append("<s>");
                    AppendInlines(builder, span.Inlines);
                    builder.Append("</s>");
                    return;
                }
                if (tag == "code")
                {
                    builder.Append("<code>");
                    AppendInlines(builder, span.Inlines);
                    builder.Append("</code>");
                    return;
                }
                AppendInlines(builder, span.Inlines);
            }
        }
    }
}
