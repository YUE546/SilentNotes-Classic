using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;

namespace SilentNotes.WindowsWpf.Workers
{
    internal sealed class HtmlToFlowDocumentConverter
    {
        /// <summary>Font family used for inline code and code blocks.</summary>
        internal static readonly FontFamily CodeFontFamily = new FontFamily("Consolas");

        /// <summary>Background brush for inline code and code blocks.</summary>
        internal static readonly SolidColorBrush CodeBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));

        /// <summary>H1 heading color (SteelBlue, full opacity).</summary>
        internal static readonly SolidColorBrush Heading1Brush = new SolidColorBrush(Color.FromArgb(255, 70, 130, 180));

        /// <summary>H2 heading color (SteelBlue, 85% opacity).</summary>
        internal static readonly SolidColorBrush Heading2Brush = new SolidColorBrush(Color.FromArgb(217, 70, 130, 180));

        /// <summary>H3 heading color (SteelBlue, 70% opacity).</summary>
        internal static readonly SolidColorBrush Heading3Brush = new SolidColorBrush(Color.FromArgb(179, 70, 130, 180));

        /// <summary>Left border brush for blockquotes.</summary>
        internal static readonly SolidColorBrush BlockquoteBorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

        /// <summary>Background brush for blockquotes.</summary>
        internal static readonly SolidColorBrush BlockquoteBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

        /// <summary>HTML void elements that need to be self-closing for XML parsing.</summary>
        private static readonly string[] VoidElements = new[] { "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed", "source", "track", "wbr" };

        /// <summary>Set to true during Convert() if the HTML contains &lt;p class="done"&gt; elements.</summary>
        private bool _isChecklistContext;

        public FlowDocument Convert(string html, bool isChecklistFormat = false)
        {
            _isChecklistContext = false;
            FlowDocument document = new FlowDocument();
            document.FontSize = 15; // Match RichTextBox default to avoid size conflicts
            if (string.IsNullOrWhiteSpace(html))
                return document;

            try
            {
                // Scan for original SilentNotes checklist format: <p class="done">item</p>
                if (Regex.IsMatch(html, @"<p[^>]*class\s*=\s*[""']done[""'][^>]*>", RegexOptions.IgnoreCase))
                    _isChecklistContext = true;

                // If the note type is known to be a checklist, treat all <p> elements as checklist items
                if (isChecklistFormat)
                    _isChecklistContext = true;

                // Pre-process HTML to make it XML-compatible, then parse
                string xmlCompatibleHtml = MakeHtmlXmlCompatible(html);
                XElement root = XElement.Parse("<root>" + xmlCompatibleHtml + "</root>");
                foreach (XNode node in root.Nodes())
                    AddBlock(document, node);
            }
            catch
            {
                document.Blocks.Add(new Paragraph(new Run(WebUtility.HtmlDecode(StripTags(html)))));
            }
            return document;
        }

        /// <summary>
        /// Pre-processes HTML content to make it compatible with XElement.Parse.
        /// Decodes non-XML HTML entities and fixes void elements that aren't self-closing.
        /// </summary>
        private static string MakeHtmlXmlCompatible(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            // Step 1: Replace non-XML HTML entities (e.g. &nbsp;, &mdash;) with decoded characters.
            string result = Regex.Replace(html,
                @"&(?!(?:amp|lt|gt|quot|apos);)[a-zA-Z]+;",
                match => WebUtility.HtmlDecode(match.Value),
                RegexOptions.IgnoreCase);

            // Step 2: Fix HTML void elements that aren't self-closing (e.g. <br> → <br />, <hr> → <hr />).
            string voidPattern = @"<(" + string.Join("|", VoidElements) + @")([^>]*?)(?<!/)\s*>";
            result = Regex.Replace(result, voidPattern, "<$1$2 />", RegexOptions.IgnoreCase);

            return result;
        }

        private void AddBlock(FlowDocument document, XNode node)
        {
            XElement element = node as XElement;
            if (element == null)
            {
                XText textNode = node as XText;
                if (textNode == null)
                    return;
                string text = textNode.Value;
                if (!string.IsNullOrWhiteSpace(text))
                    document.Blocks.Add(new Paragraph(new Run(text)));
                return;
            }

            string name = element.Name.LocalName.ToLowerInvariant();
            switch (name)
            {
                case "h1":
                case "h2":
                case "h3":
                    document.Blocks.Add(CreateParagraph(element, GetHeadingSize(name), FontWeights.SemiBold, name));
                    break;
                case "ul":
                case "ol":
                    // Backward compatibility: old checklist format <ul data-type="taskList">
                    if (name == "ul")
                    {
                        XAttribute taskListAttr = element.Attribute("data-type");
                        if (taskListAttr != null && taskListAttr.Value == "taskList")
                        {
                            CreateTaskList(document, element);
                            break;
                        }
                    }
                    document.Blocks.Add(CreateList(element, name == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc));
                    break;
                case "p":
                case "div":
                    // In original SilentNotes checklist format, <p> = unchecked item, <p class="done"> = checked item
                    if (_isChecklistContext && name == "p")
                    {
                        document.Blocks.Add(CreateChecklistItemFromP(element));
                    }
                    else
                    {
                        document.Blocks.Add(CreateParagraph(element, 0, FontWeights.Normal, null));
                    }
                    break;
                case "blockquote":
                    document.Blocks.Add(CreateParagraph(element, 0, FontWeights.Normal, "blockquote"));
                    break;
                case "pre":
                    document.Blocks.Add(CreateCodeBlock(element));
                    break;
                case "hr":
                    document.Blocks.Add(CreateHorizontalRule());
                    break;
                default:
                    document.Blocks.Add(CreateParagraph(element, 0, FontWeights.Normal, null));
                    break;
            }
        }

        private static Paragraph CreateParagraph(XElement element, double fontSize, FontWeight fontWeight, string blockType)
        {
            Paragraph paragraph = new Paragraph();
            if (fontSize > 0)
                paragraph.FontSize = fontSize;
            paragraph.FontWeight = fontWeight;

            // Set Tag for headings so save converter can detect them reliably
            if (blockType == "h1" || blockType == "h2" || blockType == "h3")
                paragraph.Tag = blockType;

            // Apply heading color
            if (blockType == "h1")
                paragraph.Foreground = Heading1Brush;
            else if (blockType == "h2")
                paragraph.Foreground = Heading2Brush;
            else if (blockType == "h3")
                paragraph.Foreground = Heading3Brush;

            if (blockType == "blockquote")
            {
                paragraph.Margin = new Thickness(12, 4, 0, 4);
                paragraph.Padding = new Thickness(8, 4, 8, 4);
                paragraph.Background = BlockquoteBackgroundBrush;
                paragraph.BorderBrush = BlockquoteBorderBrush;
                paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
                paragraph.Tag = "blockquote";
            }
            AddInlines(paragraph.Inlines, element.Nodes());
            return paragraph;
        }

        private Paragraph CreateChecklistItemFromP(XElement pElement)
        {
            XAttribute classAttr = pElement.Attribute("class");
            bool isChecked = classAttr != null &&
                string.Equals(classAttr.Value, "done", StringComparison.InvariantCultureIgnoreCase);

            Paragraph p = new Paragraph
            {
                Tag = isChecked ? "checklist-done" : "checklist"
            };

            CheckBox cb = new CheckBox
            {
                IsChecked = isChecked,
                Tag = "checklist-cb",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            p.Inlines.Add(new InlineUIContainer(cb) { Tag = "checklist-icu" });

            // Text content of the <p> element
            string text = CollectRawText(pElement);
            Run textRun = new Run(text ?? string.Empty);
            if (isChecked)
                textRun.TextDecorations = TextDecorations.Strikethrough;
            p.Inlines.Add(textRun);

            return p;
        }

        private static Paragraph CreateCodeBlock(XElement element)
        {
            Paragraph paragraph = new Paragraph
            {
                FontFamily = CodeFontFamily,
                Background = CodeBackgroundBrush,
                Padding = new Thickness(8),
                Tag = "pre",
            };

            string rawContent = CollectRawText(element);
            paragraph.Inlines.Add(new Run(rawContent));
            return paragraph;
        }

        /// <summary>Collects all text content from an element, preserving whitespace as-is.</summary>
        private static string CollectRawText(XElement element)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (XNode node in element.Nodes())
            {
                XText text = node as XText;
                if (text != null)
                {
                    sb.Append(text.Value);
                    continue;
                }
                XElement child = node as XElement;
                if (child != null)
                    sb.Append(CollectRawText(child));
            }
            return sb.ToString();
        }

        private static Block CreateHorizontalRule()
        {
            return new BlockUIContainer
            {
                Child = new Border
                {
                    Height = 1,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    Margin = new Thickness(0, 8, 0, 8),
                },
                Tag = "hr",
            };
        }

        private static List CreateList(XElement element, TextMarkerStyle markerStyle)
        {
            List list = new List { MarkerStyle = markerStyle };
            foreach (XElement itemElement in element.Elements())
            {
                if (itemElement.Name.LocalName.ToLowerInvariant() != "li")
                    continue;

                Paragraph paragraph = new Paragraph();
                AddInlines(paragraph.Inlines, itemElement.Nodes());
                list.ListItems.Add(new ListItem(paragraph));
            }
            return list;
        }

        /// <summary>
        /// Backward compatibility: old checklist format <ul data-type="taskList">.
        /// </summary>
        private static void CreateTaskList(FlowDocument document, XElement element)
        {
            foreach (XElement itemElement in element.Elements("li"))
            {
                Paragraph p = OldCreateChecklistItem(itemElement);
                document.Blocks.Add(p);
            }
        }

        private static Paragraph OldCreateChecklistItem(XElement itemElement)
        {
            string checkedValue = (string)itemElement.Attribute("data-checked") ?? "false";
            bool isChecked = string.Equals(checkedValue, "true", StringComparison.InvariantCultureIgnoreCase);

            Paragraph p = new Paragraph
            {
                Tag = isChecked ? "checklist-done" : "checklist"
            };

            CheckBox cb = new CheckBox
            {
                IsChecked = isChecked,
                Tag = "checklist-cb",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            p.Inlines.Add(new InlineUIContainer(cb) { Tag = "checklist-icu" });

            XElement pElement = itemElement.Element("p");
            string text = pElement != null ? CollectRawText(pElement) : itemElement.Value;
            p.Inlines.Add(new Run(text ?? string.Empty));

            return p;
        }

        private static void AddInlines(InlineCollection inlines, System.Collections.Generic.IEnumerable<XNode> nodes)
        {
            foreach (XNode node in nodes)
            {
                XText text = node as XText;
                if (text != null)
                {
                    inlines.Add(new Run(text.Value));
                    continue;
                }

                XElement element = node as XElement;
                if (element == null)
                    continue;

                string name = element.Name.LocalName.ToLowerInvariant();
                if (name == "br")
                {
                    inlines.Add(new LineBreak());
                    continue;
                }

                Span span = CreateInlineSpan(element, name);
                AddInlines(span.Inlines, element.Nodes());
                inlines.Add(span);
            }
        }

        private static Span CreateInlineSpan(XElement element, string name)
        {
            switch (name)
            {
                case "strong":
                case "b":
                    return new Bold();
                case "em":
                case "i":
                    return new Italic();
                case "u":
                    return new Underline();
                case "s":
                case "del":
                case "strike":
                    return new Span { TextDecorations = TextDecorations.Strikethrough, Tag = "s" };
                case "code":
                    return new Span
                    {
                        FontFamily = CodeFontFamily,
                        Background = CodeBackgroundBrush,
                        Tag = "code",
                    };
                case "a":
                    Hyperlink hyperlink = new Hyperlink();
                    XAttribute href = element.Attribute("href");
                    if (href != null)
                        hyperlink.NavigateUri = new System.Uri(href.Value, System.UriKind.RelativeOrAbsolute);
                    return hyperlink;
                default:
                    return new Span();
            }
        }

        private static double GetHeadingSize(string name)
        {
            switch (name)
            {
                case "h1":
                    return 22;
                case "h2":
                    return 20;
                default:
                    return 18;
            }
        }

        private static string StripTags(string html)
        {
            return System.Text.RegularExpressions.Regex.Replace(html ?? string.Empty, "<.*?>", string.Empty);
        }
    }
}