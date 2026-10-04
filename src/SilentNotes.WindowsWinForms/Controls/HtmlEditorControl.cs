using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SilentNotes.WindowsWinForms.Controls
{
    /// <summary>
    /// Rich text editor based on the MSHTML WebBrowser control in contentEditable mode.
    /// The note storage format is HTML, so content goes in and out without any
    /// conversion layer; formatting commands map 1:1 onto the stored dialect.
    /// </summary>
    public class HtmlEditorControl : UserControl
    {
        private readonly WebBrowser _browser;
        private bool _ready;
        private bool _readOnly;
        private bool _isChecklist;
        private bool _dark;

        /// <summary>Raised once after the editing document is initialized.</summary>
        public event EventHandler EditorReady;

        /// <summary>Raised whenever the edited content changes.</summary>
        public event EventHandler ContentChanged;

        /// <summary>Raised when the caret/selection moves; use QueryState to refresh UI state.</summary>
        public event EventHandler SelectionChanged;

        public HtmlEditorControl()
        {
            EnsureBrowserEmulationMode();

            _browser = new WebBrowser();
            _browser.Dock = DockStyle.Fill;
            _browser.AllowNavigation = false;
            _browser.AllowWebBrowserDrop = false;
            _browser.IsWebBrowserContextMenuEnabled = false;
            _browser.WebBrowserShortcutsEnabled = true;
            _browser.ScriptErrorsSuppressed = true;
            _browser.DocumentCompleted += OnDocumentCompleted;
            Controls.Add(_browser);

            _browser.DocumentText = BuildShellHtml();
        }

        public bool IsReady
        {
            get { return _ready; }
        }

        #region Document lifecycle

        private void OnDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_ready)
                return;
            if (_browser.Document == null || _browser.ReadyState != WebBrowserReadyState.Complete)
                return;

            HtmlDocument document = _browser.Document;
            document.AttachEventHandler("oninput", OnDomEvent);
            document.AttachEventHandler("onkeyup", OnDomEvent);
            document.AttachEventHandler("onpaste", OnDomEvent);
            document.AttachEventHandler("oncut", OnDomEvent);
            document.AttachEventHandler("onselectionchange", OnSelectionEvent);
            document.Click += OnDocumentClick;

            _ready = true;
            SetEditorTheme(_dark, _isChecklist);
            var handler = EditorReady;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnDomEvent(object sender, EventArgs e)
        {
            var handler = ContentChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        // Selection changes must not mark the content dirty; they only carry UI state.
        private void OnSelectionEvent(object sender, EventArgs e)
        {
            var handler = SelectionChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnDocumentClick(object sender, HtmlElementEventArgs e)
        {
            if (!_isChecklist)
                return;

            // e.ToElement is null for document-level clicks in MSHTML, so the
            // element must be resolved from the click coordinates. This also
            // covers hits on the ::before checkbox, which is not a DOM node.
            HtmlElement element = _browser.Document.GetElementFromPoint(e.ClientMousePosition);
            while (element != null && !string.Equals(element.TagName, "P", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(element.TagName, "BODY", StringComparison.OrdinalIgnoreCase))
                    return;
                element = element.Parent;
            }

            if (element == null)
                return;

            string className = element.GetAttribute("className") ?? string.Empty;
            element.SetAttribute("className", className.Contains("done") ? className.Replace("done", "").Trim() : (className + " done").Trim());
            OnDomEvent(this, EventArgs.Empty);
        }

        /// <summary>Replaces the editor content with the given note HTML.</summary>
        public void SetContent(string html, bool isChecklist)
        {
            if (!_ready)
                return;

            _isChecklist = isChecklist;
            dynamic body = ((dynamic)_browser.Document.DomDocument).body;
            body.innerHTML = string.IsNullOrEmpty(html) ? "<p><br></p>" : html;
            SetEditorTheme(_dark, isChecklist);
        }

        /// <summary>
        /// Returns the edited content as HTML. Tag and attribute names are lowercased
        /// so the output matches the dialect stored by the other SilentNotes clients.
        /// </summary>
        public string GetHtml()
        {
            if (!_ready)
                return null;

            dynamic body = ((dynamic)_browser.Document.DomDocument).body;
            string html = body.innerHTML as string;
            if (string.IsNullOrEmpty(html))
                return html;

            html = Regex.Replace(html, @"<(/?)([A-Za-z][A-Za-z0-9]*)", delegate(Match m)
            {
                return "<" + m.Groups[1].Value + m.Groups[2].Value.ToLowerInvariant();
            });
            html = Regex.Replace(html, @"\s(HREF|SRC|CLASS|TARGET|ID|STYLE|NAME|TYPE|COLSPAN|ROWSPAN)=(?<q>""[^""]*"")",
                delegate(Match m)
                {
                    return " " + m.Groups[1].Value.ToLowerInvariant() + "=" + m.Groups["q"].Value;
                });
            return html;
        }

        public void SetReadOnly(bool readOnly)
        {
            _readOnly = readOnly;
            if (!_ready)
                return;

            dynamic body = ((dynamic)_browser.Document.DomDocument).body;
            body.contentEditable = readOnly ? "false" : "true";
        }

        public bool IsReadOnly
        {
            get { return _readOnly; }
        }

        public void FocusEditor()
        {
            if (_ready)
                _browser.Document.Body.Focus();
        }

        /// <summary>Switches the editor colorsheet and the checklist rendering mode.</summary>
        public void SetEditorTheme(bool dark, bool isChecklist)
        {
            _dark = dark;
            _isChecklist = isChecklist;
            if (!_ready)
                return;

            // These hex values mirror WinFormsThemeService tokens (SurfacePaper,
            // SurfaceWindow, BorderSubtle, Accent...). MSHTML needs literal CSS, so
            // they are duplicated here — keep both sides in sync with the doc table.
            string paper = dark ? "#223030" : "#FFFFFF";
            string text = dark ? "#D9E6E2" : "#12433E";
            string quoteBg = dark ? "#1D2927" : "#F0F7F5";
            string quoteBorder = dark ? "#33443F" : "#CFE0DA";
            string codeBg = dark ? "#1D2927" : "#F0F7F5";
            string linkColor = dark ? "#2DD4BF" : "#0D9488";
            string headingColor = dark ? "#2DD4BF" : "#0D9488";
            string headingLine = dark ? "#33443F" : "#CFE0DA";

            // MSHTML honors the legacy IE scrollbar-* properties; they are the only way
            // to restyle the editor scrollbar (recolor only — the classic shape stays).
            string sbFace = dark ? "#33443F" : "#CFE0DA";
            string sbTrack = dark ? "#223030" : "#FFFFFF";
            string sbArrow = dark ? "#8FA8A0" : "#5B776E";

            var css = new System.Text.StringBuilder();
            css.Append("html{background:" + paper + ";");
            css.Append("scrollbar-face-color:" + sbFace + ";scrollbar-track-color:" + sbTrack + ";");
            css.Append("scrollbar-arrow-color:" + sbArrow + ";scrollbar-highlight-color:" + sbFace + ";");
            css.Append("scrollbar-3dlight-color:" + sbFace + ";scrollbar-shadow-color:" + sbFace + ";");
            css.Append("scrollbar-darkshadow-color:" + sbFace + ";}");
            css.Append("body{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;font-size:16px;line-height:22px;");
            css.Append("color:" + text + ";background:" + paper + ";max-width:800px;margin:0 auto;padding:24px 32px;outline:none;}");
            // Larger gap between headings and body; only H1 carries a gray dashed
            // rule. `border-bottom: dashed` renders as a dense 1px line in MSHTML
            // (looks solid), so the dash is painted with a repeating gradient
            // instead: 2px thick, 10px dash / 6px gap. IE11 does not support
            // two-position color stops — every stop must be spelled out.
            string dashLine = "border-bottom:0;background-image:repeating-linear-gradient(90deg," + headingLine + " 0," + headingLine + " 10px,transparent 10px,transparent 16px);background-size:100% 2px;background-position:0 100%;background-repeat:no-repeat;";
            css.Append("h1{color:" + headingColor + ";font-size:22px;font-weight:600;margin:26px 0 18px 0;padding-bottom:10px;" + dashLine + "}");
            css.Append("h2{color:" + headingColor + ";opacity:.85;font-size:20px;font-weight:600;margin:22px 0 14px 0;}");
            css.Append("h3{color:" + headingColor + ";opacity:.7;font-size:18px;font-weight:600;margin:18px 0 12px 0;}");
            css.Append("h1:first-child{margin-top:0;}");
            css.Append("p{margin:6px 0;}");
            css.Append("blockquote{border-left:3px solid " + quoteBorder + ";background:" + quoteBg + ";margin:8px 0;padding:4px 12px;}");
            css.Append("pre{background:" + codeBg + ";padding:8px;white-space:pre-wrap;font-family:Consolas,monospace;}");
            css.Append("code{font-family:Consolas,monospace;background:" + codeBg + ";}");
            css.Append("hr{border:0;border-top:1px solid " + quoteBorder + ";margin:12px 0;}");
            css.Append("ul,ol{padding-left:24px;margin:6px 0;}");
            css.Append("a{color:" + linkColor + ";}");
            if (isChecklist)
            {
                string accent = dark ? "#2DD4BF" : "#0D9488";
                // Neutral gray clashes with the teal-tinted dark paper; items use a
                // slightly lighter shade of the same tint instead.
                string itemBg = dark ? "#212B28" : "#F2F2F2";
                string doneText = dark ? "#6E8880" : "#8AA39C";
                // The checkmark is an inline SVG background instead of a text glyph:
                // MSHTML's font fallback renders U+2713 as a misplaced missing-glyph
                // box. Base64 keeps the data URI safe through cssText round-trips.
                string checkSvg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'>" +
                    "<path d='M4.5 8.5L7 11L11.5 5.5' fill='none' stroke='" + (dark ? "#12332E" : "#FFFFFF") +
                    "' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/></svg>";
                string checkImage = "data:image/svg+xml;base64," +
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(checkSvg));
                // Items carry a permanent gray card background; the checkbox keeps
                // a standing accent outline. The hanging indent (negative
                // text-indent pulls the box back out) keeps wrapped lines aligned
                // with the text instead of the checkbox.
                css.Append("body.sn-checklist p{margin:9px 0;padding:2px 6px 2px 30px;text-indent:-24px;");
                css.Append("background:" + itemBg + ";border-radius:6px;cursor:default;}");
                // The checkbox is drawn as a real rounded box instead of a font glyph,
                // so it stays crisp and takes the accent color in both palettes.
                css.Append("body.sn-checklist p:before{content:'';display:inline-block;width:15px;height:15px;");
                css.Append("border:1px solid " + accent + ";border-radius:4px;margin-right:9px;vertical-align:-3px;}");
                css.Append("body.sn-checklist p.done:before{background-color:" + accent + ";border-color:" + accent + ";");
                css.Append("background-image:url(" + checkImage + ");background-repeat:no-repeat;");
                css.Append("background-position:center;background-size:14px 14px;}");
                css.Append("body.sn-checklist p.done{text-decoration:line-through;color:" + doneText + ";}");
            }

            try
            {
                dynamic document = _browser.Document.DomDocument;
                dynamic head = document.head ?? document.getElementsByTagName("head").item(0);
                // Recreate the style element on every update: MSHTML only honors style
                // sheets written through styleSheet.cssText, and a fresh element is the
                // only reliable way to replace the sheet.
                dynamic old = document.getElementById("sn-editor-style");
                // MSHTML null comes back through the dynamic binder as DBNull, not null
                if (old != null && !(old is DBNull))
                    old.removeNode(true);
                dynamic styleElement = document.createElement("style");
                styleElement.id = "sn-editor-style";
                head.appendChild(styleElement);
                styleElement.styleSheet.cssText = css.ToString();

                dynamic bodyElement = document.body;
                bodyElement.className = isChecklist ? "sn-checklist" : "";
            }
            catch
            {
                // A failed style refresh must not break theme switching; the editor
                // keeps the previous sheet until the next SetEditorTheme call.
            }
        }

        #endregion

        #region Formatting commands

        private void Exec(string command, object value)
        {
            if (!_ready || _readOnly)
                return;
            _browser.Document.ExecCommand(command, false, value);
        }

        public void ToggleBold() { Exec("Bold", null); }
        public void ToggleItalic() { Exec("Italic", null); }
        public void ToggleUnderline() { Exec("Underline", null); }
        public void ToggleStrikethrough() { Exec("StrikeThrough", null); }
        public void ToggleUnorderedList() { Exec("InsertUnorderedList", null); }
        public void ToggleOrderedList() { Exec("InsertOrderedList", null); }
        public void Undo() { Exec("Undo", null); }
        public void Redo() { Exec("Redo", null); }
        public void InsertHorizontalRule() { Exec("InsertHorizontalRule", null); }
        public void RemoveLink() { Exec("UnLink", null); }

        public void CreateLink(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            Exec("CreateLink", url);
        }

        /// <summary>Sets or toggles the current block to a heading. level: 1-3, 0 = paragraph.</summary>
        public void SetHeading(int level, bool toggleOffIfActive)
        {
            string tag = "H" + level;
            if (toggleOffIfActive && string.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase))
                tag = "P";
            Exec("FormatBlock", "<" + tag + ">");
        }

        public void SetBlockquote(bool toggle)
        {
            string tag = "BLOCKQUOTE";
            if (toggle && string.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase))
                tag = "P";
            Exec("FormatBlock", "<" + tag + ">");
        }

        public void SetCodeBlock(bool toggle)
        {
            string tag = "PRE";
            if (toggle && string.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase))
                tag = "P";
            Exec("FormatBlock", "<" + tag + ">");
        }

        public bool QueryState(string command)
        {
            if (!_ready)
                return false;
            try
            {
                dynamic document = _browser.Document.DomDocument;
                return (bool)document.queryCommandState(command);
            }
            catch
            {
                return false;
            }
        }

        public string GetCurrentBlockTag()
        {
            if (!_ready)
                return null;
            try
            {
                dynamic document = _browser.Document.DomDocument;
                dynamic range = document.selection.createRange();
                if (range == null)
                    return null;
                dynamic parent = range.parentElement();
                while (parent != null)
                {
                    string tagName = parent.tagName as string;
                    if (tagName == null)
                        return null;
                    tagName = tagName.ToUpperInvariant();
                    if (tagName == "P" || tagName == "H1" || tagName == "H2" || tagName == "H3" ||
                        tagName == "BLOCKQUOTE" || tagName == "PRE" || tagName == "LI" || tagName == "DIV")
                        return tagName;
                    parent = parent.parentElement;
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Shell document and browser mode

        private static string BuildShellHtml()
        {
            return "<!DOCTYPE html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=11\">" +
                "</head><body contenteditable=\"true\"><p><br></p></body></html>";
        }

        /// <summary>
        /// Forces the embedded MSHTML engine into IE11 mode, otherwise the control runs
        /// in IE7 emulation and contentEditable/formatBlock behave differently.
        /// </summary>
        private static void EnsureBrowserEmulationMode()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    string exeName = AppDomain.CurrentDomain.FriendlyName;
                    key.SetValue(exeName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        #endregion
    }
}
